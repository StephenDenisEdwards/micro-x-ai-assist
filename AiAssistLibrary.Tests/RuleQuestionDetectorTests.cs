using AiAssistLibrary.Services.QuestionDetection;
using Xunit;

public sealed class RuleQuestionDetectorTests
{
	private readonly RuleQuestionDetector _detector = new();

	[Fact]
	public void Detects_Simple_Interrogative_Questions()
	{
		var text = "Can you help me with this? We will adjust later. What time is the meeting tomorrow?";
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(5));

		Assert.Equal(2, qs.Count);
		Assert.Contains(qs, q => q.Text.StartsWith("Can you help", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(qs, q => q.Text.StartsWith("What time is", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Ignores_NonQuestions()
	{
		var text = "We should deploy the service today. The pipeline passed.";
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(3));
		Assert.Empty(qs);
	}

	[Fact]
	public void Detects_Tag_Question()
	{
		var text = "It's working, right? The build succeeded.";
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(2));
		Assert.Single(qs);
		Assert.Equal("It's working, right?", qs[0].Text, ignoreCase: true);
	}

	[Fact]
	public void Case_Insensitive_Starter_Detection()
	{
		var text = "WHO is responsible for the update?";
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(1));
		Assert.Single(qs);
		Assert.StartsWith("WHO is", qs[0].Text, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Multiple_Sentences_With_Mixed_Punctuation()
	{
		var text = "How does this integrate with billing? I think we can skip that. Is it enabled now? ok.";
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(4));
		Assert.Equal(2, qs.Count);
		Assert.All(qs, q => Assert.True(q.Text.EndsWith("?")));
	}
}
