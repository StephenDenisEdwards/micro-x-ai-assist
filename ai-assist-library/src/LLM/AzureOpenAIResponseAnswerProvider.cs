using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Responses;
using AiAssistLibrary.ConversationMemory;

namespace AiAssistLibrary.LLM;

public sealed class AzureOpenAIResponseAnswerProvider : IAnswerProvider
{
	private readonly ChatClient _defaultChat;
	private readonly OpenAIOptions _opts;
	private readonly ILogger<AzureOpenAIResponseAnswerProvider> _log;
	private readonly ConversationMemoryClient? _memory;

	public AzureOpenAIResponseAnswerProvider(ChatClient chat, OpenAIOptions opts, ILogger<AzureOpenAIResponseAnswerProvider> log, ConversationMemoryClient? memory)
	{
		_defaultChat = chat;
		_opts = opts;
		_log = log;
		_memory = memory;
	}

	public async Task<string> GetAnswerAsync(PromptPack pack, CancellationToken ct = default, string? overrideModel = null)
	{
		// Resolve deployment to use (Responses API)
		var deploymentToUse = !string.IsNullOrWhiteSpace(overrideModel) ? overrideModel : _opts.Deployment;
		if (string.IsNullOrWhiteSpace(deploymentToUse))
		{
			_log.LogError("No deployment/model specified for Responses API.");
			return string.Empty;
		}

		// Suppress OPENAI001 diagnostics on experimental Responses API types
#pragma warning disable OPENAI001
		OpenAIResponseClient responsesClient;
#pragma warning restore OPENAI001
		try
		{
			var endpoint = _opts.Endpoint!;
			if (_opts.UseEntraId)
			{
				var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), new Azure.Identity.DefaultAzureCredential());
#pragma warning disable OPENAI001
				responsesClient = azureClient.GetOpenAIResponseClient(deploymentToUse);
#pragma warning restore OPENAI001
			}
			else
			{
				var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(_opts.ApiKey!));
#pragma warning disable OPENAI001
				responsesClient = azureClient.GetOpenAIResponseClient(deploymentToUse);
#pragma warning restore OPENAI001
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Failed to create Responses client for deployment {Deployment}.", deploymentToUse);
			return string.Empty;
		}

#pragma warning disable OPENAI001
		OpenAIResponse response;
#pragma warning restore OPENAI001
		try
		{
#pragma warning disable OPENAI001
			response = await responsesClient.CreateResponseAsync(
				userInputText: pack.AssembledPrompt,
				new ResponseCreationOptions
				{
					//Temperature =0.2f,
					MaxOutputTokenCount =512
				},
				ct);
#pragma warning restore OPENAI001
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Response generation failed.");
			return string.Empty;
		}

#pragma warning disable OPENAI001
		var text = response.GetOutputText();
#pragma warning restore OPENAI001
		_log.LogDebug("LLM answer length: {Len}", text?.Length ??0);
		return text ?? string.Empty;
	}
}
