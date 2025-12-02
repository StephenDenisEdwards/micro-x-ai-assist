// Placeholder implementation for web apps. You can wire these methods to broadcast via SignalR hubs.
using System;

public sealed class SignalRRealtimeSink : IRealtimeSink
{
	public void OnConnected() { }
	public void OnReady() { }
	public void OnDisconnected() { }
	public void OnInfo(string message) { }
	public void OnWarning(string message) { }
	public void OnDebug(string message) { }
	public void OnError(Exception ex) { }
	public void OnUserTranscript(string text) { }
	public void OnAssistantTextDelta(string delta) { }
	public void OnAssistantTextDone() { }
	public void OnFunctionCallResponse(string functionName, string answer, string code) { }
}
