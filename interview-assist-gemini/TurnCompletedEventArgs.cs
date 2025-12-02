using System;
using InterviewLiveConsole.Models;

namespace InterviewLiveConsole;

public class TurnCompletedEventArgs : EventArgs
{
    public DetectedIntent? Intent { get; set; }
    public string? AssistantAnswer { get; set; }
    public string? CodeExample { get; set; }
}
