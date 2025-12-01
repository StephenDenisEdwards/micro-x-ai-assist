using System;
using System.Collections.Generic;
using System.Linq;

internal static class ConsoleSplitUi
{
	private static readonly object _lock = new();
	private static readonly List<string> _outputLines = new();
	private static string _inputLine = string.Empty;
	private static int _lastWidth = -1;
	private static int _lastHeight = -1;

	public static void AppendOutput(string text)
	{
		if (text is null) return;
		lock (_lock)
		{
			foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
			{
				_outputLines.Add(line);
			}
			TrimOutputCapacity();
			RenderLocked();
		}
	}

	public static void SetInput(string input)
	{
		lock (_lock)
		{
			_inputLine = input ?? string.Empty;
			RenderLocked();
		}
	}

	public static void ClearInput()
	{
		lock (_lock)
		{
			_inputLine = string.Empty;
			RenderLocked();
		}
	}

	private static void TrimOutputCapacity()
	{
		const int maxLines = 2000;
		if (_outputLines.Count > maxLines)
		{
			_outputLines.RemoveRange(0, _outputLines.Count - maxLines);
		}
	}

	private static void EnsureConsole()
	{
		if (!Console.IsOutputRedirected)
		{
			try { Console.CursorVisible = false; } catch { }
		}
	}

	private static void RenderLocked()
	{
		EnsureConsole();

		int width = Math.Max(20, SafeGetWidth());
		int height = Math.Max(10, SafeGetHeight());

		int topHeight = Math.Max(3, (int)Math.Round(height * 0.75, MidpointRounding.AwayFromZero)) - 1;
		int bottomHeight = height - topHeight - 1;
		int dividerRow = topHeight;

		bool sizeChanged = width != _lastWidth || height != _lastHeight;
		_lastWidth = width; _lastHeight = height;

		if (sizeChanged)
		{
			try { Console.Clear(); } catch { }
		}

		DrawOutputRegion(0, 0, width, topHeight, _outputLines);
		DrawDivider(dividerRow, width);
		DrawInputRegion(dividerRow + 1, width, bottomHeight, _inputLine);

		int caretCol = Math.Min(Math.Max(0, width - 3), 2 + _inputLine.Length);
		SafeSetCursorPosition(caretCol, dividerRow + 1);
		try { Console.CursorVisible = true; } catch { }
	}

	private static void DrawOutputRegion(int left, int top, int width, int height, List<string> lines)
	{
		var span = Math.Max(0, height);
		var tail = TailLinesForHeight(lines, span);

		for (int i = 0; i < height; i++)
		{
			SafeSetCursorPosition(left, top + i);
			string toWrite = (i < tail.Count) ? tail[i] : string.Empty;
			WritePadded(toWrite, width);
		}
	}

	private static void DrawInputRegion(int row, int width, int height, string input)
	{
		for (int i = 0; i < height; i++)
		{
			SafeSetCursorPosition(0, row + i);
			if (i == 0)
			{
				string prompt = $"> {input}";
				WritePadded(prompt, width);
			}
			else
			{
				WritePadded(string.Empty, width);
			}
		}
	}

	private static void DrawDivider(int row, int width)
	{
		SafeSetCursorPosition(0, row);
		var line = new string('?', Math.Max(1, width));
		var prev = Console.ForegroundColor;
		try { Console.ForegroundColor = ConsoleColor.DarkGray; } catch { }
		WritePadded(line, width);
		try { Console.ForegroundColor = prev; } catch { }
	}

	private static void WritePadded(string text, int width)
	{
		if (text.Length >= width)
		{
			var slice = text.Length > width ? text.Substring(0, width) : text;
			try { Console.Write(slice); } catch { }
			return;
		}
		try { Console.Write(text); } catch { }
		int pad = width - text.Length;
		if (pad > 0)
		{
			try { Console.Write(new string(' ', pad)); } catch { }
		}
	}

	private static List<string> TailLinesForHeight(List<string> source, int height)
	{
		int count = Math.Min(height, source.Count);
		return source.Skip(Math.Max(0, source.Count - count)).ToList();
	}

	private static void SafeSetCursorPosition(int left, int top)
	{
		try
		{
			left = Math.Max(0, Math.Min(SafeGetWidth() - 1, left));
			top = Math.Max(0, Math.Min(SafeGetHeight() - 1, top));
			Console.SetCursorPosition(left, top);
		}
		catch { }
	}

	private static int SafeGetWidth()
	{
		try { return Console.WindowWidth; } catch { return 120; }
	}

	private static int SafeGetHeight()
	{
		try { return Console.WindowHeight; } catch { return 40; }
	}
}
