using System;
using System.Threading.Tasks;

public interface IRealtimeSink
{
	// Lifecycle
	void OnConnected();
	void OnReady();
	void OnDisconnected();

	// Diagnostics
	void OnInfo(string message);
	void OnWarning(string message);
	void OnDebug(string message);
	void OnError(Exception ex);

	// User transcript
	void OnUserTranscript(string text);

	// Assistant streaming text
	void OnAssistantTextDelta(string delta);
	void OnAssistantTextDone();

	// Function/tool response
	void OnFunctionCallResponse(string functionName, string answer, string code);
}
