# remote-stt

Live transcription for Windows system audio (AUDIO) and microphone (MIC) using Azure Cognitive Services Speech. This .NET8 console/hosted service captures audio from both channels in parallel, resamples to16 kHz PCM mono, and streams the data to Azure Speech for real-time partial and final transcripts.

Transcripts and diagnostics are written to the console via Serilog with clear channel tags: `AUDIO` for system/loopback and `MIC` for microphone.


## Features

- Two concurrent pipelines:
 - AUDIO: WASAPI loopback from a selected render/output device (e.g., speakers/headset/virtual cable)
 - MIC: WASAPI capture from a selected capture/input device (microphone)
- Resampling and format conversion to16 kHz,16-bit, mono (required for Azure Speech push-stream)
- Azure Speech continuous recognition with partial and final results
- Lightweight throughput diagnostics for capture, resample, and push stages
- Configurable device selection by friendly-name substring
- Secure key handling via .NET User Secrets or environment variable
- Optional AI answers using Azure OpenAI with conversation memory (Azure AI Search) and prompt packs


## How it works

Two independent pipelines run side-by-side and reuse the same components:

AUDIO pipeline
- `AudioDeviceSelector`  selects the render device
- `LoopbackSource`  captures system output via WASAPI loopback
- `AudioResampler`  converts to16 kHz PCM mono
- `SpeechPushClient`  pushes chunks to Azure Speech recognizer
- `CapturePump`  orchestrates read/push, logs throughput

MIC pipeline
- `AudioDeviceSelector`  selects the capture device
- `MicrophoneSource`  captures microphone via WASAPI
- `AudioResampler`  converts to16 kHz PCM mono
- `SpeechPushClient`  pushes chunks to Azure Speech recognizer
- `MicCapturePump`  orchestrates read/push, logs throughput

Both pipelines log output like:

- `AUDIO [partial] …`
- `AUDIO [final] …`
- `MIC [partial] …`
- `MIC [final] …`

Note: Microphone capture uses WASAPI shared mode (`WasapiCapture`), so other applications can use the mic at the same time. Exclusive-mode capture by another app could block recording (rare for conferencing apps).


## Requirements

- Windows10/11 (WASAPI + Media Foundation)
- .NET8 SDK
- Azure Cognitive Services Speech resource (Region + Key)
- Internet connectivity to Azure endpoints


## Configuration

Settings live in `appsettings.json` and can be overridden by environment variables or User Secrets.

Minimal example:

```json
{
 "Audio": {
 "DeviceNameContains": "Jabra Evolve", // Render/output device search substring (AUDIO)
 "MicrophoneNameContains": "Jabra Evolve", // Capture/input device search substring (MIC)
 "TargetSampleRate":16000,
 "TargetBitsPerSample":16,
 "TargetChannels":1,
 "ResamplerQuality":60,
 "ChunkMilliseconds":200,
 "EnableHeadphoneReminder": true
 },
 "Speech": {
 "Region": "uksouth",
 "Language": "en-GB"
 },
 "Serilog": {
 "MinimumLevel": {
 "Default": "Information",
 "Override": {
 "Microsoft": "Information",
 "System": "Information"
 }
 }
 }
}
```

- `Audio.DeviceNameContains` (string, optional): Substring to match a Windows render device (AUDIO). If empty/omitted, the default multimedia render endpoint is used.
- `Audio.MicrophoneNameContains` (string, optional): Substring to match a Windows capture device (MIC). If empty/omitted, the default communications capture endpoint is used.
- `Audio.Target*` options must remain16-bit mono for Azure Speech push-stream.
- `Speech.Region` (string): Azure Speech region (e.g., `westeurope`, `uksouth`).
- `Speech.Language` (string): BCP-47 locale (e.g., `en-GB`, `en-US`).
- `Speech.Key` (string, optional): Speech key. If omitted, the app falls back to `AZURE_SPEECH_KEY` environment variable.


## Securely provide the Speech key

Recommended (per-project User Secrets):

```bash
# From the project directory (remote-stt)
dotnet user-secrets init

# Store your key under Speech:Key
dotnet user-secrets set "Speech:Key" "<your-speech-key>"
```

Alternative (environment variable):

```bash
# Set once, new shells inherit it
setx AZURE_SPEECH_KEY "<your-speech-key>"
```


## AI answers and conversation memory (optional)

When enabled, `SpeechPushClient` detects questions/imperatives (acts), builds a prompt pack from recent context stored in Azure AI Search, sends it to the LLM, and upserts the answer linked to the originating act (`parentActId`).

- Memory: Azure AI Search single-index PoC (`conv_items`)
- LLM: Azure OpenAI via `ChatClient`
- Answering: `AnswerPipeline` + `AzureOpenAIAnswerProvider`

Wire-up in your host (`Program.cs`):

```csharp
// using AiAssistLibrary.Extensions;

builder.Services.AddConversationMemory(o =>
{
 o.Enabled = true;
 o.SessionId = "session-123";
 // You can omit these here if provided via secrets/env:
 // o.SearchEndpoint = "https://<your-search>.search.windows.net";
 // o.SearchAdminKey = "<admin-key>";
});

// Registers ChatClient, AnswerPipeline, and IAnswerProvider using config + env fallbacks
builder.Services.AddAnswering(builder.Configuration);
```

Selecting the exact LLM
- Default model comes from `OpenAI:Deployment`.
- You can override per-request programmatically via `IAnswerProvider.GetAnswerAsync(prompt, overrideModel: "my-deployment")` if you build a custom handler. `SpeechPushClient` uses the default unless you replace the provider.

### secrets.json (User Secrets) for this project

Store secrets at the `remote-stt` project level (this project has its own `UserSecretsId`). Example:

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
 "UseEntraId": false
 },
 "QuestionDetection": {
 "Enabled": true,
 "MinConfidence":0.5,
 "EnableOpenAIFallback": false,
 "OpenAIEndpoint": "https://YOUR-AOAI.openai.azure.com",
 "OpenAIDeployment": "gpt-4o-mini",
 "OpenAIKey": "YOUR_AZURE_OPENAI_KEY"
 }
}
```

Environment variable fallbacks (optional)
- Azure OpenAI: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_DEPLOYMENT`, `AZURE_OPENAI_USE_ENTRAID`
- Azure AI Search: `AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_ADMIN_KEY`
- Azure Speech: `AZURE_SPEECH_KEY`

Notes
- Set `OpenAI:UseEntraId` to `true` to authenticate with Microsoft Entra ID instead of an API key.
- Answers are automatically upserted to conversation memory with `parentActId` when `SpeechPushClient` detects an act and the answer pipeline is enabled.


## Running

From the `remote-stt` project directory:

```bash
# Restore and run
dotnet run
```

Or from the repository root:

```bash
dotnet run --project remote-stt/remote-stt.csproj
```

On startup you should see messages like:

- Selected render/capture devices
- Resampler chain ready
- `AUDIO session started` / `MIC session started`
- Partial and final transcripts prefixed with `AUDIO` or `MIC`

Stop with Ctrl+C.


## Routing application audio (AUDIO pipeline)

To transcribe remote participants from Microsoft Teams (or any app):
- Route the app’s speakers/output to the same device selected by `Audio.DeviceNameContains`.
- In Teams: Settings  Devices  Speakers  choose the device (e.g., your headset or a virtual cable).
- Tip: Keep headphones enabled to avoid your mic picking up speaker output (echo/bleed).


## Project layout (key files)

- `Program.cs` – host setup, logging (Serilog), options binding, DI registration
- `Services/AudioDeviceSelector.cs` – selects render (AUDIO) and capture (MIC) endpoints
- `Services/LoopbackSource.cs` – WASAPI loopback capture (AUDIO)
- `Services/MicrophoneSource.cs` – WASAPI mic capture (MIC)
- `Services/AudioResampler.cs` – resampling/format conversion to16 kHz PCM mono
- `Services/SpeechPushClient.cs` – Azure Speech push-stream client; logs `MIC`/`AUDIO` tags; builds prompt packs; optionally triggers AI answers
- `Services/CapturePump.cs` – AUDIO pump (loopback  resampler  speech)
- `Services/MicCapturePump.cs` – MIC pump (mic  resampler  speech)
- `appsettings.json` – configuration


## Advanced options

- `Audio.ChunkMilliseconds` – Affects push chunk size; larger chunks can reduce CPU but may slightly increase latency.
- `Audio.ResamplerQuality` – Media Foundation resampler quality (higher uses more CPU).
- Logging levels – Adjust in `Serilog.MinimumLevel` for more/less verbosity. Throughput meters use `Debug`.


## Troubleshooting

- “Speech key missing” on startup
 - Provide `Speech:Key` via User Secrets or set `AZURE_SPEECH_KEY` env var.

- No audio or empty transcripts (AUDIO)
 - Ensure the target app is routed to the selected render device.
 - Remove `Audio.DeviceNameContains` to fall back to default device.

- No audio or empty transcripts (MIC)
 - Make sure the correct mic is selected (use `Audio.MicrophoneNameContains`).
 - Verify Windows privacy settings permit microphone access.

- Device in use / cannot open device
 - Another app might be using exclusive mode. Switch that app to shared mode or pick a different device.

- Echo/feedback or cross-talk
 - Use headphones and ensure your mic does not capture speaker output.


## Privacy and data

Audio is streamed to Azure Cognitive Services for transcription. Review your organization’s compliance requirements and configure the appropriate `Region`/`Language`. Avoid sending sensitive content unless permitted by your policies.


## License

See repository root for license details (if provided).

