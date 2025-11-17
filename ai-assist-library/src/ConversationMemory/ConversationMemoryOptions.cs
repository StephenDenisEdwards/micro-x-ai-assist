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
	public bool ClearSessionOnStart { get; set; } = false; // New option: if true, will delete all documents for the current SessionId at startup

	// Configurable time windows and page sizes (avoid magic numbers)
	public TimeSpan RecentFinalWindow { get; set; } = TimeSpan.FromSeconds(40);
	public int RecentFinalsPageSize { get; set; } = 4;

	public TimeSpan RelatedActsWindow { get; set; } = TimeSpan.FromMinutes(20);
	public int RelatedActsPageSize { get; set; } = 5;

	public TimeSpan OpenActsWindow { get; set; } = TimeSpan.FromMinutes(20);
	public int OpenActsPageSize { get; set; } = 50;
}
