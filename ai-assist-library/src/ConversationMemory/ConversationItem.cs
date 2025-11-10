using System.Text.Json.Serialization;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ConversationItem
{
	[JsonPropertyName("id")] public string Id { get; set; } = default!;
	[JsonPropertyName("sessionId")] public string SessionId { get; set; } = default!;
	[JsonPropertyName("t0")] public double T0 { get; set; } // ms
	[JsonPropertyName("t1")] public double T1 { get; set; } // ms
	[JsonPropertyName("speaker")] public string? Speaker { get; set; }
	[JsonPropertyName("kind")] public string Kind { get; set; } = default!; // final | act | answer
	[JsonPropertyName("parentActId")] public string? ParentActId { get; set; }
	[JsonPropertyName("text")] public string Text { get; set; } = default!;
	[JsonPropertyName("textVector")] public float[]? TextVector { get; set; }
}

public static class ConversationKinds
{
	public const string Final = "final";
	public const string Act = "act";
	public const string Answer = "answer";
}
