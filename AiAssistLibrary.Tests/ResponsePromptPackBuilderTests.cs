using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiAssistLibrary.ConversationMemory;
using Xunit;

namespace AiAssistLibrary.Tests;

public sealed class ResponsePromptPackBuilderTests
{
    private sealed class FakeMemoryReader : IConversationMemoryReader
    {
        public IReadOnlyList<ConversationItem> Finals { get; set; } = Array.Empty<ConversationItem>();
        public IReadOnlyList<ConversationItem> Acts { get; set; } = Array.Empty<ConversationItem>();
        public IReadOnlyList<ConversationItem> OpenActs { get; set; } = Array.Empty<ConversationItem>();
        public Dictionary<string, ConversationItem?> AnswersByActId { get; set; } = new();

        public Task<IReadOnlyList<ConversationItem>> GetRecentFinalsAsync(double nowMs) => Task.FromResult(Finals);
        public Task<IReadOnlyList<ConversationItem>> GetRelatedActsAsync(string actText, double nowMs) => Task.FromResult(Acts);
        public Task<(ConversationItem Act, ConversationItem Answer)?> GetLastActAndAnswerAsync(double nowMs)
        {
	        throw new NotImplementedException();
        }

        public Task<ConversationItem?> GetLatestAnswerForActAsync(string actId)
        {
            AnswersByActId.TryGetValue(actId, out var ans);
            return Task.FromResult(ans);
        }
        public Task<IReadOnlyList<ConversationItem>> GetOpenActsAsync(double nowMs) => Task.FromResult(OpenActs);
    }

    private static ConversationItem Item(string id, string kind, string text) => new ConversationItem
    {
        Id = id,
        SessionId = "S",
        T0 = 1000,
        T1 = 2000,
        Speaker = "user",
        Kind = kind,
        ParentActId = null,
        Text = text,
        TextVector = null
    };


    [Fact]
    public async Task If_Question_Is_Not_In_Final() // 
    {
        var question = "How would you tune garbage collection for better performance?";
        var finals = new List<ConversationItem>
        {
            Item("f1","final","Some context before."),
            Item("f2","final","This application allocates millions of short lived objects per second. " + question)
        };
        var expectedFinals = new List<ConversationItem>
        {
	        Item("f1","final","Some context before."),
	        Item("f2","final","This application allocates millions of short lived objects per second.")
        };
		var reader = new FakeMemoryReader { Finals = finals };
        var builder = new ResponsePromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: finals.Last().Text, newActText: question, nowMs: 4000);
        Assert.Equal(expectedFinals, pack.RecentFinals); // same references
	}
	

	[Fact]
    public async Task Removes_Question_Text_From_Latest_Final_Containing_It()
    {
        var question = "How do I parse JSON?";
        var finals = new List<ConversationItem>
        {
            Item("f1","final","Earlier context."),
            Item("f2","final","Some lead up " + question + " trailing notes")
        };
        var reader = new FakeMemoryReader { Finals = finals };
        var builder = new ResponsePromptPackBuilder(reader);

        var pack = await builder.BuildAsync(fullFinal: finals.Last().Text, newActText: question, nowMs: 5000);

        Assert.Equal(2, pack.RecentFinals.Count);
        // First final unchanged
        Assert.Same(finals[0], pack.RecentFinals[0]);
        // Second final replaced (reference changed) and question removed
        Assert.NotSame(finals[1], pack.RecentFinals[1]);
        Assert.DoesNotContain(question, pack.RecentFinals[1].Text, StringComparison.OrdinalIgnoreCase);
        // Original surrounding text retained (after Trim)
        Assert.Equal("Some lead up trailing notes", pack.RecentFinals[1].Text);
    }

    [Fact]
    public async Task Finals_Unchanged_If_Question_Not_Found()
    {
        var question = "Nonexistent question?";
        var finals = new List<ConversationItem>
        {
            Item("f1","final","Some unrelated text."),
            Item("f2","final","More unrelated content.")
        };
        var reader = new FakeMemoryReader { Finals = finals };
        var builder = new ResponsePromptPackBuilder(reader);

        var pack = await builder.BuildAsync(fullFinal: finals.Last().Text, newActText: question, nowMs: 6000);
        Assert.Equal(finals, pack.RecentFinals); // same references
    }

    [Fact]
    public async Task Limits_To_Three_Recent_Acts_With_Answers()
    {
        var acts = Enumerable.Range(1,5).Select(i => Item($"a{i}","act", i == 1 ? "IMP Do thing" : $"Act {i}")).ToList();
        var answers = acts.Take(5).ToDictionary(a => a.Id, a => new ConversationItem
        {
            Id = $"ans-{a.Id}", SessionId = "S", T0 = 3000, T1 = 4000, Speaker = "assistant", Kind = "answer", ParentActId = a.Id, Text = $"Answer for {a.Id}", TextVector = null
        } as ConversationItem);
        var reader = new FakeMemoryReader { Acts = acts, AnswersByActId = answers };
        var builder = new ResponsePromptPackBuilder(reader);

        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "New question here?", nowMs: 7000);

        Assert.Equal(3, pack.RecentActs.Count);
        // Ensure ordering preserved (same ordering as provided acts)
        Assert.True(pack.RecentActs.Select(p => p.Act.Id).SequenceEqual(new[]{"a1","a2","a3"}));
        // Answers wired
        Assert.All(pack.RecentActs, pair => Assert.NotNull(pair.Answer));
    }

    [Fact]
    public async Task Includes_Open_Acts_In_AssembledPrompt()
    {
        var openActs = new List<ConversationItem>
        {
            Item("o1","act","Open item 1"),
            Item("o2","act","Open item 2")
        };
        var reader = new FakeMemoryReader { OpenActs = openActs };
        var builder = new ResponsePromptPackBuilder(reader);

        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "What is immutability?", nowMs: 8000);

        Assert.Contains("open_items:", pack.AssembledPrompt);
        Assert.Contains("Open item 1", pack.AssembledPrompt);
        Assert.Contains("Open item 2", pack.AssembledPrompt);
    }

    [Fact]
    public async Task AssembledPrompt_Contains_Core_Sections()
    {
        var reader = new FakeMemoryReader();
        var builder = new ResponsePromptPackBuilder(reader);
        var pack = await builder.BuildAsync(fullFinal: string.Empty, newActText: "Explain dependency injection", nowMs: 9000);

        Assert.Contains("recent_finals:", pack.AssembledPrompt);
        Assert.Contains("recent_acts:", pack.AssembledPrompt);
        Assert.Contains("question:", pack.AssembledPrompt);
        Assert.Equal(ResponsePromptPackBuilder.DefaultSystemPrompt, pack.SystemPrompt);
        Assert.Equal("Explain dependency injection", pack.NewActText);
    }

    // Direct unit tests for the public static method (now requiring fullFinal)
    [Fact]
    public void EnsureFinalContainsQuestionPreamble_Where_Preamble_And_Question_Are_FullFinal()
    {
        var question = "How would you tune garbage collection for better performance?";
        var preamble = "This application allocates millions of short lived objects per second.";
        var fullFinal = $"{preamble} How would you tune garbage collection for better performance?";
		var finals = new List<ConversationItem>
        {
            Item("f1","final","Some previous question and answer."),
            Item("f2","final","And some more irrelevant crap."),
            Item("f3","final","And yet more irrelevant crap.")

		} as IReadOnlyList<ConversationItem>;

        var adjustedFinals = ResponsePromptPackBuilder.EnsureFinalContainsQuestionPreamble(finals, question, fullFinal);

        Assert.Equal(4, adjustedFinals.Count);

        Assert.Same(finals[0], adjustedFinals[0]);
        Assert.Same(finals[1], adjustedFinals[1]);
        Assert.Same(finals[2], adjustedFinals[2]);
        Assert.Equal(preamble, adjustedFinals[3].Text);
    }
    [Fact]
    public void EnsureFinalContainsQuestionPreamble_Where_Preamble_And_Question_Are_FullFinal_And_No_Existing_Finals()
    {
	    var question = "How would you tune garbage collection for better performance?";
	    var preamble = "This application allocates millions of short lived objects per second.";
	    var fullFinal = $"{preamble} How would you tune garbage collection for better performance?";
	    var finals = new List<ConversationItem>
	    {
	    } as IReadOnlyList<ConversationItem>;

	    var adjustedFinals = ResponsePromptPackBuilder.EnsureFinalContainsQuestionPreamble(finals, question, fullFinal);

	    Assert.Equal(1, adjustedFinals.Count);

	    Assert.Equal(preamble, adjustedFinals[0].Text);
    }
}
