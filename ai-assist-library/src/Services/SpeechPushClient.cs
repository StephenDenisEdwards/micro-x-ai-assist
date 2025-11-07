using AudioCapture.Settings;
using AiAssistLibrary.Settings;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading;

namespace AiAssistLibrary.Services;

public sealed class SpeechPushClient : IAsyncDisposable
{
 private readonly ILogger<SpeechPushClient> _log;
 private readonly SpeechOptions _opts;
 private readonly SpeechRecognizer _recognizer;
 private readonly PushAudioInputStream _pushStream;
 private bool _started;
 private string _channelTag = "AUDIO";
 private readonly Stopwatch _meterSw = new();
 private long _bytesPushedThisSecond;

 public SpeechPushClient(
 ILogger<SpeechPushClient> log,
 IOptions<SpeechOptions> opts,
 IOptions<AudioOptions> audioOpts)
 {
 _log = log;
 _opts = opts.Value;
 var a = audioOpts.Value;
 var key = string.IsNullOrWhiteSpace(_opts.Key)? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY"): _opts.Key;
 if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Speech key not configured. Set 'Speech:Key' in user secrets or AZURE_SPEECH_KEY env var.");
 if (string.IsNullOrWhiteSpace(_opts.Region)) throw new InvalidOperationException("Speech region not configured. Set 'Speech:Region' in configuration.");
 if (string.IsNullOrWhiteSpace(_opts.Language)) throw new InvalidOperationException("Speech recognition language not configured. Set 'Speech:Language' in configuration (e.g., 'en-US').");
 if (a.TargetChannels !=1 || a.TargetBitsPerSample !=16) throw new InvalidOperationException("AudioOptions must produce 16-bit mono PCM for Azure Speech push-stream (TargetChannels=1, TargetBitsPerSample=16).");
 var speechConfig = SpeechConfig.FromSubscription(key, _opts.Region);
 speechConfig.SpeechRecognitionLanguage = _opts.Language;
 var streamFormat = AudioStreamFormat.GetWaveFormatPCM((uint)a.TargetSampleRate, (byte)a.TargetBitsPerSample, (byte)a.TargetChannels);
 _pushStream = AudioInputStream.CreatePushStream(streamFormat);
 var audioConfig = AudioConfig.FromStreamInput(_pushStream);
 _recognizer = new SpeechRecognizer(speechConfig, audioConfig);
 _recognizer.Recognizing += (s,e)=>{ if (!string.IsNullOrWhiteSpace(e.Result.Text)) _log.LogInformation("{Tag} [partial] {Text}", _channelTag, e.Result.Text); };
 _recognizer.Recognized += (s,e)=>{ if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text)) _log.LogInformation("{Tag} [final] {Text}", _channelTag, e.Result.Text); };
 _recognizer.Canceled += (s,e)=>{ _log.LogWarning("{Tag} canceled: Reason={Reason}, ErrorCode={ErrorCode}, Error={Error}", _channelTag, e.Reason, e.ErrorCode, e.ErrorDetails); };
 _recognizer.SessionStarted += (s,e)=> _log.LogInformation("{Tag} session started", _channelTag);
 _recognizer.SessionStopped += (s,e)=> _log.LogInformation("{Tag} session stopped", _channelTag);
 }
 public SpeechPushClient SetChannelTag(string tag){ if (!string.IsNullOrWhiteSpace(tag)) _channelTag = tag; return this; }
 public async Task StartAsync(CancellationToken ct){ if (_started) return; await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false); _meterSw.Restart(); _started = true; }
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
 public async ValueTask DisposeAsync()
 {
 try{ await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);} catch {}
 _pushStream.Close();
 _recognizer.Dispose();
 }
}
