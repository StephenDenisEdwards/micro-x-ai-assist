using AiAssistLibrary.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace AiAssistLibrary.Services;

public sealed class CapturePump : BackgroundService
{
	private readonly ILogger<CapturePump> _log;
	private readonly AudioDeviceSelector _selector;
	private readonly LoopbackSource _source;
	private readonly IServiceProvider _services;
	private readonly AudioOptions _opts;

	public CapturePump(
	ILogger<CapturePump> log,
	AudioDeviceSelector selector,
	LoopbackSource source,
	Microsoft.Extensions.Options.IOptions<AudioOptions> opts,
	IServiceProvider services)
	{
		_log = log;
		_selector = selector;
		_source = source;
		_opts = opts.Value;
		_services = services;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		MMDevice device;
		try
		{
			device = _selector.SelectRenderDevice();
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Failed to select render device.");
			return;
		}

		IWaveProvider loop = _source.Start(device);
		using var scope = _services.CreateScope();
		var resampler = scope.ServiceProvider.GetRequiredService<AudioResampler>();
		var speech = scope.ServiceProvider.GetRequiredService<SpeechPushClient>().SetChannelTag("AUDIO");
		var chain = resampler.BuildChain(loop);
		var target = resampler.TargetFormat;
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
				int read = resampler.Read(buffer, 0, buffer.Length);
				if (read > 0)
				{
					speech.Write(new ReadOnlySpan<byte>(buffer, 0, read));
					resamplerBytesThisSec += read;
					zeroReadsThisSec = 0;
					consecutiveZeroReads = 0;
				}
				else
				{
					zeroReadsThisSec++;
					consecutiveZeroReads++;
					int buffered = _source.BufferedBytes;
					if (!hypothesisLogged && buffered > 0 && consecutiveZeroReads >= 10)
					{
						_log.LogWarning(
							"Hypothesis: resampler returning zeros while loopback has data (Buffered={Buffered} B, ZeroReads={ZeroReads}).",
							buffered, consecutiveZeroReads);
						hypothesisLogged = true;
					}

					await Task.Delay(5, stoppingToken);
				}

				if (meter.ElapsedMilliseconds >= 1000)
				{
					_log.LogDebug(
						"Resampler: read {Bps} B/s, zeroReads {ZeroReads}, consecutiveZeroReads {Consecutive}, loopbackBuffered {Buffered} B",
						resamplerBytesThisSec, zeroReadsThisSec, consecutiveZeroReads, _source.BufferedBytes);
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
			_source.Stop();
			await speech.DisposeAsync();
			resampler.Dispose();
			_log.LogInformation("Capture pump stopped.");
		}
	}
}
