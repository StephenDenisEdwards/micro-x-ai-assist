namespace AiAssistLibrary.LLM;

public enum LlmApiMode
{
	Responses,
	Chat
}

public sealed class OpenAIOptions
{
	public string? Endpoint { get; set; }
	public string? ApiKey { get; set; }
	public string? Deployment { get; set; }
	public bool UseEntraId { get; set; } = false;
	public LlmApiMode Mode { get; set; } = LlmApiMode.Responses;
	// Allow overriding model per-request by name, but keep default Deployment for fallback
}
