namespace AiAssistLibrary.ConversationMemory;

public interface IPromptPackBuilder
{
	Task<PromptPack> BuildAsync(string fullFinal, string newActText, double nowMs);
}