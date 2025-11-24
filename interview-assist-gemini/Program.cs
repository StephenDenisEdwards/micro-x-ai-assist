using GeminiLiveConsole;
using GeminiLiveConsole.Models; // Added for IntentType
using Microsoft.Extensions.Configuration;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
	static async Task Main(string[] args)
	{
		var rootCommand = new RootCommand("Gemini Live Interview Assist console client");

		var sourceOption = new Option<string>("--source", "-s")
		{
			Description = "Initial audio source: mic | sys | loopback",
			DefaultValueFactory = _ => "mic"
		};

		var modelOption = new Option<string>("--model")
		{
			Description = "Gemini model ID to use (e.g. gemini-2.0-flash-exp)",
			DefaultValueFactory = _ => "gemini-2.0-flash-exp"
		};

		rootCommand.Options.Add(sourceOption);
		rootCommand.Options.Add(modelOption);

		rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
		{
			var sourceValue = parseResult.GetValue(sourceOption) ?? "mic";
			var modelValue = parseResult.GetValue(modelOption) ?? "gemini-2.0-flash-exp";
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var token = cts.Token;
			await RunAsync(sourceValue, modelValue, token);
		});

		var parseResult = rootCommand.Parse(args);
		await parseResult.InvokeAsync();
	}

	private static async Task RunAsync(string sourceValue, string modelValue, CancellationToken token)
	{
		var selectedSource = MapSource(sourceValue.Trim());
		var model = string.IsNullOrWhiteSpace(modelValue) ? "gemini-2.0-flash-exp" : modelValue.Trim();

		var configBuilder = new ConfigurationBuilder().AddUserSecrets<Program>();
		var configuration = configBuilder.Build();
		var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? configuration["GoogleGemini:ApiKey"];
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			Console.WriteLine("ERROR: Please set GEMINI_API_KEY env var or GoogleGemini:ApiKey in user secrets.");
			return;
		}

		Console.WriteLine("Gemini Live Interview Assist\n");
		Console.WriteLine("Commands: /quit, /stop, /start, /end (signal end of audio stream), /mic, /sys (switch source)\nPress ENTER to start after connection.");
		Console.WriteLine($"Initial audio source: {selectedSource} (override via --source mic|sys|loopback or -s)");
		Console.WriteLine($"Model: {model} (override via --model <modelId>)\n");

		var manager = new LiveSessionManager(apiKey, model: model, selectedSource);

		var lastTranscriptionLength = 0;
		var transcriptionPrefix = selectedSource == AudioInputSource.Microphone ? "You: " : "System: ";

		// Buffer for assistant markdown during a turn
		var assistantBuffer = new StringBuilder();
		var assistantStreamingActive = false;

		manager.OnInputTranscriptionUpdate += t =>
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			var line = transcriptionPrefix + t;
			int pad = Math.Max(0, lastTranscriptionLength - line.Length);
			Console.Write('\r');
			Console.Write(line + new string(' ', pad));
			lastTranscriptionLength = line.Length;
			Console.ResetColor();
		};

		// Accumulate deltas instead of printing raw markdown fragments
		manager.OnAssistantResponsePart += part =>
		{
			assistantBuffer.Append(part);
			assistantStreamingActive = true;
			lastTranscriptionLength = 0; // release transcription line overwrite
		};

		manager.OnAssistantTurnComplete += () =>
		{
			if (!assistantStreamingActive) return;
			Console.WriteLine();
			RenderMarkdownToConsole("Assistant", assistantBuffer.ToString());
			assistantBuffer.Clear();
			assistantStreamingActive = false;
		};

		manager.OnIntentFinal += intent =>
		{
			Console.ForegroundColor = intent.Type == IntentType.QUESTION ? ConsoleColor.Green : ConsoleColor.Yellow;
			Console.WriteLine($"Detected {intent.Type}: {intent.Text}");
			Console.ResetColor();
		};

		manager.OnError += e =>
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[Error] {e.Message}");
			Console.ResetColor();
		};

		manager.OnDisconnect += () =>
		{
			Console.WriteLine();
			Console.WriteLine("Disconnected.");
		};

		Console.WriteLine("Connecting...");
		await manager.ConnectAsync(token);
		Console.WriteLine("Connected. Press ENTER to start streaming audio.");
		Console.ReadLine();
		Console.WriteLine("Streaming audio. Type commands or speak.\n");

		while (!token.IsCancellationRequested)
		{
			var line = Console.ReadLine();
			if (line == null) continue;
			line = line.Trim();
			if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("Quitting..."); break; }
			if (line.Equals("/stop", StringComparison.OrdinalIgnoreCase)) { manager.StopAudio(); Console.WriteLine("\nAudio capture stopped."); continue; }
			if (line.Equals("/start", StringComparison.OrdinalIgnoreCase)) { manager.StartAudio(); Console.WriteLine($"\nAudio capture started. Source = {manager.CurrentAudioSource}."); continue; }
			if (line.Equals("/end", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("\nSending audioStreamEnd..."); await manager.SendAudioStreamEndAsync(token); continue; }
			if (line.Equals("/mic", StringComparison.OrdinalIgnoreCase)) { manager.UseMicrophone(); transcriptionPrefix = "You: "; Console.WriteLine("\nSwitched audio source to Microphone."); continue; }
			if (line.Equals("/sys", StringComparison.OrdinalIgnoreCase)) { manager.UseSystemAudio(); transcriptionPrefix = "System: "; Console.WriteLine("\nSwitched audio source to System (loopback)."); continue; }
		}

		await manager.DisconnectAsync();
		Console.WriteLine("Done.");
	}

	private static AudioInputSource MapSource(string raw) => raw.ToLowerInvariant() switch
	{
		"mic" or "microphone" => AudioInputSource.Microphone,
		"sys" or "system" or "loopback" => AudioInputSource.Loopback,
		_ => AudioInputSource.Microphone
	};

	// Basic markdown renderer for console with bold colouring
	private static void RenderMarkdownToConsole(string title, string markdown)
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"{title}:");
		Console.ResetColor();

		var lines = markdown.Replace("\r", string.Empty).Split('\n');
		bool inCode = false;
		string codeFenceLang = string.Empty;
		var boldRegex = new Regex(@"\*\*(.+?)\*\*");
		foreach (var rawLine in lines)
		{
			var line = rawLine;
			if (line.StartsWith("```"))
			{
				if (!inCode)
				{
					inCode = true;
					codeFenceLang = line.Length > 3 ? line[3..].Trim() : string.Empty;
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine($"--- code ({codeFenceLang}) ---");
					Console.ResetColor();
				}
				else
				{
					inCode = false;
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine("--- end code ---");
					Console.ResetColor();
				}
				continue;
			}

			if (inCode)
			{
				Console.ForegroundColor = ConsoleColor.Magenta;
				Console.WriteLine(line);
				Console.ResetColor();
				continue;
			}

			if (line.StartsWith("#"))
			{
				int level = line.TakeWhile(c => c == '#').Count();
				var text = line[level..].Trim();
				Console.ForegroundColor = level switch { 1 => ConsoleColor.Yellow, 2 => ConsoleColor.Yellow, _ => ConsoleColor.DarkYellow };
				Console.WriteLine(text.ToUpperInvariant());
				Console.ResetColor();
				continue;
			}

			if (line.StartsWith("- ") || line.StartsWith("* "))
			{
				WriteBoldProcessed("  • " + line[2..], boldRegex);
				continue;
			}

			// Numbered list (simple)
			if (char.IsDigit(line.FirstOrDefault()) && line.Contains('.') && line.IndexOf('.') < 4)
			{
				WriteBoldProcessed("  " + line, boldRegex);
				continue;
			}

			// Inline code simplification: wrap `code` with brackets
			if (line.Contains('`'))
			{
				line = Regex.Replace(line, "`([^`]+)`", m => "[" + m.Groups[1].Value + "]");
			}

			WriteBoldProcessed(line, boldRegex);
		}
	}

	private static void WriteBoldProcessed(string line, Regex boldRegex)
	{
		var matches = boldRegex.Matches(line);
		if (matches.Count == 0)
		{
			Console.WriteLine(line);
			return;
		}
		int lastIndex = 0;
		foreach (Match m in matches)
		{
			if (m.Index > lastIndex)
			{
				Console.Write(line.Substring(lastIndex, m.Index - lastIndex));
			}
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.Write(m.Groups[1].Value); // bold content
			Console.ResetColor();
			lastIndex = m.Index + m.Length;
		}
		if (lastIndex < line.Length)
			Console.Write(line.Substring(lastIndex));
		Console.WriteLine();
	}
}
