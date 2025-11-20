using Newtonsoft.Json;

namespace GeminiLiveConsole;

public enum IntentType { Question, Imperative }

public sealed record ReportIntentPayload(
    [property: JsonProperty("text")] string Text,
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("answer")] string Answer
);

public sealed record FunctionCall(
    [property: JsonProperty("name")] string Name,
    [property: JsonProperty("arguments")] ReportIntentPayload Arguments
);

public sealed record IncomingMessage(
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("functionCall")] FunctionCall? FunctionCall,
    [property: JsonProperty("transcript")] string? Transcript
);

public sealed class GeminiLiveConfig
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public Uri Endpoint { get; init; } = new("wss://generativelanguage.googleapis.com/v1beta/live:connect");
    public int SampleRate { get; init; } = 16000;
}
