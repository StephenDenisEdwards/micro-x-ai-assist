using System;
using System.Threading;
using System.Threading.Tasks;

public interface IRealtimeApi
{
	// Lifecycle
	event Action? OnConnected;
	event Action? OnReady;
	event Action? OnDisconnected;

	// Diagnostics
	event Action<string>? OnInfo;
	event Action<string>? OnWarning;
	event Action<string>? OnDebug;
	event Action<Exception>? OnError;

	// Input/Output
	event Action<string>? OnUserTranscript;
	event Action? OnSpeechStarted;
	event Action? OnSpeechStopped;
	event Action<string>? OnAssistantTextDelta;
	event Action? OnAssistantTextDone;
	event Action<string>? OnAssistantAudioTranscriptDelta;
	event Action? OnAssistantAudioTranscriptDone;

	// Tool/function response
	event Action<string, string, string>? OnFunctionCallResponse; // functionName, answer, consoleCode

	Task StartAsync(CancellationToken cancellationToken);
}
