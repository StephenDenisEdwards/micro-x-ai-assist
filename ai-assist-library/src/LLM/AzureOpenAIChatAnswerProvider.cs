using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using AiAssistLibrary.ConversationMemory;

namespace AiAssistLibrary.LLM;

public sealed class AzureOpenAIChatAnswerProvider : IAnswerProvider
{
	private readonly ChatClient _chatClient;
	private readonly ILogger<AzureOpenAIChatAnswerProvider> _log;

	public AzureOpenAIChatAnswerProvider(ChatClient chatClient, ILogger<AzureOpenAIChatAnswerProvider> log)
	{
		_chatClient = chatClient;
		_log = log;
	}

	public async Task<string> GetAnswerAsync(PromptPack pack, CancellationToken ct = default, string? overrideModel = null)
	{
		try
		{
			var systemText = string.IsNullOrWhiteSpace(pack.SystemPrompt)
				? "You are a helpful assistant. Answer concisely."
				: pack.SystemPrompt;
			var resp = await _chatClient.CompleteChatAsync([
				new SystemChatMessage(systemText),
				new UserChatMessage(pack.AssembledPrompt)
			], cancellationToken: ct);
			var text = resp.Value.Content[0].Text;
			_log.LogDebug("Chat answer length: {Len}", text?.Length ??0);
			return text ?? string.Empty;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Chat completion failed.");
			return string.Empty;
		}
	}
}
