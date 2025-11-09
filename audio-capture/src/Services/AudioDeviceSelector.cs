using AudioCapture.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;

namespace AudioCapture.Services;

public sealed class AudioDeviceSelector
{
	private readonly MMDeviceEnumerator _enumerator = new();
	private readonly ILogger<AudioDeviceSelector> _log;
	private readonly AudioOptions _opts;

	public AudioDeviceSelector(ILogger<AudioDeviceSelector> log, IOptions<AudioOptions> opts)
	{
		_log = log;
		_opts = opts.Value;
	}

	public MMDevice SelectRenderDevice()
	{
		var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
		if (devices.Count == 0) throw new InvalidOperationException("No active render endpoints found.");

		if (!string.IsNullOrWhiteSpace(_opts.DeviceNameContains))
		{
			var device = devices.FirstOrDefault(d =>
				d.FriendlyName.Contains(_opts.DeviceNameContains, StringComparison.OrdinalIgnoreCase));
			if (device is not null)
			{
				_log.LogInformation("Selected render endpoint by substring \"{Sub}\": {Name}", _opts.DeviceNameContains,
					device.FriendlyName);
				return device;
			}
		}

		var def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		_log.LogInformation("Selected default render endpoint: {Name}", def.FriendlyName);
		return def;
	}

	public MMDevice SelectCaptureDevice()
	{
		var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
		if (devices.Count == 0) throw new InvalidOperationException("No active capture endpoints found.");

		if (!string.IsNullOrWhiteSpace(_opts.MicrophoneNameContains))
		{
			var device = devices.FirstOrDefault(d =>
				d.FriendlyName.Contains(_opts.MicrophoneNameContains, StringComparison.OrdinalIgnoreCase));
			if (device is not null)
			{
				_log.LogInformation("Selected capture endpoint by substring \"{Sub}\": {Name}",
					_opts.MicrophoneNameContains, device.FriendlyName);
				return device;
			}
		}

		// Prefer Communications default, but fall back to Multimedia if not available
		try
		{
			var defComm = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
			_log.LogInformation("Selected default capture endpoint (Communications): {Name}", defComm.FriendlyName);
			return defComm;
		}
		catch (Exception ex)
		{
			_log.LogDebug(ex, "No default Communications capture endpoint; falling back to Multimedia.");
			var defMulti = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
			_log.LogInformation("Selected default capture endpoint (Multimedia): {Name}", defMulti.FriendlyName);
			return defMulti;
		}
	}
}