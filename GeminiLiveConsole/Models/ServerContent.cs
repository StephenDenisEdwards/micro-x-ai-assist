namespace GeminiLiveConsole.Models;
// --- DTOs for Gemini Live responses ---
internal sealed class ServerContent
{
	public ModelTurn? ModelTurn { get; set; }
	public bool? TurnComplete { get; set; }
}