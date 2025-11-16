using System.Text.Json.Serialization;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ConversationItem : IEquatable<ConversationItem>
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

	public bool Equals(ConversationItem? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Id == other.Id &&
		       SessionId == other.SessionId &&
		       T0.Equals(other.T0) &&
		       T1.Equals(other.T1) &&
		       Speaker == other.Speaker &&
		       Kind == other.Kind &&
		       ParentActId == other.ParentActId &&
		       Text == other.Text;
	}

	public override bool Equals(object? obj) => Equals(obj as ConversationItem);

	public override int GetHashCode()
	{
		return HashCode.Combine(Id, SessionId, T0, T1, Speaker, Kind, ParentActId, Text);
	}
}