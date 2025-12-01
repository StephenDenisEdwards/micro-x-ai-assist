using System;
using System.Text;

public sealed class ConsoleRealtimeSink : IRealtimeSink
{
	private readonly StringBuilder _assistantBuffer = new StringBuilder();
	private readonly object _lock = new();

	public void OnConnected() => WriteInfo("Realtime connected");
	public void OnReady() => WriteInfo("Realtime ready");
	public void OnDisconnected() => WriteInfo("Realtime disconnected");

	public void OnInfo(string message) => WriteInfo(message);
	public void OnWarning(string message) => WriteWith(ConsoleColor.Yellow, message);
	public void OnDebug(string message) => WriteWith(ConsoleColor.DarkGray, message);
	public void OnError(Exception ex) => WriteWith(ConsoleColor.Red, ex.ToString());

	public void OnUserTranscript(string text)
	{
		lock (_lock)
		{
			var prev = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine($"You: {text}");
			Console.ForegroundColor = prev;
		}
	}

	public void OnAssistantTextDelta(string delta)
	{
		lock (_lock)
		{
			_assistantBuffer.Append(delta);
		}
	}

	public void OnAssistantTextDone()
	{
		string text;
		lock (_lock)
		{
			if (_assistantBuffer.Length == 0) return;
			text = _assistantBuffer.ToString();
			_assistantBuffer.Clear();
		}
		var prev = Console.ForegroundColor;
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("Assistant:");
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine(text);
		Console.ForegroundColor = prev;
	}

	public void OnFunctionCallResponse(string functionName, string answer, string code)
	{
		var prev = Console.ForegroundColor;
		if (!string.IsNullOrWhiteSpace(answer))
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("Assistant (function):");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(answer);
		}
		if (!string.IsNullOrWhiteSpace(code))
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("Code:");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(code);
		}
		Console.ForegroundColor = prev;
	}

	private static void WriteWith(ConsoleColor color, string text)
	{
		var prev = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.WriteLine(text);
		Console.ForegroundColor = prev;
	}
	private static void WriteInfo(string text) => WriteWith(ConsoleColor.White, text);
}
