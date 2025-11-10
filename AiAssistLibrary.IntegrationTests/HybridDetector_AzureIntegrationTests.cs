using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AiAssistLibrary.IntegrationTests;

public sealed class HybridDetector_AzureIntegrationTests
{
	private readonly ITestOutputHelper _output;
	public HybridDetector_AzureIntegrationTests(ITestOutputHelper output) => _output = output;

	private static (string? endpoint, string? deployment, string? key) ReadAzureOpenAI()
	{
		var ep = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
		var dep = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
		var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
		return (ep, dep, key);
	}

	[SkippableFact]
	public void Classifies_With_Azure_Backend_When_Configured()
	{
		var (endpoint, deployment, key) = ReadAzureOpenAI();
		Skip.If(
			string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) ||
			string.IsNullOrWhiteSpace(key),
			"Azure OpenAI env vars not set: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_DEPLOYMENT, AZURE_OPENAI_API_KEY");

		using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
		var detector = new HybridQuestionDetector(
			NullLogger<HybridQuestionDetector>.Instance,
			minConfidence: 0.0,
			http: http,
			endpoint: endpoint!,
			deployment: deployment!,
			apiKey: key!,
			enableFallback: true);

		// Simulated STT stream: feed utterances one by one as SpeechPushClient would on final results.
		var questions = new List<DetectedQuestion>();
		foreach (var u in SimulatedTranscription.StandupSample)
		{
			var qs = detector.Detect(u.Text, u.Start, u.End, u.SpeakerId);
			questions.AddRange(qs);
			_output.WriteLine($"[{u.SpeakerId}] {u.Text} -> {qs.Count} detected");
		}

		Assert.NotEmpty(questions);
		// Expect at least the three interrogative sentences to be kept as questions
		var expectedStarts = new[]
		{
			"Can we finalize the sprint plan?",
			"What are the current error rates?",
			"How do we add tracing?"
		};
		foreach (var e in expectedStarts)
		{
			Assert.Contains(questions, q => q.Text.Equals(e, StringComparison.OrdinalIgnoreCase));
		}

		// Tag question should likely remain with confidence possibly adjusted
		Assert.Contains(questions, q => q.Text.Equals("It's stable, right?", StringComparison.OrdinalIgnoreCase));
	}
}
