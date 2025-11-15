using System.Text;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ResponsePromptPackBuilder : IPromptPackBuilder
{
	private readonly IConversationMemoryReader _memory;
	public ResponsePromptPackBuilder(IConversationMemoryReader memory) => _memory = memory;

	public async Task<PromptPack> BuildAsync(string fullFinal, string newActText, double nowMs)
	{
		var finals = await _memory.GetRecentFinalsAsync(nowMs);
		var adjustedFinals = EnsureFinalContainsQuestionPreamble(finals, newActText);
		var acts = await _memory.GetRelatedActsAsync(newActText, nowMs);
		var pairs = new List<(ConversationItem, ConversationItem?)>();
		foreach (var a in acts)
		{
			var ans = await _memory.GetLatestAnswerForActAsync(a.Id);
			pairs.Add((a, ans));
		}
		var trimmed = pairs.Take(3).ToList();
		var open = await _memory.GetOpenActsAsync(nowMs);
		var systemPrompt = DefaultSystemPrompt;

		var sb = new StringBuilder();
		sb.AppendLine("recent_finals:");
		foreach (var f in adjustedFinals)
		{
			sb.AppendLine($"- [{f.Speaker} {Fmt(f.T0)}] {Trunc(f.Text, 180)}");
		}

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
			RecentFinals = adjustedFinals,
			RecentActs = trimmed,
			OpenActs = open,
			NewActText = newActText,
			SystemPrompt = systemPrompt,
			AssembledPrompt = sb.ToString()
		};
	}

	public static string DefaultSystemPrompt => @"You are an answer engine.
- If in doubt, all questions relate to .NET and C# (C Sharp) 
- There will be no questions relating to C. Treat these as referring to C# (C Sharp). 
- Answer in 1-3 sentences.
- Use concise, technically precise language.
- Do not ask follow-up questions.
- If you do not know the answer, say 'I don't know.'
";

	internal static IReadOnlyList<ConversationItem> EnsureFinalContainsQuestionPreamble(IReadOnlyList<ConversationItem> finals, string question)
	{
		if (finals is null || finals.Count == 0 || string.IsNullOrWhiteSpace(question)) return finals;
		for (int i = finals.Count - 1; i >= 0; i--)
		{
			var f = finals[i];
			var text = f.Text ?? string.Empty;
			var pos = text.IndexOf(question, StringComparison.OrdinalIgnoreCase);
			if (pos >= 0)
			{
				var newText = RemoveFirstOccurrenceIgnoreCase(text, question).Trim();
				var adjusted = new ConversationItem
				{
					Id = f.Id,
					SessionId = f.SessionId,
					T0 = f.T0,
					T1 = f.T1,
					Speaker = f.Speaker,
					Kind = f.Kind,
					ParentActId = f.ParentActId,
					Text = newText,
					TextVector = f.TextVector
				};
				var list = new List<ConversationItem>(finals) { [i] = adjusted };
				return list;
			}
		}
		return finals;
	}

	internal static string RemoveFirstOccurrenceIgnoreCase(string source, string value)
	{
		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value)) return source;
		var pos = source.IndexOf(value, StringComparison.OrdinalIgnoreCase);
		if (pos < 0) return source;
		return source.Remove(pos, value.Length);
	}

	internal static string Trunc(string t, int m) => t.Length <= m ? t : t.Substring(0, m) + "…";
	internal static string Fmt(double ms) { var ts = TimeSpan.FromMilliseconds(ms); return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"; }
}

