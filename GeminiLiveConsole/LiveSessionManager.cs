namespace GeminiLiveConsole;

public sealed class LiveSessionManager : IAsyncDisposable
{
    private readonly GeminiLiveConfig _config;
    private readonly AudioCaptureService _audio;
    private readonly GeminiLiveClient _client;
    private CancellationTokenSource? _loopCts;

    public event Action<string>? OnTranscript;
    public event Action<ReportIntentPayload>? OnIntent;

    public LiveSessionManager(GeminiLiveConfig config)
    {
        _config = config;
        _audio = new AudioCaptureService(config.SampleRate);
        _client = new GeminiLiveClient(config);
        _client.OnTranscript += t => OnTranscript?.Invoke(t);
        _client.OnIntent += i => OnIntent?.Invoke(i);
        _client.OnError += e => Console.Error.WriteLine($"[ERROR] {e.Message}");
    }

    public async Task StartAsync(string systemInstruction, CancellationToken ct = default)
    {
        await _client.ConnectAsync(systemInstruction, ct);
        _audio.Start();
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => PumpAudioAsync(_loopCts.Token));
    }

    private async Task PumpAudioAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _audio.GetPcm16Chunks(ct))
            {
                await _client.SendAudioChunkAsync(chunk, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync()
    {
        _loopCts?.Cancel();
        _audio.Stop();
        await _client.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _audio.Dispose();
    }
}
