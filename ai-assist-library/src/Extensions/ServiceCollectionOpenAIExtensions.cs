using AiAssistLibrary.LLM;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiAssistLibrary.Extensions;

public static class ServiceCollectionOpenAIExtensions
{
	public static IServiceCollection AddAzureOpenAI(this IServiceCollection services, Action<OpenAIOptions> configure)
	{
		services.Configure(configure);

		services.AddSingleton(sp =>
		{
			var o = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
			if (string.IsNullOrWhiteSpace(o.Endpoint))
				throw new InvalidOperationException("OpenAIOptions.Endpoint is required.");
			if (string.IsNullOrWhiteSpace(o.Deployment))
				throw new InvalidOperationException("OpenAIOptions.Deployment is required.");

			AzureOpenAIClient azureClient = o.UseEntraId
				? new AzureOpenAIClient(new Uri(o.Endpoint!), new DefaultAzureCredential())
				: new AzureOpenAIClient(new Uri(o.Endpoint!),
					new AzureKeyCredential(o.ApiKey ??
										   throw new InvalidOperationException(
											   "OpenAIOptions.ApiKey is required when UseEntraId=false.")));

			return azureClient.GetChatClient(o.Deployment!);
		});

		return services;
	}
}
