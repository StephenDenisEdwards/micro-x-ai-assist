namespace AiAssistLibrary.ConversationMemory;

/// <summary>
/// Adapter to bridge existing <see cref="ConversationMemoryClient"/> to <see cref="IConversationMemoryReader"/>.
/// </summary>
public sealed class ConversationMemoryClientReaderAdapter : IConversationMemoryReader
{
	private readonly ConversationMemoryClient _inner;
	public ConversationMemoryClientReaderAdapter(ConversationMemoryClient inner) => _inner = inner;

	public Task<IReadOnlyList<ConversationItem>> GetRecentFinalsAsync(double nowMs) => _inner.GetRecentFinalsAsync(nowMs);
	public Task<IReadOnlyList<ConversationItem>> GetRelatedActsAsync(string actText, double nowMs) => _inner.GetRelatedActsAsync(actText, nowMs);

	public Task<(ConversationItem Act, ConversationItem Answer)?> GetLastActAndAnswerAsync(double nowMs) =>
		_inner.GetLastActAndAnswerAsync(nowMs);

	public Task<ConversationItem?> GetLatestAnswerForActAsync(string actId) => _inner.GetLatestAnswerForActAsync(actId);
	public Task<IReadOnlyList<ConversationItem>> GetOpenActsAsync(double nowMs) => _inner.GetOpenActsAsync(nowMs);
}