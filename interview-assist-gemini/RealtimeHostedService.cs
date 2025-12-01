using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class RealtimeHostedService : BackgroundService
{
	private readonly IRealtimeApi _api;
	private readonly IRealtimeSink _sink;
	private readonly ILogger<RealtimeHostedService> _logger;

	public RealtimeHostedService(IRealtimeApi api, IRealtimeSink sink, ILogger<RealtimeHostedService> logger)
	{
		_api = api;
		_sink = sink;
		_logger = logger;

		_api.OnConnected += () => _sink.OnConnected();
		_api.OnReady += () => _sink.OnReady();
		_api.OnDisconnected += () => _sink.OnDisconnected();

		_api.OnInfo += msg => _sink.OnInfo(msg);
		_api.OnWarning += msg => _sink.OnWarning(msg);
		_api.OnDebug += msg => _sink.OnDebug(msg);
		_api.OnError += ex => _sink.OnError(ex);

		_api.OnUserTranscript += t => _sink.OnUserTranscript(t);
		_api.OnAssistantTextDelta += d => _sink.OnAssistantTextDelta(d);
		_api.OnAssistantTextDone += () => _sink.OnAssistantTextDone();
		_api.OnAssistantAudioTranscriptDelta += d => _logger.LogTrace("Assistant audio transcript: {Delta}", d);
		_api.OnAssistantAudioTranscriptDone += () => _logger.LogTrace("Assistant audio transcript done");
		_api.OnFunctionCallResponse += (fn, answer, code) => _sink.OnFunctionCallResponse(fn, answer, code);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await _api.StartAsync(stoppingToken);
		}
		catch (OperationCanceledException)
		{
			// normal during shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "RealtimeHostedService failed");
		}
	}
}
