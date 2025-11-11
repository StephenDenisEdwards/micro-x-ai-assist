using AiAssistLibrary.ConversationMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiAssistLibrary.Extensions;

public static class ServiceCollectionMemoryExtensions
{
	public static IServiceCollection AddConversationMemory(this IServiceCollection services,
		Action<ConversationMemoryOptions> configure)
	{
		services.Configure(configure);
		services.AddSingleton<ConversationMemoryClient>(sp =>
		{
			var opts = sp.GetRequiredService<IOptions<ConversationMemoryOptions>>().Value;
			// Fallback to environment variable if admin key not provided via configuration/user secrets
			if (string.IsNullOrWhiteSpace(opts.SearchAdminKey))
			{
				var env = Environment.GetEnvironmentVariable("AZURE_SEARCH_ADMIN_KEY");
				if (!string.IsNullOrWhiteSpace(env))
				{
					opts.SearchAdminKey = env;
				}
			}
			// Fallback for endpoint
			if (string.IsNullOrWhiteSpace(opts.SearchEndpoint))
			{
				var envEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
				if (!string.IsNullOrWhiteSpace(envEndpoint))
				{
					opts.SearchEndpoint = envEndpoint;
				}
			}
			return new ConversationMemoryClient(opts);
		});
		return services;
	}
}
