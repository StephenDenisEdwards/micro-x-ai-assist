//using System.ComponentModel.DataAnnotations;
//using System.Net.WebSockets;
//using System.Text;
//using System.Text.Json;
//using GeminiLiveConsole.Models;

//namespace GeminiLiveConsole;

//public sealed class GeminiLiveClient : IAsyncDisposable
//{
//    private readonly string _apiKey;
//    private readonly string _model;
//    //private string _systemPrompt;
//    private ClientWebSocket _ws = new();
//    private CancellationTokenSource? _cts;
//    private Task? _recvTask;

//    public event Action? OnOpen;
//    public event Action? OnClose;
//    public event Action<Exception>? OnError;
//    public event Action<GeminiMessage>? OnMessage;
//    public bool IsConnected { get; private set; }
//	//You are a dedicated Conversation Monitor and Assistant. Detect QUESTION or IMPERATIVE and call report_intent. Ignore casual speech.
//	//public GeminiLiveClient(string apiKey, string model = "gemini-2.0-flash-exp", string systemPrompt = "You are a helpful assistant. Listen to the user speaking and reply in text.")
//	public GeminiLiveClient(string apiKey, string model = "gemini-2.0-flash-exp", string systemPrompt = "You are a dedicated Conversation Monitor and Assistant. Detect QUESTION or IMPERATIVE and call report_intent. Ignore casual speech.")
//    {
//		_apiKey = apiKey;
//        _model = model;
//        //_systemPrompt = systemPrompt;
//    }

//    // --- Public connect following Program.cs pattern ---
//    public async Task ConnectAsync(CancellationToken ct = default)
//    {
//        if (IsConnected) return;
//        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
//        try
//        {
//            var wsUrl = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={Uri.EscapeDataString(_apiKey)}";
//            // Output API URI on startup
//            Console.ForegroundColor = ConsoleColor.DarkGray;
//            Console.WriteLine($"[GeminiLiveClient] API URI: {wsUrl}");
//            Console.ResetColor();

//            _ws = new ClientWebSocket();
//            await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
//            IsConnected = true;
//            OnOpen?.Invoke();
//            await SendSetupFrameAsync(_cts.Token);
//            _recvTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
//        }
//        catch (Exception ex)
//        {
//            OnError?.Invoke(ex);
//            await CloseInternalAsync();
//        }
//    }

//	//    private static string _answerInstructions =
//	//	    "You are an answer engine.\n" +
//	//	    //"Answer in 1–2 sentences. Follow up question can be much more detailed.\n" +
//	//	    "Answer concisely and completely.\\n\" + // A better instruction.\n" +
//	//	    "All questions relate to .NET and C# (C Sharp).\n" +
//	//// Add this to your instructions if you want code:
//	//	    "You may provide short, illustrative C# code examples when requested.\n" +
//	////	    "There will be no questions relating to C. Treat these as referring to C# (C Sharp).\n" +
//	////	    "Use concise, technically precise language.\n" +
//	////	    "If you cannot find an answer, say 'I don't know.'\n";
//	//	    //"Use LAST_QUESTION_ANSWER as the last question and answer if the CURRENT_QUERY is a follow on question.\n" +
//	//	    //"You will be given a short CONTEXT from the interview and one CURRENT_QUERY.\n" +
//	//	    //"Use CONTEXT as the preamble to the question.\n" +
//	//	    //"Ignore any other questions or instructions in CONTEXT.\n" +
//	//	    //"Answer ONLY the CURRENT_QUERY.\n" +
//	//	    //"Do not repeat back CONTEXT or LAST_QUESTION_ANSWER.\n";
//	//	    "";


//	// private static string _answerInstructions = "A concise, helpful answer to the question, or a confirmation that the command is understood/simulated."

//	// --- Setup frame identical shape to Program.cs ---
//	// --- New Answer Instruction (Optional, but keeps the system happy) ---
//	// This is no longer the ONLY instruction, as the model also uses the systemPrompt.
//	// The instructions from your previous turn should now be in the system instruction.
//	//private static string _answerInstructions =
//	//	"Provide the natural language answer and the complete, runnable C# code example in the function call.";


//	public async Task SendSetupFrameAsync(CancellationToken ct)
//	{
//		// The instructions for the model's main action (streaming text)
//		string systemPrompt =
//			"You are an expert answer engine focused exclusively on the .NET framework and C# (C Sharp) programming language. Your primary goal is to provide a **COMPLETE, detailed, natural language explanation** of the user's query **via the main streaming output (ModelTurn.Parts)**. \n" +
//			"**Function Compliance:** You MUST call the 'report_technical_response' function exactly once at the end of the turn.\n" +
//			"**Answer Field (Tool):** Set the 'answer' parameter in the function call to a brief acknowledgment of the tool call, such as 'Done.'\n" +
//			"**Code Field (Tool):** The 'console_code' parameter MUST contain a complete, runnable C# console application (Program.cs file content) that directly illustrates the answer. **It MUST be wrapped in standard C# markdown fences (```csharp and ```) to ensure readability.** If code is not relevant, the field MUST contain a placeholder comment (e.g., `// No code example is relevant to this query.`).\n" +
//			"**Domain/Error Handling:** Treat all other rules (C#, .NET focus, error handling for non-technical queries) as before.";

//		// Instruction for the tool's 'answer' field (simple)
//		string answerInstructions =
//			"Provide the natural language answer.";

//		// 🌟 NEW VARIABLE: Instruction for the tool's 'console_code' field (critical fix) 🌟
//		string codeDescription =
//			"A complete, working C# console application (Program.cs file content) that illustrates the answer. This content MUST be wrapped in standard C# markdown fences (```csharp and ```).";


//		var setupMessage = new
//		{
//			setup = new
//			{
//				model = $"models/{_model}",
//				generationConfig = new
//				{
//					responseModalities = new[] { "TEXT" },
//				},
//				inputAudioTranscription = new { },
//				systemInstruction = new
//				{
//					role = "system",
//					parts = new[]
//					{
//					new { text = systemPrompt }
//				}
//				},
//				tools = new[]
//				{
//				new
//				{
//					function_declarations = new[]
//					{
//						new
//						{
//							name = "report_technical_response",
//							description =
//								"Report the answer, classify the intent, and provide a complete, working C# console application code example.",
//							parameters = new
//							{
//								type = "object",
//								properties = new
//								{
//									text = new
//									{
//										type = "string",
//										description = "The verbatim text of the question or command detected."
//									},
//									type = new
//									{
//										type = "string",
//										@enum = new[] { "QUESTION", "IMPERATIVE" },
//										description = "The classification of the detected speech."
//									},
//									answer = new
//									{
//										type = "string",
//										description = answerInstructions
//									},
//                                    // 🌟 USING THE NEW HARMONIZED DESCRIPTION 🌟
//                                    console_code = new
//									{
//										type = "string",
//										description = codeDescription
//									}
//								},
//								required = new[] { "text", "type", "answer", "console_code" }
//							}
//						}
//					}
//				}
//			}
//			}
//		};

//		// --- Debug/Serialization Logic (Example) ---
//		try
//		{
//			var pretty = JsonSerializer.Serialize(setupMessage, new JsonSerializerOptions { WriteIndented = true });
//			Console.ForegroundColor = ConsoleColor.DarkGray;
//			Console.WriteLine("[GeminiLiveClient] Setup message:");
//			Console.ResetColor();
//			Console.WriteLine(pretty);
//		}
//		catch
//		{
//			Console.WriteLine("Failed to serialize setup message.");
//		}

//		// --- Placeholder for your actual send logic ---
//		await SendJsonAsync(setupMessage, ct);
//	}

//	//public async Task SendSetupFrameAsync(CancellationToken ct)
//	//{
//	//	// TEMPORARY TEST: Reduce _systemPrompt to a single, simple line
//	//	//string systemPrompt =
//	//	//	"You are a C# expert. Call the 'report_technical_response' function with the answer and code.";
//	//	// Instructions passed to the LLM via the System Instruction field
//	//	//		string systemPrompt =
//	//	//			"You are an expert answer engine focused exclusively on the .NET framework and C# (C Sharp) programming language.\n" +
//	//	//			"You MUST call the 'report_technical_response' function for every response.\n" +
//	//	//			"**Domain Focus:** All technical questions relate to .NET and C#. Treat any reference to 'C' as referring to 'C# (C Sharp).'\n" +
//	//	//			"**Response Style:** Provide concise, technically precise, and complete answers.\n" +
//	//	////			"**Answer Field:** The 'answer' parameter in the function call must contain the full natural language explanation. Do not adhere to a 1–2 sentence limit; be as long as necessary.\n" +
//	//	//			"**Answer Field:** The 'answer' parameter in the function call MUST contain the complete, detailed, natural language explanation of the query's answer. **This explanation must be self-contained and stand alone without referring to the code example.** Use markdown formatting (headings, lists, bolding) to structure the explanation." +


//	//	//			"**Code Field:** The 'console_code' parameter MUST contain a complete, runnable C# console application (the contents of a Program.cs file) that illustrates the answer. This code should be ready to save and run without markdown fences (e.g., no ```csharp).\n" +
//	//	//			"**Error Handling:** If you cannot find a definitive answer or if the request is non-technical, set the 'answer' field to 'I cannot answer that question based on the C# and .NET domain focus.' and set the 'console_code' field to an empty string (\" \").\n";

//	//	//string systemPrompt =
//	//	//	"You are an expert answer engine focused exclusively on the .NET framework and C# (C Sharp) programming language. Your primary goal is to provide **COMPLETE, high-quality, and highly detailed natural language explanations**. You must structure your output using a required function call.\n" +
//	//	//	"**PRIORITY 1: THE EXPLANATION (Answer Field)**\n" +
//	//	//	"The 'answer' parameter in the function call MUST contain the complete, detailed, natural language explanation of the query's answer. This explanation must be self-contained, using clear headings, bolding, and bullet points (Markdown format). It must NOT refer to the code example (e.g., do not say 'See the code below'). This is the main output.\n" +
//	//	//	"**PRIORITY 2: THE CODE EXAMPLE (console_code Field)**\n" +
//	//	//	"The 'console_code' parameter MUST contain a complete, runnable C# console application (Program.cs file content) that directly illustrates the answer. Provide the full code without markdown fences (e.g., no ```csharp).\n" +
//	//	//	"**Domain Constraints:** All technical questions relate to .NET and C#.\n" +
//	//	//	"**Function Compliance:** You MUST call the 'report_technical_response' function for every response. If code is not relevant, the 'console_code' field MUST contain a placeholder comment (e.g., `// No code example is relevant to this query.`) to ensure the field is not empty.\n" +
//	//	//	"**Error Handling:** If the request is non-technical, set the 'answer' field to 'I cannot answer that question based on the C# and .NET domain focus.' and use the code placeholder comment.";

//	//string systemPrompt =
//	//	"You are an expert answer engine focused exclusively on the .NET framework and C# (C Sharp) programming language. Your primary goal is to provide a **COMPLETE, detailed, natural language explanation** of the user's query **via the main streaming output (ModelTurn.Parts)**. \n" +
//	//	"**Function Compliance:** You MUST call the 'report_technical_response' function exactly once at the end of the turn.\n" +
//	//	"**Answer Field (Tool):** Set the 'answer' parameter in the function call to a brief acknowledgment of the tool call, such as 'Done.'\n" +
//	//	"**Code Field (Tool):** The 'console_code' parameter MUST contain a complete, runnable C# console application (Program.cs file content) that directly illustrates the answer. Provide the full code without markdown fences (e.g., no ```csharp). If code is not relevant, the field MUST contain a placeholder comment (e.g., `// No code example is relevant to this query.`).\n" +
//	//	"**Domain/Error Handling:** Treat all other rules (C#, .NET focus, error handling for non-technical queries) as before.";
//	//	// Instructions passed to the LLM as the function parameter description (optional, but kept for completeness)
//	//	//string answerInstructions =
//	//	//	"Provide the natural language answer and the complete, runnable C# code example in the function call.";
//	//	string answerInstructions =
//	//		"Provide the natural language answer.";

//	//	var setupMessage = new
//	//	{
//	//		setup = new
//	//		{
//	//			model = $"models/{_model}",
//	//			generationConfig = new
//	//			{
//	//				responseModalities = new[] { "TEXT" },
//	//			},
//	//			inputAudioTranscription = new { },
//	//			systemInstruction = new
//	//			{
//	//				role = "system",
//	//				parts = new[]
//	//				{
//	//					new { text = systemPrompt }
//	//				}
//	//			},
//	//			tools = new[]
//	//			{
//	//				new
//	//				{
//	//					function_declarations = new[]
//	//					{
//	//						new
//	//						{
//	//							name = "report_technical_response",
//	//							description =
//	//								"Report the answer, classify the intent, and provide a complete, working C# console application code example.",
//	//							parameters = new
//	//							{
//	//								type = "object",
//	//								properties = new
//	//								{
//	//									text = new
//	//									{
//	//										type = "string",
//	//										description = "The verbatim text of the question or command detected."
//	//									},
//	//									type = new
//	//									{
//	//										type = "string",
//	//										@enum = new[] { "QUESTION", "IMPERATIVE" },
//	//										description = "The classification of the detected speech."
//	//									},
//	//									answer = new
//	//									{
//	//										type = "string",
//	//										description = answerInstructions // Uses the simple instruction string
//	//									},
//	//									console_code = new
//	//									{
//	//										type = "string",
//	//										description =
//	//											"A complete, working C# console application (Program.cs file content) that illustrates the answer. This must be the raw, runnable C# code without markdown fences."
//	//									}
//	//								},
//	//								required = new[] { "text", "type", "answer", "console_code" }
//	//							}
//	//						}
//	//					}
//	//				}
//	//			}
//	//		}
//	//	};

//	//	// --- Debug/Serialization Logic (Example) ---
//	//	try
//	//	{
//	//		var pretty = JsonSerializer.Serialize(setupMessage, new JsonSerializerOptions { WriteIndented = true });
//	//		Console.ForegroundColor = ConsoleColor.DarkGray;
//	//		Console.WriteLine("[GeminiLiveClient] Setup message:");
//	//		Console.ResetColor();
//	//		Console.WriteLine(pretty);
//	//	}
//	//	catch
//	//	{
//	//		Console.WriteLine("Failed to serialize setup message.");
//	//	}

//	//	// --- Placeholder for your actual send logic ---
//	//	await SendJsonAsync(setupMessage, ct); 
//	//}

//	private async Task SendSetupFrameAsync_WORKS_NICELY(CancellationToken ct)
//	{
//		string _answerInstructions = "The instructions are handled by the system prompt.";
//		string _systemPrompt =
//			"You are an expert answer engine focused exclusively on the .NET framework and C# (C Sharp) programming language.\n" +
//			"**Domain Focus:** All technical questions relate to .NET and C#. Treat any reference to 'C' as referring to 'C# (C Sharp).'\n" +
//			"**Response Style:** Provide concise, technically precise, and complete answers.\n" +
//			"**Length:** Do not adhere to a 1–2 sentence limit. Answers should be as long as necessary to fully explain the topic, including multi-paragraph explanations, bullet points, and code.\n" +
//			"**Code Examples:** You are authorized and encouraged to provide clear, illustrative C# code examples when they are requested or would significantly clarify the answer.\n" +
//			"**Error Handling:** If you cannot find a definitive answer within the domain, or if a request is non-technical, simply say: 'I cannot answer that question based on the C# and .NET domain focus.'\n";

//		var setupMessage = new
//		{
//			setup = new
//			{
//				model = $"models/{_model}",
//				generationConfig = new
//				{
//					//responseModalities = new[] { "AUDIO" },
//					responseModalities = new[] { "TEXT" },
//				},
//				inputAudioTranscription = new { },
//				//systemInstruction = _systemPrompt,
//				systemInstruction = new
//				{
//					role = "system",
//					parts = new[]
//					{
//						new { text = _systemPrompt }
//					}
//				},
//				tools = new[]
//				{
//					new
//					{
//						function_declarations = new[]
//						{
//							new
//							{
//								name = "report_intent",
//								description = "Report a detected question or imperative command and provide an answer or acknowledgment.",
//								parameters = new
//								{
//									type = "object",
//									properties = new
//									{
//										text = new
//										{
//											type = "string",
//											description = "The verbatim text of the question or command detected."
//										},
//										type = new
//										{
//											type = "string",
//											// 👇 THIS is the key change
//											// 'enum', NOT 'enumValues'
//											// use @enum because 'enum' is a C# keyword
//											@enum = new[] { "QUESTION", "IMPERATIVE" },
//											description = "The classification of the detected speech."
//										},
//										answer = new
//										{
//											type = "string", 
//                                            description = _answerInstructions
//										}
//									},
//									required = new[] { "text", "type", "answer" }
//								}
//							}
//						}
//					}}
//			}
//		};

//		// Display the setup message for debugging
//		try
//		{
//			var pretty = JsonSerializer.Serialize(setupMessage, new JsonSerializerOptions { WriteIndented = true });
//			Console.ForegroundColor = ConsoleColor.DarkGray;
//			Console.WriteLine("[GeminiLiveClient] Setup message:");
//			Console.ResetColor();
//			Console.WriteLine(pretty);
//		}
//		catch
//		{
//			// Fallback if serialization fails
//			Console.WriteLine(setupMessage?.ToString());
//		}

//		await SendJsonAsync(setupMessage, ct);
//    }


//    ////private async Task SendSetupFrameAsync_2(CancellationToken ct)
//    ////{
//    ////    var setupMessage = new
//    ////    {
//    ////        setup = new
//    ////        {
//    ////            model = $"models/{_model}",
//    ////            generationConfig = new
//    ////            {
//    ////                responseModalities = new[] { "TEXT" }
//    ////            },
//    ////            inputAudioTranscription = new { },
//    ////            systemInstruction = new
//    ////            {
//    ////                parts = new[]
//    ////                {
//    ////                    new { text = _systemPrompt }
//    ////                }
//    ////            }
//    ////        }
//    ////    };
//    ////    await SendJsonAsync(setupMessage, ct);
//    ////}

//    // --- Stream PCM16 mono 16kHz chunks ---
//    public async Task SendAudioChunkAsync(byte[] pcm16Buffer, int bytesRecorded, CancellationToken ct = default)
//    {
//        if (!IsConnected || _ws.State != WebSocketState.Open) return;
//        var base64 = Convert.ToBase64String(pcm16Buffer, 0, bytesRecorded);
//        var audioFrame = new
//        {
//            realtimeInput = new
//            {
//                audio = new
//                {
//                    mimeType = "audio/pcm;rate=16000",
//                    data = base64
//                }
//            }
//        };
//        await SendJsonAsync(audioFrame, ct);
//    }
//	public async Task SendToolResponseAsync(ToolFunctionCall call, CancellationToken ct = default)
//	{
//		var payload = new
//		{
//			toolResponse = new
//			{
//				functionResponses = new[]
//				{
//					new { id = call.Id, name = call.Name, response = new { result = "logged" } }
//				}
//			}
//		};
//		await SendJsonAsync(payload, ct);
//	}

//	// --- Signal end of audio stream ---
//	public async Task SendAudioStreamEndAsync(CancellationToken ct = default)
//    {
//        if (!IsConnected) return;
//        var endMessage = new
//        {
//            realtimeInput = new
//            {
//                audioStreamEnd = true
//            }
//        };
//        await SendJsonAsync(endMessage, ct);
//    }

//    private static readonly JsonSerializerOptions JsonOpts = new()
//    {
//        PropertyNameCaseInsensitive = true,
//        ReadCommentHandling = JsonCommentHandling.Skip,
//        AllowTrailingCommas = true
//    };

//    private async Task SendJsonAsync(object payload, CancellationToken ct)
//    {
//        var json = JsonSerializer.Serialize(payload);
//        var bytes = Encoding.UTF8.GetBytes(json);
//        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
//    }

//    private async Task ReceiveLoopAsync(CancellationToken token)
//    {
//        var buffer = new byte[16 * 1024];
//        try
//        {
//            while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
//            {
//                using var ms = new MemoryStream();
//                WebSocketReceiveResult? result;
//                do
//                {
//                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
//                    if (result.MessageType == WebSocketMessageType.Close)
//                    {
//	                    Console.ForegroundColor = ConsoleColor.Cyan;
//						Console.WriteLine(result.CloseStatusDescription);
//						Console.ResetColor();

//						await CloseInternalAsync();
//                        return;
//                    }
//                    ms.Write(buffer, 0, result.Count);
//                } while (!result.EndOfMessage);

//                var data = ms.ToArray();
//                var json = Encoding.UTF8.GetString(data);
//                try
//                {
//                    var msg = JsonSerializer.Deserialize<GeminiMessage>(json, JsonOpts);
//                    if (msg != null)
//                        OnMessage?.Invoke(msg);
//                }
//                catch (JsonException jex)
//                {
//                    OnError?.Invoke(jex);
//                }
//            }
//        }
//        catch (OperationCanceledException)
//        {
//            // normal
//        }
//        catch (Exception ex)
//        {
//            OnError?.Invoke(ex);
//            await CloseInternalAsync();
//        }
//    }

//    public async Task DisconnectAsync()
//    {
//        await CloseInternalAsync();
//    }

//    private async Task CloseInternalAsync()
//    {
//        if (_ws.State == WebSocketState.Open)
//        {
//            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None); } catch { }
//        }
//        IsConnected = false;
//        _cts?.Cancel();
//        OnClose?.Invoke();
//    }

//    public async ValueTask DisposeAsync()
//    {
//        await CloseInternalAsync();
//        _ws.Dispose();
//        _cts?.Dispose();
//    }
//}
