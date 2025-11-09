namespace AiAssistLibrary.Settings;

public sealed class SpeechOptions
{
	public string Region { get; set; } = "westeurope";
	public string Language { get; set; } = "en-GB";
	public string? Key { get; set; }
}
