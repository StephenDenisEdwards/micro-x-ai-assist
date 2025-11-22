using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GeminiLiveConsole.Models;

namespace GeminiLiveConsole;

public sealed class GeminiLiveClient : IAsyncDisposable
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _systemPrompt;
    private ClientWebSocket _ws = new();
    private CancellationTokenSource? _cts;
    private Task? _recvTask;

    public event Action? OnOpen;
    public event Action? OnClose;
    public event Action<Exception>? OnError;
    public event Action<GeminiMessage>? OnMessage;
    public bool IsConnected { get; private set; }
	//You are a dedicated Conversation Monitor and Assistant. Detect QUESTION or IMPERATIVE and call report_intent. Ignore casual speech.
	//public GeminiLiveClient(string apiKey, string model = "gemini-2.0-flash-exp", string systemPrompt = "You are a helpful assistant. Listen to the user speaking and reply in text.")
	public GeminiLiveClient(string apiKey, string model = "gemini-2.0-flash-exp", string systemPrompt = "You are a dedicated Conversation Monitor and Assistant. Detect QUESTION or IMPERATIVE and call report_intent. Ignore casual speech.")
    {
		_apiKey = apiKey;
        _model = model;
        _systemPrompt = systemPrompt;
    }

    // --- Public connect following Program.cs pattern ---
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var wsUrl = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={Uri.EscapeDataString(_apiKey)}";
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

	// --- Setup frame identical shape to Program.cs ---
	private async Task SendSetupFrameAsync(CancellationToken ct)
	{
		var setupMessage = new
		{
			setup = new
			{
				model = $"models/{_model}",
				generationConfig = new
				{
					//responseModalities = new[] { "AUDIO" },
					responseModalities = new[] { "TEXT" },
				},
				inputAudioTranscription = new { },
				//systemInstruction = _systemPrompt,
				systemInstruction = new
				{
					role = "system",
					parts = new[]
					{
						new { text = _systemPrompt }
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
								name = "report_intent",
								description = "Report detected question or imperative command.",
								parameters = new
								{
									type = "object",
									properties = new
									{
										text = new { type = "string" },
										type = new
										{
											type = "string",
											// 👇 THIS is the key change
											// 'enum', NOT 'enumValues'
											// use @enum because 'enum' is a C# keyword
											@enum = new[] { "QUESTION", "IMPERATIVE" }
										},
										answer = new { type = "string" }
									},
									required = new[] { "text", "type", "answer" }
								}
							}
						}
					}}
			}
		};
		await SendJsonAsync(setupMessage, ct);
	}


    private async Task SendSetupFrameAsync_2(CancellationToken ct)
    {
        var setupMessage = new
        {
            setup = new
            {
                model = $"models/{_model}",
                generationConfig = new
                {
                    responseModalities = new[] { "TEXT" }
                },
                inputAudioTranscription = new { },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = _systemPrompt }
                    }
                }
            }
        };
        await SendJsonAsync(setupMessage, ct);
    }

    // --- Stream PCM16 mono 16kHz chunks ---
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

	// --- Signal end of audio stream ---
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
