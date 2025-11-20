using System.Linq;
using System.Text.RegularExpressions;

namespace AiAssistLibrary.Services.QuestionDetection;

// Detects imperative, instructional, or command-form utterances and reports them as questions (category = "Imperative").
public sealed class RulesImperativeDetect : IQuestionDetector
{
	// High-confidence imperative starters commonly seen in coding tasks and guidance.
	private static readonly string[] HighConfidenceStarters =
	{
		"define", "declare", "create", "write", "implement", "use", "add", "inherit", "override",
		"instantiate", "register", "configure", "serialize", "deserialize", "demonstrate", "sort",
		"remove", "subscribe", "await", "lock", "explain", "describe", "show me", "tell me", "give me",
		"help me", "walk me through", "please explain", "please describe", "please tell me", "please show me",
		"please give me", "please help me", "please walk me through"
	};

	// Lower-confidence starters that often indicate examples or samples — keep them separate so they can be reviewed.
	private static readonly string[] LowConfidenceStarters =
	{
		"for example", "example", "examples", "sample", "samples"
	};

	// Compiled, case-insensitive regexes
	private static readonly Regex HighStartersRegex =
		new(@"^(?:" + string.Join("|", HighConfidenceStarters.Select(Regex.Escape)) + @")\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex LowStartersRegex =
		new(@"^(?:" + string.Join("|", LowConfidenceStarters.Select(Regex.Escape)) + @")\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// Matches a leading verb followed by an article (Unicode-aware), e.g., "Create a method ..."
	private static readonly Regex VerbArticleRegex =
		new(@"^\p{L}+\s+(?:a|an|the)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// Detect polite/modal requests like "Could you", "Can you", "Would you"
	private static readonly Regex ModalRequestRegex =
		new(@"^(?:(?:could|can|would|will)\s+you\b|please\b|(?:show|tell|help)\s+me\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// Sentence split (simple). Consider a full sentence tokenizer for production if you need higher accuracy (handles abbreviations, ellipses, etc).
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

			bool isImperative = false;
			double confidence = 0.0;

			// High-confidence starters
			if (HighStartersRegex.IsMatch(sentence))
			{
				isImperative = true;
				confidence = 0.80; // keep existing strong baseline for clear imperatives
			}
			// Modal/polite requests — often question-like but can be treated as imperative-style intent
			else if (ModalRequestRegex.IsMatch(sentence))
			{
				isImperative = true;
				confidence = 0.72; // medium confidence so downstream can re-evaluate if needed
			}
			// Verb + article heuristic (Unicode-aware)
			else if (VerbArticleRegex.IsMatch(sentence))
			{
				isImperative = true;
				confidence = 0.75;
			}
			// Low-confidence starters (examples/samples) — mark but set low confidence to allow review/reclassification
			else if (LowStartersRegex.IsMatch(sentence))
			{
				isImperative = true;
				confidence = 0.65;
			}

			if (isImperative)
			{
				results.Add(new DetectedQuestion
				{
					Text = sentence,
					Confidence = confidence,
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
