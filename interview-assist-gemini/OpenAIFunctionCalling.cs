using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeminiLiveConsole;

public class OpenAIRealtimeAPI : IRealtimeApi
{
	private readonly string _apiKey;
	private const string WS_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17";

	private ClientWebSocket _ws;
	private CancellationTokenSource _cts;
	private StringBuilder _currentFunctionArgs = new StringBuilder();
	private string _currentFunctionName = "";
	private IAudioCaptureService? _audio;
	private string? _currentCallId;

	public OpenAIRealtimeAPI(IAudioCaptureService audioCaptureService, string openAiApiKey)
	{
		_audio = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
		_apiKey = openAiApiKey ?? throw new ArgumentNullException(nameof(openAiApiKey));
	}

	// Events
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
	public event Action<string, string, string>? OnFunctionCallResponse;

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

			_audio.OnAudioChunk += bytes => _ = SendAudioChunk(bytes);
			_audio.Start();
			OnInfo?.Invoke("AudioCaptureService active");

			OnReady?.Invoke();

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
				modalities = new[] { "text" },
				instructions = "You are a C# programming expert. " +
							  "\n\nCRITICAL RULES FOR FUNCTION CALLING:" +
							  "\n1. You MUST call report_technical_response for EVERY programming question" +
							  "\n2. You MUST provide BOTH parameters EVERY TIME:" +
							  "\n   - answer: Your explanation" +
							  "\n   - console_code: A complete C# code example" +
							  "\n3. NEVER omit console_code. If you don't have a code example, use this:" +
							  "\n   console_code: \"// No code example applicable\"" +
							  "\n4. The code MUST be a complete, runnable C# console application" +
							  "\n5. Include using statements and a Main method" +
							  "\n\nExample of correct function call:" +
							  "\n{" +
							  "\n  \"answer\": \"Explanation here\"," +
							  "\n  \"console_code\": \"using System;\\nclass Program { static void Main() { } }\"" +
							  "\n}",
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
						description = "MANDATORY function to call for programming questions. MUST include both 'answer' AND 'console_code' - no exceptions!",
						parameters = new
						{
							type = "object",
							properties = new
							{
								answer = new
								{
									type = "string",
									description = "Detailed explanation of the C# concept"
								},
								console_code = new
								{
									type = "string",
									description = "REQUIRED - NEVER omit this. Complete runnable C# console application with using statements and Main method. Minimum: 'using System;\\nclass Program { static void Main() { Console.WriteLine(\"Example\"); } }'"
								}
							},
							required = new[] { "answer", "console_code" }
						}
					}
				},
				tool_choice = "required"
			}
		};

		await SendMessage(config);
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
			item = new
			{
				type = "message",
				role = "user",
				content = new object[] { new { type = "input_text", text = text } }
			}
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
				PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
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
					if (root.TryGetProperty("call_id", out var callId))
					{
						_currentCallId = callId.GetString();
					}

					if (root.TryGetProperty("delta", out var delta))
					{
						var deltaText = delta.GetString();
						_currentFunctionArgs.Append(deltaText);
					}
					break;

				case "response.function_call_arguments.done":
					if (root.TryGetProperty("name", out var funcName))
					{
						_currentFunctionName = funcName.GetString();
					}

					try
					{
						var completeArgsJson = _currentFunctionArgs.ToString();

						if (!string.IsNullOrWhiteSpace(completeArgsJson))
						{
							using var argsDoc = JsonDocument.Parse(completeArgsJson);
							var args = argsDoc.RootElement;

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
								OnWarning?.Invoke("No console_code provided - extracting from answer...");
								var (_, extractedCode) = ExtractCodeFromText(answerText);
								codeText = extractedCode;
							}

							OnFunctionCallResponse?.Invoke(_currentFunctionName, answerText, codeText);
						}
					}
					catch (JsonException jsonEx)
					{
						OnError?.Invoke(jsonEx);
					}
					finally
					{
						_currentFunctionArgs.Clear();
						_currentFunctionName = "";
						_currentCallId = "";
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

				// Ignore verbose events
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

	// Minimal: used by SendTextAsync echo and function args extraction
	private (string explanation, string code) ExtractCodeFromText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return ("", "");

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