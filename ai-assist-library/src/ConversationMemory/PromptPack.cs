namespace AiAssistLibrary.ConversationMemory;

public sealed class PromptPack
{
	public IReadOnlyList<ConversationItem> RecentFinals { get; init; } = Array.Empty<ConversationItem>();
	public IReadOnlyList<(ConversationItem Act, ConversationItem? Answer)> RecentActs { get; init; } = Array.Empty<(ConversationItem, ConversationItem?)>();
	public IReadOnlyList<ConversationItem> OpenActs { get; init; } = Array.Empty<ConversationItem>();
	public string NewActText { get; init; } = string.Empty;
	public string SystemPrompt { get; init; } = string.Empty;
	public string AssembledPrompt { get; init; } = string.Empty;
}