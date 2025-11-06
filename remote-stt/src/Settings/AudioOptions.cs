namespace TeamsRemoteSTT.App.Settings;

public sealed class AudioOptions
{
    public string? DeviceNameContains { get; set; }
    public string? MicrophoneNameContains { get; set; }
    public int TargetSampleRate { get; set; } = 16000;
    public int TargetBitsPerSample { get; set; } = 16;
    public int TargetChannels { get; set; } = 1;
    public int ResamplerQuality { get; set; } = 60;
    public int ChunkMilliseconds { get; set; } = 200;
    public bool EnableHeadphoneReminder { get; set; } = true;
}