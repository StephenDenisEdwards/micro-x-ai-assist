using System.Linq;
using System.Text.RegularExpressions;

namespace AiAssistLibrary.Services.QuestionDetection;

// Detects imperative, instructional, or command-form utterances and reports them as questions (category = "Imperative").
public sealed class RulesImperativeDetect : IQuestionDetector
{
	// Heuristic starters for imperative/instructional prompts commonly seen in coding tasks and guidance.
	private static readonly string[] ImperativeStarters =
	{
		"define", "declare", "create", "write", "implement", "use", "add", "inherit", "override",
		"instantiate", "register", "configure", "serialize", "deserialize", "demonstrate", "sort",
		"remove", "subscribe", "await", "lock", "explain", "describe", "show me", "tell me", "give me",
		"help me", "walk me through", "please explain", "please describe", "please tell me", "please show me",
		"please give me", "please help me", "please walk me through"
	};

	// Matches any starter at the beginning of the sentence with a word-boundary so punctuation or end-of-sentence is accepted.
	private static readonly Regex ImperativeStartersRegex =
		new(@"^(?:" + string.Join("|", ImperativeStarters.Select(Regex.Escape)) + @")\b", RegexOptions.Compiled);

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
			bool isImperative = false;

			// Use a single compiled regex to detect any starter at the start of the sentence
			if (ImperativeStartersRegex.IsMatch(lower))
			{
				isImperative = true;
			}

			// Additional heuristic: sentences starting with verbs followed by "a"/"an"/"the" are often imperatives in docs (e.g., "Create a method ...").
			if (!isImperative)
			{
				if (Regex.IsMatch(lower, @"^[a-z]+\s+(a|an|the)\b"))
					isImperative = true;
			}

			if (isImperative)
			{
				results.Add(new DetectedQuestion
				{
					Text = sentence,
					Confidence = 0.8, // strong baseline so Azure fallback won't try to reclassify (<0.7 is reviewed)
					Start = cursorStart,
					End = cursorStart + perSentenceApprox,
					SpeakerId = speakerId,
					Category = "Imperative"
				});
			}

			cursorStart += perSentenceApprox;
		}

		return results;
	}
}
