using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class HybridQuestionDetectorTests
{
	[Fact]
	public void Emits_Rule_Based_Questions_When_Fallback_Disabled()
	{
		var detector = new HybridQuestionDetector(
			NullLogger<HybridQuestionDetector>.Instance,
			minConfidence: 0.0,
			http: null,
			endpoint: null,
			deployment: null,
			apiKey: null,
			enableFallback: false);

		var text = "What is the latency? The API seems slow. Can we improve it?";
		var qs = detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(6));

		Assert.Equal(2, qs.Count);
		Assert.All(qs, q => Assert.True(q.Confidence >= 0.0));
	}

	[Fact]
	public void Respects_MinConfidence_Filter()
	{
		var detector = new HybridQuestionDetector(
			NullLogger<HybridQuestionDetector>.Instance,
			minConfidence: 0.5,
			http: null,
			endpoint: null,
			deployment: null,
			apiKey: null,
			enableFallback: false);

		var text = "Is this deployed?";
		var qs = detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(2));

		Assert.True(qs.Count == 0 || qs.All(q => q.Confidence >= 0.5), "Adjust threshold or confidence expectation.");
	}

	[Fact]
	public void Preserves_SpeakerId_When_Provided()
	{
		var detector = new HybridQuestionDetector(
			NullLogger<HybridQuestionDetector>.Instance,
			minConfidence: 0.0,
			http: null,
			endpoint: null,
			deployment: null,
			apiKey: null,
			enableFallback: false);

		var text = "Can we roll back?";
		var qs = detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(1), speakerId: "SPEAKER_1");

		Assert.Single(qs);
		Assert.Equal("SPEAKER_1", qs[0].SpeakerId);
	}
}
