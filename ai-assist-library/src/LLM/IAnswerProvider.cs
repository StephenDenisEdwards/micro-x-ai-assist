using AiAssistLibrary.ConversationMemory;

namespace AiAssistLibrary.LLM;

public interface IAnswerProvider
{
	Task<string> GetAnswerAsync(PromptPack pack, CancellationToken ct = default, string? overrideModel = null);
}
