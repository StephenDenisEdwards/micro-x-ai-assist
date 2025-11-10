namespace AiAssistLibrary.IntegrationTests;

public sealed record SimulatedUtterance(string SpeakerId, string Text, TimeSpan Start, TimeSpan End);

public static class SimulatedTranscription
{
 public static IReadOnlyList<SimulatedUtterance> StandupSample => new[]
 {
 new SimulatedUtterance("S1", "Good morning team.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
 new SimulatedUtterance("S2", "Can we finalize the sprint plan?", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(8)),
 new SimulatedUtterance("S1", "We deployed the new payments service yesterday.", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(14)),
 new SimulatedUtterance("S3", "What are the current error rates?", TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(19)),
 new SimulatedUtterance("S2", "It's stable, right?", TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(23)),
 new SimulatedUtterance("S1", "I think logs look clean.", TimeSpan.FromSeconds(23), TimeSpan.FromSeconds(27)),
 new SimulatedUtterance("S3", "How do we add tracing?", TimeSpan.FromSeconds(27), TimeSpan.FromSeconds(31)),
 };
}
