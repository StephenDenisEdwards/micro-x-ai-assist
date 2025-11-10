using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AiAssistLibrary.IntegrationTests;

public sealed class HybridDetector_AzureIntegrationTests
{
	private readonly ITestOutputHelper _output;
	public HybridDetector_AzureIntegrationTests(ITestOutputHelper output) => _output = output;

	public static IEnumerable<object[]> GetScenarios()
	{
		yield return new object[]
		{
			new DetectionScenario(
				Name: "Standup",
				Utterances: SimulatedTranscription.StandupSample,
				ExpectedQuestionTexts: new []
				{
					"Can we finalize the sprint plan?",
					"What are the current error rates?",
					"How do we add tracing?",
					"It's stable, right?"
				})
		};

		yield return new object[]
		{
			new DetectionScenario(
				Name: "SupportTicket",
				Utterances: SimulatedTranscription.SupportTicketSample,
				ExpectedQuestionTexts: new []
				{
					"Why is my account locked?",
					"Can you unlock it now?",
					"It's urgent, okay?"
				})
		};
	}

	private static (string? endpoint, string? deployment, string? key) ReadAzureOpenAI()
	{
		var ep = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
		var dep = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
		var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

		if (string.IsNullOrWhiteSpace(ep) || string.IsNullOrWhiteSpace(dep) || string.IsNullOrWhiteSpace(key))
		{
			var cfg = new ConfigurationBuilder()
				.AddEnvironmentVariables()
				.AddUserSecrets<HybridDetector_AzureIntegrationTests>(optional: true)
				.Build();
			ep = string.IsNullOrWhiteSpace(ep) ? cfg["AZURE_OPENAI_ENDPOINT"] ?? cfg["QuestionDetection:OpenAIEndpoint"] : ep;
			dep = string.IsNullOrWhiteSpace(dep) ? cfg["AZURE_OPENAI_DEPLOYMENT"] ?? cfg["QuestionDetection:OpenAIDeployment"] : dep;
			key = string.IsNullOrWhiteSpace(key) ? cfg["AZURE_OPENAI_API_KEY"] ?? cfg["QuestionDetection:OpenAIKey"] : key;
		}
		return (ep, dep, key);
	}

	[SkippableTheory]
	[MemberData(nameof(GetScenarios))]
	public void Classifies_With_Azure_Backend_When_Configured(DetectionScenario scenario)
	{
		var (endpoint, deployment, key) = ReadAzureOpenAI();
		Skip.If(string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(key),
			"Azure OpenAI env vars or user secrets not set.");

		using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
		var detector = new HybridQuestionDetector(
			NullLogger<HybridQuestionDetector>.Instance,
			minConfidence:0.0,
			http: http,
			endpoint: endpoint!,
			deployment: deployment!,
			apiKey: key!,
			enableFallback: true);

		var questions = new List<DetectedQuestion>();
		foreach (var u in scenario.Utterances)
		{
			var qs = detector.Detect(u.Text, u.Start, u.End, u.SpeakerId);
			questions.AddRange(qs);
			_output.WriteLine($"[{scenario.Name}] [{u.SpeakerId}] {u.Text} -> {qs.Count} detected");
		}

		Assert.NotEmpty(questions);
		foreach (var expected in scenario.ExpectedQuestionTexts)
		{
			Assert.Contains(questions, q => q.Text.Equals(expected, StringComparison.OrdinalIgnoreCase));
		}
	}
}
