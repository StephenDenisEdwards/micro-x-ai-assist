using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

namespace GeminiLiveConsole;

public sealed class GeminiLiveClient : IAsyncDisposable
{
    private readonly GeminiLiveConfig _config;
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    public event Action? OnOpen;
    public event Action<string>? OnTranscript;
    public event Action<ReportIntentPayload>? OnIntent;
    public event Action<Exception>? OnError;
    public event Action? OnClose;

    public GeminiLiveClient(GeminiLiveConfig config) => _config = config;

    public async Task ConnectAsync(string systemInstruction, CancellationToken ct = default)
    {
        var uri = new Uri(_config.Endpoint + $"?model={_config.Model}&key={_config.ApiKey}");
        await _ws.ConnectAsync(uri, ct);
        OnOpen?.Invoke();
        // Send initial config message (structure may evolve; placeholder)
        var init = new
        {
            type = "setup",
            systemInstruction,
            inputAudioTranscription = new { enabled = true }
        };
        await SendJsonAsync(init, ct);
        _ = Task.Run(() => ReceiveLoop(_cts.Token));
    }

    public async Task SendAudioChunkAsync(byte[] pcm16Chunk, CancellationToken ct = default)
    {
        var base64 = Convert.ToBase64String(pcm16Chunk);
        var msg = new { type = "input_audio", audio = base64 }; // Placeholder schema
        await SendJsonAsync(msg, ct);
    }

    private async Task SendJsonAsync(object obj, CancellationToken ct)
    {
        var json = JsonConvert.SerializeObject(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                        OnClose?.Invoke();
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var json = sb.ToString();
                IncomingMessage? message = null;
                try { message = JsonConvert.DeserializeObject<IncomingMessage>(json); }
                catch (Exception ex) { OnError?.Invoke(ex); }
                if (message == null) continue;

                if (message.Transcript is not null)
                    OnTranscript?.Invoke(message.Transcript);
                if (message.FunctionCall?.Name == "report_intent" && message.FunctionCall.Arguments is not null)
                    OnIntent?.Invoke(message.FunctionCall.Arguments);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None);
        _ws.Dispose();
        _cts.Dispose();
    }
}
