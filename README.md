# Teams Remote STT (Speech-to-Text)

A .NET8 console/hosted app that captures Windows system audio from a selected output device (WASAPI loopback), resamples it to16 kHz PCM mono, and streams it to Azure Cognitive Services Speech for live transcription.

Use it to transcribe remote participants in Microsoft Teams (or any app routed to the chosen output device). Partial and final results are logged to the console via Serilog.


## How it works

- Device selection
 - `AudioDeviceSelector` chooses an active Windows render endpoint. If `Audio:DeviceNameContains` is set, the first device whose friendly name contains that substring is selected; otherwise the default multimedia endpoint is used.
- Loopback capture
 - `LoopbackSource` uses NAudio `WasapiLoopbackCapture` to capture the audio leaving the selected output device into a buffer.
- Resampling and format conversion
 - `AudioResampler` converts the source to PCM16‑bit, mono, at the target sample rate (default16 kHz) using Media Foundation.
- Chunking and push stream
 - `CapturePump` reads fixed-size chunks from the resampler and writes them into a `PushAudioInputStream`.
- Speech recognition
 - `SpeechPushClient` feeds chunks to the Azure Speech SDK `SpeechRecognizer` which raises `Recognizing` (partial) and `Recognized` (final) events that are logged.

Data flow

`AudioDeviceSelector` → `LoopbackSource` → `AudioResampler` → `CapturePump` → `SpeechPushClient` → Azure Speech → console logs


## Features

- Capture any app’s output (Teams, browser, media player, etc.) by routing it to a chosen output device
- Live partial and final transcription via Azure Speech SDK
- Configurable device selection and audio pipeline (rate, channels, chunk size, resampler quality)
- Simple, composable services with logging via Serilog


## Requirements

- Windows10/11 (uses WASAPI loopback and Media Foundation)
- .NET8 SDK
- Azure Cognitive Services Speech resource (Region + Key)
- Network access to Azure Speech endpoints


## Quick start

1) Clone

- Clone this repository.

2) Configure Azure Speech key

- Recommended: store the key with .NET user-secrets (scoped to this project):

```
cd src/TeamsRemoteSTT.App
dotnet user-secrets init
dotnet user-secrets set "Speech:Key" "<your-speech-key>"
```

- Alternatively, set an environment variable (new console sessions will pick it up):

```
setx AZURE_SPEECH_KEY "<your-speech-key>"
```

3) Configure app settings

- Edit `src/TeamsRemoteSTT.App/appsettings.json`:
 - `Audio.DeviceNameContains`: Substring of the Windows output device to capture (e.g., "VB-Audio Cable", your headphones/speakers name). If omitted/empty, the default render endpoint is used.
 - `Speech.Region`: Azure Speech resource region (e.g., `westeurope`).
 - `Speech.Language`: BCP-47 language tag (e.g., `en-GB`).

Example:

```
{
 "Audio": {
 "DeviceNameContains": "VB-Audio Cable",
 "TargetSampleRate":16000,
 "TargetBitsPerSample":16,
 "TargetChannels":1,
 "ResamplerQuality":60,
 "ChunkMilliseconds":200,
 "EnableHeadphoneReminder": true
 },
 "Speech": {
 "Region": "westeurope",
 "Language": "en-GB"
 },
 "Serilog": {
 "MinimumLevel": "Information"
 }
}
```

4) Route Teams (or any app) to the selected device

- In Teams: Settings → Devices → Speakers: pick the same output device configured above.
- Tip: Use headphones to avoid your microphone picking up playback audio. When enabled, the app logs a reminder on startup.

5) Run

```
dotnet run --project src/TeamsRemoteSTT.App
```

You should see logs like:
- `[partial] hello there ...`
- `[final] hello there`

Stop with Ctrl+C.


## Configuration reference

`Audio` (see `src/TeamsRemoteSTT.App/Settings/AudioOptions.cs`)

- `DeviceNameContains` (string): Optional substring to match a Windows render device friendly name.
- `TargetSampleRate` (int, default16000): Output sample rate used by the resampler.
- `TargetBitsPerSample` (int, default16): Bits per sample.
- `TargetChannels` (int, default1): Channels (mono recommended).
- `ResamplerQuality` (int, default60): Media Foundation resampler quality (higher = better quality, more CPU).
- `ChunkMilliseconds` (int, default200): Approximate chunk size pushed to Speech.
- `EnableHeadphoneReminder` (bool, default true): Prints a one-time tip to use headphones.

`Speech` (see `src/TeamsRemoteSTT.App/Settings/SpeechOptions.cs`)

- `Region` (string): Azure Speech region, e.g., `westeurope`.
- `Language` (string): Recognition language, e.g., `en-GB`.
- `Key` (string, optional): Speech key. If omitted, the app falls back to `AZURE_SPEECH_KEY` environment variable.

Note: `SpeechPushClient` currently opens its push stream with16 kHz,16-bit, mono. Keep `Audio` targets aligned with these defaults for best results.


## Key files

- `src/TeamsRemoteSTT.App/Program.cs`: Host setup, logging (Serilog), options binding, service wiring, startup checks.
- `src/TeamsRemoteSTT.App/Services/AudioDeviceSelector.cs`: Picks the render/output device.
- `src/TeamsRemoteSTT.App/Services/LoopbackSource.cs`: WASAPI loopback capture via NAudio.
- `src/TeamsRemoteSTT.App/Services/AudioResampler.cs`: Media Foundation resampling/format conversion to PCM16 mono @ target rate.
- `src/TeamsRemoteSTT.App/Services/CapturePump.cs`: Main pump reading from loopback/resampler and pushing chunks to Speech.
- `src/TeamsRemoteSTT.App/Services/SpeechPushClient.cs`: Azure Speech SDK client using a push stream, emits partial/final logs.
- `src/TeamsRemoteSTT.App/appsettings.json`: Configuration.


## Troubleshooting

- No active render endpoints found
 - Ensure an output device is enabled in Windows Sound settings.
 - Adjust `Audio.DeviceNameContains` or remove it to use the default device.

- No transcription / repeated "Speech canceled" logs
 - Verify the Speech Key and Region are correct.
 - Check firewall/network access to Azure endpoints.
 - Ensure the app audio is routed to the selected output device.

- Echo/feedback or poor results
 - Use headphones and ensure the microphone does not capture speaker output.

- High CPU usage
 - Lower `ResamplerQuality` or increase `ChunkMilliseconds`.


## Dependencies

- NAudio (WASAPI loopback capture)
- Azure Cognitive Services Speech SDK
- Serilog (console sink)


## Notes

- The app does not record or persist audio; it streams to Azure Speech and logs transcripts.
- The focus is on remote audio capture; your microphone is not captured unless your system routes mic audio into the chosen output device.
- Extend `SpeechPushClient` if you need to forward transcripts to other sinks (files, UI, events, APIs).