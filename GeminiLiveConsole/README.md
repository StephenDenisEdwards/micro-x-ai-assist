# GeminiLiveConsole (.NET)

A .NET 8 console prototype mirroring the TypeScript `LiveManager` real‑time intent monitoring using Gemini Live.

## Features
- Captures microphone audio at 16 kHz PCM (mono) via NAudio.
- Streams base64 audio chunks over WebSocket to Gemini Live (placeholder message schema; adjust per official docs).
- Receives transcripts and function/tool calls (`report_intent`).
- Reports intents (QUESTION / IMPERATIVE) similarly to the original app.

## Project Structure
- `AudioCaptureService.cs`: Microphone capture, yields raw PCM chunks.
- `GeminiLiveClient.cs`: WebSocket connection + send/receive JSON messages.
- `LiveSessionManager.cs`: Orchestrates audio pumping and event propagation.
- `Models.cs`: DTOs / config classes.
- `Program.cs`: Minimal runner wiring events.

## Setup
1. Set environment variable `GEMINI_API_KEY`.
2. Confirm model name in `Program.cs` matches an enabled Gemini Live model.
3. Review live API message schemas (the `input_audio` and `setup` JSON are placeholders).

## Run
```powershell
cd dotnet/GeminiLiveConsole
# Restore and run
dotnet restore
dotnet run
```
Press ENTER to stop.

## Adapting to Official Gemini Live Schema
Replace the `setup` and `input_audio` messages in `GeminiLiveClient.SendAudioChunkAsync` & `ConnectAsync` with the exact required JSON per current Google docs (may include session IDs, modality declarations, tool/function declarations, etc.).

## Intent Handling
When a function call named `report_intent` arrives, the client prints the intent with its answer. Persist or route externally for long‑term memory.

## Notes
- Error handling is minimal; expand with reconnection, latency metrics, and backpressure if needed.
- For production, secure the API key (do not leave `YOUR_API_KEY` fallback).
- Consider configurable chunk size / buffering for lower latency.
