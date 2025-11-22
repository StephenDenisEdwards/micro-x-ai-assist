namespace GeminiLiveConsole.Models;
// --- DTOs for Gemini Live responses ---
public sealed class ServerContent
{
	public ModelTurn? ModelTurn { get; set; }
	public bool? TurnComplete { get; set; }

	public InputTranscription? InputTranscription { get; set; }
}

public sealed class InputTranscription
{
	public string Text { get; set; }
}