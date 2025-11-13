#pragma warning disable OPENAI001
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using AiAssistLibrary.ConversationMemory;

namespace AiAssistLibrary.LLM;

public sealed class AzureOpenAIResponseAnswerProvider : IAnswerProvider
{
	//private readonly ChatClient _defaultChat;
	private readonly OpenAIOptions _opts;
	private readonly ILogger<AzureOpenAIResponseAnswerProvider> _log;
	private readonly ConversationMemoryClient? _memory;

	public AzureOpenAIResponseAnswerProvider(OpenAIOptions opts, ILogger<AzureOpenAIResponseAnswerProvider> log, ConversationMemoryClient? memory)
	{
		//_defaultChat = chat;
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

		// Build OpenAI Responses client targeting Azure OpenAI endpoint
		OpenAIResponseClient responsesClient;
		try
		{
			var endpoint = _opts.Endpoint!; // expected like https://<resource>.openai.azure.com
			var baseUrl = endpoint.TrimEnd('/');
			if (!baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
			{
				baseUrl = baseUrl + "/openai/v1/";
			}
			else
			{
				// ensure trailing slash
				if (!baseUrl.EndsWith("/")) baseUrl += "/";
			}

			var clientOptions = new OpenAIClientOptions
			{
				
				Endpoint = new Uri(baseUrl)
			};

			if (_opts.UseEntraId)
			{
				var tokenPolicy = new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default");
				responsesClient = new OpenAIResponseClient(
					model: deploymentToUse!,
					authenticationPolicy: tokenPolicy,
					options: clientOptions);
			}
			else
			{
				var key = _opts.ApiKey!;
				responsesClient = new OpenAIResponseClient(
					model: deploymentToUse,
					credential: new ApiKeyCredential(key),
					options: clientOptions);
			}
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Failed to create Responses client for deployment {Deployment}.", deploymentToUse);
			return string.Empty;
		}

		OpenAIResponse response;
		try
		{
			response = await responsesClient.CreateResponseAsync(
				//userInputText: "What is the difference between a class and a struct in C#?", //pack.NewActText,  //pack.AssembledPrompt,
				userInputText: pack.NewActText,  
				new ResponseCreationOptions
				{
					//Temperature =0.2f,
					//Instructions = "The question will be one about .NET and C#.",
					// Explicitly guide the model to produce text output
					Instructions = string.IsNullOrWhiteSpace(pack.SystemPrompt) ? null : pack.SystemPrompt,

					MaxOutputTokenCount = 512
				},
				ct);
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Response generation failed.");
			return string.Empty;
		}

		var text = response.GetOutputText();
		_log.LogDebug("LLM answer length: {Len}", text?.Length ??0);
		return text ?? string.Empty;
	}
}
#pragma warning restore OPENAI001
