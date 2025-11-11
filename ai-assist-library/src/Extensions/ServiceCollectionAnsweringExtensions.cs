using AiAssistLibrary.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiAssistLibrary.Extensions;

public static class ServiceCollectionAnsweringExtensions
{
	// Registers ChatClient via AddAzureOpenAI and wires the answer pipeline & provider
	public static IServiceCollection AddAnswering(this IServiceCollection services, IConfiguration config,
		Action<OpenAIOptions>? configure = null)
	{
		// Bind defaults from config (support secrets.json + env via configuration providers in host)
		var section = config.GetSection("OpenAI");
		var options = new OpenAIOptions();
		section.Bind(options);

		// Supplement from environment variables if missing
		options.Endpoint ??= Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
		options.ApiKey ??= Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
		options.Deployment ??= Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
		if (bool.TryParse(Environment.GetEnvironmentVariable("AZURE_OPENAI_USE_ENTRAID"), out var useEntra))
		{
			options.UseEntraId = useEntra;
		}
		if (Enum.TryParse(Environment.GetEnvironmentVariable("AZURE_OPENAI_MODE"), ignoreCase:true, out LlmApiMode mode))
		{
			options.Mode = mode;
		}

		configure?.Invoke(options);

		services.AddAzureOpenAI(o =>
		{
			o.Endpoint = options.Endpoint;
			o.ApiKey = options.ApiKey;
			o.Deployment = options.Deployment;
			o.UseEntraId = options.UseEntraId;
		});
		services.AddSingleton(options); // for provider selection

		// Register provider based on mode
		if (options.Mode == LlmApiMode.Chat)
		{
			services.AddSingleton<IAnswerProvider, AzureOpenAIChatAnswerProvider>();
		}
		else
		{
			services.AddSingleton<IAnswerProvider, AzureOpenAIResponseAnswerProvider>();
		}
		services.AddSingleton<AnswerPipeline>();
		return services;
	}
}
