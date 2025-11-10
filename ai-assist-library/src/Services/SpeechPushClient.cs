using AiAssistLibrary.Services.QuestionDetection;
using AiAssistLibrary.Settings;
using AudioCapture.Settings;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription; // added for diarization
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AiAssistLibrary.Services;

public sealed class SpeechPushClient : IAsyncDisposable
{
	private readonly ILogger<SpeechPushClient> _log;
	private readonly Stopwatch _meterSw = new();
	private readonly SpeechOptions _opts;
	private readonly PushAudioInputStream _pushStream;
	private readonly ConversationTranscriber _transcriber; // replaced SpeechRecognizer with ConversationTranscriber
	private long _bytesPushedThisSecond;
	private string _channelTag = "AUDIO";
	private bool _started;

	private readonly QuestionDetectionOptions _qdOpts;
	private readonly IQuestionDetector? _detector;
	private readonly ILogger<HybridQuestionDetector>? _hybridLog;
	private readonly HttpClient _httpClient;

	public event Action<DetectedQuestion>? QuestionDetected;

	// Track text already manually detected to avoid duplicate processing on final.
	private string? _lastManualDetectionText;

	public SpeechPushClient(
		ILogger<SpeechPushClient> log,
		IOptions<SpeechOptions> opts,
		IOptions<AudioOptions> audioOpts,
		IOptions<QuestionDetectionOptions> qdOpts,
		IServiceProvider services)
	{
		_log = log;
		_opts = opts.Value;
		_qdOpts = qdOpts.Value;
		_hybridLog = services.GetService(typeof(ILogger<HybridQuestionDetector>)) as ILogger<HybridQuestionDetector>;
		_httpClient = services.GetRequiredService<HttpClient>();
		var a = audioOpts.Value;
		var key = string.IsNullOrWhiteSpace(_opts.Key)
			? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
			: _opts.Key;
		if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Speech key not configured.");
		if (string.IsNullOrWhiteSpace(_opts.Region)) throw new InvalidOperationException("Speech region not configured.");
		if (string.IsNullOrWhiteSpace(_opts.Language)) throw new InvalidOperationException("Speech recognition language not configured.");
		if (a.TargetChannels !=1 || a.TargetBitsPerSample !=16) throw new InvalidOperationException("AudioOptions must produce16-bit mono PCM.");
		var speechConfig = SpeechConfig.FromSubscription(key, _opts.Region);
		speechConfig.SpeechRecognitionLanguage = _opts.Language;
		speechConfig.SetProperty("SpeechServiceResponse_RequestPunctuation", "true");
		speechConfig.SetProperty("SpeechServiceResponse_PostProcessingOption", "TrueText");
		// Enable diarization intermediate results (string key for broader SDK compatibility)
		speechConfig.SetProperty("SpeechServiceResponse_DiarizeIntermediateResults", "true");
		// Commit phrases faster after short pauses (milliseconds). Adjust to taste (500–1200).
		speechConfig.SetProperty("Speech_SegmentationSilenceTimeoutMs", _opts.SegmentationSilenceTimeoutMs.ToString());
		// Optional: how long to wait for the first speech before timing out (milliseconds).
		speechConfig.SetProperty("SpeechServiceConnection_InitialSilenceTimeoutMs", _opts.InitialSilenceTimeoutMs.ToString());
		var streamFormat = AudioStreamFormat.GetWaveFormatPCM((uint)a.TargetSampleRate, (byte)a.TargetBitsPerSample, (byte)a.TargetChannels);
		_pushStream = AudioInputStream.CreatePushStream(streamFormat);
		var audioConfig = AudioConfig.FromStreamInput(_pushStream);
		_transcriber = new ConversationTranscriber(speechConfig, audioConfig);

		if (_qdOpts.Enabled)
		{
			_detector = new HybridQuestionDetector(_hybridLog, _qdOpts.MinConfidence, _httpClient, _qdOpts.OpenAIEndpoint, _qdOpts.OpenAIDeployment, _qdOpts.OpenAIKey, _qdOpts.EnableOpenAIFallback);
		}

		// Event wiring with speaker diarization
		_transcriber.Transcribing += (s, e) =>
		{
			if (!string.IsNullOrWhiteSpace(e.Result.Text))
			{
				_log.LogInformation("{Tag} [partial] (Speaker={Speaker}) {Text}", _channelTag, e.Result.SpeakerId, e.Result.Text);
				// Question detection on partials currently disabled (retain existing behavior)
				// RunDetection(e.Result.Text, TimeSpan.FromTicks(e.Result.OffsetInTicks), TimeSpan.FromTicks(e.Result.OffsetInTicks) + e.Result.Duration, manual: false, e.Result.SpeakerId);
			}
		};
		_transcriber.Transcribed += (s, e) =>
		{
			if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
			{
				_log.LogInformation("{Tag} [final] (Speaker={Speaker}) {Text}", _channelTag, e.Result.SpeakerId, e.Result.Text);
				RunDetection(e.Result.Text, TimeSpan.FromTicks(e.Result.OffsetInTicks), TimeSpan.FromTicks(e.Result.OffsetInTicks) + e.Result.Duration, manual: false, e.Result.SpeakerId);

				// PropertyCollection isn't enumerable in this SDK version; remove invalid foreach.
				// If specific properties are needed, they can be accessed via GetProperty(string key).
				// Example (commented):
				// var speakerProp = e.Result.Properties.GetProperty("SpeakerId");
			}
			else if (e.Result.Reason == ResultReason.NoMatch)
			{
				_log.LogDebug("{Tag} no match", _channelTag);
			}
		};
		_transcriber.Canceled += (s, e) =>
		{
			_log.LogWarning("{Tag} canceled: Reason={Reason}, ErrorCode={ErrorCode}, Error={Error}", _channelTag, e.Reason, e.ErrorCode, e.ErrorDetails);
		};
		_transcriber.SessionStarted += (s, e) => _log.LogInformation("{Tag} session started", _channelTag);
		_transcriber.SessionStopped += (s, e) => _log.LogInformation("{Tag} session stopped", _channelTag);

		var sdkVer = typeof(SpeechConfig).Assembly.GetName().Version;
		_log.LogInformation("Speech SDK version {Ver}", sdkVer);
	}

	public async ValueTask DisposeAsync()
	{
		try { await _transcriber.StopTranscribingAsync().ConfigureAwait(false); } catch { }
		_pushStream.Close();
		_transcriber.Dispose();
	}

	public SpeechPushClient SetChannelTag(string tag) { if (!string.IsNullOrWhiteSpace(tag)) _channelTag = tag; return this; }

	public async Task StartAsync(CancellationToken ct)
	{
		if (_started) return;
		await _transcriber.StartTranscribingAsync().ConfigureAwait(false);
		_meterSw.Restart();
		_started = true;
	}

	public void Write(ReadOnlySpan<byte> pcm16Mono)
	{
		var arr = pcm16Mono.ToArray();
		_pushStream.Write(arr);
		Interlocked.Add(ref _bytesPushedThisSecond, arr.Length);
		if (_meterSw.ElapsedMilliseconds >=1000)
		{
			var bps = Interlocked.Exchange(ref _bytesPushedThisSecond,0);
			_log.LogDebug("{Tag} PushStream: wrote {Bps} B/s to Azure", _channelTag, bps);
			_meterSw.Restart();
		}
	}

	private void RunDetection(string text, TimeSpan start, TimeSpan end, bool manual, string? speakerId)
	{
		try
		{
			var questions = _detector?.Detect(text, start, end, speakerId) ?? Array.Empty<DetectedQuestion>();
			foreach (var q in questions.Where(q => q.Confidence >= _qdOpts.MinConfidence))
			{
				var mode = manual ? "MANUAL" : "FINAL";
				_log.LogInformation("[{Tag}] [{Mode} QUESTION conf={Conf:F2} speaker={Speaker}] {Q}", _channelTag, mode, q.Confidence, speakerId ?? q.SpeakerId ?? "?", q.Text);
				Console.ForegroundColor = manual ? ConsoleColor.Yellow : ConsoleColor.Green;
				Console.WriteLine($"[{_channelTag}] {mode} QUESTION: {q.Text} (conf {q.Confidence:F2}, speaker {speakerId ?? q.SpeakerId ?? "?"})");
				Console.ResetColor();
				QuestionDetected?.Invoke(q);
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "{Tag} detection failed.", _channelTag);
		}
	}
}