using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace VulkanMC.Platform;

public readonly record struct SystemMetrics(float? CpuUsagePercent, float? GpuUsagePercent, float? RamUsagePercent)
{
    public static readonly SystemMetrics Empty = new(null, null, null);
}

public interface ISystemMetricsProvider
{
    SystemMetrics GetMetrics();
}

public static class SystemMetricsProviderFactory
{
    public static ISystemMetricsProvider Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxSystemMetricsProvider();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSystemMetricsProvider();
        }

        return new NullSystemMetricsProvider();
    }
}

internal sealed class LinuxSystemMetricsProvider : ISystemMetricsProvider
{
    private readonly string[] _gpuBusyPercentPaths;
    private CpuTimes? _previousCpuTimes;

    public LinuxSystemMetricsProvider()
    {
        _gpuBusyPercentPaths = DiscoverGpuBusyPercentPaths();
    }

    public SystemMetrics GetMetrics()
    {
        float? cpu = ReadCpuUsage();
        float? gpu = ReadGpuUsage();
        float? ram = ReadRamUsage();
        return new SystemMetrics(cpu, gpu, ram);
    }

    private float? ReadCpuUsage()
    {
        const string statPath = "/proc/stat";
        if (!File.Exists(statPath))
        {
            return null;
        }

        string? line = File.ReadLines(statPath).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts[0] != "cpu")
        {
            return null;
        }

        ulong user = ParseUlong(parts, 1);
        ulong nice = ParseUlong(parts, 2);
        ulong system = ParseUlong(parts, 3);
        ulong idle = ParseUlong(parts, 4);
        ulong iowait = ParseUlong(parts, 5);
        ulong irq = ParseUlong(parts, 6);
        ulong softirq = ParseUlong(parts, 7);
        ulong steal = ParseUlong(parts, 8);

        var current = new CpuTimes(user + nice + system + idle + iowait + irq + softirq + steal, idle + iowait);
        if (_previousCpuTimes is null)
        {
            _previousCpuTimes = current;
            return null;
        }

        ulong totalDelta = current.Total - _previousCpuTimes.Value.Total;
        ulong idleDelta = current.Idle - _previousCpuTimes.Value.Idle;
        _previousCpuTimes = current;

        if (totalDelta == 0)
        {
            return null;
        }

        float usage = 100.0f * (1.0f - (idleDelta / (float)totalDelta));
        return Math.Clamp(usage, 0.0f, 100.0f);
    }

    private float? ReadRamUsage()
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath))
        {
            return null;
        }

        ulong totalKb = 0;
        ulong availableKb = 0;

        foreach (string line in File.ReadLines(memInfoPath))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKb = ParseMemInfoKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKb = ParseMemInfoKb(line);
            }

            if (totalKb > 0 && availableKb > 0)
            {
                break;
            }
        }

        if (totalKb == 0 || availableKb == 0)
        {
            return null;
        }

        float usage = 100.0f * (1.0f - (availableKb / (float)totalKb));
        return Math.Clamp(usage, 0.0f, 100.0f);
    }

    private float? ReadGpuUsage()
    {
        if (_gpuBusyPercentPaths.Length == 0)
        {
            return null;
        }

        float maxUsage = -1.0f;
        foreach (string path in _gpuBusyPercentPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string text = File.ReadAllText(path).Trim();
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float usage))
            {
                maxUsage = Math.Max(maxUsage, usage);
            }
        }

        if (maxUsage < 0.0f)
        {
            return null;
        }

        return Math.Clamp(maxUsage, 0.0f, 100.0f);
    }

    private static string[] DiscoverGpuBusyPercentPaths()
    {
        const string drmPath = "/sys/class/drm";
        if (!Directory.Exists(drmPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(drmPath, "card*")
            .Select(cardDir => Path.Combine(cardDir, "device", "gpu_busy_percent"))
            .Where(File.Exists)
            .ToArray();
    }

    private static ulong ParseMemInfoKb(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : 0;
    }

    private static ulong ParseUlong(string[] parts, int index)
    {
        return parts.Length > index && ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : 0;
    }

    private readonly record struct CpuTimes(ulong Total, ulong Idle);
}

internal sealed class WindowsSystemMetricsProvider : ISystemMetricsProvider
{
    private readonly Computer _computer;

    public WindowsSystemMetricsProvider()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsControllerEnabled = false,
            IsBatteryEnabled = false,
            IsPsuEnabled = false
        };
        _computer.Open();
    }

    public SystemMetrics GetMetrics()
    {
        try
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                UpdateHardware(hardware);
            }

            float? cpu = ReadCpuUsage();
            float? gpu = ReadGpuUsage();
            float? ram = ReadRamUsage();
            return new SystemMetrics(cpu, gpu, ram);
        }
        catch
        {
            return SystemMetrics.Empty;
        }
    }

    private float? ReadCpuUsage()
    {
        return _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.Cpu)
            .SelectMany(GetAllSensors)
            .Where(s => s.SensorType == SensorType.Load)
            .Where(s => s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Cast<float?>()
            .FirstOrDefault();
    }

    private float? ReadGpuUsage()
    {
        var values = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuIntel)
            .SelectMany(GetAllSensors)
            .Where(s => s.SensorType == SensorType.Load)
            .Where(s =>
                s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        if (values.Length == 0)
        {
            return null;
        }

        return values.Max();
    }

    private float? ReadRamUsage()
    {
        float? directLoad = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.Memory)
            .SelectMany(GetAllSensors)
            .Where(s => s.SensorType == SensorType.Load)
            .Where(s => s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Cast<float?>()
            .FirstOrDefault();

        if (directLoad.HasValue)
        {
            return directLoad.Value;
        }

        float? usedGb = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.Memory)
            .SelectMany(GetAllSensors)
            .Where(s => s.SensorType == SensorType.Data)
            .Where(s => s.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Cast<float?>()
            .FirstOrDefault();

        float? availableGb = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.Memory)
            .SelectMany(GetAllSensors)
            .Where(s => s.SensorType == SensorType.Data)
            .Where(s => s.Name.Contains("Memory Available", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Cast<float?>()
            .FirstOrDefault();

        if (usedGb.HasValue && availableGb.HasValue && usedGb.Value + availableGb.Value > 0.0f)
        {
            return Math.Clamp(100.0f * usedGb.Value / (usedGb.Value + availableGb.Value), 0.0f, 100.0f);
        }

        return null;
    }

    private static void UpdateHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
        {
            UpdateHardware(subHardware);
        }
    }

    private static ISensor[] GetAllSensors(IHardware hardware)
    {
        return hardware.Sensors.Concat(hardware.SubHardware.SelectMany(GetAllSensors)).ToArray();
    }
}

internal sealed class NullSystemMetricsProvider : ISystemMetricsProvider
{
    public SystemMetrics GetMetrics()
    {
        return SystemMetrics.Empty;
    }
}