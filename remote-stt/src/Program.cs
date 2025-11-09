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
		"Tip: Use headphones and route Teams 'Speakers' to the selected device to avoid local voice bleed.");

await app.RunAsync();