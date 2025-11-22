using GeminiLiveConsole.Models;

namespace GeminiLiveConsole;

public sealed class LiveSessionManager
{
    private readonly GeminiLiveClient _client;
    private readonly AudioCaptureService _audio;

    public event Action<string>? OnTranscript;
    public event Action<DetectedIntent>? OnIntent; // currently unused with new message schema
    public event Action<double>? OnVolume;
    public event Action<Exception>? OnError;
    public event Action? OnDisconnect;

    public LiveSessionManager(string apiKey, string model)
    {
        _client = new GeminiLiveClient(apiKey, model);
        _audio = new AudioCaptureService(16000);

        _client.OnOpen += () => _audio.Start();
        _client.OnMessage += HandleMessage;
        _client.OnError += e => OnError?.Invoke(e);
        _client.OnClose += () =>
        {
            _audio.Stop();
            OnDisconnect?.Invoke();
        };

        _audio.OnAudioChunk += async chunk =>
        {
            var rms = ComputeRms(chunk);
            OnVolume?.Invoke(rms);
            // chunk length is bytesRecorded
            await _client.SendAudioChunkAsync(chunk, chunk.Length);
        };
    }

    public Task ConnectAsync(CancellationToken ct = default) => _client.ConnectAsync(ct);
    public Task DisconnectAsync() => _client.DisconnectAsync();

    private void HandleMessage(GeminiMessage msg)
    {
        var transcript = msg.ServerContent?.InputTranscription?.Text;
        if (!string.IsNullOrWhiteSpace(transcript))
            OnTranscript?.Invoke(transcript);

        // Streamed model turn text parts (assistant responses)
        var parts = msg.ServerContent?.ModelTurn?.Parts;
        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p.Text))
                {
                    // For now treat assistant textual output as transcript as well
                    OnTranscript?.Invoke(p.Text);
                }
            }
        }
        // Intent/tool handling removed; protocol no longer provides function calls in this simplified schema.
    }

    private static double ComputeRms(byte[] pcm16)
    {
        int samples = pcm16.Length / 2;
        if (samples == 0) return 0;
        double sumSq = 0;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8));
            double norm = s / 32768.0;
            sumSq += norm * norm;
        }
        return Math.Sqrt(sumSq / samples);
    }
}
