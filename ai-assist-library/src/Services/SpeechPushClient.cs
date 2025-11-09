using AiAssistLibrary.Services.QuestionDetection;
using AiAssistLibrary.Settings;
using AudioCapture.Settings;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
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
	private readonly SpeechRecognizer _recognizer;
	private long _bytesPushedThisSecond;
	private string _channelTag = "AUDIO";
	private bool _started;

	private readonly QuestionDetectionOptions _qdOpts;
	private readonly IQuestionDetector? _detector;
	private readonly ILogger<HybridQuestionDetector>? _hybridLog;
	private readonly HttpClient _httpClient;

	public event Action<DetectedQuestion>? QuestionDetected;

	// Track latest partial so a manual trigger (space key) can run detection on it.
	//private volatile string? _lastPartialText;
	//private TimeSpan _lastPartialStart;
	//private TimeSpan _lastPartialEnd;

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
		if (a.TargetChannels != 1 || a.TargetBitsPerSample != 16) throw new InvalidOperationException("AudioOptions must produce16-bit mono PCM.");
		var speechConfig = SpeechConfig.FromSubscription(key, _opts.Region);
		speechConfig.SpeechRecognitionLanguage = _opts.Language;
		speechConfig.SetProperty("SpeechServiceResponse_RequestPunctuation", "true");
		speechConfig.SetProperty("SpeechServiceResponse_PostProcessingOption", "TrueText");
		var streamFormat = AudioStreamFormat.GetWaveFormatPCM((uint)a.TargetSampleRate, (byte)a.TargetBitsPerSample, (byte)a.TargetChannels);
		_pushStream = AudioInputStream.CreatePushStream(streamFormat);
		var audioConfig = AudioConfig.FromStreamInput(_pushStream);
		_recognizer = new SpeechRecognizer(speechConfig, audioConfig);

		if (_qdOpts.Enabled)
		{
			_detector = new HybridQuestionDetector(_hybridLog, _qdOpts.MinConfidence, _httpClient, _qdOpts.OpenAIEndpoint, _qdOpts.OpenAIDeployment, _qdOpts.OpenAIKey, _qdOpts.EnableOpenAIFallback);
		}

		_recognizer.Recognizing += (s, e) =>
		{
			if (!string.IsNullOrWhiteSpace(e.Result.Text))
			{
				//_lastPartialText = e.Result.Text;
				//_lastPartialStart = TimeSpan.FromTicks(e.Result.OffsetInTicks);
				//_lastPartialEnd = _lastPartialStart + e.Result.Duration;
				_log.LogInformation("{Tag} [partial] {Text}", _channelTag, e.Result.Text);
				//RunDetection(e.Result.Text, TimeSpan.FromTicks(e.Result.OffsetInTicks), TimeSpan.FromTicks(e.Result.OffsetInTicks) + e.Result.Duration, manual: false);

			}
		};
		_recognizer.Recognized += (s, e) =>
		{
			if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
			{
				_log.LogInformation("{Tag} [final] {Text}", _channelTag, e.Result.Text);
				// Skip if we already manually detected this exact text.
				//if (_detector is not null && e.Result.Text != _lastManualDetectionText)
				//{
				RunDetection(e.Result.Text, TimeSpan.FromTicks(e.Result.OffsetInTicks), TimeSpan.FromTicks(e.Result.OffsetInTicks) + e.Result.Duration, manual: false);
				//}
			}
		};
		_recognizer.Canceled += (s, e) => _log.LogWarning("{Tag} canceled: Reason={Reason}, ErrorCode={ErrorCode}, Error={Error}", _channelTag, e.Reason, e.ErrorCode, e.ErrorDetails);
		_recognizer.SessionStarted += (s, e) => _log.LogInformation("{Tag} session started", _channelTag);
		_recognizer.SessionStopped += (s, e) => _log.LogInformation("{Tag} session stopped", _channelTag);
	}

	public async ValueTask DisposeAsync()
	{
		try { await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false); } catch { }
		_pushStream.Close();
		_recognizer.Dispose();
	}

	public SpeechPushClient SetChannelTag(string tag) { if (!string.IsNullOrWhiteSpace(tag)) _channelTag = tag; return this; }

	public async Task StartAsync(CancellationToken ct)
	{
		if (_started) return;
		await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
		_meterSw.Restart();
		_started = true;
	}

	public void Write(ReadOnlySpan<byte> pcm16Mono)
	{
		var arr = pcm16Mono.ToArray();
		_pushStream.Write(arr);
		Interlocked.Add(ref _bytesPushedThisSecond, arr.Length);
		if (_meterSw.ElapsedMilliseconds >= 1000)
		{
			var bps = Interlocked.Exchange(ref _bytesPushedThisSecond, 0);
			_log.LogDebug("{Tag} PushStream: wrote {Bps} B/s to Azure", _channelTag, bps);
			_meterSw.Restart();
		}
	}

	/// <summary>
	/// Manually trigger question detection on the latest partial hypothesis (e.g. space key).
	/// </summary>
	//public void ManualDetectLatestPartial()
	//{
	//	if (_detector is null) return;
	//	var text = _lastPartialText;
	//	if (string.IsNullOrWhiteSpace(text)) return;
	//	_lastManualDetectionText = text;
	//	//_log.LogDebug("{Tag} manual detection invoked on partial: {Text}", _channelTag, text);
	//	Console.ForegroundColor = ConsoleColor.DarkRed;

	//	Console.WriteLine("LM: {0}", _lastManualDetectionText);

	//	Console.ForegroundColor = ConsoleColor.Yellow;
	//	Console.WriteLine("LP: {0}", _lastPartialText);

	//	Console.ResetColor();

	//	return;

	//	RunDetection(text, _lastPartialStart, _lastPartialEnd, manual: true);
	//}

	/// <summary>
	/// Manually trigger detection on arbitrary user-entered text (not tied to current partial).
	/// </summary>
	//public void ManualDetect(string text)
	//{
	//	if (_detector is null || string.IsNullOrWhiteSpace(text)) return;
	//	_lastManualDetectionText = text;
	//	_log.LogDebug("{Tag} manual detection invoked on custom text: {Text}", _channelTag, text);
	//	RunDetection(text, TimeSpan.Zero, TimeSpan.Zero, manual: true);
	//}

	private void RunDetection(string text, TimeSpan start, TimeSpan end, bool manual)
	{
		try
		{
			var questions = _detector?.Detect(text, start, end) ?? Array.Empty<DetectedQuestion>();
			foreach (var q in questions.Where(q => q.Confidence >= _qdOpts.MinConfidence))
			{
				var mode = manual ? "MANUAL" : "FINAL";
				_log.LogInformation("[{Tag}] [{Mode} QUESTION conf={Conf:F2}] {Q}", _channelTag, mode, q.Confidence, q.Text);
				Console.ForegroundColor = manual ? ConsoleColor.Yellow : ConsoleColor.Green;
				Console.WriteLine($"[{_channelTag}] {mode} QUESTION: {q.Text} (conf {q.Confidence:F2})");
				Console.ResetColor();
				QuestionDetected?.Invoke(q);
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "{Tag} manual detection failed.", _channelTag);
		}
	}
}