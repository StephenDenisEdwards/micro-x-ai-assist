using GeminiLiveConsole.Models;
using System.Linq;
using System.Collections.Generic;

namespace GeminiLiveConsole;

public sealed class LiveSessionManager
{
    private readonly GeminiLiveClient _client;
    private readonly AudioCaptureService _audio;

    public event Action<string>? OnTranscript; // aggregated
    public event Action<string>? OnInputTranscriptionUpdate; // incremental microphone transcription
    public event Action<string>? OnAssistantResponsePart; // streamed assistant output (delta)
    public event Action<DetectedIntent>? OnIntent; // per-call updates (optional)
    public event Action<DetectedIntent>? OnIntentFinal; // finalized per turn
    public event Action<double>? OnVolume;
    public event Action<Exception>? OnError;
    public event Action? OnDisconnect;
    public event Action? OnAssistantTurnComplete; // raised when TurnComplete=true

    // Track assistant streaming state to dedupe cumulative parts
    private string _assistantLastFullText = string.Empty;

    // Track pending intent (take the longest text within the turn)
    private string _pendingIntentText = string.Empty;
    private IntentType _pendingIntentType = IntentType.QUESTION;

    // Capture full answer provided inline via tool call (may be more complete than streamed parts)
    private string _pendingToolAnswer = string.Empty;

    public LiveSessionManager(string apiKey, string model, AudioInputSource audioSource= AudioInputSource.Microphone)
    {
        _client = new GeminiLiveClient(apiKey, model);
        _audio = new AudioCaptureService(16000, audioSource);

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

    // Switch between microphone and system (loopback) audio
    public void UseMicrophone() => _audio.SetSource(AudioInputSource.Microphone);
    public void UseSystemAudio() => _audio.SetSource(AudioInputSource.Loopback);
    public AudioInputSource CurrentAudioSource => _audio.Source;

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
                if (!string.IsNullOrEmpty(p.Text))
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

        // Streamed assistant parts may be cumulative and may include whitespace-only parts.
        var parts = msg.ServerContent?.ModelTurn?.Parts;
        if (parts != null)
        {
            // Preserve whitespace-only parts to avoid losing spaces between tokens.
            var fullText = string.Concat(parts.Select(p => p.Text ?? string.Empty));
            if (fullText.Length > 0)
            {
                // Compute longest common prefix between previous and current text
                int lcp = 0;
                int max = Math.Min(_assistantLastFullText.Length, fullText.Length);
                while (lcp < max && _assistantLastFullText[lcp] == fullText[lcp]) lcp++;

                if (fullText.Length > _assistantLastFullText.Length && lcp == _assistantLastFullText.Length)
                {
                    // Simple append case
                    var delta = fullText.Substring(_assistantLastFullText.Length);
                    _assistantLastFullText = fullText;
                    if (delta.Length > 0)
                    {
                        OnAssistantResponsePart?.Invoke(delta);
                        OnTranscript?.Invoke(delta);
                    }
                }
                else if (fullText.Length >= _assistantLastFullText.Length && lcp < fullText.Length)
                {
                    // Re-edit within already printed text; emit only the non-overlapping tail
                    var delta = fullText.Substring(lcp);
                    _assistantLastFullText = fullText;
                    if (delta.Length > 0)
                    {
                        OnAssistantResponsePart?.Invoke(delta);
                        OnTranscript?.Invoke(delta);
                    }
                }
                else if (fullText.Length < _assistantLastFullText.Length)
                {
                    // Model rewrote and got shorter; update state but do not attempt to backspace the console
                    _assistantLastFullText = fullText;
                }
            }
        }

        if (msg.ToolCall?.FunctionCalls != null)
        {
            foreach (var fc in msg.ToolCall.FunctionCalls)
            {
                if (fc.Name == "report_intent" && fc.Args != null)
                {
                    var rawType = fc.Args.TryGetValue("type", out var tp) ? tp?.ToString() ?? "" : "";
                    var textVal = fc.Args.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
                    var answerVal = fc.Args.TryGetValue("answer", out var ans) ? ans?.ToString() ?? "" : "";

                    var type = rawType is "QUESTION" or "QIESTIOM" ? IntentType.QUESTION : IntentType.IMPERATIVE;

                    // Track the longest text seen for the turn (likely the full question/imperative)
                    if (!string.IsNullOrWhiteSpace(textVal) && textVal.Length >= _pendingIntentText.Length)
                    {
                        _pendingIntentText = textVal;
                        _pendingIntentType = type;
                    }

                    // Capture the full answer (we will reconcile at turn end)
                    if (!string.IsNullOrWhiteSpace(answerVal))
                    {
                        // Prefer the longest answer seen
                        if (answerVal.Length > _pendingToolAnswer.Length)
                            _pendingToolAnswer = answerVal;
                    }

                    // Optional interim updates
                    OnIntent?.Invoke(new DetectedIntent { Text = textVal, Type = type });

                    // Ack so model can proceed
                    _ = _client.SendToolResponseAsync(fc);
                }
            }
        }

        // When the model signals end of turn, reconcile streaming vs tool answer, then reset state
        if (msg.ServerContent?.TurnComplete == true)
        {
            OnAssistantTurnComplete?.Invoke();

            if (!string.IsNullOrWhiteSpace(_pendingIntentText))
            {
                OnIntentFinal?.Invoke(new DetectedIntent
                {
                    Text = _pendingIntentText,
                    Type = _pendingIntentType
                });
            }

            // Emit missing suffix of tool answer if any
            if (!string.IsNullOrWhiteSpace(_pendingToolAnswer))
            {
                if (string.IsNullOrWhiteSpace(_assistantLastFullText))
                {
                    // Nothing streamed; emit full answer
                    OnAssistantResponsePart?.Invoke(_pendingToolAnswer);
                    OnTranscript?.Invoke(_pendingToolAnswer);
                }
                else if (_pendingToolAnswer.StartsWith(_assistantLastFullText) && _pendingToolAnswer.Length > _assistantLastFullText.Length)
                {
                    var tail = _pendingToolAnswer.Substring(_assistantLastFullText.Length);
                    if (tail.Length > 0)
                    {
                        OnAssistantResponsePart?.Invoke(tail);
                        OnTranscript?.Invoke(tail);
                    }
                }
                else if (_pendingToolAnswer != _assistantLastFullText)
                {
                    // Divergent; emit full tool answer to ensure completeness (could duplicate; acceptable for correctness)
                    OnAssistantResponsePart?.Invoke(_pendingToolAnswer);
                    OnTranscript?.Invoke(_pendingToolAnswer);
                }
            }

            _assistantLastFullText = string.Empty;
            _pendingIntentText = string.Empty;
            _pendingIntentType = IntentType.QUESTION;
            _pendingToolAnswer = string.Empty;
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
