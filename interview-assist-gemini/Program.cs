using GeminiLiveConsole;
using Microsoft.Extensions.Configuration;
using System.Text;

class Program
{
    static async Task Main()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddUserSecrets<Program>();
        var configuration = configBuilder.Build();

        var apiKey =
            Environment.GetEnvironmentVariable("GEMINI_API_KEY") ??
            configuration["GoogleGemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("ERROR: Please set GEMINI_API_KEY env var or GoogleGemini:ApiKey in user secrets.");
            return;
        }

        Console.WriteLine("Gemini Live Interview Assist\n");
        Console.WriteLine("Commands: /quit, /stop, /start, /end (signal end of audio stream), /mic, /sys (switch source)\nPress ENTER to start after connection.");

        var manager = new LiveSessionManager(apiKey, model: "gemini-2.0-flash-exp", AudioInputSource.Microphone);

        var lastTranscriptionLength = 0;
        var transcriptionPrefix = "You: ";

        manager.OnInputTranscriptionUpdate += t =>
        {
            // Live microphone/system transcription on one line
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
            // When assistant responds, move to new line (clear current transcription line)
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Assistant: " + part);
            Console.ResetColor();
            // Reset transcription line after assistant output
            lastTranscriptionLength = 0;
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

        using var cts = new CancellationTokenSource();

        Console.WriteLine("Connecting...");
        await manager.ConnectAsync(cts.Token);
        Console.WriteLine("Connected. Press ENTER to start streaming audio.");
        Console.ReadLine();

        Console.WriteLine("Streaming audio. Type commands or speak.\n");

        // Simple command loop
        while (true)
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
                await manager.SendAudioStreamEndAsync(cts.Token);
                continue;
            }
            if (line.Equals("/mic", StringComparison.OrdinalIgnoreCase))
            {
                manager.UseMicrophone();
                Console.WriteLine();
                Console.WriteLine("Switched audio source to Microphone.");
                continue;
            }
            if (line.Equals("/sys", StringComparison.OrdinalIgnoreCase))
            {
                manager.UseSystemAudio();
                Console.WriteLine();
                Console.WriteLine("Switched audio source to System (loopback).");
                continue;
            }
        }

        await manager.DisconnectAsync();
        Console.WriteLine("Done.");
    }
}