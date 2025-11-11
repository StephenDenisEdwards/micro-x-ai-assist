namespace AiAssistLibrary.ConversationMemory;

public interface IPromptPackBuilder
{
	Task<PromptPack> BuildAsync(string newActText, double nowMs);
}