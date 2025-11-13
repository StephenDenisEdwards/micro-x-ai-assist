using System.Text;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ResponsePromptPackBuilder : IPromptPackBuilder
{
	private readonly ConversationMemoryClient _memory;
	public ResponsePromptPackBuilder(ConversationMemoryClient memory) => _memory = memory;

	public async Task<PromptPack> BuildAsync(string newActText, double nowMs)
	{
		var finals = await _memory.GetRecentFinalsAsync(nowMs);
		var acts = await _memory.GetRelatedActsAsync(newActText, nowMs);
		var pairs = new List<(ConversationItem, ConversationItem?)>();
		foreach (var a in acts)
		{
			var ans = await _memory.GetLatestAnswerForActAsync(a.Id);
			pairs.Add((a, ans));
		}
		var trimmed = pairs.Take(3).ToList();
		var open = await _memory.GetOpenActsAsync(nowMs);
		var systemPrompt = @"You are an answer engine.
- Answer in 1-3 sentences.
- Use concise, technically precise language.
- Do not ask follow-up questions.
";
		var sb = new StringBuilder();
		sb.AppendLine("recent_finals:");
		foreach (var f in finals) sb.AppendLine($"- [{f.Speaker} {Fmt(f.T0)}] {Trunc(f.Text, 180)}");
		sb.AppendLine();
		sb.AppendLine("recent_acts:");
		foreach (var (act, ans) in trimmed)
		{
			var prefix = act.Text.StartsWith("IMP", StringComparison.OrdinalIgnoreCase) ? "IMP" : "Q";
			var ansStr = ans is null ? "(no answer)" : $"{ans.Speaker}: {Trunc(ans.Text, 180)}";
			sb.AppendLine($"- {prefix}: \"{Trunc(act.Text, 200)}\" A: {ansStr}");
		}
		if (open.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("open_items:");
			foreach (var o in open) sb.AppendLine($"- IMP: \"{Trunc(o.Text, 180)}\"");
		}
		sb.AppendLine();
		sb.AppendLine("question:");
		sb.AppendLine($"\"{newActText}\"");

		return new PromptPack
		{
			RecentFinals = finals,
			RecentActs = trimmed,
			OpenActs = open,
			NewActText = newActText,
			SystemPrompt = systemPrompt,
			AssembledPrompt = sb.ToString()
		};
	}

	private static string Trunc(string t, int m) => t.Length <= m ? t : t.Substring(0, m) + "…";
	private static string Fmt(double ms) { var ts = TimeSpan.FromMilliseconds(ms); return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"; }
}

