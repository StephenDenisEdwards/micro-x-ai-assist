# Azure AI Search "Recall & Evidence" PoC in ai-assist-library

Summary
- Added a minimal, production-friendly PoC memory layer using Azure AI Search to persist and recall conversation context (finals, acts, answers).
- Assembles the prompt pack (recent finals, related acts+answers, open items) for a newly detected act.

What was added
- Files
 - `src/ConversationMemory/ConversationItem.cs` – POCO for the search index document and `ConversationKinds` constants.
 - `src/ConversationMemory/ConversationMemoryOptions.cs` – options for configuring the memory layer.
 - `src/ConversationMemory/ConversationSearchIndexFactory.cs` – builds single-index definition (`conv_items`) with vector profile (HNSW).
 - `src/ConversationMemory/ConversationMemoryClient.cs` – upserts and queries:
 - Upsert `final`, `act`, `answer` items
 - Query recent finals (40s), related acts (lexical for now), latest answer for an act, and open (unanswered) acts
 - `src/ConversationMemory/PromptPackBuilder.cs` – builds the prompt pack text block per the spec.
 - `src/Extensions/ServiceCollectionMemoryExtensions.cs` – DI extension to register the memory client via options.
- Edits
 - `src/Services/SpeechPushClient.cs`
 - Upserts each final transcript line to Search (`kind = "final"`).
 - On detected questions/imperatives, upserts an `act` and raises `PromptPackReady` with the assembled prompt pack.
 - `ai-assist-library.csproj` – added `Azure.Search.Documents` package.

Index (single-index PoC)
- Name: `conv_items` (configurable)
- Fields:
 - `id` (key), `sessionId`, `t0`, `t1`, `speaker`, `kind` ("final" | "act" | "answer"), `parentActId`, `text`, `textVector`.
- Vector profile: HNSW with a configurable dimension (`EmbeddingDimensions`, default1536). The PoC currently writes a zero-vector placeholder to keep the schema consistent without requiring an embeddings deployment.

Behavior
- Upserts
 - Final transcript turn: `kind="final"`, writes `text` and `textVector` (placeholder).
 - Detected act (question/imperative): `kind="act"`.
 - Answers: `kind="answer"`, `parentActId=<actId>` (API available; call from your answer handler).
- Queries (used to build the prompt pack)
 - Recent finals: filter `sessionId`, `kind='final'`, `t0 >= now-40s`, ordered ASC, top4.
 - Related acts: lexical search on `text` + filter `kind='act'` and `t0 >= now-20m`, top5, ordered chronologically.
 - Note: Switch to vector similarity once embeddings are configured.
 - Latest answer for act: filter by `parentActId`, ordered by `t0 desc`, top1.
 - Open items: acts without a linked answer in the last window (20m).
- Prompt pack
 - Format:
 - system: "Answer in1–2 sentences +1 short follow-up. Use ONLY provided snippets."
 - `recent_finals`, `recent_acts` (Q/A pairs), `open_items` (if any), and the new `question`.

How to enable in your host (example)
- Register the memory client and configure options in your composition root (e.g., `remote-stt` `Program.cs`):

```csharp
using AiAssistLibrary.ConversationMemory;
using AiAssistLibrary.Extensions;

builder.Services.AddConversationMemory(o =>
{
 o.Enabled = true;
 o.SessionId = "session-123"; // choose a session ID strategy
 o.SearchEndpoint = "https://<your-search>.search.windows.net";
 o.SearchAdminKey = "<admin-key>";
 // o.IndexName = "conv_items"; // optional override
 // o.EmbeddingDimensions =1536; // default
});
```

- Subscribe to prompt packs if desired:

```csharp
var speech = app.Services.GetRequiredService<SpeechPushClient>();
speech.PromptPackReady += pack =>
{
 // Forward pack.AssembledPrompt to your LLM or log it
};
```

- Upserting answers (optional):
 - After producing an answer to an act, call `ConversationMemoryClient.UpsertAnswerAsync` with `parentActId` of the act. You can obtain the act ID by wiring your own handler when the act is created or by searching for it.

Embeddings (next step)
- The PoC uses a zero-vector placeholder. To enable vector similarity:
 - Add package `Azure.AI.OpenAI`.
 - Configure `OpenAIEndpoint`, `OpenAIKey`, and a1536-dim embedding deployment (e.g., `text-embedding-3-small`).
 - Replace the placeholder embedding in `ConversationMemoryClient` with real embeddings and switch related-acts query to the vector API.

Notes
- Timestamps `t0/t1` are in milliseconds.
- One index keeps PoC simple and supports finals, acts, answers via `kind` + `parentActId`.
- Safe to run without OpenAI dependencies; Search index is created automatically if missing.

Potential improvements
- Switch related-acts to hybrid (keyword + vector).
- Batch embeddings and document upserts.
- Cache recent finals in-memory to cut latency.
- Use `Azure.Identity` (Managed Identity) instead of keys.
