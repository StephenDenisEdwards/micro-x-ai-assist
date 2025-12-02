using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InterviewLiveConsole;

public class GeminiFunctionCallingExample
{
	private static string apiKey;
	private static string WS_URL = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={apiKey}";

	public static async Task Start(string geminiApiKey)
	{
		apiKey = geminiApiKey;

		using var ws = new ClientWebSocket();
		await ws.ConnectAsync(new Uri(WS_URL), CancellationToken.None);
		Console.WriteLine("Connected to Gemini WebSocket");

		// Send setup message
		await SendSetupMessage(ws);

		// Send user query
		await SendUserQuery(ws, "Explain LINQ deferred execution with an example");

		// Receive and process responses
		await ReceiveResponses(ws);

		Console.WriteLine("Done");
	}

	private static async Task SendSetupMessage(ClientWebSocket ws)
	{
		var setupMessage = new
		{
			setup = new
			{
				model = "models/gemini-2.0-flash-exp",
				systemInstruction = new
				{
					parts = new[]
					{
						new
						{
							text = "You are a C# and .NET expert. For every query, you MUST call the " +
								   "'report_technical_response' function with a complete explanation and code example."
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
								description = "Reports the technical response with explanation and runnable code",
								parameters = new
								{
									type = "object",
									properties = new
									{
										answer = new
										{
											type = "string",
											description = "Complete natural language explanation of the concept"
										},
										console_code = new
										{
											type = "string",
											description = "Complete, runnable C# console application code demonstrating the concept, " +
														  "wrapped in ```csharp markdown fences"
										}
									},
									required = new[] { "answer", "console_code" }
								}
							}
						}
					}
				},
				//toolConfig = new
				//{
				//	functionCallingConfig = new
				//	{
				//		mode = "ANY" // Force function calling
				//	}
				//},
				generationConfig = new
				{
					temperature = 0.7,
					maxOutputTokens = 8192
				}
			}
		};

		var json = JsonSerializer.Serialize(setupMessage);
		var bytes = Encoding.UTF8.GetBytes(json);
		await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
		Console.WriteLine("Setup message sent");
	}

	private static async Task SendUserQuery(ClientWebSocket ws, string query)
	{
		var clientContent = new
		{
			client_content = new
			{
				turns = new[]
				{
					new
					{
						role = "user",
						parts = new[]
						{
							new { text = query }
						}
					}
				},
				turn_complete = true
			}
		};

		var json = JsonSerializer.Serialize(clientContent);
		var bytes = Encoding.UTF8.GetBytes(json);
		await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
		Console.WriteLine($"Query sent: {query}");
	}

	private static async Task ReceiveResponses(ClientWebSocket ws)
	{
		var buffer = new byte[1024 * 64]; // 64KB buffer
		var messageBuilder = new StringBuilder();

		while (ws.State == WebSocketState.Open)
		{
			var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

			if (result.MessageType == WebSocketMessageType.Close)
			{
				await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
				break;
			}

			var messageChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
			messageBuilder.Append(messageChunk);

			if (result.EndOfMessage)
			{
				var fullMessage = messageBuilder.ToString();
				messageBuilder.Clear();

				ProcessResponse(fullMessage);
			}
		}
	}

	private static void ProcessResponse(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// This is the message with your actual function call data
			if (root.TryGetProperty("toolCall", out var toolCall))
			{
				if (toolCall.TryGetProperty("functionCalls", out var functionCalls))
				{
					foreach (var functionCall in functionCalls.EnumerateArray())
					{
						if (functionCall.TryGetProperty("args", out var args))
						{
							string answer = "";
							string code = "";

							if (args.TryGetProperty("answer", out var answerProp))
							{
								answer = answerProp.GetString() ?? "";
							}

							if (args.TryGetProperty("console_code", out var codeProp))
							{
								code = CleanCodeBlock(codeProp.GetString() ?? "");
							}

							// Now you have clean answer and code
							Console.WriteLine("\n" + new string('=', 80));
							Console.WriteLine("EXPLANATION:");
							Console.WriteLine(new string('=', 80));
							Console.WriteLine(answer);

							Console.WriteLine("\n" + new string('=', 80));
							Console.WriteLine("CODE:");
							Console.WriteLine(new string('=', 80));
							Console.WriteLine(code);
							Console.WriteLine(new string('=', 80) + "\n");
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing response: {ex.Message}");
		}
	}

	private static string CleanCodeBlock(string code)
	{
		code = code.Trim();

		// Remove triple-quote wrappers
		if (code.StartsWith("\"\"\"") && code.EndsWith("\"\"\""))
		{
			code = code.Substring(3, code.Length - 6);
		}

		// Remove language identifier
		if (code.StartsWith("csharp\n") || code.StartsWith("csharp\r\n"))
		{
			code = code.Substring(code.IndexOf('\n') + 1);
		}

		return code.Trim();
	}

	private static void ProcessResponse_3(string json)
	{
		Console.WriteLine("\n" + new string('=', 80));
		Console.WriteLine("RAW JSON RECEIVED:");
		Console.WriteLine(new string('=', 80));

		// Pretty print the JSON
		try
		{
			using var doc = JsonDocument.Parse(json);
			var prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			Console.WriteLine(prettyJson);
		}
		catch
		{
			Console.WriteLine(json);
		}

		Console.WriteLine(new string('=', 80) + "\n");
	}

	private static void ProcessResponse_2(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// Check for server content
			if (root.TryGetProperty("serverContent", out var serverContent))
			{
				if (serverContent.TryGetProperty("modelTurn", out var modelTurn))
				{
					if (modelTurn.TryGetProperty("parts", out var parts))
					{
						foreach (var part in parts.EnumerateArray())
						{
							// Check for function call
							if (part.TryGetProperty("functionCall", out var functionCall))
							{
								var functionName = functionCall.GetProperty("name").GetString();
								Console.WriteLine($"\n=== Function Called: {functionName} ===\n");

								if (functionCall.TryGetProperty("args", out var args))
								{
									if (args.TryGetProperty("answer", out var answer))
									{
										Console.WriteLine("EXPLANATION:");
										Console.WriteLine(answer.GetString());
										Console.WriteLine();
									}

									if (args.TryGetProperty("console_code", out var code))
									{
										Console.WriteLine("CODE:");
										Console.WriteLine(code.GetString());
										Console.WriteLine();
									}
								}
							}

							// Check for text response (if any)
							if (part.TryGetProperty("text", out var text))
							{
								Console.WriteLine("Text Response:");
								Console.WriteLine(text.GetString());
							}
						}
					}
				}

				// Check if turn is complete
				if (serverContent.TryGetProperty("turnComplete", out var turnComplete) &&
					turnComplete.GetBoolean())
				{
					Console.WriteLine("\n=== Turn Complete ===");
				}
			}

			// Check for tool call cancellation or other events
			if (root.TryGetProperty("toolCallCancellation", out var cancellation))
			{
				Console.WriteLine("Tool call was cancelled");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing response: {ex.Message}");
			Console.WriteLine($"Raw JSON: {json}");
		}
	}
}