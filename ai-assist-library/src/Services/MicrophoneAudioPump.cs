using AudioCapture.Services;
using AudioCapture.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AiAssistLibrary.Services;

public sealed class MicrophoneAudioPump : AudioCapturePump
{
	public MicrophoneAudioPump(
		ILogger<AudioCapturePump> log,
		AudioDeviceSelector selector,
		IServiceProvider services,
		IOptions<AudioOptions> opts,
		MicrophoneSource mic)
		: base(
			log,
			selector,
			services,
			opts,
			channelTag: "MIC",
			meterLabel: "Mic Resampler",
			selectDevice: s => s.SelectCaptureDevice(),
			startCapture: d => mic.Start(d),
			bufferedBytes: () => mic.BufferedBytes,
			stopCapture: () => mic.Stop(),
			enableZeroReadHypothesisLogging: false)
	{
	}
}
