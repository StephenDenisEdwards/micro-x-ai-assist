using System;
using System.Text;

public sealed class ConsoleRealtimeSink : IRealtimeSink
{
	private readonly StringBuilder _assistantBuffer = new StringBuilder();
	private readonly bool _twoColumnCode;

	public ConsoleRealtimeSink(bool twoColumnCode = false)
	{
		_twoColumnCode = twoColumnCode;
	}

	public void OnConnected() => ConsoleSplitUi.AppendOutputWithColor("Realtime connected", ConsoleColor.White);
	public void OnReady() => ConsoleSplitUi.AppendOutputWithColor("Realtime ready", ConsoleColor.White);
	public void OnDisconnected() => ConsoleSplitUi.AppendOutputWithColor("Realtime disconnected", ConsoleColor.White);

	public void OnInfo(string message) => ConsoleSplitUi.AppendOutputWithColor(message, ConsoleColor.White);
	public void OnWarning(string message) => ConsoleSplitUi.AppendOutputWithColor($"[warn] {message}", ConsoleColor.Yellow);
	public void OnDebug(string message) { /* suppressed */ }
	public void OnError(Exception ex) => ConsoleSplitUi.AppendOutputWithColor($"[error] {ex}", ConsoleColor.Red);

	public void OnUserTranscript(string text)
	{
		ConsoleSplitUi.AppendOutputWithColor($"You: {text}", ConsoleColor.DarkGray);
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

		ConsoleSplitUi.AppendOutputWithColor("Assistant:", ConsoleColor.Cyan);
		ConsoleSplitUi.AppendOutputWithColor(text, ConsoleColor.Green);
	}

	public void OnFunctionCallResponse(string functionName, string answer, string code)
	{
		if (_twoColumnCode && (!string.IsNullOrWhiteSpace(answer) || !string.IsNullOrWhiteSpace(code)))
		{
			ConsoleSplitUi.AppendTwoColumns(answer?.Trim() ?? string.Empty, code?.Trim() ?? string.Empty, ConsoleColor.Green, ConsoleColor.Yellow);
			return;
		}

		if (!string.IsNullOrWhiteSpace(answer))
		{
			ConsoleSplitUi.AppendOutputWithColor("Assistant (function):", ConsoleColor.Cyan);
			ConsoleSplitUi.AppendOutputWithColor(answer.Trim(), ConsoleColor.Green);
		}
		if (!string.IsNullOrWhiteSpace(code))
		{
			ConsoleSplitUi.AppendOutputWithColor("Code:", ConsoleColor.Cyan);
			ConsoleSplitUi.AppendOutputWithColor(code.Trim(), ConsoleColor.Yellow);
		}
	}
}
