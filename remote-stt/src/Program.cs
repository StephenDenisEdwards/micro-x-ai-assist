using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AiAssistLibrary.Settings;
using AiAssistLibrary.Services;

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

// Options
builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection("Audio"));
builder.Services.Configure<SpeechOptions>(builder.Configuration.GetSection("Speech"));

// Services
builder.Services.AddSingleton<AudioDeviceSelector>();
builder.Services.AddSingleton<LoopbackSource>();
builder.Services.AddSingleton<MicrophoneSource>();

// Resampler and SpeechPush are stateful to a pipeline; register as transient
builder.Services.AddTransient<AudioResampler>();
builder.Services.AddTransient<SpeechPushClient>();

builder.Services.AddHostedService<CapturePump>();
builder.Services.AddHostedService<MicCapturePump>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Validate speech key from configuration (User Secrets) with env var fallback
var speechOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SpeechOptions>>().Value;
var speechKey = string.IsNullOrWhiteSpace(speechOpts.Key)
 ? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
 : speechOpts.Key;

if (string.IsNullOrWhiteSpace(speechKey))
{
	logger.LogError(
		"Speech key missing. Set 'Speech:Key' via User Secrets or set AZURE_SPEECH_KEY environment variable.");
	return;
}

var audioOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AudioOptions>>().Value;
if (audioOpts.EnableHeadphoneReminder)
{
	logger.LogInformation(
		"Tip: Use headphones and route Teams 'Speakers' to the selected device to avoid local voice bleed.");
}

await app.RunAsync();

