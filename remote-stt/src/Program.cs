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
using AiAssistLibrary.Extensions;

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
});

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