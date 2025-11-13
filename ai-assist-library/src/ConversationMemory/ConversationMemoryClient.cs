using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

namespace AiAssistLibrary.ConversationMemory;

public sealed class ConversationMemoryClient
{
	private readonly ConversationMemoryOptions _opts;
	private readonly SearchIndexClient? _indexClient;
	private readonly SearchClient? _searchClient;
	private readonly int _dims;
	private bool _indexEnsured;
	private readonly object _indexLock = new();

	public ConversationMemoryClient(ConversationMemoryOptions opts)
	{
		_opts = opts;
		if (!_opts.Enabled) return;
		if (!string.IsNullOrWhiteSpace(opts.SearchEndpoint) && !string.IsNullOrWhiteSpace(opts.SearchAdminKey))
		{
			_indexClient = new SearchIndexClient(new Uri(opts.SearchEndpoint!), new AzureKeyCredential(opts.SearchAdminKey!));
			_searchClient = _indexClient.GetSearchClient(opts.IndexName);
		}
		_dims = opts.EmbeddingDimensions;
	}

	// Clear all documents for the current session (used at startup if option set)
	public async Task ClearSessionAsync()
	{
		if (!_opts.Enabled || _searchClient is null) return;
		await EnsureIndexAsync();
		// Use search + batch delete (no direct session purge API)
		var filter = $"sessionId eq '{Escape(_opts.SessionId)}'";
		var options = new SearchOptions { Filter = filter, Size = 500 }; // batch size
		var results = await _searchClient.SearchAsync<ConversationItem>("*", options);
		var toDelete = new List<ConversationItem>();
		await foreach (var r in results.Value.GetResultsAsync()) toDelete.Add(r.Document);
		if (toDelete.Count == 0) return;
		await _searchClient.DeleteDocumentsAsync(toDelete.Select(d => new { id = d.Id }));
	}

	private async Task EnsureIndexAsync()
	{
		if (_indexClient is null) return;
		if (_indexEnsured) return;
		lock (_indexLock)
		{
			if (_indexEnsured) return;
		}
		try
		{
			await _indexClient.GetIndexAsync(_opts.IndexName);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			var def = ConversationSearchIndexFactory.Create(_opts.IndexName, _dims);
			await _indexClient.CreateOrUpdateIndexAsync(def);
		}
		lock (_indexLock)
		{
			_indexEnsured = true;
		}
	}

	// Placeholder embedding (zero vector) to keep index schema PoC-simple without deploying model yet.
	private float[] ZeroEmbedding()
	{
		var arr = new float[_dims];
		return arr;
	}

	private static string NewId(string sessionId, string kind) => $"{sessionId}-{kind}-{Guid.NewGuid():N}";

	public async Task UpsertFinalAsync(string speaker, string text, double t0, double t1)
	{
		if (!_opts.Enabled || _searchClient is null) return;
		await EnsureIndexAsync();
		var item = new ConversationItem
		{
			Id = NewId(_opts.SessionId, ConversationKinds.Final),
			SessionId = _opts.SessionId,
			Speaker = speaker,
			Kind = ConversationKinds.Final,
			Text = text,
			T0 = t0,
			T1 = t1,
			TextVector = ZeroEmbedding()
		};
		await _searchClient.UploadDocumentsAsync(new[] { item });
	}

	public async Task<ConversationItem?> UpsertActAsync(string speaker, string text, double t0, double t1)
	{
		if (!_opts.Enabled || _searchClient is null) return null;
		await EnsureIndexAsync();
		var item = new ConversationItem
		{
			Id = NewId(_opts.SessionId, ConversationKinds.Act),
			SessionId = _opts.SessionId,
			Speaker = speaker,
			Kind = ConversationKinds.Act,
			Text = text,
			T0 = t0,
			T1 = t1,
			TextVector = ZeroEmbedding()
		};
		await _searchClient.UploadDocumentsAsync(new[] { item });
		return item;
	}

	public async Task UpsertAnswerAsync(string speaker, string text, double t0, double t1, string parentActId)
	{
		if (!_opts.Enabled || _searchClient is null) return;
		await EnsureIndexAsync();
		var item = new ConversationItem
		{
			Id = NewId(_opts.SessionId, ConversationKinds.Answer),
			SessionId = _opts.SessionId,
			Speaker = speaker,
			Kind = ConversationKinds.Answer,
			Text = text,
			ParentActId = parentActId,
			T0 = t0,
			T1 = t1,
			TextVector = ZeroEmbedding()
		};
		await _searchClient.UploadDocumentsAsync(new[] { item });
	}

	public async Task<IReadOnlyList<ConversationItem>> GetRecentFinalsAsync(double nowMs)
	{
		if (!_opts.Enabled || _searchClient is null) return Array.Empty<ConversationItem>();
		var cutoff = nowMs - 40000; //40s
		var filter = $"sessionId eq '{Escape(_opts.SessionId)}' and kind eq 'final' and t0 ge {cutoff}";
		var options = new SearchOptions { Filter = filter, OrderBy = { "t0 asc" }, Size = 4 };
		var results = await _searchClient.SearchAsync<ConversationItem>("*", options);
		var list = new List<ConversationItem>();
		await foreach (var r in results.Value.GetResultsAsync()) list.Add(r.Document);
		return list;
	}

	public async Task<IReadOnlyList<ConversationItem>> GetRelatedActsAsync(string actText, double nowMs)
	{
		if (!_opts.Enabled || _searchClient is null || string.IsNullOrWhiteSpace(actText)) return Array.Empty<ConversationItem>();
		var cutoff = nowMs - 1_200_000; //20m
		var filter = $"sessionId eq '{Escape(_opts.SessionId)}' and kind eq 'act' and t0 ge {cutoff}";
		var options = new SearchOptions();
		options.Filter = filter;
		options.Size = 5;
		// Leave default query mode; rely on full-text of 'text'.
		options.SearchFields.Add("text");
		var results = await _searchClient.SearchAsync<ConversationItem>(actText, options);
		var acts = new List<ConversationItem>();
		await foreach (var r in results.Value.GetResultsAsync()) acts.Add(r.Document);
		return acts.OrderBy(a => a.T0).ToList();
	}

	public async Task<ConversationItem?> GetLatestAnswerForActAsync(string actId)
	{
		if (!_opts.Enabled || _searchClient is null) return null;
		var filter = $"sessionId eq '{Escape(_opts.SessionId)}' and kind eq 'answer' and parentActId eq '{Escape(actId)}'";
		var options = new SearchOptions { Filter = filter, OrderBy = { "t0 desc" }, Size = 1 };
		var results = await _searchClient.SearchAsync<ConversationItem>("*", options);
		await foreach (var r in results.Value.GetResultsAsync()) return r.Document;
		return null;
	}

	public async Task<IReadOnlyList<ConversationItem>> GetOpenActsAsync(double nowMs)
	{
		if (!_opts.Enabled || _searchClient is null) return Array.Empty<ConversationItem>();
		var cutoff = nowMs - 1_200_000;
		var filter = $"sessionId eq '{Escape(_opts.SessionId)}' and kind eq 'act' and t0 ge {cutoff}";
		var options = new SearchOptions { Filter = filter, Size = 50 };
		var results = await _searchClient.SearchAsync<ConversationItem>("*", options);
		var acts = new List<ConversationItem>();
		await foreach (var r in results.Value.GetResultsAsync()) acts.Add(r.Document);
		var open = new List<ConversationItem>();
		foreach (var act in acts)
		{
			var ans = await GetLatestAnswerForActAsync(act.Id);
			if (ans == null) open.Add(act);
		}
		return open.OrderBy(a => a.T0).ToList();
	}

	private static string Escape(string s) => s.Replace("'", "''");
}
