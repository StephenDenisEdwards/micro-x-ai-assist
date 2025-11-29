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
		var configBuilder = new ConfigurationBuilder().AddUserSecrets<Program>();
		var configuration = configBuilder.Build();
		var apiGeminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? configuration["GoogleGemini:ApiKey"];
		var apiOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? configuration["OpenAI:ApiKey"];

		await OpenAIRealtimeAPI.Go(apiOpenAiKey);
		//await GeminiLiveAPI.Go(apiGeminiKey);
		//await GeminiFunctionCallingExample.Start();
		//await Start(args);
	}

	static async Task Start(string[] args)
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

		manager.OnCodeExample += t =>
		{
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.WriteLine("\n--- CODE ---");
			// Basic cleaning of markdown fences for direct console output
			var cleanedCode = t.Replace("```csharp", "").Replace("```", "").Trim();
			Console.WriteLine(cleanedCode);
			Console.WriteLine("--- END CODE ---\n");
			Console.ResetColor();
		};

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

		manager.OnAssistantResponsePart += part =>
		{
			assistantBuffer.Append(part);
			assistantStreamingActive = true;
			lastTranscriptionLength = 0; // release transcription line overwrite
		};

		manager.OnAssistantTurnComplete += () =>
		{
			if (!assistantStreamingActive) return;
			Console.WriteLine(); // Newline before assistant answer
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("Assistant:");
			Console.ResetColor();
			Console.WriteLine(assistantBuffer.ToString());
			assistantBuffer.Clear();
			assistantStreamingActive = false;
		};

		manager.OnIntentFinal += intent =>
		{
			Console.WriteLine(); // Start on a new line
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"Question: {intent.Text}");
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

	private static void Manager_OnCodeExample(string obj)
	{
		throw new NotImplementedException();
	}

	private static AudioInputSource MapSource(string raw) => raw.ToLowerInvariant() switch
	{
		"mic" or "microphone" => AudioInputSource.Microphone,
		"sys" or "system" or "loopback" => AudioInputSource.Loopback,
		_ => AudioInputSource.Microphone
	};

	// This complex renderer is no longer needed with the simplified output.
	// Keeping the method stubs to avoid breaking references if any exist, but clearing the bodies.
	private static void RenderMarkdownToConsole(string title, string markdown)
	{
		// No-op
	}

	private static void WriteBoldProcessed(string line, Regex boldRegex)
	{
		// No-op
	}
}
