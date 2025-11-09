using System.Diagnostics;
using AiAssistLibrary.Settings;
using AudioCapture.Settings;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

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
			if (!string.IsNullOrWhiteSpace(e.Result.Text)) _log.LogInformation("{Tag} [partial] {Text}", _channelTag, e.Result.Text);
		};
		_recognizer.Recognized += (s, e) =>
		{
			if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
			{
				_log.LogInformation("{Tag} [final] {Text}", _channelTag, e.Result.Text);
				if (_detector is not null)
				{
					var start = TimeSpan.FromTicks(e.Result.OffsetInTicks);
					var end = start + e.Result.Duration;
					var questions = _detector.Detect(e.Result.Text, start, end);
					foreach (var q in questions.Where(q => q.Confidence >= _qdOpts.MinConfidence))
					{
						_log.LogInformation("[{Tag}] [QUESTION conf={Conf:F2}] {Q}", _channelTag, q.Confidence, q.Text);
						// direct console line to make questions stand out
						Console.WriteLine($"[{_channelTag}] QUESTION: {q.Text} (conf {q.Confidence:F2})");
						QuestionDetected?.Invoke(q);
					}
				}
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
	{ if (_started) return; await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false); _meterSw.Restart(); _started = true; }

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
}