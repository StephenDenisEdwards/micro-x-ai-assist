using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using GeminiLiveConsole; // for AudioCaptureService

public class OpenAIRealtimeAPI2 : IRealtimeApi
{
	private readonly string _apiKey;
	private const string WS_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17";

	private ClientWebSocket _ws;
	private CancellationTokenSource _cts;
	private IAudioCaptureService _audio; // replaced WaveInEvent

	public OpenAIRealtimeAPI2(IAudioCaptureService audioCaptureService, string openAiApiKey)
	{
		_audio = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
		_apiKey = openAiApiKey ?? throw new ArgumentNullException(nameof(openAiApiKey));
	}

	// Events to decouple from console
	public event Action? OnConnected;
	public event Action? OnReady;
	public event Action? OnDisconnected;
	public event Action<string>? OnInfo;
	public event Action<string>? OnWarning;
	public event Action<string>? OnDebug;
	public event Action<Exception>? OnError;
	public event Action<string>? OnUserTranscript;
	public event Action? OnSpeechStarted;
	public event Action? OnSpeechStopped;
	public event Action<string>? OnAssistantTextDelta;
	public event Action? OnAssistantTextDone;
	public event Action<string>? OnAssistantAudioTranscriptDelta;
	public event Action? OnAssistantAudioTranscriptDone;
	// functionName, answer, consoleCode
	public event Action<string, string, string>? OnFunctionCallResponse;

	// Track function calls by call_id
	private Dictionary<string, StringBuilder> _functionCallBuffers = new Dictionary<string, StringBuilder>();
	private Dictionary<string, string> _functionCallNames = new Dictionary<string, string>();

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		try
		{
			_ws = new ClientWebSocket();
			_ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
			_ws.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

			await _ws.ConnectAsync(new Uri(WS_URL), _cts.Token);
			OnConnected?.Invoke();

			_ = Task.Run(() => ReceiveResponses(), _cts.Token);

			await SendSessionConfig();
			SetupAudioInput();
			OnReady?.Invoke();

			// Wait until cancelled
			try
			{
				await Task.Delay(Timeout.Infinite, _cts.Token);
			}
			catch (OperationCanceledException) { }
		}
		catch (Exception ex)
		{
			OnError?.Invoke(ex);
		}
		finally
		{
			Cleanup();
			OnDisconnected?.Invoke();
		}
	}

	private async Task SendSessionConfig()
	{
		var config = new
		{
			type = "session.update",
			session = new
			{
				modalities = new[] { "text" }, // kept as-is
				instructions = "You are a C# programming expert assistant.\n\n" +
							  "MANDATORY BEHAVIOR:\n" +
							  "When calling report_technical_response, you MUST ALWAYS provide both parameters:\n" +
							  "1. answer - your explanation\n" +
							  "2. console_code - complete C# code\n\n" +
							  "NEVER call the function with only 'answer'. ALWAYS include 'console_code'.\n" +
							  "If no code is needed, set console_code to: \"// No code example needed\"\n\n" +
							  "The console_code must be a complete, runnable C# program with Main method.",
				voice = "alloy",
				input_audio_format = "pcm16",
				output_audio_format = "pcm16",
				input_audio_transcription = new
				{
					model = "whisper-1"
				},
				turn_detection = new
				{
					type = "server_vad",
					threshold = 0.5,
					prefix_padding_ms = 300,
					silence_duration_ms = 500
				},
				tools = new[]
				{
					new
					{
						type = "function",
						name = "report_technical_response",
						description = "Answer programming questions. MUST include both 'answer' and 'console_code' parameters - never omit console_code.",
						parameters = new
						{
							type = "object",
							properties = new
							{
								answer = new
								{
									type = "string",
									description = "Explanation of the concept"
								},
								console_code = new
								{
									type = "string",
									description = "Complete C# console application code. REQUIRED - must always be provided. Use '// No code needed' if not applicable."
								}
							},
							required = new[] { "answer", "console_code" }
						}
					}
				},
				tool_choice = "auto"
			}
		};

		await SendMessage(config);
	}

	private void SetupAudioInput()
	{
		_audio.OnAudioChunk += bytes => _ = SendAudioChunk(bytes);
		_audio.Start();
		OnInfo?.Invoke("AudioCaptureService active (microphone)");
	}

	private async Task SendAudioChunk(byte[] audioData)
	{
		var base64Audio = Convert.ToBase64String(audioData);

		var message = new
		{
			type = "input_audio_buffer.append",
			audio = base64Audio
		};

		await SendMessage(message);
	}

	// Minimal impl to satisfy interface for this variant
	public async Task SendTextAsync(string text, bool requestResponse = true, bool interrupt = false)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		if (_ws == null || _ws.State != WebSocketState.Open) return;

		if (interrupt)
		{
			await SendMessage(new { type = "response.cancel" });
		}

		var itemCreate = new
		{
			type = "conversation.item.create",
			role = "user",
			content = new object[] { new { type = "input_text", text = text } }
		};
		await SendMessage(itemCreate);
		OnUserTranscript?.Invoke(text);
		if (requestResponse)
		{
			await SendMessage(new { type = "response.create" });
		}
	}

	private async Task SendMessage(object message)
	{
		try
		{
			var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
			{
				WriteIndented = false
			});

			var bytes = Encoding.UTF8.GetBytes(json);
			await _ws.SendAsync(
				new ArraySegment<byte>(bytes),
				WebSocketMessageType.Text,
				true,
				_cts.Token);
		}
		catch (Exception ex)
		{
			OnError?.Invoke(ex);
		}
	}

	private async Task ReceiveResponses()
	{
		var buffer = new byte[1024 * 64];
		var messageBuilder = new StringBuilder();

		try
		{
			while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
			{
				var result = await _ws.ReceiveAsync(
					new ArraySegment<byte>(buffer),
					_cts.Token);

				if (result.MessageType == WebSocketMessageType.Close)
					break;

				var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
				messageBuilder.Append(chunk);

				if (result.EndOfMessage)
				{
					ProcessResponse(messageBuilder.ToString());
					messageBuilder.Clear();
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			OnError?.Invoke(ex);
		}
	}

	private string FixMalformedJson(string json)
	{
		var result = new StringBuilder(json.Length);
		bool inString = false;
		bool escaped = false;

		for (int i = 0; i < json.Length; i++)
		{
			char c = json[i];

			if (escaped)
			{
				result.Append(c);
				escaped = false;
				continue;
			}

			if (c == '\\')
			{
				result.Append(c);
				escaped = true;
				continue;
			}

			if (c == '"')
			{
				result.Append(c);
				inString = !inString;
				continue;
			}

			if (inString)
			{
				switch (c)
				{
					case '\n':
						result.Append("\\n");
						break;
					case '\r':
						result.Append("\\r");
						break;
					case '\t':
						result.Append("\\t");
						break;
					case '\b':
						result.Append("\\b");
						break;
					case '\f':
						result.Append("\\f");
						break;
					default:
						result.Append(c);
						break;
				}
			}
			else
			{
				result.Append(c);
			}
		}

		return result.ToString();
	}

	private bool IsCompleteJson(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return false;

		var trimmed = json.TrimEnd();
		bool inString = false;
		bool escaped = false;
		int depth = 0;

		for (int i = 0; i < json.Length; i++)
		{
			char c = json[i];

			if (escaped)
			{
				escaped = false;
				continue;
			}

			if (c == '\\')
			{
				escaped = true;
				continue;
			}

			if (c == '"')
			{
				inString = !inString;
				continue;
			}

			if (inString)
				continue;

			if (c == '{')
				depth++;
			else if (c == '}')
				depth--;
		}

		return !inString && depth == 0 && trimmed.EndsWith("}");
	}

	private void ParseFunctionArgs(string json, string functionName)
	{
		var fixedJson = FixMalformedJson(json);
		JsonElement args;
		JsonDocument argsDoc = null;
		try
		{
			argsDoc = JsonDocument.Parse(fixedJson);
			args = argsDoc.RootElement;

		}
		catch (Exception)
		{
			var repaired  = JsonRepairUtility.Repair(json);

			if (!string.IsNullOrEmpty(repaired))
			{
				argsDoc = JsonDocument.Parse(repaired);
				args = argsDoc.RootElement;
			}
			else
			{
				argsDoc?.Dispose();
				throw;
			}
		}

		string answerText = "";
		string codeText = "";

		if (args.TryGetProperty("answer", out var answer))
		{
			answerText = answer.GetString();
		}

		if (args.TryGetProperty("console_code", out var code))
		{
			codeText = code.GetString();
		}

		if (string.IsNullOrWhiteSpace(codeText) && !string.IsNullOrWhiteSpace(answerText))
		{
			OnWarning?.Invoke("No console_code provided - attempting to extract from answer.");
			var (_, extractedCode) = ExtractCodeFromText(answerText);
			codeText = extractedCode;
		}

		OnFunctionCallResponse?.Invoke(functionName ?? string.Empty, answerText ?? string.Empty, codeText ?? string.Empty);

		argsDoc?.Dispose();
	}

	private void ProcessResponse(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var eventType))
				return;

			var type = eventType.GetString();

			switch (type)
			{
				case "session.created":
				case "session.updated":
					// Session events
					break;

				case "conversation.item.input_audio_transcription.completed":
					if (root.TryGetProperty("transcript", out var transcript))
					{
						OnUserTranscript?.Invoke(transcript.GetString());
					}
					break;

				case "input_audio_buffer.speech_started":
					OnSpeechStarted?.Invoke();
					break;

				case "input_audio_buffer.speech_stopped":
					OnSpeechStopped?.Invoke();
					break;

				case "response.function_call_arguments.delta":
					string callId = "";

					if (root.TryGetProperty("call_id", out var callIdProp))
					{
						callId = callIdProp.GetString();
					}

					if (!string.IsNullOrEmpty(callId))
					{
						// Create buffer for this call_id if it doesn't exist
						if (!_functionCallBuffers.ContainsKey(callId))
						{
							_functionCallBuffers[callId] = new StringBuilder();
						}

						if (root.TryGetProperty("delta", out var delta))
						{
							var deltaText = delta.GetString();
							_functionCallBuffers[callId].Append(deltaText);
						}

						// Store function name if provided
						if (root.TryGetProperty("name", out var name))
						{
							_functionCallNames[callId] = name.GetString();
						}
					}
					break;

				case "response.function_call_arguments.done":
					string doneCallId = "";
					string functionName = "";

					if (root.TryGetProperty("call_id", out var doneCallIdProp))
					{
						doneCallId = doneCallIdProp.GetString();
					}

					if (root.TryGetProperty("name", out var funcNameProp))
					{
						functionName = funcNameProp.GetString();
					}
					else if (_functionCallNames.ContainsKey(doneCallId))
					{
						functionName = _functionCallNames[doneCallId];
					}

					if (!string.IsNullOrEmpty(doneCallId) && _functionCallBuffers.ContainsKey(doneCallId))
					{
						var raw = _functionCallBuffers[doneCallId].ToString();
						if (!string.IsNullOrWhiteSpace(raw))
						{
							if (!IsCompleteJson(raw))
							{
								OnWarning?.Invoke("Incomplete function args – waiting for remaining deltas...");

								var capturedCallId = doneCallId;
								var capturedFuncName = functionName;

								_ = Task.Run(async () =>
								{
									try
									{
										await Task.Delay(250);
										if (_functionCallBuffers.TryGetValue(capturedCallId, out var retrySb))
										{
											var retry = retrySb.ToString();
											if (IsCompleteJson(retry))
											{
												ParseFunctionArgs(retry, capturedFuncName);
											}
											else
											{
												OnWarning?.Invoke("Still incomplete after retry – skipping parse.");
												throw new Exception("JSON Incomplete or malformed.");
											}
										}
									}
									catch (Exception ex)
									{
										OnError?.Invoke(ex);
									}
									finally
									{
										_functionCallBuffers.Remove(capturedCallId);
										_functionCallNames.Remove(capturedCallId);
									}
								});
							}
							else
							{
								try
								{
									ParseFunctionArgs(raw, functionName);
									SaveRawJsonToFile(functionName, Guid.NewGuid().ToString(), raw);
								}
								catch (Exception ex)
								{
									OnError?.Invoke(ex);
								}
								finally
								{
									_functionCallBuffers.Remove(doneCallId);
									_functionCallNames.Remove(doneCallId);
								}
							}
						}
					}
					break;
				case "response.text.delta":
					if (root.TryGetProperty("delta", out var textDelta))
					{
						OnAssistantTextDelta?.Invoke(textDelta.GetString());
					}
					break;

				case "response.text.done":
					OnAssistantTextDone?.Invoke();
					break;

				case "response.audio_transcript.delta":
					if (root.TryGetProperty("delta", out var audioDelta))
					{
						OnAssistantAudioTranscriptDelta?.Invoke(audioDelta.GetString());
					}
					break;

				case "response.audio_transcript.done":
					OnAssistantAudioTranscriptDone?.Invoke();
					break;

				case "response.done":
					break;

				case "error":
					if (root.TryGetProperty("error", out var error))
					{
						var msg = error.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
						OnWarning?.Invoke($"Error: {msg}");
					}
					break;

				// ignored verbose events
				case "response.audio.delta":
				case "input_audio_buffer.committed":
				case "input_audio_buffer.cleared":
				case "conversation.item.created":
				case "response.created":
				case "response.output_item.added":
				case "response.output_item.done":
				case "response.content_part.added":
				case "response.content_part.done":
					break;

				default:
					OnDebug?.Invoke($"Event: {type}");
					break;
			}
		}
		catch (JsonException jsonEx)
		{
			OnError?.Invoke(jsonEx);
		}
		catch (Exception ex)
		{
			OnError?.Invoke(ex);
		}
	}

	private void SaveRawJsonToFile(string functionName, string callId, string rawJson)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(rawJson)) return;

			var safeFunc = string.IsNullOrWhiteSpace(functionName) ? "unknown" : MakeFileNameSafe(functionName);
			var safeCall = string.IsNullOrWhiteSpace(callId) ? "nocallid" : MakeFileNameSafe(callId);
			var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
			var baseName = $"{safeFunc}_{safeCall}_{timestamp}";
			var dir = Path.Combine(AppContext.BaseDirectory, "function-args-logs");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, baseName + ".json");

			int suffix = 0;
			while (File.Exists(path))
			{
				suffix++;
				path = Path.Combine(dir, $"{baseName}_{suffix}.json");
			}

			File.WriteAllText(path, rawJson);
			OnDebug?.Invoke($"Saved raw function args to: {path}");
		}
		catch (Exception fileEx)
		{
			OnError?.Invoke(fileEx);
		}
	}

	private static string MakeFileNameSafe(string name)
	{
		foreach (var c in System.IO.Path.GetInvalidFileNameChars())
		{
			name = name.Replace(c, '_');
		}
		return name;
	}

	private (string explanation, string code) ExtractCodeFromText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return ("", "");

		// Pattern to match code blocks with optional language identifier
		var codeBlockPattern = @"```(?:csharp|cs|c#)?\s*\n(.*?)\n```";
		var regex = new System.Text.RegularExpressions.Regex(codeBlockPattern,
			System.Text.RegularExpressions.RegexOptions.Singleline |
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		var match = regex.Match(text);

		if (match.Success)
		{
			var code = match.Groups[1].Value.Trim();
			var explanation = regex.Replace(text, "\n[CODE EXTRACTED]\n").Trim();
			return (explanation, code);
		}

		return (text, "");
	}

	private void Cleanup()
	{
		_audio?.Stop();
		_audio?.Dispose();
		_cts?.Cancel();
		_ws?.Dispose();
	}
}