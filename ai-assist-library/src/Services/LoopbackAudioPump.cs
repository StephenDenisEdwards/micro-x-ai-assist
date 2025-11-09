using AudioCapture.Services;
using AudioCapture.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AiAssistLibrary.Services;

public sealed class LoopbackAudioPump : AudioCapturePump
{
	public LoopbackAudioPump(
		ILogger<AudioCapturePump> log,
		AudioDeviceSelector selector,
		IServiceProvider services,
		IOptions<AudioOptions> opts,
		LoopbackSource loop)
		: base(
			log,
			selector,
			services,
			opts,
			channelTag: "AUDIO",
			meterLabel: "Loopback Resampler",
			selectDevice: s => s.SelectRenderDevice(),
			startCapture: d => loop.Start(d),
			bufferedBytes: () => loop.BufferedBytes,
			stopCapture: () => loop.Stop(),
			enableZeroReadHypothesisLogging: true)
	{
	}
}
