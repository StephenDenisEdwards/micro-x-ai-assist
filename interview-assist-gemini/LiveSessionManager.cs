using GeminiLiveConsole.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeminiLiveConsole;

public sealed class LiveSessionManager
{
	private readonly GeminiLiveClient _client;
	private readonly AudioCaptureService _audio;

	// --- NEW EVENT FOR CODE OUTPUT ---
	public event Action<string>? OnCodeExample; // The complete, runnable C# code block

	// Existing Events
	public event Action<string>? OnTranscript; // aggregated
	public event Action<string>? OnInputTranscriptionUpdate; // incremental microphone transcription
	public event Action<string>? OnAssistantResponsePart; // streamed assistant output (delta)
	public event Action<DetectedIntent>? OnIntent; // per-call updates (optional)
	public event Action<DetectedIntent>? OnIntentFinal; // finalized per turn
	public event Action<double>? OnVolume;
	public event Action<Exception>? OnError;
	public event Action? OnDisconnect;
	public event Action? OnAssistantTurnComplete; // raised when TurnComplete=true

	// Track assistant streaming state to dedupe cumulative parts
	private string _assistantLastFullText = string.Empty;

	// Track pending intent (take the longest text within the turn)
	private string _pendingIntentText = string.Empty;
	private IntentType _pendingIntentType = IntentType.QUESTION;

	// Capture full answer provided inline via tool call
	private string _pendingToolAnswer = string.Empty;

	// --- NEW STATE TRACKING FOR CODE ---
	private string _pendingToolCode = string.Empty;

	public LiveSessionManager(string apiKey, string model, AudioInputSource audioSource = AudioInputSource.Microphone)
	{
		_client = new GeminiLiveClient(apiKey, model);
		_audio = new AudioCaptureService(16000, audioSource);

		_client.OnOpen += () => _audio.Start();
		_client.OnMessage += HandleMessage_2;
		_client.OnError += e => OnError?.Invoke(e);
		_client.OnClose += () =>
		{
			_audio.Stop();
			OnDisconnect?.Invoke();
		};

		_audio.OnAudioChunk += async chunk =>
		{
			var rms = ComputeRms(chunk);
			OnVolume?.Invoke(rms);
			await _client.SendAudioChunkAsync(chunk, chunk.Length);
		};
	}

	public Task ConnectAsync(CancellationToken ct = default) => _client.ConnectAsync(ct);
	public Task DisconnectAsync() => _client.DisconnectAsync();

	// Allow manual control of audio capture
	public void StartAudio() => _audio.Start();
	public void StopAudio() => _audio.Stop();

	// Switch between microphone and system (loopback) audio
	public void UseMicrophone() => _audio.SetSource(AudioInputSource.Microphone);
	public void UseSystemAudio() => _audio.SetSource(AudioInputSource.Loopback);
	public AudioInputSource CurrentAudioSource => _audio.Source;

	// Forward end-of-stream signal to underlying client
	public Task SendAudioStreamEndAsync(CancellationToken ct = default) => _client.SendAudioStreamEndAsync(ct);

	// Renamed to HandleMessage_OLD as it's no longer used, kept for context
	private void HandleMessage_OLD(GeminiMessage msg)
	{
		// ... original HandleMessage logic
	}

	private void HandleMessage_2(GeminiMessage msg)
	{
		var transcript = msg.ServerContent?.InputTranscription?.Text;
		if (!string.IsNullOrWhiteSpace(transcript))
		{
			OnTranscript?.Invoke(transcript);
			OnInputTranscriptionUpdate?.Invoke(transcript);
		}

		// --- Assistant Streaming Logic (Unchanged) ---
		var parts = msg.ServerContent?.ModelTurn?.Parts;
		if (parts != null)
		{
			var fullText = string.Concat(parts.Select(p => p.Text ?? string.Empty));
			if (fullText.Length > 0)
			{
				int lcp = 0;
				int max = Math.Min(_assistantLastFullText.Length, fullText.Length);
				while (lcp < max && _assistantLastFullText[lcp] == fullText[lcp]) lcp++;

				if (fullText.Length > _assistantLastFullText.Length && lcp == _assistantLastFullText.Length)
				{
					var delta = fullText.Substring(_assistantLastFullText.Length);
					_assistantLastFullText = fullText;
					if (delta.Length > 0)
					{
						OnAssistantResponsePart?.Invoke(delta);
						OnTranscript?.Invoke(delta);
					}
				}
				else if (fullText.Length >= _assistantLastFullText.Length && lcp < fullText.Length)
				{
					var delta = fullText.Substring(lcp);
					_assistantLastFullText = fullText;
					if (delta.Length > 0)
					{
						OnAssistantResponsePart?.Invoke(delta);
						OnTranscript?.Invoke(delta);
					}
				}
				else if (fullText.Length < _assistantLastFullText.Length)
				{
					_assistantLastFullText = fullText;
				}
			}
		}

		// --- Intent/Tool Call Handling (Fixed Logic) ---
		if (msg.ToolCall?.FunctionCalls != null)
		{
			foreach (var fc in msg.ToolCall.FunctionCalls)
			{
				if (fc.Name == "report_technical_response" && fc.Args != null)
				{
					var rawType = fc.Args.TryGetValue("type", out var tp) ? tp?.ToString() ?? "" : "";
					var textVal = fc.Args.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
					var answerVal = fc.Args.TryGetValue("answer", out var ans) ? ans?.ToString() ?? "" : "";
					var codeVal = fc.Args.TryGetValue("console_code", out var code) ? code?.ToString() ?? "" : "";

					// 1. Capture CODE: Always take the latest non-blank code. (FIX for V2 bug)
					if (!string.IsNullOrWhiteSpace(codeVal))
					{
						_pendingToolCode = codeVal;
					}

					// 2. Capture ANSWER: Capture the longest answer seen.
					if (!string.IsNullOrWhiteSpace(answerVal) && answerVal.Length > _pendingToolAnswer.Length)
						_pendingToolAnswer = answerVal;

					// 3. Capture INTENT: Uses the latest, longest text.
					if (!string.IsNullOrWhiteSpace(textVal) && textVal.Length >= _pendingIntentText.Length)
					{
						_pendingIntentText = textVal;
						// NOTE: Using the robust QUESTION check. Add "or QIESTIOM" back if needed.
						_pendingIntentType = rawType is "QUESTION" ? IntentType.QUESTION : IntentType.IMPERATIVE;
					}

					OnIntent?.Invoke(new DetectedIntent { Text = textVal, Type = _pendingIntentType });

					// Ack so model can proceed
					_ = _client.SendToolResponseAsync(fc);
				}
			}
		}

		// --- Turn Completion Logic (Fixed Logic) ---
		if (msg.ServerContent?.TurnComplete == true)
		{
			OnAssistantTurnComplete?.Invoke();

			if (!string.IsNullOrWhiteSpace(_pendingIntentText))
			{
				//Console.ForegroundColor = ConsoleColor.Red;
				//Console.WriteLine(_pendingIntentText);
				//Console.ResetColor();
				
				OnIntentFinal?.Invoke(new DetectedIntent
				{
					Text = _pendingIntentText,
					Type = _pendingIntentType
				});
			}

			// 🌟 FIX FOR TRUNCATED ANSWERS: Prioritize the final tool answer 🌟
			if (!string.IsNullOrWhiteSpace(_pendingToolAnswer))
			{
				// To prevent reconciliation issues and ensure the full answer is shown:
				// 1. Clear streaming state to prevent confusion/duplicate output.
				_assistantLastFullText = string.Empty;

				// 2. Emit the definitive, complete answer from the tool call.
				OnAssistantResponsePart?.Invoke(_pendingToolAnswer);
				OnTranscript?.Invoke(_pendingToolAnswer);
			}

			// 🌟 EMIT FINAL CODE OUTPUT (The code is complete now) 🌟
			if (!string.IsNullOrWhiteSpace(_pendingToolCode))
			{
				// Remove the C# markdown fences (```csharp and ```) before emitting the raw code.
				//var cleanedCode = _pendingToolCode
				//	.Replace("```csharp", "")
				//	.Replace("```", "")
				//	.Trim();

				OnCodeExample?.Invoke(_pendingToolCode);
			}

			// Reset state for the next turn
			_assistantLastFullText = string.Empty;
			_pendingIntentText = string.Empty;
			_pendingIntentType = IntentType.QUESTION;
			_pendingToolAnswer = string.Empty;
			_pendingToolCode = string.Empty;
		}
	}

	private static double ComputeRms(byte[] pcm16)
	{
		int samples = pcm16.Length / 2;
		if (samples == 0) return 0;
		double sumSq = 0;
		for (int i = 0; i < samples; i++)
		{
			// Compute short value from two bytes (assuming little-endian)
			short s = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8));
			double norm = s / 32768.0;
			sumSq += norm * norm;
		}
		return Math.Sqrt(sumSq / samples);
	}
}