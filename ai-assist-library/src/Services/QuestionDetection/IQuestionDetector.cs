namespace AiAssistLibrary.Services.QuestionDetection;

public interface IQuestionDetector
{
	IReadOnlyList<DetectedQuestion> Detect(string transcriptSegment, TimeSpan start, TimeSpan end, string? speakerId = null);
}
