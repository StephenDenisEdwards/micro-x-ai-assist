using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace AudioCapture.Services;

public sealed class MicrophoneSource : IDisposable
{
	private readonly ILogger<MicrophoneSource> _log;
	private readonly Stopwatch _meterSw = new();
	private BufferedWaveProvider? _buffer;
	private long _bytesCapturedThisSecond;
	private WasapiCapture? _capture;

	public MicrophoneSource(ILogger<MicrophoneSource> log)
	{
		_log = log;
	}

	public int BufferedBytes => _buffer?.BufferedBytes ?? 0;

	public void Dispose()
	{
		Stop();
	}

	public IWaveProvider Start(MMDevice device)
	{
		Stop();
		_capture = new WasapiCapture(device);
		_buffer = new BufferedWaveProvider(_capture.WaveFormat)
		{
			DiscardOnBufferOverflow = true,
			ReadFully = false
		};
		_capture.DataAvailable += OnDataAvailable;
		_capture.RecordingStopped += (s, e) =>
		{
			if (e.Exception != null) _log.LogError(e.Exception, "Microphone recording stopped with error");
			else _log.LogInformation("Microphone recording stopped");
		};
		_meterSw.Restart();
		_capture.StartRecording();
		_log.LogInformation("Microphone capture started on: {Name}, Format={Format}", device.FriendlyName,
			_capture.WaveFormat);
		return new BufferedProviderWrapper(_buffer, _capture.WaveFormat);
	}

	public void Stop()
	{
		try
		{
			_capture?.StopRecording();
		}
		catch
		{
		}
		finally
		{
			_capture?.Dispose();
			_capture = null;
			_buffer = null;
			_meterSw.Reset();
			Interlocked.Exchange(ref _bytesCapturedThisSecond, 0);
		}
	}

	private void OnDataAvailable(object? sender, WaveInEventArgs e)
	{
		_buffer!.AddSamples(e.Buffer, 0, e.BytesRecorded);
		Interlocked.Add(ref _bytesCapturedThisSecond, e.BytesRecorded);
		if (_meterSw.ElapsedMilliseconds >= 1000)
		{
			var bps = Interlocked.Exchange(ref _bytesCapturedThisSecond, 0);
			var buffered = _buffer.BufferedBytes;
			_log.LogDebug("Microphone: captured {Bps} B/s, buffered {Buffered} B", bps, buffered);
			_meterSw.Restart();
		}
	}

	private sealed class BufferedProviderWrapper : IWaveProvider
	{
		private readonly BufferedWaveProvider _buffer;

		public BufferedProviderWrapper(BufferedWaveProvider buffer, WaveFormat format)
		{
			_buffer = buffer;
			WaveFormat = format;
		}

		public WaveFormat WaveFormat { get; }

		public int Read(byte[] buffer, int offset, int count)
		{
			return _buffer.Read(buffer, offset, count);
		}
	}
}