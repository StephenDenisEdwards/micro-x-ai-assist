using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

public class OpenAIRealtimeAPI
{
	private static string apiKey;
	private const string WS_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17";

	private ClientWebSocket _ws;
	private WaveInEvent _waveIn;
	private CancellationTokenSource _cts;
	private StringBuilder _currentFunctionArgs = new StringBuilder();
	private string _currentFunctionName = "";
	public static async Task Go(string openApiKey)
	{
		apiKey = openApiKey;
		var api = new OpenAIRealtimeAPI();
		await api.Start();
	}

	public async Task Start()
	{
		_cts = new CancellationTokenSource();

		Console.WriteLine("=== OpenAI Realtime API ===\n");

		// Connect with auth header
		_ws = new ClientWebSocket();
		_ws.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
		_ws.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

		await _ws.ConnectAsync(new Uri(WS_URL), CancellationToken.None);
		Console.WriteLine("✓ Connected");

		_ = Task.Run(() => ReceiveResponses());

		await SendSessionConfig();
		SetupAudioInput();

		Console.WriteLine("✓ Ready! Start speaking...");
		Console.WriteLine("Press Q to quit\n");

		while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

		Cleanup();
	}

	private async Task SendSessionConfig()
	{
		var config = new
		{
			type = "session.update",
			session = new
			{
				modalities = new[] { "text", "audio" },
				instructions = "You are a C# programming expert. " +
							  "When users ask programming questions, always call the report_technical_response function " +
							  "with both an explanation and code example.",
				voice = "alloy",
				input_audio_format = "pcm16",
				output_audio_format = "pcm16",
				input_audio_transcription = new
				{
					model = "whisper-1"
				},
				turn_detection = new
				{
					type = "server_vad",  // Automatic voice activity detection
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
						description = "Provides a technical C# answer with code example",
						parameters = new
						{
							type = "object",
							properties = new
							{
								answer = new
								{
									type = "string",
									description = "Detailed explanation of the concept"
								},
								console_code = new
								{
									type = "string",
									description = "Complete runnable C# code example"
								}
							},
							required = new[] { "answer", "console_code" }
						}
					}
				},
				tool_choice = "auto"  // Can also use "required" to force function calling
			}
		};

		await SendMessage(config);
		Console.WriteLine("✓ Session configured");
	}

	private void SetupAudioInput()
	{
		// OpenAI uses 24kHz for input
		_waveIn = new WaveInEvent
		{
			WaveFormat = new WaveFormat(24000, 16, 1),
			BufferMilliseconds = 100
		};

		_waveIn.DataAvailable += async (s, e) =>
		{
			byte[] audioData = new byte[e.BytesRecorded];
			Array.Copy(e.Buffer, audioData, e.BytesRecorded);
			await SendAudioChunk(audioData);
		};

		_waveIn.StartRecording();
		Console.WriteLine("✓ Microphone active");
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
			Console.WriteLine($"Send error: {ex.Message}");
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
			Console.WriteLine($"Receive error: {ex.Message}");
		}
	}




	// ... (rest of your existing code)

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
				case "conversation.item.input_audio_transcription.completed":
					// User's speech transcribed
					if (root.TryGetProperty("transcript", out var transcript))
					{
						Console.ForegroundColor = ConsoleColor.Gray;
						Console.WriteLine($"\n[You said]: {transcript.GetString()}");
						Console.ResetColor();
					}
					break;

				case "response.function_call_arguments.delta":
					// Function arguments streaming in
					if (root.TryGetProperty("delta", out var delta))
					{
						_currentFunctionArgs.Append(delta.GetString());
					}

					if (root.TryGetProperty("name", out var name))
					{
						_currentFunctionName = name.GetString();
					}
					break;

				case "response.function_call_arguments.done":
					// Function call completed - now we have all arguments
					try
					{
						var completeArgsJson = _currentFunctionArgs.ToString();

						if (!string.IsNullOrWhiteSpace(completeArgsJson))
						{
							using var argsDoc = JsonDocument.Parse(completeArgsJson);
							var args = argsDoc.RootElement;

							Console.ForegroundColor = ConsoleColor.Green;
							Console.WriteLine("\n" + new string('═', 70));
							Console.WriteLine($"📋 FUNCTION CALL: {_currentFunctionName}");
							Console.WriteLine(new string('═', 70));
							Console.ResetColor();

							if (args.TryGetProperty("answer", out var answer))
							{
								Console.WriteLine($"\n{answer.GetString()}\n");
							}

							if (args.TryGetProperty("console_code", out var code))
							{
								var codeText = code.GetString();
								Console.ForegroundColor = ConsoleColor.Cyan;
								Console.WriteLine("Code:");
								Console.WriteLine(new string('-', 70));
								Console.WriteLine(codeText);
								Console.WriteLine(new string('-', 70));
								Console.ResetColor();
								Console.WriteLine();
							}
						}
					}
					catch (JsonException jsonEx)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine($"\nJSON Parse Error: {jsonEx.Message}");
						Console.WriteLine($"Accumulated args: {_currentFunctionArgs}");
						Console.ResetColor();
					}
					finally
					{
						// Reset for next function call
						_currentFunctionArgs.Clear();
						_currentFunctionName = "";
					}
					break;

				case "response.output_item.done":
					// Response item completed
					if (root.TryGetProperty("item", out var item))
					{
						if (item.TryGetProperty("type", out var itemType) &&
							itemType.GetString() == "function_call")
						{
							// Alternative way to get function call data
							if (item.TryGetProperty("name", out var funcName) &&
								item.TryGetProperty("arguments", out var argsStr))
							{
								Console.WriteLine($"\n[Function: {funcName.GetString()}]");
							}
						}
					}
					break;

				case "response.text.delta":
					// Streaming text response
					if (root.TryGetProperty("delta", out var textDelta))
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write(textDelta.GetString());
						Console.ResetColor();
					}
					break;

				case "response.text.done":
					// Text response completed
					Console.WriteLine(); // New line after streaming text
					break;

				case "response.audio_transcript.delta":
					// AI's speech being transcribed
					if (root.TryGetProperty("delta", out var audioDelta))
					{
						Console.ForegroundColor = ConsoleColor.Magenta;
						Console.Write(audioDelta.GetString());
						Console.ResetColor();
					}
					break;

				case "response.audio.delta":
					// Audio response chunk (if you want to play it back)
					// Just indicate audio is playing
					break;

				case "error":
					if (root.TryGetProperty("error", out var error))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine($"\nError: {error.GetProperty("message").GetString()}");
						Console.ResetColor();
					}
					break;

				case "session.created":
					Console.WriteLine("✓ Session created");
					break;

				case "session.updated":
					Console.WriteLine("✓ Session updated");
					break;

				case "input_audio_buffer.speech_started":
					Console.ForegroundColor = ConsoleColor.Cyan;
					Console.Write("\n🎤 ");
					Console.ResetColor();
					break;

				case "input_audio_buffer.speech_stopped":
					Console.WriteLine(" (processing...)");
					break;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Parse error: {ex.Message}");
			Console.WriteLine($"JSON: {json.Substring(0, Math.Min(500, json.Length))}...");
		}
	}
	private void ProcessResponse_OLD_1(string json)
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
				case "conversation.item.input_audio_transcription.completed":
					// User's speech transcribed
					if (root.TryGetProperty("transcript", out var transcript))
					{
						Console.ForegroundColor = ConsoleColor.Gray;
						Console.WriteLine($"\n[You said]: {transcript.GetString()}");
						Console.ResetColor();
					}
					break;

				case "response.function_call_arguments.done":
					// Function call completed
					if (root.TryGetProperty("name", out var funcName) &&
						root.TryGetProperty("arguments", out var argsJson))
					{
						var args = JsonSerializer.Deserialize<JsonElement>(argsJson.GetString());

						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\n" + new string('═', 70));
						Console.WriteLine($"📋 FUNCTION CALL: {funcName.GetString()}");
						Console.WriteLine(new string('═', 70));
						Console.ResetColor();

						if (args.TryGetProperty("answer", out var answer))
						{
							Console.WriteLine($"\n{answer.GetString()}\n");
						}

						if (args.TryGetProperty("console_code", out var code))
						{
							Console.ForegroundColor = ConsoleColor.Cyan;
							Console.WriteLine("Code:");
							Console.WriteLine(new string('-', 70));
							Console.WriteLine(code.GetString());
							Console.WriteLine(new string('-', 70));
							Console.ResetColor();
						}
					}
					break;

				case "response.text.delta":
					// Streaming text response
					if (root.TryGetProperty("delta", out var delta))
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write(delta.GetString());
						Console.ResetColor();
					}
					break;

				case "response.audio.delta":
					// Audio response chunk (if you want to play it back)
					Console.Write("🔊");
					break;

				case "error":
					if (root.TryGetProperty("error", out var error))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine($"\nError: {error.GetProperty("message").GetString()}");
						Console.ResetColor();
					}
					break;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Parse error: {ex.Message}");
		}
	}

	private void Cleanup()
	{
		_waveIn?.StopRecording();
		_waveIn?.Dispose();
		_cts?.Cancel();
		_ws?.Dispose();
		Console.WriteLine("\nStopped.");
	}
}