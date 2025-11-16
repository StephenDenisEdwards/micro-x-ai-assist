using AiAssistLibrary.ConversationMemory; // for ConversationMemoryOptions / Client
using AiAssistLibrary.Extensions;
using AiAssistLibrary.LLM;
using AiAssistLibrary.Services;
using AiAssistLibrary.Settings;
using AudioCapture.Services;
using AudioCapture.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.CommandLine;
using System.Linq;

// Define command line options
Option<bool> clearSessionOpt = new("--clear-session")
{
	Description = "Override ConversationMemory:ClearSessionOnStart (true/false)"
};
Option<bool> dumpMemoryOpt = new("--dump-memory")
{
	Description = "Dump all conversation memory items for the current session to console"
};
Option<string?> dumpKindOpt = new("--dump-kind")
{
	Description = "Optional kind filter for dump (final | act | answer)"
};

var rootCmd = new RootCommand { clearSessionOpt, dumpMemoryOpt, dumpKindOpt };
var parseResult = rootCmd.Parse(args);
var clearOverride = parseResult.GetValue(clearSessionOpt);
var dumpMemory = parseResult.GetValue(dumpMemoryOpt);
var dumpKind = parseResult.GetValue(dumpKindOpt);

var builder = Host.CreateApplicationBuilder(args);

// Ensure User Secrets are loaded regardless of environment
builder.Configuration.AddUserSecrets<Program>();

// Serilog
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddHttpClient();

// Options
builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection("Audio"));
builder.Services.Configure<SpeechOptions>(builder.Configuration.GetSection("Speech"));
builder.Services.Configure<QuestionDetectionOptions>(builder.Configuration.GetSection("QuestionDetection"));

// Conversation memory (binds from configuration; generates a SessionId if missing)
builder.Services.AddConversationMemory(o =>
{
	builder.Configuration.GetSection("ConversationMemory").Bind(o);
	if (string.IsNullOrWhiteSpace(o.SessionId))
	{
		o.SessionId = $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
	}
	// Apply command line override if provided
	if (clearOverride)
	{
		o.ClearSessionOnStart = clearOverride;
	}
});

builder.Services.AddSingleton<IConversationMemoryReader>(sp =>
    new ConversationMemoryClientReaderAdapter(sp.GetRequiredService<ConversationMemoryClient>()));

// AI answering (LLM + pipeline) using config + env fallbacks
builder.Services.AddAnswering(builder.Configuration, opts =>
{
	// Allow selecting API mode from config/env
	var modeStr = builder.Configuration["OpenAI:Mode"];
	if (!string.IsNullOrWhiteSpace(modeStr) && Enum.TryParse<LlmApiMode>(modeStr, true, out var parsed))
	{
		opts.Mode = parsed;
	}
});

// Select ResponsePromptPackBuilder in Program setup (can override default mapping)
var promptPref = builder.Configuration["OpenAI:PromptBuilder"]; // "Chat" or "Responses"
if (!string.IsNullOrWhiteSpace(promptPref) && promptPref.Equals("Chat", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IPromptPackBuilder, ChatPromptPackBuilder>();
}
else if (!string.IsNullOrWhiteSpace(promptPref) && promptPref.Equals("Responses", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton<IPromptPackBuilder, ResponsePromptPackBuilder>();
}
else
{
	// Default: map to API mode
	var modeStr = builder.Configuration["OpenAI:Mode"] ?? "Responses";
	if (Enum.TryParse<LlmApiMode>(modeStr, true, out var mode) && mode == LlmApiMode.Chat)
		builder.Services.AddSingleton<IPromptPackBuilder, ChatPromptPackBuilder>();
	else
		builder.Services.AddSingleton<IPromptPackBuilder, ResponsePromptPackBuilder>();
}

// Audio capture services
builder.Services.AddSingleton<AudioDeviceSelector>();
builder.Services.AddSingleton<LoopbackSource>();
builder.Services.AddSingleton<MicrophoneSource>();

// Resampler and SpeechPush are stateful to a pipeline; register as transient
builder.Services.AddTransient<AudioResampler>();
builder.Services.AddTransient<SpeechPushClient>();

// Hosted pumps
builder.Services.AddHostedService<LoopbackAudioPump>();
builder.Services.AddHostedService<MicrophoneAudioPump>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

ConversationMemoryOptions? memOpts = null;
ConversationMemoryClient? memClient = null;
try
{
	memOpts = app.Services.GetRequiredService<IOptions<ConversationMemoryOptions>>().Value;
	memClient = app.Services.GetRequiredService<ConversationMemoryClient>();
}
catch (Exception ex)
{
	logger.LogWarning(ex, "Conversation memory services not available");
}

// Optional clearing of conversation memory at startup
try
{
	// If dumping memory, do not clear anything
	if (!dumpMemory && memOpts?.Enabled == true && memOpts.ClearSessionOnStart && memClient is not null)
	{
		await memClient.ClearSessionAsync();
		logger.LogInformation("Conversation memory cleared for session {SessionId}", memOpts.SessionId);
	}
	else if (clearOverride && !dumpMemory)
	{
		logger.LogInformation("ClearSessionOnStart overridden to {Value} via command line", clearOverride);
	}
}
catch (Exception ex)
{
	logger.LogWarning(ex, "Failed to clear conversation memory at startup");
}

// Dump memory if requested and exit without starting app
if (dumpMemory)
{
	if (memOpts?.Enabled == true && memClient is not null)
	{
		try
		{
			var items = await memClient.GetAllSessionItemsAsync(dumpKind);
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"=== Conversation Memory Dump (session {memOpts.SessionId}) count={items.Count} kind={(dumpKind ?? "*")}" );
			Console.ResetColor();
			if (items.Count == 0)
			{
				Console.WriteLine("No items.");
			}
			else
			{
				foreach (var item in items.OrderBy(i => i.T0))
				{
					var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)item.T0).ToString("HH:mm:ss.fff");
					Console.ForegroundColor = item.Kind switch
					{
						"act" => ConsoleColor.White,
						"answer" => ConsoleColor.Yellow,
						"final" => ConsoleColor.Green,
						_ => ConsoleColor.Gray
					};
					Console.WriteLine($"[{ts}] kind={item.Kind} speaker={item.Speaker ?? "?"} id={item.Id} parent={item.ParentActId ?? "-"}");
					Console.ResetColor();
					Console.WriteLine(item.Text);
					Console.WriteLine();
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed dumping conversation memory");
		}
	}
	else
	{
		logger.LogInformation("Conversation memory not enabled or unavailable; cannot dump.");
	}
	return;
}

// Validate speech key from configuration (User Secrets) with env var fallback
var speechOpts = app.Services.GetRequiredService<IOptions<SpeechOptions>>().Value;
var speechKey = string.IsNullOrWhiteSpace(speechOpts.Key)
	? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
	: speechOpts.Key;

if (string.IsNullOrWhiteSpace(speechKey))
{
	logger.LogError(
		"Speech key missing. Set 'Speech:Key' via User Secrets or set AZURE_SPEECH_KEY environment variable.");
	return;
}

var audioOpts = app.Services.GetRequiredService<IOptions<AudioOptions>>().Value;
if (audioOpts.EnableHeadphoneReminder)
	logger.LogInformation(
		"Tip: Use headphones to avoid local voice bleed into loopback capture.");

await app.RunAsync();