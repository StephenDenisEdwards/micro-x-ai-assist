using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiAssistLibrary.ConversationMemory;

/// <summary>
/// Read-only abstraction over conversation memory used by <see cref="ResponsePromptPackBuilder"/>.
/// Enables mocking in unit tests without hitting external services or storage.
/// </summary>
public interface IConversationMemoryReader
{
    Task<IReadOnlyList<ConversationItem>> GetRecentFinalsAsync(double nowMs);
    Task<IReadOnlyList<ConversationItem>> GetRelatedActsAsync(string actText, double nowMs);
    Task<(ConversationItem Act, ConversationItem Answer)?> GetLastActAndAnswerAsync(double nowMs);

	Task<ConversationItem?> GetLatestAnswerForActAsync(string actId);
    Task<IReadOnlyList<ConversationItem>> GetOpenActsAsync(double nowMs);
}