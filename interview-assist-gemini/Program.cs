using GeminiLiveConsole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Program
{
	static async Task Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Load appsettings and user-secrets
		builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
		builder.Configuration.AddUserSecrets<Program>();

		// Logging
		builder.Logging.ClearProviders();
		builder.Logging.AddConsole();

		// Services
		builder.Services.AddSingleton<IAudioCaptureService>(_ => 
			new AudioCaptureService(24000, AudioInputSource.Loopback));

		// Realtime sink selection
		builder.Services.AddSingleton<IRealtimeSink>(sp =>
		{
			var cfg = sp.GetRequiredService<IConfiguration>();
			var which = cfg["Realtime:Sink"] ?? (Environment.UserInteractive ? "console" : "logger");
			bool twoCol = false;
			bool.TryParse(cfg["UI:TwoColumnCode"], out twoCol);
			return which.ToLowerInvariant() switch
			{
				"console" => new ConsoleRealtimeSink(twoCol),
				"logger" => new LoggerRealtimeSink(sp.GetRequiredService<ILogger<LoggerRealtimeSink>>()),
				"signalr" => new SignalRRealtimeSink(),
				_ => new ConsoleRealtimeSink(twoCol)
			};
		});

		// Choose which realtime API to use via config env (default API2)
		builder.Services.AddSingleton<IRealtimeApi>(sp =>
		{
			var cfg = sp.GetRequiredService<IConfiguration>();
			var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? cfg["OpenAI:ApiKey"];
			if (string.IsNullOrWhiteSpace(apiKey))
				throw new InvalidOperationException("OPENAI_API_KEY missing. Set env var or OpenAI:ApiKey in user secrets.");
			var which = cfg["Realtime:Implementation"] ?? "api2"; // api, api2, api3
			var audio = sp.GetRequiredService<IAudioCaptureService>();
			return which.ToLowerInvariant() switch
			{
				"api3" => new OpenAIRealtimeAPI3(audio, apiKey),
				"api" => new OpenAIRealtimeAPI(audio, apiKey),
				_ => new OpenAIRealtimeAPI2(audio, apiKey)
			};
		});
		builder.Services.AddHostedService<RealtimeHostedService>();
		builder.Services.AddHostedService<KeyboardInputHostedService>();

		var app = builder.Build();
		var logger = app.Services.GetRequiredService<ILogger<Program>>();
		logger.LogInformation("Starting host. Press Ctrl+C to exit.");
		await app.RunAsync();
		logger.LogInformation("Host stopped.");
	}
}
