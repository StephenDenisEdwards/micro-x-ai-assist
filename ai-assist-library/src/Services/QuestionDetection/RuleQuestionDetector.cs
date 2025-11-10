using System.Text.RegularExpressions;

namespace AiAssistLibrary.Services.QuestionDetection;

public sealed class RuleQuestionDetector : IQuestionDetector
{
	private static readonly string[] InterrogativeStarters =
	{
		"who", "what", "when", "where", "why", "how", "which", "whose", "whom",
		"is", "are", "am", "was", "were", "do", "does", "did", "can", "could", "will", "would", "shall", "should",
		"may", "might", "have", "has", "had"
	};

	private static readonly Regex TagQuestionRegex = new(@"(?i)(,?\s+(isn['’]t it|doesn['’]t it|don['’]t you|right|okay|ok|no)\?)$", RegexOptions.Compiled);
	private static readonly Regex SentenceSplitRegex = new(@"(?<=[\.!?])\s+", RegexOptions.Compiled);

	public IReadOnlyList<DetectedQuestion> Detect(string transcriptSegment, TimeSpan start, TimeSpan end, string? speakerId = null)
	{
		var results = new List<DetectedQuestion>();
		if (string.IsNullOrWhiteSpace(transcriptSegment)) return results;

		var sentences = SentenceSplitRegex.Split(transcriptSegment.Trim());
		var cursorStart = start;
		var totalDuration = end - start;
		var perSentenceApprox = totalDuration / Math.Max(1, sentences.Length);

		foreach (var s in sentences)
		{
			var sentence = s.Trim();
			if (sentence.Length == 0)
			{
				cursorStart += perSentenceApprox;
				continue;
			}

			var lower = sentence.ToLowerInvariant();
			bool hasQuestionMark = sentence.EndsWith("?");
			bool startsInterrogative = InterrogativeStarters.Any(w => lower.StartsWith(w + " "));
			bool hasTag = TagQuestionRegex.IsMatch(sentence);

			double confidence = 0.0;
			if (hasQuestionMark) confidence += 0.6;
			if (startsInterrogative) confidence += 0.25;
			if (hasTag) confidence += 0.15;

			// If it reads like a question/request but lacks a question mark, give it a baseline confidence.
			if (!hasQuestionMark && startsInterrogative && sentence.Length <= 80)
				confidence = Math.Max(confidence, 0.5);

			if (confidence >= 0.5)
			{
				results.Add(new DetectedQuestion
				{
					Text = sentence,
					Confidence = Math.Min(confidence, 1.0),
					Start = cursorStart,
					End = cursorStart + perSentenceApprox,
					SpeakerId = speakerId,
					Category = "Interrogative"
				});
			}

			cursorStart += perSentenceApprox;
		}
		return results;
	}
}
