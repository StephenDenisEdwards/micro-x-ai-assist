using NAudio.Wave;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace GeminiLiveConsole;

public interface IAudioSource : IDisposable
{
    int SampleRate { get; }
    IAsyncEnumerable<byte[]> GetPcm16Chunks(CancellationToken ct);
}

public sealed class AudioCaptureService : IAudioSource
{
    private readonly int _sampleRate;
    private readonly WaveInEvent _waveIn;
    private readonly BlockingCollection<byte[]> _queue = new();
    private bool _disposed;

    public int SampleRate => _sampleRate;

    public AudioCaptureService(int sampleRate = 16000)
    {
        _sampleRate = sampleRate;
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(sampleRate, 16, 1),
            BufferMilliseconds = 100
        };
        _waveIn.DataAvailable += (s, e) =>
        {
            var buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            _queue.Add(buffer);
        };
    }

    public void Start() => _waveIn.StartRecording();
    public void Stop() => _waveIn.StopRecording();

    public async IAsyncEnumerable<byte[]> GetPcm16Chunks([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            byte[]? data;
            try { data = _queue.Take(ct); }
            catch (OperationCanceledException) { yield break; }
            yield return data;
            await Task.Yield();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _waveIn.Dispose();
        _queue.Dispose();
    }
}
