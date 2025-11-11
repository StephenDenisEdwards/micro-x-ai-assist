namespace AiAssistLibrary.LLM;

public interface IAnswerProvider
{
	Task<string> GetAnswerAsync(string assembledPrompt, CancellationToken ct = default, string? overrideModel = null);
}
