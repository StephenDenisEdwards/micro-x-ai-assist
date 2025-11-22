namespace GeminiLiveConsole.Models;
// --- DTOs for Gemini Live responses ---
public sealed class GeminiMessage
{
	public ServerContent? ServerContent { get; set; }
	public UsageMetadata? UsageMetadata { get; set; }

	// Present as an empty object {} when the server acknowledges setup
	public object? SetupComplete { get; set; }
}