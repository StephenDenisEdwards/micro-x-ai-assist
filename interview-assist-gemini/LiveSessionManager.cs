using GeminiLiveConsole.Models;

namespace GeminiLiveConsole;

public sealed class LiveSessionManager
{
    private readonly GeminiLiveClient _client;
    private readonly AudioCaptureService _audio;

    public event Action<string>? OnTranscript; // aggregated
    public event Action<string>? OnInputTranscriptionUpdate; // incremental microphone transcription
    public event Action<string>? OnAssistantResponsePart; // streamed assistant output
    public event Action<DetectedIntent>? OnIntent; // currently unused
    public event Action<double>? OnVolume;
    public event Action<Exception>? OnError;
    public event Action? OnDisconnect;

    public LiveSessionManager(string apiKey, string model)
    {
        _client = new GeminiLiveClient(apiKey, model);
        _audio = new AudioCaptureService(16000);

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
            // chunk length is bytesRecorded
            await _client.SendAudioChunkAsync(chunk, chunk.Length);
        };
    }

    public Task ConnectAsync(CancellationToken ct = default) => _client.ConnectAsync(ct);
    public Task DisconnectAsync() => _client.DisconnectAsync();

    // Allow manual control of audio capture
    public void StartAudio() => _audio.Start();
    public void StopAudio() => _audio.Stop();

    // Forward end-of-stream signal to underlying client
    public Task SendAudioStreamEndAsync(CancellationToken ct = default) => _client.SendAudioStreamEndAsync(ct);

    private void HandleMessage(GeminiMessage msg)
    {
        var transcript = msg.ServerContent?.InputTranscription?.Text;
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            OnTranscript?.Invoke(transcript);
            OnInputTranscriptionUpdate?.Invoke(transcript);
        }

        // Streamed model turn text parts (assistant responses)
        var parts = msg.ServerContent?.ModelTurn?.Parts;
        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p.Text))
                {
                    // For now treat assistant textual output as transcript as well
                    OnTranscript?.Invoke(p.Text);
                    OnAssistantResponsePart?.Invoke(p.Text);
                }
            }
        }
        // Intent/tool handling removed; protocol no longer provides function calls in this simplified schema.
    }

    private void HandleMessage_2(GeminiMessage msg)
    {
	    var transcript = msg.ServerContent?.InputTranscription?.Text;
	    if (!string.IsNullOrWhiteSpace(transcript))
	    {
		    OnTranscript?.Invoke(transcript);
		    OnInputTranscriptionUpdate?.Invoke(transcript);
	    }

		if (msg.ToolCall?.FunctionCalls != null)
	    {
		    foreach (var fc in msg.ToolCall.FunctionCalls)
		    {
			    if (fc.Name == "report_intent" && fc.Args != null)
			    {
				    var intent = new DetectedIntent
				    {
					    Text = fc.Args.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "",
					    Type = fc.Args.TryGetValue("type", out var tp) && tp?.ToString() == "QUESTION"
						    ? IntentType.QUESTION : IntentType.IMPERATIVE,
					    Answer = fc.Args.TryGetValue("answer", out var ans) ? ans?.ToString() ?? "" : ""
				    };
				    OnIntent?.Invoke(intent);
				    _ = _client.SendToolResponseAsync(fc); // ack
			    }
		    }
	    }
    }

	private static double ComputeRms(byte[] pcm16)
    {
        int samples = pcm16.Length / 2;
        if (samples == 0) return 0;
        double sumSq = 0;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8));
            double norm = s / 32768.0;
            sumSq += norm * norm;
        }
        return Math.Sqrt(sumSq / samples);
    }
}
