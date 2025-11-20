using GeminiLiveConsole;

var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "YOUR_API_KEY"; // Replace or set env var.
var config = new GeminiLiveConfig
{
    ApiKey = apiKey,
    Model = "gemini-2.0-flash-exp" // Placeholder
};

var systemInstruction = @"You are a dedicated Conversation Monitor and Assistant.
1. Listen: Monitor the user's audio stream.
2. Analyze: Detect if the user asks a QUESTION or issues an IMPERATIVE COMMAND.
3. Respond: - If a Question is detected, formulate a concise, helpful answer.
            - If an Imperative is detected, formulate a confirmation or simulated execution response.
4. Report: Immediately return a function call 'report_intent' with text, type (QUESTION|IMPERATIVE), answer.
Ignore unrelated chatter.";

await using var manager = new LiveSessionManager(config);
manager.OnTranscript += t => Console.WriteLine($"[Transcript] {t}");
manager.OnIntent += i => Console.WriteLine($"[Intent] {i.Type} | {i.Text} => {i.Answer}");

Console.WriteLine("Starting live session. Press ENTER to stop.");
await manager.StartAsync(systemInstruction);
Console.ReadLine();
Console.WriteLine("Stopping...");
await manager.StopAsync();
