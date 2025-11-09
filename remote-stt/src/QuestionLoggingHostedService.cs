using AiAssistLibrary.Services;
using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RemoteStt;

public sealed class QuestionLoggingHostedService : IHostedService
{
 private readonly ILogger<QuestionLoggingHostedService> _log;
 private readonly IServiceProvider _sp;
 private SpeechPushClient? _loopClient;
 private SpeechPushClient? _micClient;

 public QuestionLoggingHostedService(ILogger<QuestionLoggingHostedService> log, IServiceProvider sp)
 {
 _log = log;
 _sp = sp;
 }

 public Task StartAsync(CancellationToken cancellationToken)
 {
 _log.LogInformation("QuestionLoggingHostedService active. Detected questions will appear in Speech logs.");
 // Attach to transient SpeechPushClients created by pumps via DI scopes
 // Here we only demonstrate logging when events fire; pumps own lifecycle.
 return Task.CompletedTask;
 }

 public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
