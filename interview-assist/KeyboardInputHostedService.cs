using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class KeyboardInputHostedService : BackgroundService
{
	private readonly IRealtimeApi _api;
	private readonly ILogger<KeyboardInputHostedService> _logger;
	private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public KeyboardInputHostedService(IRealtimeApi api, ILogger<KeyboardInputHostedService> logger)
	{
		_api = api;
		_logger = logger;
		_api.OnReady += () => _readyTcs.TrySetResult();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try { await _readyTcs.Task.WaitAsync(stoppingToken); }
		catch (OperationCanceledException) { return; }

		_logger.LogInformation("Keyboard active. Type and press Enter to send. Prefix with ! to interrupt. Ctrl+C to exit. Use PageUp/PageDown or Ctrl+Up/Down to scroll history.");

		var buffer = string.Empty;
		ConsoleSplitUi.SetInput(buffer);

		while (!stoppingToken.IsCancellationRequested)
		{
			ConsoleKeyInfo key;
			try
			{
				key = Console.ReadKey(intercept: true);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Console.ReadKey failed");
				await Task.Delay(100, stoppingToken);
				continue;
			}

			// Scroll controls
			if (key.Key == ConsoleKey.PageUp)
			{
				ConsoleSplitUi.ScrollPageUp();
				continue;
			}
			if (key.Key == ConsoleKey.PageDown)
			{
				ConsoleSplitUi.ScrollPageDown();
				continue;
			}
			if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.UpArrow)
			{
				ConsoleSplitUi.ScrollUpLines(1);
				continue;
			}
			if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.DownArrow)
			{
				ConsoleSplitUi.ScrollDownLines(1);
				continue;
			}
			if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.Home)
			{
				// Jump to oldest
				ConsoleSplitUi.ScrollUpLines(int.MaxValue);
				continue;
			}
			if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.End)
			{
				ConsoleSplitUi.ScrollToBottom();
				continue;
			}

			if (key.Key == ConsoleKey.Enter)
			{
				var text = buffer.Trim();
				if (!string.IsNullOrWhiteSpace(text))
				{
					var interrupt = text.StartsWith("!");
					if (interrupt) text = text.TrimStart('!').TrimStart();
					try
					{
						await _api.SendTextAsync(text, requestResponse: true, interrupt: interrupt);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						_logger.LogError(ex, "Failed to send input");
					}
				}
				buffer = string.Empty;
				ConsoleSplitUi.ClearInput();
				continue;
			}

			if (key.Key == ConsoleKey.Backspace)
			{
				if (buffer.Length > 0)
				{
					buffer = buffer[..^1];
					ConsoleSplitUi.SetInput(buffer);
				}
				continue;
			}

			if (!char.IsControl(key.KeyChar))
			{
				buffer += key.KeyChar;
				ConsoleSplitUi.SetInput(buffer);
			}
		}
	}
}
