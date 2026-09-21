using LibreHardwareMonitor.Hardware;
using SanchesTV.Desktop.Playback;

namespace SanchesTV.Desktop.Diagnostics;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsNetworkEnabled = true
    };
    private bool _opened;

    public string GetSummary()
    {
        EnsureOpen();
        var lines = new List<string>
        {
            $"CPU lógico: {Environment.ProcessorCount}",
            $"Memória do processo: {Environment.WorkingSet / 1_048_576d:N0} MB"
        };

        foreach (var hardware in _computer.Hardware)
        {
            try { hardware.Update(); } catch { }
            lines.Add(hardware.Name);

            foreach (var sensor in EnumerateSensors(hardware))
            {
                if (sensor.Value is not float value)
                    continue;

                if (sensor.SensorType is SensorType.Temperature or SensorType.Load or SensorType.Power or SensorType.Clock)
                    lines.Add($"  {sensor.Name}: {Format(sensor.SensorType, value)}");
            }
        }

        return string.Join(Environment.NewLine, lines.Take(80));
    }

    public PlaybackQualityProfile RecommendPlaybackProfile()
    {
        EnsureOpen();
        var temperatures = new List<float>();
        var loads = new List<float>();

        foreach (var hardware in _computer.Hardware)
        {
            try { hardware.Update(); } catch { }
            foreach (var sensor in EnumerateSensors(hardware))
            {
                if (sensor.Value is not float value)
                    continue;
                if (sensor.SensorType == SensorType.Temperature)
                    temperatures.Add(value);
                else if (sensor.SensorType == SensorType.Load)
                    loads.Add(value);
            }
        }

        var maxTemp = temperatures.DefaultIfEmpty(0).Max();
        var maxLoad = loads.DefaultIfEmpty(0).Max();

        if (maxTemp >= 88 || maxLoad >= 97)
            return PlaybackQualityProfile.LowPower;
        if (maxTemp >= 80 || maxLoad >= 90)
            return PlaybackQualityProfile.Balanced;

        return PlaybackQualityProfile.MaximumQuality;
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
            yield return sensor;

        foreach (var sub in hardware.SubHardware)
        {
            try { sub.Update(); } catch { }
            foreach (var sensor in sub.Sensors)
                yield return sensor;
        }
    }

    private void EnsureOpen()
    {
        if (_opened)
            return;
        _computer.Open();
        _opened = true;
    }

    private static string Format(SensorType type, float value) => type switch
    {
        SensorType.Temperature => $"{value:N1} °C",
        SensorType.Load => $"{value:N0}%",
        SensorType.Power => $"{value:N1} W",
        SensorType.Clock => $"{value:N0} MHz",
        _ => value.ToString("N1")
    };

    public void Dispose()
    {
        if (!_opened)
            return;
        _computer.Close();
        _opened = false;
    }
}
