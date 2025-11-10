namespace AiAssistLibrary.Services.QuestionDetection;

public sealed class DetectedQuestion
{
	public required string Text { get; init; }
	public double Confidence { get; set; }
	public TimeSpan Start { get; init; }
	public TimeSpan End { get; init; }
	public string? SpeakerId { get; init; }
	// Category is now mutable so merging detectors can update it.
	public string? Category { get; set; }
}
