using System;
using System.Threading.Tasks;
using AiAssistLibrary.ConversationMemory;
using Microsoft.Extensions.Logging;

namespace AiAssistLibrary.LLM;

public sealed class AnswerPipeline
{
	private readonly IAnswerProvider _answer;
	private readonly ConversationMemoryClient? _memory;
	private readonly ILogger<AnswerPipeline> _log;

	public AnswerPipeline(IAnswerProvider answer, ILogger<AnswerPipeline> log, ConversationMemoryClient? memory = null)
	{
		_answer = answer;
		_memory = memory;
		_log = log;
	}

	// Handles PromptPack, sends to LLM, writes answer, and optionally upserts to memory using provided actId
	public async Task<string> AnswerAndPersistAsync(PromptPack pack, string? speakerForAnswer = "assistant", string? actId = null)
	{
		var answer = await _answer.GetAnswerAsync(pack);
		Console.ForegroundColor = ConsoleColor.DarkCyan;
		Console.WriteLine($"AI: {answer}");
		Console.ResetColor();

		if (!string.IsNullOrWhiteSpace(answer) && _memory != null && !string.IsNullOrWhiteSpace(actId))
		{
			var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			try
			{
				await _memory.UpsertAnswerAsync(speakerForAnswer ?? "assistant", answer, nowMs, nowMs, actId);
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "Failed to upsert answer for act {ActId}", actId);
			}
		}
		return answer;
	}
}
		