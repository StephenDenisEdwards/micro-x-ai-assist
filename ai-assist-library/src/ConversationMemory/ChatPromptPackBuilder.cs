using System.Text;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ChatPromptPackBuilder : IPromptPackBuilder
{
	private readonly ConversationMemoryClient _memory;
	public ChatPromptPackBuilder(ConversationMemoryClient memory) => _memory = memory;

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
		// var systemPrompt = "Answer in1–2 sentences +1 short follow-up. Use ONLY provided snippets.";
		var systemPrompt = @"SYSTEM:
You are an answer engine.

Rules:
- Answer in 1–3 sentences.
- Do NOT ask follow-up questions or suggest them.
- If the question is ambiguous, assume the most likely intent and include a short clarifying note.
- If the provided snippets contain the answer, use ONLY those.
- If the snippets do not contain the answer, say: ""No direct answer found in the provided snippets."" Then provide a short general-knowledge answer prefixed with ""General knowledge:"".
- Avoid rhetorical questions.
- Use concise, technically precise language.
- Lists are allowed only if explicitly requested or necessary for clarity.
- No greetings or small talk.

Format:
- One compact paragraph answer.
- Optionally one short clarification line (if ambiguity exists).

Examples:
Q: What is the difference between a class and a struct in C?
A: Interpreting ""C"" as C#: classes are reference types allocated on the heap and support inheritance; structs are value types stored inline and cannot derive from other structs. If you meant C language, note that C has only simple structs and no classes.
---
Q: Explain async in C#.
A: The async keyword marks methods that can await asynchronous operations without blocking; they return Task or Task<T> and resume after awaited tasks complete.
---
Q: How does dependency injection work?
A: Dependency injection supplies object dependencies through constructors or parameters rather than creating them inside the class. This improves testability and decouples implementation from configuration.
";
		var sb = new StringBuilder();
		sb.AppendLine(systemPrompt);
		sb.AppendLine();
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
		sb.AppendLine();
		if (open.Count > 0)
		{
			sb.AppendLine("open_items:");
			foreach (var o in open) sb.AppendLine($"- IMP: \"{Trunc(o.Text, 180)}\"");
			sb.AppendLine();
		}
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


	private static string Trunc(string t, int m) => t.Length <= m ? t : t.Substring(0, m) + "�";
	private static string Fmt(double ms) { var ts = TimeSpan.FromMilliseconds(ms); return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"; }
}