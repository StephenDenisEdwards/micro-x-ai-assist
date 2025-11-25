using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GeminiLiveConsole.Models;

namespace GeminiLiveConsole;

public sealed class GeminiLiveClient : IAsyncDisposable
{
	private readonly string _apiKey;
	private readonly string _model;
	private ClientWebSocket _ws = new();
	private CancellationTokenSource? _cts;
	private Task? _recvTask;

	public event Action? OnOpen;
	public event Action? OnClose;
	public event Action<Exception>? OnError;
	public event Action<GeminiMessage>? OnMessage;
	public bool IsConnected { get; private set; }

	public GeminiLiveClient(string apiKey, string model = "gemini-2.0-flash-exp", string systemPrompt = "You are a dedicated Conversation Monitor and Assistant. Detect QUESTION or IMPERATIVE and call report_intent. Ignore casual speech.")
	{
		_apiKey = apiKey;
		_model = model;
	}

	public async Task ConnectAsync(CancellationToken ct = default)
	{
		if (IsConnected) return;
		_cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		try
		{
			var wsUrl = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={Uri.EscapeDataString(_apiKey)}";

			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine($"[GeminiLiveClient] API URI: {wsUrl}");
			Console.ResetColor();

			_ws = new ClientWebSocket();
			await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
			IsConnected = true;
			OnOpen?.Invoke();
			await SendSetupFrameAsync(_cts.Token);
			_recvTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
		}
		catch (Exception ex)
		{
			OnError?.Invoke(ex);
			await CloseInternalAsync();
		}
	}

	public async Task SendSetupFrameAsync(CancellationToken ct)
	{
		// 🌟 FINAL, STABLE SYSTEM PROMPT 🌟
		// Instructs the model to put the ANSWER in the stream and the CODE in the tool call,
		// but explicitly allows markdown fences for compliance.
		string systemPrompt =
			"You are a C# and .NET expert. Your primary goal is to provide a complete and detailed natural language explanation to the user's query via the main streaming output. " +
			"At the end of your response, you MUST call the 'report_technical_response' function. " +
			"The 'answer' parameter of this function should contain the full natural language answer you provided. " +
			"The 'console_code' parameter must contain a complete, runnable C# console application that demonstrates the concept, wrapped in C# markdown fences. If no code is relevant, you must return a C# comment stating that, for example: '// No code is applicable for this query.'";

		// Instruction for the tool's 'answer' field (simple)
		string answerInstructions =
			"The complete, natural language answer to the user's query. This should match the content streamed in the main response.";

		// Instruction for the tool's 'console_code' field (harmonized with the system prompt)
		string codeDescription =
			"A complete, runnable C# console application (Program.cs content) that illustrates the answer. The code MUST be wrapped in ```csharp ... ``` markdown fences. If no code is applicable, this field MUST contain a C# comment explaining why, such as '// No code example is relevant.'";

		var setupMessage = new
		{
			setup = new
			{
				model = $"models/{_model}",
				generationConfig = new
				{
					responseModalities = new[] { "TEXT" },
				},
				inputAudioTranscription = new { },
				systemInstruction = new
				{
					role = "system",
					parts = new[]
					{
						new { text = systemPrompt }
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
									"Report the answer, classify the intent, and provide a complete, working C# console application code example.",
								parameters = new
								{
									type = "object",
									properties = new
									{
										text = new
										{
											type = "string",
											description = "The verbatim text of the question or command detected."
										},
										type = new
										{
											type = "string",
											@enum = new[] { "QUESTION", "IMPERATIVE" },
											description = "The classification of the detected speech."
										},
										answer = new
										{
											type = "string",
											description = answerInstructions
										},
                                        // 🌟 CRITICAL FIX: USING THE RELAXED, HARMONIZED DESCRIPTION 🌟
                                        console_code = new
										{
											type = "string",
											description = codeDescription
										}
									},
									required = new[] { "text", "type", "answer", "console_code" }
								}
							}
						}
					}
				}
			}
		};

		// --- Debug/Serialization Logic ---
		try
		{
			var pretty = JsonSerializer.Serialize(setupMessage, new JsonSerializerOptions { WriteIndented = true });
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("[GeminiLiveClient] Setup message:");
			Console.ResetColor();
			Console.WriteLine(pretty);
		}
		catch
		{
			Console.WriteLine("Failed to serialize setup message.");
		}

		await SendJsonAsync(setupMessage, ct);
	}

	public async Task SendAudioChunkAsync(byte[] pcm16Buffer, int bytesRecorded, CancellationToken ct = default)
	{
		if (!IsConnected || _ws.State != WebSocketState.Open) return;
		var base64 = Convert.ToBase64String(pcm16Buffer, 0, bytesRecorded);
		var audioFrame = new
		{
			realtimeInput = new
			{
				audio = new
				{
					mimeType = "audio/pcm;rate=16000",
					data = base64
				}
			}
		};
		await SendJsonAsync(audioFrame, ct);
	}

	// 🌟 FUNCTION TO SEND TOOL RESPONSE 🌟
	public async Task SendToolResponseAsync(ToolFunctionCall call, CancellationToken ct = default)
	{
		var payload = new
		{
			toolResponse = new
			{
				functionResponses = new[]
				{
					new { id = call.Id, name = call.Name, response = new { result = "logged" } }
				}
			}
		};
		await SendJsonAsync(payload, ct);
	}

	public async Task SendAudioStreamEndAsync(CancellationToken ct = default)
	{
		if (!IsConnected) return;
		var endMessage = new
		{
			realtimeInput = new
			{
				audioStreamEnd = true
			}
		};
		await SendJsonAsync(endMessage, ct);
	}

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private async Task SendJsonAsync(object payload, CancellationToken ct)
	{
		var json = JsonSerializer.Serialize(payload);
		var bytes = Encoding.UTF8.GetBytes(json);
		await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
	}

	private async Task ReceiveLoopAsync(CancellationToken token)
	{
		var buffer = new byte[16 * 1024];
		try
		{
			while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
			{
				using var ms = new MemoryStream();
				WebSocketReceiveResult? result;
				do
				{
					result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
					if (result.MessageType == WebSocketMessageType.Close)
					{
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.WriteLine(result.CloseStatusDescription);
						Console.ResetColor();

						await CloseInternalAsync();
						return;
					}
					ms.Write(buffer, 0, result.Count);
				} while (!result.EndOfMessage);

				var data = ms.ToArray();
				var json = Encoding.UTF8.GetString(data);
				try
				{
					var msg = JsonSerializer.Deserialize<GeminiMessage>(json, JsonOpts);
					if (msg != null)
						OnMessage?.Invoke(msg);
				}
				catch (JsonException jex)
				{
					OnError?.Invoke(jex);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// normal
		}
		catch (Exception ex)
		{
			OnError?.Invoke(ex);
			await CloseInternalAsync();
		}
	}

	public async Task DisconnectAsync()
	{
		await CloseInternalAsync();
	}

	private async Task CloseInternalAsync()
	{
		if (_ws.State == WebSocketState.Open)
		{
			try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None); } catch { }
		}
		IsConnected = false;
		_cts?.Cancel();
		OnClose?.Invoke();
	}

	public async ValueTask DisposeAsync()
	{
		await CloseInternalAsync();
		_ws.Dispose();
		_cts?.Dispose();
	}
}