namespace AiAssistLibrary.Settings;

public sealed class SpeechOptions
{
	public string Region { get; set; } = "";
	public string Language { get; set; } = "";
	public string? Key { get; set; }
	// Added: silence timeout for segmentation/finalization (ms)
	public int SegmentationSilenceTimeoutMs { get; set; } = 500;
	public int InitialSilenceTimeoutMs { get; set; } = 5000;
}
