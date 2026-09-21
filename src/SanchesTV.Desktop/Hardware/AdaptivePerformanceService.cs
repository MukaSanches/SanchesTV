using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using SanchesTV.Desktop.Playback;

namespace SanchesTV.Desktop.Hardware;

public sealed record HardwareSnapshot(
    string CpuName,
    string GpuName,
    float? CpuLoad,
    float? CpuTemperature,
    float? GpuLoad,
    float? GpuTemperature,
    float? MemoryLoad,
    bool OnBattery)
{
    public string Summary => string.Join(Environment.NewLine,
        $"CPU: {CpuName} • carga {(CpuLoad is null ? "?" : CpuLoad.Value.ToString("0") + "%")} • {(CpuTemperature is null ? "temp ?" : CpuTemperature.Value.ToString("0") + " °C")}",
        $"GPU: {GpuName} • carga {(GpuLoad is null ? "?" : GpuLoad.Value.ToString("0") + "%")} • {(GpuTemperature is null ? "temp ?" : GpuTemperature.Value.ToString("0") + " °C")}",
        $"Memória: {(MemoryLoad is null ? "?" : MemoryLoad.Value.ToString("0") + "%")} • energia: {(OnBattery ? "bateria" : "rede")}");
}

public sealed class AdaptivePerformanceService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true
    };
    private bool _opened;

    public HardwareSnapshot Read()
    {
        EnsureOpen();

        string cpu = "CPU";
        string gpu = "GPU";
        float? cpuLoad = null, cpuTemp = null, gpuLoad = null, gpuTemp = null, memoryLoad = null;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Update();

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                cpu = hardware.Name;
                cpuLoad = Pick(hardware, SensorType.Load, "CPU Total") ?? Pick(hardware, SensorType.Load, null);
                cpuTemp = Pick(hardware, SensorType.Temperature, "CPU Package") ?? Pick(hardware, SensorType.Temperature, null);
            }
            else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            {
                gpu = hardware.Name;
                gpuLoad = Pick(hardware, SensorType.Load, "GPU Core") ?? Pick(hardware, SensorType.Load, null);
                gpuTemp = Pick(hardware, SensorType.Temperature, "GPU Core") ?? Pick(hardware, SensorType.Temperature, null);
            }
            else if (hardware.HardwareType == HardwareType.Memory)
            {
                memoryLoad = Pick(hardware, SensorType.Load, "Memory") ?? Pick(hardware, SensorType.Load, null);
            }
        }

        return new HardwareSnapshot(cpu, gpu, cpuLoad, cpuTemp, gpuLoad, gpuTemp, memoryLoad, IsOnBattery());
    }

    public PlaybackQualityProfile Recommend(HardwareSnapshot snapshot)
    {
        if (snapshot.OnBattery)
            return PlaybackQualityProfile.LowPower;

        if ((snapshot.CpuTemperature ?? 0) >= 88 || (snapshot.GpuTemperature ?? 0) >= 86)
            return PlaybackQualityProfile.LowPower;

        if ((snapshot.CpuLoad ?? 0) >= 92 || (snapshot.GpuLoad ?? 0) >= 97)
            return PlaybackQualityProfile.Balanced;

        if ((snapshot.GpuLoad ?? 0) <= 75 && (snapshot.GpuTemperature ?? 0) <= 78)
            return PlaybackQualityProfile.MaximumQuality;

        return PlaybackQualityProfile.Balanced;
    }

    private void EnsureOpen()
    {
        if (_opened)
            return;
        _computer.Open();
        _opened = true;
    }

    private static float? Pick(IHardware hardware, SensorType type, string? name)
    {
        return hardware.Sensors
            .Where(x => x.SensorType == type)
            .Where(x => name is null || x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .FirstOrDefault(x => x.HasValue);
    }

    private static bool IsOnBattery()
    {
        if (!GetSystemPowerStatus(out var status))
            return false;
        return status.ACLineStatus == 0;
    }

    public void Dispose()
    {
        if (_opened)
            _computer.Close();
        _opened = false;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
