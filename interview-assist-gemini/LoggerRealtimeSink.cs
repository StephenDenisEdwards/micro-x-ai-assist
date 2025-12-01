using Microsoft.Extensions.Logging;

public sealed class LoggerRealtimeSink : IRealtimeSink
{
	private readonly ILogger<LoggerRealtimeSink> _logger;
	private readonly System.Text.StringBuilder _buffer = new();

	public LoggerRealtimeSink(ILogger<LoggerRealtimeSink> logger)
	{
		_logger = logger;
	}

	public void OnConnected() => _logger.LogInformation("Realtime connected");
	public void OnReady() => _logger.LogInformation("Realtime ready");
	public void OnDisconnected() => _logger.LogInformation("Realtime disconnected");

	public void OnInfo(string message) => _logger.LogInformation("{Message}", message);
	public void OnWarning(string message) => _logger.LogWarning("{Message}", message);
	public void OnDebug(string message) => _logger.LogDebug("{Message}", message);
	public void OnError(System.Exception ex) => _logger.LogError(ex, "Realtime error");

	public void OnUserTranscript(string text) => _logger.LogInformation("You: {Text}", text);

	public void OnAssistantTextDelta(string delta) => _buffer.Append(delta);

	public void OnAssistantTextDone()
	{
		if (_buffer.Length == 0) return;
		_logger.LogInformation("Assistant:\n{Text}", _buffer.ToString());
		_buffer.Clear();
	}

	public void OnFunctionCallResponse(string functionName, string answer, string code)
	{
		_logger.LogInformation("Function {Function} responded.", functionName);
		if (!string.IsNullOrWhiteSpace(answer))
			_logger.LogInformation("Answer:\n{Answer}", answer);
		if (!string.IsNullOrWhiteSpace(code))
			_logger.LogInformation("Code:\n{Code}", code);
	}
}
