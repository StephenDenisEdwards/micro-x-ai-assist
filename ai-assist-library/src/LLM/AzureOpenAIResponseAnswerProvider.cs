#pragma warning disable OPENAI001
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using AiAssistLibrary.ConversationMemory;

namespace AiAssistLibrary.LLM;

public sealed class AzureOpenAIResponseAnswerProvider : IAnswerProvider
{
	private readonly OpenAIResponseClient _responsesClient;
	private readonly ILogger<AzureOpenAIResponseAnswerProvider> _log;
	private readonly OpenAIOptions _opts;
	private readonly ConversationMemoryClient? _memory;

	public AzureOpenAIResponseAnswerProvider(OpenAIResponseClient responsesClient, OpenAIOptions opts, ILogger<AzureOpenAIResponseAnswerProvider> log, ConversationMemoryClient? memory)
	{
		_responsesClient = responsesClient;
		_opts = opts;
		_log = log;
		_memory = memory;
	}

	public async Task<string> GetAnswerAsync(PromptPack pack, CancellationToken ct = default, string? overrideModel = null)
	{
		try
		{
			var options = new ResponseCreationOptions
			{
				Instructions = string.IsNullOrWhiteSpace(pack.SystemPrompt) ? null : pack.SystemPrompt,
				MaxOutputTokenCount = 512
			};

#pragma warning disable OPENAI001
			var result = await _responsesClient.CreateResponseAsync(
				userInputText: pack.NewActText,
				options,
				ct);
			var text = result.Value.GetOutputText();
#pragma warning restore OPENAI001

			_log.LogDebug("LLM answer length: {Len}", text?.Length ?? 0);
			return text ?? string.Empty;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Response generation failed.");
			return string.Empty;
		}
	}
}
#pragma warning restore OPENAI001
