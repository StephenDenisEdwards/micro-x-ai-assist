namespace AiAssistLibrary.IntegrationTests;

public sealed record DetectionScenario(
	string Name,
	IReadOnlyList<SimulatedUtterance> Utterances,
	IReadOnlyList<string> ExpectedQuestionTexts
);
