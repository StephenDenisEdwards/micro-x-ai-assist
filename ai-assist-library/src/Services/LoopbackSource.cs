using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;
using System.Threading;

namespace AiAssistLibrary.Services;

public sealed class LoopbackSource : IDisposable
{
 private readonly ILogger<LoopbackSource> _log;
 private WasapiLoopbackCapture? _capture;
 private BufferedWaveProvider? _buffer;
 private readonly Stopwatch _meterSw = new();
 private long _bytesCapturedThisSecond;

 public LoopbackSource(ILogger<LoopbackSource> log) { _log = log; }

 public int BufferedBytes => _buffer?.BufferedBytes ??0;

 public IWaveProvider Start(MMDevice device)
 {
 Stop();
 _capture = new WasapiLoopbackCapture(device);
 _buffer = new BufferedWaveProvider(_capture.WaveFormat)
 {
 DiscardOnBufferOverflow = true,
 ReadFully = false
 };
 _capture.DataAvailable += OnDataAvailable;
 _capture.RecordingStopped += (s,e)=>
 {
 if (e.Exception != null) _log.LogError(e.Exception, "Loopback recording stopped with error");
 else _log.LogInformation("Loopback recording stopped");
 };
 _meterSw.Restart();
 _capture.StartRecording();
 _log.LogInformation("Loopback capture started on: {Name}, Format={Format}", device.FriendlyName, _capture.WaveFormat);
 return new BufferedProviderWrapper(_buffer, _capture.WaveFormat);
 }

 public void Stop()
 {
 try { _capture?.StopRecording(); } catch { }
 finally
 {
 _capture?.Dispose();
 _capture = null;
 _buffer = null;
 _meterSw.Reset();
 Interlocked.Exchange(ref _bytesCapturedThisSecond,0);
 }
 }

 public void Dispose() => Stop();

 private sealed class BufferedProviderWrapper : IWaveProvider
 {
 private readonly BufferedWaveProvider _buffer;
 public WaveFormat WaveFormat { get; }
 public BufferedProviderWrapper(BufferedWaveProvider buffer, WaveFormat format){ _buffer = buffer; WaveFormat = format; }
 public int Read(byte[] buffer, int offset, int count)=> _buffer.Read(buffer, offset, count);
 }

 private void OnDataAvailable(object? sender, WaveInEventArgs e)
 {
 _buffer!.AddSamples(e.Buffer,0, e.BytesRecorded);
 Interlocked.Add(ref _bytesCapturedThisSecond, e.BytesRecorded);
 if (_meterSw.ElapsedMilliseconds >=1000)
 {
 var bps = Interlocked.Exchange(ref _bytesCapturedThisSecond,0);
 var buffered = _buffer.BufferedBytes;
 _log.LogDebug("Loopback: captured {Bps} B/s, buffered {Buffered} B", bps, buffered);
 _meterSw.Restart();
 }
 }
}
