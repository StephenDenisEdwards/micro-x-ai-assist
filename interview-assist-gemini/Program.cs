using GeminiLiveConsole;
using Microsoft.Extensions.Configuration;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading;

class Program
{
	static async Task Main(string[] args)
	{
		// Root command shown in --help
		var rootCommand = new RootCommand("Gemini Live Interview Assist console client");

		// System.CommandLine 2.0.0 style options
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

		// Async action for the root command (2.0.0 uses SetAction instead of SetHandler)
		rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
		{
			var sourceValue = parseResult.GetValue(sourceOption) ?? "mic";
			var modelValue = parseResult.GetValue(modelOption) ?? "gemini-2.0-flash-exp";

			// Link System.CommandLine cancellation (Ctrl+C) with our own CTS
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var token = cts.Token;

			await RunAsync(sourceValue, modelValue, token);
		});

		// Parse + invoke (2.0.0 pattern)
		var parseResult = rootCommand.Parse(args);
		await parseResult.InvokeAsync();
	}

	private static async Task RunAsync(string sourceValue, string modelValue, CancellationToken token)
	{
		var selectedSource = MapSource(sourceValue.Trim());
		var model = string.IsNullOrWhiteSpace(modelValue)
			? "gemini-2.0-flash-exp"
			: modelValue.Trim();

		var configBuilder = new ConfigurationBuilder();
		configBuilder.AddUserSecrets<Program>();
		var configuration = configBuilder.Build();

		var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
					 ?? configuration["GoogleGemini:ApiKey"];

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

		// Render assistant output as a single streaming line
		var assistantLineStarted = false;
		manager.OnAssistantResponsePart += part =>
		{
			if (!assistantLineStarted)
			{
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.Write("Assistant: ");
				assistantLineStarted = true;
			}
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write(part);
			Console.ResetColor();
			lastTranscriptionLength = 0;
		};

		manager.OnAssistantTurnComplete += () =>
		{
			if (assistantLineStarted)
			{
				Console.WriteLine();
				assistantLineStarted = false;
			}
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

			if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Quitting...");
				break;
			}

			if (line.Equals("/stop", StringComparison.OrdinalIgnoreCase))
			{
				manager.StopAudio();
				Console.WriteLine();
				Console.WriteLine("Audio capture stopped.");
				continue;
			}

			if (line.Equals("/start", StringComparison.OrdinalIgnoreCase))
			{
				manager.StartAudio();
				Console.WriteLine();
				Console.WriteLine($"Audio capture started. Source = {manager.CurrentAudioSource}.");
				continue;
			}

			if (line.Equals("/end", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine();
				Console.WriteLine("Sending audioStreamEnd...");
				await manager.SendAudioStreamEndAsync(token);
				continue;
			}

			if (line.Equals("/mic", StringComparison.OrdinalIgnoreCase))
			{
				manager.UseMicrophone();
				transcriptionPrefix = "You: ";
				Console.WriteLine();
				Console.WriteLine("Switched audio source to Microphone.");
				continue;
			}

			if (line.Equals("/sys", StringComparison.OrdinalIgnoreCase))
			{
				manager.UseSystemAudio();
				transcriptionPrefix = "System: ";
				Console.WriteLine();
				Console.WriteLine("Switched audio source to System (loopback).");
				continue;
			}
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
}
