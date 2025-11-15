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

// Define command line option to override memory clearing
//var clearSessionOpt = new Option<bool?>("--clear-session", "Override ConversationMemory:ClearSessionOnStart (true/false)");

Option<bool> clearSessionOpt = new("--clear-session")
{
	Description = "Override ConversationMemory:ClearSessionOnStart (true/false)"
};

var rootCmd = new RootCommand { clearSessionOpt };
var parseResult = rootCmd.Parse(args);
var clearOverride = parseResult.GetValue(clearSessionOpt);

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

// Optional clearing of conversation memory at startup
try
{
	var memOpts = app.Services.GetRequiredService<IOptions<ConversationMemoryOptions>>().Value;
	if (memOpts.Enabled && memOpts.ClearSessionOnStart)
	{
		var memClient = app.Services.GetRequiredService<ConversationMemoryClient>();
		await memClient.ClearSessionAsync();
		logger.LogInformation("Conversation memory cleared for session {SessionId}", memOpts.SessionId);
	}
	else if (clearOverride)
	{
		logger.LogInformation("ClearSessionOnStart overridden to {Value} via command line", clearOverride);
	}
}
catch (Exception ex)
{
	logger.LogWarning(ex, "Failed to clear conversation memory at startup");
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