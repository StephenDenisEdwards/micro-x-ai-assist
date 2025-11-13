using AiAssistLibrary.LLM;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace AiAssistLibrary.Extensions;

public static class ServiceCollectionOpenAIExtensions
{
	public static IServiceCollection AddAzureOpenAI(this IServiceCollection services, Action<OpenAIOptions> configure)
	{
		services.Configure(configure);

		// Register ChatClient
		services.AddSingleton<ChatClient>(sp =>
		{
			var o = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
			if (string.IsNullOrWhiteSpace(o.Endpoint))
				throw new InvalidOperationException("OpenAIOptions.Endpoint is required.");
			if (string.IsNullOrWhiteSpace(o.Deployment))
				throw new InvalidOperationException("OpenAIOptions.Deployment is required.");

			var baseUrl = o.Endpoint!.TrimEnd('/');
			if (!baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
			{
				baseUrl = baseUrl + "/openai/v1/";
			}
			else if (!baseUrl.EndsWith("/"))
			{
				baseUrl += "/";
			}

			var options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };

			ChatClient client;
#pragma warning disable OPENAI001
			if (o.UseEntraId)
			{
				var tokenPolicy = new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default");
				client = new ChatClient(
					model: o.Deployment!,
					authenticationPolicy: tokenPolicy,
					options: options);
			}
			else
			{
				var apiKey = o.ApiKey ?? throw new InvalidOperationException("OpenAIOptions.ApiKey is required when UseEntraId=false.");
				client = new ChatClient(
					model: o.Deployment!,
					credential: new ApiKeyCredential(apiKey),
					options: options);
			}
#pragma warning restore OPENAI001
			return client;
		});

		// Register OpenAIResponseClient for Responses API usage
#pragma warning disable OPENAI001
		services.AddSingleton<OpenAIResponseClient>(sp =>
		{
			var o = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
			if (string.IsNullOrWhiteSpace(o.Endpoint))
				throw new InvalidOperationException("OpenAIOptions.Endpoint is required.");
			if (string.IsNullOrWhiteSpace(o.Deployment))
				throw new InvalidOperationException("OpenAIOptions.Deployment is required.");

			var baseUrl = o.Endpoint!.TrimEnd('/');
			if (!baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
			{
				baseUrl = baseUrl + "/openai/v1/";
			}
			else if (!baseUrl.EndsWith("/"))
			{
				baseUrl += "/";
			}

			var options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };

			OpenAIResponseClient responseClient;
			if (o.UseEntraId)
			{
				var tokenPolicy = new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default");
				responseClient = new OpenAIResponseClient(
					model: o.Deployment!,
					authenticationPolicy: tokenPolicy,
					options: options);
			}
			else
			{
				var apiKey = o.ApiKey ?? throw new InvalidOperationException("OpenAIOptions.ApiKey is required when UseEntraId=false.");
				responseClient = new OpenAIResponseClient(
					model: o.Deployment!,
					credential: new ApiKeyCredential(apiKey),
					options: options);
			}
			return responseClient;
		});
#pragma warning restore OPENAI001

		return services;
	}
}
