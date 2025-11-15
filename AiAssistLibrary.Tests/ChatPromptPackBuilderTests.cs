using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiAssistLibrary.ConversationMemory;
using Xunit;

namespace AiAssistLibrary.Tests;

public sealed class ChatPromptPackBuilderTests
{
    private sealed class FakeMemoryReader : IConversationMemoryReader
    {
        public IReadOnlyList<ConversationItem> Finals { get; set; } = Array.Empty<ConversationItem>();
        public IReadOnlyList<ConversationItem> Acts { get; set; } = Array.Empty<ConversationItem>();
        public IReadOnlyList<ConversationItem> OpenActs { get; set; } = Array.Empty<ConversationItem>();
        public Dictionary<string, ConversationItem?> AnswersByActId { get; set; } = new();

        public Task<IReadOnlyList<ConversationItem>> GetRecentFinalsAsync(double nowMs) => Task.FromResult(Finals);
        public Task<IReadOnlyList<ConversationItem>> GetRelatedActsAsync(string actText, double nowMs) => Task.FromResult(Acts);
        public Task<ConversationItem?> GetLatestAnswerForActAsync(string actId)
        {
            AnswersByActId.TryGetValue(actId, out var ans);
            return Task.FromResult(ans);
        }
        public Task<IReadOnlyList<ConversationItem>> GetOpenActsAsync(double nowMs) => Task.FromResult(OpenActs);
    }

    private static ConversationItem Item(string id, string kind, string text, string speaker = "user") => new ConversationItem
    {
        Id = id,
        SessionId = "S",
        T0 = 1000,
        T1 = 2000,
        Speaker = speaker,
        Kind = kind,
        ParentActId = null,
        Text = text,
        TextVector = null
    };

    [Fact]
    public async Task Builds_Prompt_With_All_Core_Sections()
    {
        var reader = new FakeMemoryReader();
        var builder = new ChatPromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "Explain generics", nowMs: 5000);

        Assert.Contains("recent_finals:", pack.AssembledPrompt);
        Assert.Contains("recent_acts:", pack.AssembledPrompt);
        Assert.Contains("question:", pack.AssembledPrompt);
        Assert.Contains("\"Explain generics\"", pack.AssembledPrompt);
        Assert.Equal(ChatPromptPackBuilder.DefaultSystemPrompt, pack.SystemPrompt);
    }

    [Fact]
    public async Task Limits_Acts_To_Three_With_Answers()
    {
        var acts = Enumerable.Range(1, 6).Select(i => Item($"a{i}", "act", i == 1 ? "IMP First act" : $"Act {i}")).ToList();
        var reader = new FakeMemoryReader { Acts = acts };
        foreach (var a in acts)
        {
            reader.AnswersByActId[a.Id] = Item($"ans-{a.Id}", "answer", $"Answer for {a.Id}", speaker: "assistant");
        }
        var builder = new ChatPromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "What is DI?", nowMs: 6000);

        Assert.Equal(3, pack.RecentActs.Count);
        Assert.True(pack.RecentActs.Select(p => p.Act.Id).SequenceEqual(new[] { "a1", "a2", "a3" }));
        Assert.All(pack.RecentActs, pair => Assert.NotNull(pair.Answer));
    }

    [Fact]
    public async Task Includes_OpenActs_When_Present()
    {
        var open = new List<ConversationItem> { Item("o1", "act", "Unanswered act 1"), Item("o2", "act", "Unanswered act 2") };
        var reader = new FakeMemoryReader { OpenActs = open };
        var builder = new ChatPromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "What is a record?", nowMs: 7000);

        Assert.Contains("open_items:", pack.AssembledPrompt);
        Assert.Contains("Unanswered act 1", pack.AssembledPrompt);
        Assert.Contains("Unanswered act 2", pack.AssembledPrompt);
    }

    [Fact]
    public async Task Preserves_Speaker_And_Timestamps_In_Finals()
    {
        var finals = new List<ConversationItem>
        {
            Item("f1", "final", "Earlier line", speaker: "user"),
            Item("f2", "final", "Later line", speaker: "assistant")
        };
        var reader = new FakeMemoryReader { Finals = finals };
        var builder = new ChatPromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: finals.Last().Text, newActText: "Explain tasks", nowMs: 8000);

        Assert.Equal(finals.Count, pack.RecentFinals.Count);
        Assert.True(pack.RecentFinals.Zip(finals).All(z => z.First.Speaker == z.Second.Speaker && z.First.T0 == z.Second.T0));
    }

    [Fact]
    public async Task SystemPrompt_Exposed_Publicly()
    {
        Assert.Contains("SYSTEM:", ChatPromptPackBuilder.DefaultSystemPrompt);
        Assert.Contains("Rules:", ChatPromptPackBuilder.DefaultSystemPrompt);
    }
}
