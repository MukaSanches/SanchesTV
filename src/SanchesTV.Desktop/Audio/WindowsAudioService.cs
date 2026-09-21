using NAudio.CoreAudioApi;

namespace SanchesTV.Desktop.Audio;

public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    bool IsDefault,
    float Peak,
    float MasterVolume);

public sealed class WindowsAudioService
{
    public string NAudioVersion =>
        typeof(MMDeviceEnumerator).Assembly.GetName().Version?.ToString() ?? "3.x";

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var defaultId = defaultDevice.ID;

            var result = new List<AudioDeviceInfo>();
            using var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                try
                {
                    result.Add(new AudioDeviceInfo(
                        device.ID,
                        device.FriendlyName,
                        string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase),
                        device.AudioMeterInformation.MasterPeakValue,
                        device.AudioEndpointVolume.MasterVolumeLevelScalar));
                }
                catch
                {
                    result.Add(new AudioDeviceInfo(
                        device.ID,
                        device.FriendlyName,
                        string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase),
                        0,
                        0));
                }
                finally
                {
                    device.Dispose();
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<AudioDeviceInfo>();
        }
    }

    public string GetSummary()
    {
        var devices = GetRenderDevices();
        var defaultDevice = devices.FirstOrDefault(x => x.IsDefault);

        return $"NAudio {NAudioVersion} / WASAPI\n" +
               $"Saídas ativas: {devices.Count}\n" +
               $"Padrão: {defaultDevice?.Name ?? "não detectado"}";
    }
}
