namespace AiAssistLibrary.ConversationMemory;

public sealed class ConversationMemoryOptions
{
	public string SessionId { get; set; } = "default-session"; // caller can set externally
	public bool Enabled { get; set; } = true;
	public string? SearchEndpoint { get; set; }
	public string? SearchAdminKey { get; set; }
	public string IndexName { get; set; } = "conv_items";
	public string? OpenAIEndpoint { get; set; }
	public string? OpenAIKey { get; set; }
	public string? EmbeddingDeployment { get; set; } // e.g. text-embedding-3-small
	public int EmbeddingDimensions { get; set; } = 1536;
}
