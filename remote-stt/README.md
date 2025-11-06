# remote-stt

A .NET8 console/hosted app that captures Windows system audio from a selected output device (WASAPI loopback), resamples it to16 kHz PCM mono, and streams it to Azure Cognitive Services Speech for live transcription.

Use it to transcribe remote participants in Microsoft Teams (or any app routed to the chosen output device). Partial and final results are logged to the console via Serilog.


## How it works

- Device selection
 - `Services/AudioDeviceSelector.cs` chooses an active Windows render endpoint. If `Audio:DeviceNameContains` is set, the first device whose friendly name contains that substring is selected; otherwise the default multimedia endpoint is used.
- Loopback capture
 - `Services/LoopbackSource.cs` uses NAudio `WasapiLoopbackCapture` to capture the audio leaving the selected output device into a buffer.
- Resampling and format conversion
 - `Services/AudioResampler.cs` converts the source to PCM16?bit, mono, at the target sample rate (default16 kHz) using Media Foundation.
- Chunking and push stream
 - `Services/CapturePump.cs` reads fixed-size chunks from the resampler and writes them into a `PushAudioInputStream`.
- Speech recognition
 - `Services/SpeechPushClient.cs` feeds chunks to the Azure Speech SDK `SpeechRecognizer` which raises `Recognizing` (partial) and `Recognized` (final) events that are logged.

Data flow: `AudioDeviceSelector` ? `LoopbackSource` ? `AudioResampler` ? `CapturePump` ? `SpeechPushClient` ? Azure Speech ? console logs


## Requirements

- Windows10/11 (uses WASAPI loopback and Media Foundation)
- .NET8 SDK
- Azure Cognitive Services Speech resource (Region + Key)
- Network access to Azure Speech endpoints


## Quick start

1) Configure Azure Speech key

- Recommended: store the key with .NET user-secrets (scoped to this project directory):

```
dotnet user-secrets init
dotnet user-secrets set "Speech:Key" "<your-speech-key>"
```

- Alternatively, set an environment variable (new console sessions will pick it up):

```
setx AZURE_SPEECH_KEY "<your-speech-key>"
```

2) Configure app settings

- Edit `appsettings.json`:
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

3) Route Teams (or any app) to the selected device

- In Teams: Settings ? Devices ? Speakers: pick the same output device configured above.
- Tip: Use headphones to avoid your microphone picking up playback audio. When enabled, the app logs a reminder on startup.

4) Run

```
dotnet run
```

You should see logs like:
- `[partial] hello there ...`
- `[final] hello there`

Stop with Ctrl+C.


## Configuration reference

- `Settings/AudioOptions.cs`
 - `DeviceNameContains` (string): Optional substring to match a Windows render device friendly name.
 - `TargetSampleRate` (int, default16000): Output sample rate used by the resampler.
 - `TargetBitsPerSample` (int, default16): Bits per sample.
 - `TargetChannels` (int, default1): Channels (mono recommended).
 - `ResamplerQuality` (int, default60): Media Foundation resampler quality (higher = better quality, more CPU).
 - `ChunkMilliseconds` (int, default200): Approximate chunk size pushed to Speech.
 - `EnableHeadphoneReminder` (bool, default true): Prints a one-time tip to use headphones.

- `Settings/SpeechOptions.cs`
 - `Region` (string): Azure Speech region, e.g., `westeurope`.
 - `Language` (string): Recognition language, e.g., `en-GB`.
 - `Key` (string, optional): Speech key. If omitted, the app falls back to `AZURE_SPEECH_KEY` environment variable.

Note: `SpeechPushClient` opens its push stream with16 kHz,16-bit, mono. Keep audio targets aligned with these defaults for best results.


## Key files

- `Program.cs`: Host setup, logging (Serilog), options binding, service wiring, startup checks.
- `Services/AudioDeviceSelector.cs`: Picks the render/output device.
- `Services/LoopbackSource.cs`: WASAPI loopback capture via NAudio.
- `Services/AudioResampler.cs`: Media Foundation resampling/format conversion to PCM16 mono @ target rate.
- `Services/CapturePump.cs`: Main pump reading from loopback/resampler and pushing chunks to Speech.
- `Services/SpeechPushClient.cs`: Azure Speech SDK client using a push stream, emits partial/final logs.
- `appsettings.json`: Configuration.


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



---

Short version: nothing “functional” changed; we only added meters. It started working because the added logging/timing slightly changes scheduling and pacing, which avoids the timing window that previously caused the resampler to latch on a zero-byte read. The pipeline was already correct; the issue was intermittent and timing-sensitive.
What the monitoring does
•	Loopback meter: logs bytes captured per second and current BufferedBytes. Confirms the device is producing audio and the buffer isn’t empty.
•	Resampler meter: logs bytes read per second and counts zero-reads and consecutive zero-reads. This would reveal a stuck condition if consecutiveZeroReads climbed while BufferedBytes stayed > 0.
•	Push meter: logs bytes written to Azure per second. Confirms the SDK is being fed continuously.
Why “just monitoring” can change outcome
•	The counters and logging add tiny, periodic work on the capture and pump threads (Stopwatch checks, Interlocked ops, a log write per second). That micro back-pressure gives the loopback buffer a bit more time to fill before reads, reducing the chance of an initial 0-length read reaching MediaFoundationResampler and triggering an EOS-like state. Classic Heisenbug/timing effect.
How it works now
•	Capture consistently produces ~370 KB/s at 44.1 kHz float stereo, buffer stays non-empty.
•	Resampler outputs ~32–34 KB/s at 16 kHz mono PCM16, with only short, isolated zero-reads (never sustained).
•	Push stream mirrors the resampler throughput, so Azure receives a steady stream and recognition continues.
If you want this stable without relying on logging timing:
•	Make loopback reads never return 0: set BufferedWaveProvider.ReadFully = true.
•	Keep a watchdog: if consecutiveZeroReads stays high while BufferedBytes > 0 for ~1s, rebuild the resampler chain.
•	Optional warm-up: wait until BufferedBytes exceeds a small threshold (e.g., 10–20 ms of audio) before starting the pump.
Those changes remove the timing sensitivity and keep it robust even with diagnostics off.

[15:13:50 INF] Tip: Use headphones and route Teams 'Speakers' to the selected device to avoid local voice bleed.
[15:13:51 INF] Selected render endpoint by substring "Jabra Evolve": Headphones (Jabra Evolve2 65)
[15:13:51 INF] Loopback capture started on: Headphones (Jabra Evolve2 65), Format=32 bit IEEFloat: 44100Hz 2 channels
[15:13:51 INF] Resampler chain ready: 16 bit PCM: 16000Hz 1 channels
[15:13:51 INF] Application started. Press Ctrl+C to shut down.
[15:13:51 INF] Hosting environment: Production
[15:13:51 INF] Speech session started
[15:13:51 INF] Content root path: C:\Users\steph\source\repos\AI\sample\AI-chat-assist\src\TeamsRemoteSTT.App\bin\Debug\net8.0
[15:13:52 INF] [partial] but it
[15:13:52 DBG] Loopback: captured 366912 B/s, buffered 24696 B
[15:13:52 DBG] Resampler: read 37120 B/s, zeroReads 4, consecutiveZeroReads 4, loopbackBuffered 21168 B
[15:13:52 DBG] PushStream: wrote 39040 B/s to Azure
[15:13:52 INF] [partial] but it's a fair
[15:13:53 INF] [partial] but it's a fair balance between
[15:13:53 INF] [partial] but it's a fair balance between the
[15:13:53 DBG] Loopback: captured 370440 B/s, buffered 21168 B
[15:13:53 DBG] Resampler: read 33600 B/s, zeroReads 1, consecutiveZeroReads 1, loopbackBuffered 0 B
[15:13:53 INF] [partial] but it's a fair balance between the rights of individuals
[15:13:53 DBG] PushStream: wrote 33600 B/s to Azure
[15:13:54 INF] [partial] but it's a fair balance between the rights of individuals and
[15:13:54 INF] [partial] but it's a fair balance between the rights of individuals and the interest
[15:13:54 DBG] Loopback: captured 370440 B/s, buffered 21168 B
[15:13:54 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the
[15:13:54 DBG] Resampler: read 32000 B/s, zeroReads 3, consecutiveZeroReads 3, loopbackBuffered 0 B
[15:13:54 DBG] PushStream: wrote 33920 B/s to Azure
[15:13:55 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community
[15:13:55 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large
[15:13:55 DBG] Loopback: captured 373968 B/s, buffered 21168 B
[15:13:55 DBG] Resampler: read 31680 B/s, zeroReads 3, consecutiveZeroReads 3, loopbackBuffered 0 B
[15:13:56 DBG] PushStream: wrote 33920 B/s to Azure
[15:13:56 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to
[15:13:56 DBG] Loopback: captured 370440 B/s, buffered 21168 B
[15:13:56 DBG] Resampler: read 33600 B/s, zeroReads 1, consecutiveZeroReads 1, loopbackBuffered 0 B
[15:13:57 DBG] PushStream: wrote 33600 B/s to Azure
[15:13:57 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article
[15:13:57 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10
[15:13:57 DBG] Resampler: read 32000 B/s, zeroReads 2, consecutiveZeroReads 2, loopbackBuffered 0 B
[15:13:57 DBG] Loopback: captured 373968 B/s, buffered 21168 B
[15:13:58 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not
[15:13:58 DBG] PushStream: wrote 33920 B/s to Azure
[15:13:58 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite
[15:13:58 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so
[15:13:58 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so straight
[15:13:58 DBG] Resampler: read 31680 B/s, zeroReads 3, consecutiveZeroReads 3, loopbackBuffered 0 B
[15:13:58 DBG] Loopback: captured 370440 B/s, buffered 21168 B
[15:13:59 DBG] PushStream: wrote 33600 B/s to Azure
[15:13:59 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so straightforward
[15:13:59 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so straightforward there
[15:13:59 DBG] Resampler: read 31680 B/s, zeroReads 4, consecutiveZeroReads 4, loopbackBuffered 21168 B
[15:14:00 DBG] Loopback: captured 370440 B/s, buffered 21168 B
[15:14:00 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so straightforward there was
[15:14:00 DBG] PushStream: wrote 33600 B/s to Azure
[15:14:00 INF] [partial] but it's a fair balance between the rights of individuals and the interests of the community at large moving to article 10 not quite so straightforward there was 1 case
[15:14:00 DBG] Resampler: read 33600 B/s, zeroReads 1, consecutiveZeroReads 1, loopbackBuffered 0 B
[15:14:01 DBG] Loopback: captured 373968 B/s, buffered 21168 B

