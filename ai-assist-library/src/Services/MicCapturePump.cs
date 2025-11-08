using AudioCapture.Services;
using AudioCapture.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics; // added for Stopwatch

namespace AiAssistLibrary.Services;

public sealed class MicCapturePump : BackgroundService
{
	private readonly ILogger<MicCapturePump> _log;
	private readonly AudioOptions _opts;
	private readonly AudioDeviceSelector _selector;
	private readonly IServiceProvider _services;
	private readonly MicrophoneSource _source;

	public MicCapturePump(
		ILogger<MicCapturePump> log,
		AudioDeviceSelector selector,
		MicrophoneSource source,
		IOptions<AudioOptions> opts,
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
		try
		{
			var device = _selector.SelectCaptureDevice();
			var wave = _source.Start(device);
			using var scope = _services.CreateScope();
			var resampler = scope.ServiceProvider.GetRequiredService<AudioResampler>();
			var speech = scope.ServiceProvider.GetRequiredService<SpeechPushClient>().SetChannelTag("MIC");

			// Build processing chain and use returned provider for reads & format (mirrors CapturePump)
			var chain = resampler.BuildChain(wave);
			var target = chain.WaveFormat;

			var bytesPerMs = target.AverageBytesPerSecond /1000;
			var requested = bytesPerMs * _opts.ChunkMilliseconds;
			var minimum = Math.Max(target.AverageBytesPerSecond /100, target.BlockAlign);
			var chunkBytes = Math.Max(requested, minimum);
			chunkBytes -= chunkBytes % target.BlockAlign;
			var buffer = new byte[chunkBytes];
			await speech.StartAsync(stoppingToken);
			try
			{
				var meter = Stopwatch.StartNew();
				long resamplerBytesThisSec =0;
				while (!stoppingToken.IsCancellationRequested)
				{
					var read = chain.Read(buffer,0, buffer.Length); // changed from resampler.Read
					if (read >0)
					{
						speech.Write(new ReadOnlySpan<byte>(buffer,0, read));
						resamplerBytesThisSec += read;
					}
					else
					{
						await Task.Delay(5, stoppingToken);
					}

					if (meter.ElapsedMilliseconds >=1000)
					{
						_log.LogDebug("Mic Resampler: read {Bps} B/s, buffered {Buffered} B", resamplerBytesThisSec,
							_source.BufferedBytes);
						resamplerBytesThisSec =0;
						meter.Restart();
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "Mic capture pump error.");
			}
			finally
			{
				_source.Stop();
				await speech.DisposeAsync();
				resampler.Dispose();
				_log.LogInformation("Mic capture pump stopped.");
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Failed to select capture device.");
		}
	}
}