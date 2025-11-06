using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

/// <summary>
/// Converts arbitrary render format to PCM16 mono @ target sample rate.
/// </summary>
public sealed class AudioResampler : IDisposable
{
    private readonly ILogger<AudioResampler> _log;
    private readonly AudioOptions _opts;
    private IWaveProvider? _chain;
    private MediaFoundationResampler? _resampler;
    private WaveFormat? _target;

    public AudioResampler(ILogger<AudioResampler> log, IOptions<AudioOptions> opts)
    {
        _log = log;
        _opts = opts.Value;
    }

    public IWaveProvider BuildChain(IWaveProvider source)
    {
        // Let MF handle float->PCM16, resample, and channel downmix
        var sp = source.ToSampleProvider();
        if (sp.WaveFormat.Channels != 1) sp = new StereoToMonoSampleProvider(sp);
        var resampled = new WdlResamplingSampleProvider(sp, _opts.TargetSampleRate);
        var wav16 = new SampleToWaveProvider16(resampled);

        _target = new WaveFormat(_opts.TargetSampleRate, _opts.TargetBitsPerSample, _opts.TargetChannels);
        _resampler = new MediaFoundationResampler(wav16, _target)
        {
            ResamplerQuality = _opts.ResamplerQuality
        };
        _chain = _resampler;
        _log.LogInformation("Resampler chain ready: {Desc}", _target);
        return _chain;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_resampler is null) return 0;
        return _resampler.Read(buffer, offset, count);
    }

    public WaveFormat TargetFormat => _target ?? new WaveFormat(16000, 16, 1);

    public void Dispose()
    {
        _resampler?.Dispose();
        _resampler = null;
        _chain = null;
    }
}