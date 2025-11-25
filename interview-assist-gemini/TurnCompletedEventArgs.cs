using GeminiLiveConsole.Models;
using System;

namespace GeminiLiveConsole;

public class TurnCompletedEventArgs : EventArgs
{
    public DetectedIntent? Intent { get; set; }
    public string? AssistantAnswer { get; set; }
    public string? CodeExample { get; set; }
}
