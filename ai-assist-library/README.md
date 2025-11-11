# ai-assist-library

Production-friendly building blocks for:
- Real-time speech transcription (pushed in from host apps via `SpeechPushClient`)
- Question / imperative detection (acts)
- Conversation memory in Azure AI Search (finals, acts, answers)
- Prompt pack assembly (evidence-based minimal context)
- Azure OpenAI answering pipeline with automatic answer persistence

## Core Concepts

Conversation items are stored in a single Azure AI Search index (`conv_items` by default) using a `kind` discriminator:
- `final` – finalized transcript turns
- `act` – detected question or imperative triggers
- `answer` – model (or human) responses linked by `parentActId`

The prompt pack builder queries recent context:
- Recent finals (?40s window, top4)
- Related acts (+ their latest answers) via lexical search (20 min window)
- Open (unanswered) acts (20 min window)
- Newly detected act text (question)

System instruction currently:
```
Answer in1–2 sentences +1 short follow-up. Use ONLY provided snippets.
```

## Packages
Added packages for LLM + Search integration:
- `Azure.Search.Documents`
- `Azure.AI.OpenAI`
- `Azure.Identity` (optional keyless auth)

## Configuration Overview
All options bind from configuration (appsettings.json, user secrets, env vars). Environment fallbacks are used when values are absent in configuration.

### SpeechOptions
```
Speech:Region (string, required)
Speech:Language (string, required)
Speech:Key (string, optional – or AZURE_SPEECH_KEY env)
Speech:SegmentationSilenceTimeoutMs (int, default500)
Speech:InitialSilenceTimeoutMs (int, default5000)
```

### ConversationMemoryOptions
```
ConversationMemory:Enabled (bool)
ConversationMemory:SessionId (string, required if Enabled)
ConversationMemory:SearchEndpoint (string, or AZURE_SEARCH_ENDPOINT env)
ConversationMemory:SearchAdminKey (string, or AZURE_SEARCH_ADMIN_KEY env)
ConversationMemory:IndexName (string, default conv_items)
ConversationMemory:EmbeddingDimensions (int, default1536)
```
Embeddings currently use zero vectors (placeholder). Replace in `ConversationMemoryClient` once you deploy an embedding model.

### QuestionDetectionOptions
```
QuestionDetection:Enabled (bool)
QuestionDetection:MinConfidence (double, default0.5)
QuestionDetection:EnableOpenAIFallback (bool)
QuestionDetection:OpenAIEndpoint (string, optional)
QuestionDetection:OpenAIDeployment (string, optional)
QuestionDetection:OpenAIKey (string, optional)
```
If fallback is enabled, the hybrid detector can promote borderline cases using Azure OpenAI.

### OpenAIOptions
```
OpenAI:Endpoint (string, e.g. https://YOUR.openai.azure.com)
OpenAI:ApiKey (string, or AZURE_OPENAI_API_KEY env)
OpenAI:Deployment (string, model deployment name)
OpenAI:UseEntraId (bool, default false; set true to use DefaultAzureCredential)
OpenAI:Mode (string, default "Responses"; one of "Responses" or "Chat")
```
`UseEntraId=true` switches to token-based auth (Managed Identity / developer login). Ensure your identity has appropriate RBAC (e.g., Cognitive Services OpenAI User). `Mode` selects which Azure OpenAI API surface the library uses via `LlmApiMode`.

### Environment Variable Fallbacks
```
AZURE_SPEECH_KEY
AZURE_SEARCH_ENDPOINT
AZURE_SEARCH_ADMIN_KEY
AZURE_OPENAI_ENDPOINT
AZURE_OPENAI_API_KEY
AZURE_OPENAI_DEPLOYMENT
AZURE_OPENAI_USE_ENTRAID (true/false)
AZURE_OPENAI_MODE (Responses|Chat)
```

## Services / DI Extensions
- `AddConversationMemory` – registers `ConversationMemoryClient` with endpoint/admin key fallbacks.
- `AddAnswering` – binds `OpenAIOptions`, registers the Azure OpenAI client, selects `IAnswerProvider` based on `OpenAI:Mode` (`AzureOpenAIResponseAnswerProvider` or `AzureOpenAIChatAnswerProvider`), and wires `AnswerPipeline`.
- `SpeechPushClient` – when question detection fires, upserts an act, builds a prompt pack, invokes the answer pipeline, and persists the answer (with `parentActId`).

## Answer Flow
1. Speech final result arrives -> stored as `final`.
2. Text triggers detection -> upsert `act`.
3. Prompt pack assembled from memory.
4. `AnswerPipeline` sends the assembled pack to Azure OpenAI using the selected API mode (Responses or Chat).
5. Answer returned -> printed to console (host responsibility) -> upserted as `answer` referencing the act ID.

## secrets.json Example
For a host project (e.g., remote-stt) using this library:
```json
{
 "Speech": {
 "Region": "YOUR_SPEECH_REGION",
 "Language": "en-US",
 "Key": "YOUR_SPEECH_KEY"
 },
 "ConversationMemory": {
 "Enabled": true,
 "SessionId": "session-123",
 "SearchEndpoint": "https://YOUR-SEARCH.search.windows.net",
 "SearchAdminKey": "YOUR_SEARCH_ADMIN_KEY",
 "IndexName": "conv_items",
 "EmbeddingDimensions":1536
 },
 "OpenAI": {
 "Endpoint": "https://YOUR-AOAI.openai.azure.com",
 "ApiKey": "YOUR_AZURE_OPENAI_KEY",
 "Deployment": "gpt-4o-mini",
 "UseEntraId": false,
 "Mode": "Responses"
 },
 "QuestionDetection": {
 "Enabled": true,
 "MinConfidence":0.5,
 "EnableOpenAIFallback": false
 }
}
```

## Selecting API Mode
- Default is `Responses` (new Azure OpenAI Responses API).
- Set `OpenAI:Mode` to `Chat` or environment `AZURE_OPENAI_MODE=Chat` to use the Chat Completions provider.

## Switching Models Per Request
Call `IAnswerProvider.GetAnswerAsync(prompt, overrideModel: "another-deployment")` for an override. Default deployment comes from `OpenAI:Deployment`.

## Enabling Vector Similarity (Future)
1. Deploy embedding model (e.g., `text-embedding-3-small`,1536 dims).
2. Add embedding generation before upserts (replace `ZeroEmbedding()` in `ConversationMemoryClient`).
3. Change related acts query to vector/hybrid search.

## Extensibility Notes
- Replace `HybridQuestionDetector` with custom detector (LLM-driven classification, regex, etc.).
- Add caching inside `PromptPackBuilder` for high-traffic sessions.
- Swap keys for Managed Identity by using `DefaultAzureCredential` for Search (not implemented yet; currently admin key).

## Console Output (Act + Answer)
```
[AUDIO] FINAL QUESTION: <text> (conf0.87, speaker S1)
----- Prompt Pack (FINAL) -----
SYSTEM:
...
AI: <model answer printed by AnswerPipeline>
```

## License
See repository root for license details.
