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
			return new ConversationMemoryClient(opts);
		});
		return services;
	}
}
