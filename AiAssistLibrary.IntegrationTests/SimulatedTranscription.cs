namespace AiAssistLibrary.IntegrationTests;

public sealed record SimulatedUtterance(string SpeakerId, string Text, TimeSpan Start, TimeSpan End);

public static class SimulatedTranscription
{
	public static IReadOnlyList<SimulatedUtterance> StandupSample => new[]
	{
		new SimulatedUtterance("S1", "Good morning team.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
		new SimulatedUtterance("S2", "Can we finalize the sprint plan?", TimeSpan.FromSeconds(3),
			TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("S1", "We deployed the new payments service yesterday.", TimeSpan.FromSeconds(8),
			TimeSpan.FromSeconds(14)),
		new SimulatedUtterance("S3", "What are the current error rates?", TimeSpan.FromSeconds(14),
			TimeSpan.FromSeconds(19)),
		new SimulatedUtterance("S2", "It's stable, right?", TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(23)),
		new SimulatedUtterance("S1", "I think logs look clean.", TimeSpan.FromSeconds(23), TimeSpan.FromSeconds(27)),
		new SimulatedUtterance("S3", "How do we add tracing?", TimeSpan.FromSeconds(27), TimeSpan.FromSeconds(31)),
	};

	public static IReadOnlyList<SimulatedUtterance> SupportTicketSample => new[]
	{
		new SimulatedUtterance("AGENT", "Hello, thanks for contacting support.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("USER", "Why is my account locked?", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)),
		new SimulatedUtterance("AGENT", "Let me check.", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("USER", "Can you unlock it now?", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(12)),
		new SimulatedUtterance("AGENT", "We can request a reset.", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15)),
		new SimulatedUtterance("USER", "It's urgent, okay?", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(18)),
	};

	/// <summary>
	/// The rule detector will fall back on low confidence for this question. It will then be sent to Azure for review.
	/// </summary>
	public static IReadOnlyList<SimulatedUtterance> LowConfidenceOfQuestion => new[]
	{
		new SimulatedUtterance("S1", "Hey, what's the weather?", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
	};


	// Explain the difference between class and struct in C sharp.
	public static IReadOnlyList<SimulatedUtterance> ImperativeRequests => new[]
	{
		new SimulatedUtterance("S1", "Explain the difference between class and struct in C sharp.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Explain dependency injection in .NET applications.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Explain the difference between interface and abstract class.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),


		// Explain the difference between interface and abstract class.
	};

}
