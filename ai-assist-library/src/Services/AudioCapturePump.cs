using AudioCapture.Services;
using AudioCapture.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace AiAssistLibrary.Services;

/// <summary>
/// Unified audio capture pump. Parameterized to drive either loopback (render) or microphone (capture).
/// </summary>
public class AudioCapturePump : BackgroundService // removed sealed
{
	private readonly ILogger<AudioCapturePump> _log;
	private readonly AudioDeviceSelector _selector;
	private readonly IServiceProvider _services;
	private readonly AudioOptions _opts;

	private readonly string _channelTag;
	private readonly string _meterLabel;
	private readonly Func<AudioDeviceSelector, MMDevice> _selectDevice;
	private readonly Func<MMDevice, IWaveProvider> _startCapture;
	private readonly Func<int> _bufferedBytes;
	private readonly Action _stopCapture;
	private readonly bool _enableZeroReadHypothesisLogging;

	public AudioCapturePump(
		ILogger<AudioCapturePump> log,
		AudioDeviceSelector selector,
		IServiceProvider services,
		IOptions<AudioOptions> opts,
		string channelTag,
		string meterLabel,
		Func<AudioDeviceSelector, MMDevice> selectDevice,
		Func<MMDevice, IWaveProvider> startCapture,
		Func<int> bufferedBytes,
		Action stopCapture,
		bool enableZeroReadHypothesisLogging)
	{
		_log = log;
		_selector = selector;
		_services = services;
		_opts = opts.Value;
		_channelTag = channelTag;
		_meterLabel = meterLabel;
		_selectDevice = selectDevice;
		_startCapture = startCapture;
		_bufferedBytes = bufferedBytes;
		_stopCapture = stopCapture;
		_enableZeroReadHypothesisLogging = enableZeroReadHypothesisLogging;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_log.LogInformation("Starting {Label} ({Channel}) pump...", _meterLabel, _channelTag);
		try
		{
			var device = _selectDevice(_selector);
			var sourceProvider = _startCapture(device);

			using var scope = _services.CreateScope();
			var resampler = scope.ServiceProvider.GetRequiredService<AudioResampler>();
			var speech = scope.ServiceProvider.GetRequiredService<SpeechPushClient>().SetChannelTag(_channelTag);

			var chain = resampler.BuildChain(sourceProvider);
			var target = chain.WaveFormat;

			var bytesPerMs = target.AverageBytesPerSecond / 1000;
			var requested = bytesPerMs * _opts.ChunkMilliseconds;
			var minimum = Math.Max(target.AverageBytesPerSecond / 100, target.BlockAlign);
			var chunkBytes = Math.Max(requested, minimum);
			chunkBytes -= chunkBytes % target.BlockAlign;

			var buffer = new byte[chunkBytes];
			await speech.StartAsync(stoppingToken);

			var meter = Stopwatch.StartNew();
			long resamplerBytesThisSec = 0;
			int zeroReadsThisSec = 0;
			int consecutiveZeroReads = 0;
			bool hypothesisLogged = false;

			try
			{
				while (!stoppingToken.IsCancellationRequested)
				{
					int read = chain.Read(buffer, 0, buffer.Length);
					if (read > 0)
					{
						speech.Write(new ReadOnlySpan<byte>(buffer, 0, read));
						resamplerBytesThisSec += read;
						zeroReadsThisSec = 0;
						consecutiveZeroReads = 0;
						hypothesisLogged = false;
					}
					else
					{
						zeroReadsThisSec++;
						consecutiveZeroReads++;

						if (_enableZeroReadHypothesisLogging)
						{
							int buffered = _bufferedBytes();
							if (!hypothesisLogged && buffered > 0 && consecutiveZeroReads >= 10)
							{
								_log.LogWarning(
									"Hypothesis: resampler returning zeros while source has data (Buffered={Buffered} B, ZeroReads={ZeroReads}).",
									buffered, consecutiveZeroReads);
								hypothesisLogged = true;
							}
							if (buffered == 0)
							{
								hypothesisLogged = false;
							}
						}

						await Task.Delay(5, stoppingToken);
					}

					if (meter.ElapsedMilliseconds >= 1000)
					{
						_log.LogDebug(
							"{Label}: read {Bps} B/s, zeroReads {ZeroReads}, consecutiveZeroReads {Consecutive}, buffered {Buffered} B",
							_meterLabel, resamplerBytesThisSec, zeroReadsThisSec, consecutiveZeroReads, _bufferedBytes());
						resamplerBytesThisSec = 0;
						zeroReadsThisSec = 0;
						meter.Restart();
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "Capture pump error.");
			}
			finally
			{
				_stopCapture();
				await speech.DisposeAsync();
				resampler.Dispose();
				_log.LogInformation("Capture pump stopped.");
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Capture pump failed.");
		}
	}
}
