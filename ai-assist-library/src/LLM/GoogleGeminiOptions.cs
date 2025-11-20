namespace AiAssistLibrary.LLM;

public sealed class GoogleGeminiOptions
{
	// e.g. "gemini-2.0-flash" or whatever you pick in AI Studio
	public string Model { get; set; } = "gemini-2.0-flash";

	// Base URL for the Generative Language API
	// For standard REST: "https://generativelanguage.googleapis.com/v1beta"
	public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

	// Your API key (you’ll likely inject via user-secrets / KeyVault)
	public string ApiKey { get; set; } = string.Empty;
}