using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

public class GeminiLiveAPI
{
	private static string apiKey;
	private static string WS_URL => $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent?key={apiKey}";

	private ClientWebSocket _ws;
	private WaveInEvent _waveIn;
	private CancellationTokenSource _cts;

	public static async Task Go(string geminiApiKey)
	{
		apiKey = geminiApiKey;
		var liveAPI = new GeminiLiveAPI();
		await liveAPI.Start();
	}

	public async Task Start()
	{
		_cts = new CancellationTokenSource();

		Console.WriteLine("=== Gemini Live API - Voice to Text ===\n");

		// Connect to WebSocket
		_ws = new ClientWebSocket();
		await _ws.ConnectAsync(new Uri(WS_URL), CancellationToken.None);
		Console.WriteLine("✓ Connected");

		// Start receiving
		_ = Task.Run(() => ReceiveResponses());

		// Send setup configuration
		await SendSetup();

		// Setup audio input (microphone)
		SetupAudioInput();

		Console.WriteLine("✓ Ready! Start speaking...");
		Console.WriteLine("Press Q to quit\n");

		while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

		Cleanup();
	}

	private async Task SendSetup()
	{
		var setup = new
		{
			setup = new
			{
				model = "models/gemini-2.0-flash-exp",
				generation_config = new
				{
					response_modalities = new[] { "TEXT" }
				},
				system_instruction = new
				{
					parts = new[]
					{
						new
						{
							text = "You are a C# programming expert. " +
							       "CRITICAL INSTRUCTION: For EVERY C# or programming question you receive, " +
							       "you MUST call the 'report_technical_response' function. " +
							       "DO NOT respond with plain text for programming questions. " +
							       "ALWAYS use the function with both 'answer' and 'console_code' parameters. " +
							       "The 'answer' should contain your explanation, and 'console_code' should contain a runnable C# example. " +
							       "If no code example is needed, set 'console_code' to '// No code example needed'."
						}
					}
				},
				tools = new[]
				{
					new
					{
						function_declarations = new[]
						{
							new
							{
								name = "report_technical_response",
								description =
									"MUST be called for every C# or programming question. Provides technical answer with code.",
								parameters = new
								{
									type = "object",
									properties = new
									{
										answer = new
										{
											type = "string",
											description = "Complete detailed explanation of the concept"
										},
										console_code = new
										{
											type = "string",
											description = "Complete runnable C# code example demonstrating the concept"
										}
									},
									required = new[] { "answer", "console_code" }
								}
							}
						}
					}
				}
			}
		};

		await SendMessage(setup);
		Console.WriteLine("✓ Setup complete");
	}

	private void SetupAudioInput()
	{
		// Capture from microphone: 16kHz, 16-bit, mono (required by Live API)
		_waveIn = new WaveInEvent
		{
			WaveFormat = new WaveFormat(16000, 16, 1),
			BufferMilliseconds = 100
		};

		_waveIn.DataAvailable += async (s, e) =>
		{
			// Send audio chunk to Gemini
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
			realtime_input = new
			{
				media_chunks = new[]
				{
					new
					{
						mime_type = "audio/pcm;rate=16000",
						data = base64Audio
					}
				}
			}
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

	private void ProcessResponse(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// Handle text responses
			if (root.TryGetProperty("serverContent", out var serverContent))
			{
				if (serverContent.TryGetProperty("modelTurn", out var modelTurn))
				{
					if (modelTurn.TryGetProperty("parts", out var parts))
					{
						foreach (var part in parts.EnumerateArray())
						{
							// Text response from Gemini
							if (part.TryGetProperty("text", out var text))
							{
								Console.ForegroundColor = ConsoleColor.Yellow;
								Console.WriteLine($"\n[Gemini]: {text.GetString()}");
								Console.ResetColor();
							}
						}
					}
				}
			}

			// Handle function calls
			if (root.TryGetProperty("toolCall", out var toolCall))
			{
				if (toolCall.TryGetProperty("functionCalls", out var functionCalls))
				{
					foreach (var fc in functionCalls.EnumerateArray())
					{
						if (fc.TryGetProperty("args", out var args))
						{
							Console.ForegroundColor = ConsoleColor.Green;
							Console.WriteLine("\n" + new string('═', 70));
							Console.WriteLine("📋 TECHNICAL RESPONSE");
							Console.WriteLine(new string('═', 70));
							Console.ResetColor();

							if (args.TryGetProperty("answer", out var answer))
							{
								Console.WriteLine($"\n{answer.GetString()}\n");
							}

							//if (args.TryGetProperty("consoleCode", out var code))
							if (args.TryGetProperty("console_code", out var code))
							{
									var codeText = code.GetString();
								if (!string.IsNullOrWhiteSpace(codeText))
								{
									Console.ForegroundColor = ConsoleColor.Cyan;
									Console.WriteLine("Code:");
									Console.WriteLine(new string('-', 70));
									Console.WriteLine(codeText);
									Console.WriteLine(new string('-', 70));
									Console.ResetColor();
								}
							}
						}
					}
				}
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