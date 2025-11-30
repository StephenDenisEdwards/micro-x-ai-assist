namespace GeminiLiveConsole;

public interface IAudioCaptureService: IDisposable
{
	event Action<byte[]>? OnAudioChunk;
	void SetSource(AudioInputSource source);
	void Start();
	void Stop();
}