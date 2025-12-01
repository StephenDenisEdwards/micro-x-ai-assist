using System;
using System.Text;

public sealed class ConsoleRealtimeSink : IRealtimeSink
{
	private readonly StringBuilder _assistantBuffer = new StringBuilder();

	public void OnConnected() => ConsoleSplitUi.AppendOutput("Realtime connected");
	public void OnReady() => ConsoleSplitUi.AppendOutput("Realtime ready");
	public void OnDisconnected() => ConsoleSplitUi.AppendOutput("Realtime disconnected");

	public void OnInfo(string message) => ConsoleSplitUi.AppendOutput(message);
	public void OnWarning(string message) => ConsoleSplitUi.AppendOutput($"[warn] {message}");
	public void OnDebug(string message) => ConsoleSplitUi.AppendOutput($"[debug] {message}");
	public void OnError(Exception ex) => ConsoleSplitUi.AppendOutput($"[error] {ex}");

	public void OnUserTranscript(string text)
	{
		ConsoleSplitUi.AppendOutput($"You: {text}");
	}

	public void OnAssistantTextDelta(string delta)
	{
		_assistantBuffer.Append(delta);
	}

	public void OnAssistantTextDone()
	{
		if (_assistantBuffer.Length == 0) return;
		var text = _assistantBuffer.ToString();
		_assistantBuffer.Clear();

		ConsoleSplitUi.AppendOutput("Assistant:");
		ConsoleSplitUi.AppendOutput(text);
	}

	public void OnFunctionCallResponse(string functionName, string answer, string code)
	{
		if (!string.IsNullOrWhiteSpace(answer))
		{
			ConsoleSplitUi.AppendOutput("Assistant (function):");
			ConsoleSplitUi.AppendOutput(answer.Trim());
		}
		if (!string.IsNullOrWhiteSpace(code))
		{
			ConsoleSplitUi.AppendOutput("Code:");
			ConsoleSplitUi.AppendOutput(code.Trim());
		}
	}
}
