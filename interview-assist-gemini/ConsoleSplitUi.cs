using System;
using System.Collections.Generic;
using System.Linq;

internal static class ConsoleSplitUi
{
	private static readonly object _lock = new();
	private static readonly List<string> _outputLines = new();
	private static readonly List<ConsoleColor?> _outputColors = new();
	private static string _inputLine = string.Empty;
	private static int _lastWidth = -1;
	private static int _lastHeight = -1;
	private static int _lastTopHeight = 0;
	private static int _scrollOffset = 0; // 0 = follow bottom; >0 scroll up

	public static void AppendOutput(string text)
	{
		AppendOutputInternal(text, null);
	}

	public static void AppendOutputWithColor(string text, ConsoleColor color)
	{
		AppendOutputInternal(text, color);
	}

	private static void AppendOutputInternal(string text, ConsoleColor? color)
	{
		if (text is null) return;
		lock (_lock)
		{
			bool atBottom = _scrollOffset == 0;
			var lines = text.Replace("\r\n", "\n").Split('\n');
			foreach (var line in lines)
			{
				_outputLines.Add(line);
				_outputColors.Add(color);
			}
			TrimOutputCapacity();
			if (atBottom) _scrollOffset = 0;
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

	public static void ScrollPageUp()
	{
		lock (_lock)
		{
			_scrollOffset = Math.Min(MaxScrollOffset(), _scrollOffset + Math.Max(1, _lastTopHeight - 1));
			RenderLocked();
		}
	}

	public static void ScrollPageDown()
	{
		lock (_lock)
		{
			_scrollOffset = Math.Max(0, _scrollOffset - Math.Max(1, _lastTopHeight - 1));
			RenderLocked();
		}
	}

	public static void ScrollUpLines(int lines)
	{
		if (lines <= 0) return;
		lock (_lock)
		{
			_scrollOffset = Math.Min(MaxScrollOffset(), _scrollOffset + lines);
			RenderLocked();
		}
	}

	public static void ScrollDownLines(int lines)
	{
		if (lines <= 0) return;
		lock (_lock)
		{
			_scrollOffset = Math.Max(0, _scrollOffset - lines);
			RenderLocked();
		}
	}

	public static void ScrollToBottom()
	{
		lock (_lock)
		{
			_scrollOffset = 0;
			RenderLocked();
		}
	}

	private static int MaxScrollOffset()
	{
		int span = Math.Max(0, _lastTopHeight);
		int total = _outputLines.Count;
		return Math.Max(0, total - span);
	}

	private static void TrimOutputCapacity()
	{
		const int maxLines = 5000;
		if (_outputLines.Count > maxLines)
		{
			int toRemove = _outputLines.Count - maxLines;
			_outputLines.RemoveRange(0, toRemove);
			_outputColors.RemoveRange(0, Math.Min(toRemove, _outputColors.Count));
			_scrollOffset = Math.Max(0, _scrollOffset - toRemove);
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

		// 90% top, 10% bottom
		int topHeight = Math.Max(3, (int)Math.Round(height * 0.90, MidpointRounding.AwayFromZero)) - 1;
		int bottomHeight = height - topHeight - 1;
		int dividerRow = topHeight;

		bool sizeChanged = width != _lastWidth || height != _lastHeight;
		_lastWidth = width; _lastHeight = height; _lastTopHeight = topHeight;

		_scrollOffset = Math.Min(_scrollOffset, MaxScrollOffset());

		if (sizeChanged)
		{
			try { Console.Clear(); } catch { }
		}

		DrawOutputRegion(0, 0, width, topHeight, _scrollOffset);
		DrawDivider(dividerRow, width);
		DrawInputRegion(dividerRow + 1, width, bottomHeight, _inputLine);

		int caretCol = Math.Min(Math.Max(0, width - 3), 2 + _inputLine.Length);
		SafeSetCursorPosition(caretCol, dividerRow + 1);
		try { Console.CursorVisible = true; } catch { }
	}

	private static void DrawOutputRegion(int left, int top, int width, int height, int scrollOffset)
	{
		int span = Math.Max(0, height);
		int total = _outputLines.Count;
		int start = Math.Max(0, total - span - scrollOffset);
		if (start > Math.Max(0, total - span)) start = Math.Max(0, total - span);

		var prev = Console.ForegroundColor;
		for (int i = 0; i < height; i++)
		{
			SafeSetCursorPosition(left, top + i);
			int idx = start + i;
			string toWrite = (idx >= 0 && idx < total) ? _outputLines[idx] : string.Empty;
			ConsoleColor? color = (idx >= 0 && idx < _outputColors.Count) ? _outputColors[idx] : null;
			if (color.HasValue)
			{
				try { Console.ForegroundColor = color.Value; } catch { }
			}
			WritePadded(toWrite, width);
			if (color.HasValue)
			{
				try { Console.ForegroundColor = prev; } catch { }
			}
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
