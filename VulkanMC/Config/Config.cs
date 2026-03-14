using System;
using System.IO;
using System.Globalization;
using Tomlyn;
using Silk.NET.Input;

namespace VulkanMC.Config;

public class ConfigData
{
    public RenderingConfig Rendering { get; set; } = new();
    public PerformanceConfig Performance { get; set; } = new();
    public SafetyConfig Safety { get; set; } = new();
    public CameraConfig Camera { get; set; } = new();
    public DebugConfig Debug { get; set; } = new();
    public PhysicsConfig Physics { get; set; } = new();
    public WindowConfig Window { get; set; } = new();
    public ControlsConfig Controls { get; set; } = new();
    public HardwareConfig Hardware { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class LoggingConfig
{
    public string Level { get; set; } = "Info";
    public bool EnableConsoleColors { get; set; } = true;
    public bool IncludeTimestamp { get; set; } = true;
    public bool IncludeThreadId { get; set; } = false;
    public bool UseUtcTimestamp { get; set; } = false;
    public string TimestampFormat { get; set; } = "HH:mm:ss.fff";
    public bool EnableFileLogging { get; set; } = false;
    public string FilePath { get; set; } = "logs/vulkanmc.log";
    public bool AppendToFile { get; set; } = true;
    public int FlushIntervalMs { get; set; } = 250;
    public int MaxFileSizeMB { get; set; } = 25;
    public int MaxRetainedFiles { get; set; } = 5;
    public int AsyncQueueCapacity { get; set; } = 4096;
    public bool DropMessagesWhenQueueFull { get; set; } = true;
    public int DuplicateSuppressionMs { get; set; } = 0;
    public bool EnableMetricsSummaryLogs { get; set; } = false;
    public int MetricsSummaryIntervalMs { get; set; } = 5000;
}

public class RenderingConfig
{
    public int RenderDistanceThreshold { get; set; } = 24;
    public int ChunkSize { get; set; } = 16;
    public int WorldSeed { get; set; } = 42;
    public float FieldOfViewRadians { get; set; } = 1.0f;
    public float NearPlane { get; set; } = 0.1f;
    public bool EnableTrees { get; set; } = true;
    public bool EnableEntities { get; set; } = true;

    public float FarPlane { get; set; } = 1000.0f;
    public bool UseShaders { get; set; } = true;
}

public class PerformanceConfig
{
    public bool UseVSync { get; set; } = true;
    public int MaxFps { get; set; } = 0;
    public int UpdatesPerSecond { get; set; } = 60;
    public int ChunkUpdateIntervalMs { get; set; } = 100;
    public int ChunkUploadBudgetPerPass { get; set; } = 6;
    public int MaxEffectiveRenderDistance { get; set; } = 12;
}

public class SafetyConfig
{
    public bool EnableResourceGuard { get; set; } = true;
    public float CpuUsageSoftLimitPercent { get; set; } = 90.0f;
    public float RamUsageSoftLimitPercent { get; set; } = 90.0f;
    public float GpuUsageSoftLimitPercent { get; set; } = 95.0f;
    public int MaxLoadedChunks { get; set; } = 900;
    public int UnloadBatchSize { get; set; } = 24;
    public int PressureCooldownMs { get; set; } = 200;
    public int MaxPendingUploadActions { get; set; } = 256;
}

public class CameraConfig
{
    public float MouseSensitivity { get; set; } = 0.1f;
    public float MinPitch { get; set; } = -89.0f;
    public float MaxPitch { get; set; } = 89.0f;
    public bool LockCursorWhenPlaying { get; set; } = true;
}

public class DebugConfig
{
    public bool ShowOverlay { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public bool ShowChunk { get; set; } = true;
    public bool ShowFpsInWindowTitle { get; set; } = false;
    public bool ShowChunkCount { get; set; } = true;
    public bool ShowVertexCount { get; set; } = true;
}

public class PhysicsConfig
{
    // BaseMovementSpeed is expressed as units per second scaled by the update dt.
    // Values tuned to approximate Minecraft feel at ~60 UPS.
    public float BaseMovementSpeed { get; set; } = 4.317f;
    public float SprintMultiplier { get; set; } = 1.3f;
    public float CrouchMultiplier { get; set; } = 0.3f;
    public float JumpForce { get; set; } = 5.0f;
    public float Gravity { get; set; } = 30.0f;
    public float Drag { get; set; } = 10.0f;
    public bool GravityEnabled { get; set; } = true;
    public bool AutoJump { get; set; } = true;
}

public class WindowConfig
{
    public bool Fullscreen { get; set; } = false;
    public bool Maximized { get; set; } = true;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public string Title { get; set; } = "VulkanMC Engine";
}

public class ControlsConfig
{
    public string Forward { get; set; } = "W";
    public string Backward { get; set; } = "S";
    public string Left { get; set; } = "A";
    public string Right { get; set; } = "D";
    public string Jump { get; set; } = "Space";
    public string Sprint { get; set; } = "LeftCtrl";
    public string Crouch { get; set; } = "LeftShift";
    public string Escape { get; set; } = "Escape";
    public string QuickExit { get; set; } = "Delete";
    // Minecraft defaults: Inventory = E, Drop = Q, Chat = T, PlayerList = Tab, ToggleDebug = F3
    public string Inventory { get; set; } = "E";
    public string Drop { get; set; } = "Q";
    public string Chat { get; set; } = "T";
    public string PlayerList { get; set; } = "Tab";
    public string ToggleDebug { get; set; } = "F3";

    public Key ForwardKey => ParseKey(Forward, Key.W);
    public Key BackwardKey => ParseKey(Backward, Key.S);
    public Key LeftKey => ParseKey(Left, Key.A);
    public Key RightKey => ParseKey(Right, Key.D);
    public Key JumpKey => ParseKey(Jump, Key.Space);
    public Key SprintKey
    {
        get
        {
            var parsed = ParseKey(Sprint, default);
            if (!EqualityComparer<Key>.Default.Equals(parsed, default))
                return parsed;

            foreach (Key k in Enum.GetValues<Key>())
            {
                string name = k.ToString();
                if (name.IndexOf("Ctrl", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Control", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return k;
                }
            }

            return Key.ShiftLeft;
        }
    }
    public Key CrouchKey => ParseKey(Crouch, Key.ShiftLeft);
    public Key EscapeKey => ParseKey(Escape, Key.Escape);
    public Key QuickExitKey => ParseKey(QuickExit, Key.Delete);
    public Key InventoryKey => ParseKey(Inventory, Key.E);
    public Key DropKey => ParseKey(Drop, Key.Q);
    public Key ChatKey => ParseKey(Chat, Key.T);
    public Key PlayerListKey => ParseKey(PlayerList, Key.Tab);
    public Key ToggleDebugKey => ParseKey(ToggleDebug, Key.F3);

    private static Key ParseKey(string? value, Key fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string trimmed = value.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int legacyInt))
        {
            if (Enum.IsDefined(typeof(Key), legacyInt))
            {
                return (Key)legacyInt;
            }
            return fallback;
        }

        string normalized = trimmed.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        foreach (Key key in Enum.GetValues<Key>())
        {
            if (string.Equals(key.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return fallback;
    }

    // Attempt to verify that configured control names exist in System.Windows.Input.Key (if available).
    // This is informational only; it will log a warning if PresentationCore is not available or a name isn't found.
    public void VerifyAgainstWindowsKeyList()
    {
        try
        {
            var keyType = Type.GetType("System.Windows.Input.Key, PresentationCore");
            if (keyType == null)
            {
                Logger.Warning("PresentationCore Key enum not available; skipping Windows Key verification.");
                return;
            }

            string[] toCheck = new[] { Forward, Backward, Left, Right, Jump, Sprint, Crouch, Escape, QuickExit, Inventory, Drop, Chat, PlayerList, ToggleDebug };
            foreach (var s in toCheck)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                string normalized = s.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
                bool found = false;
                foreach (var name in Enum.GetNames(keyType))
                {
                    if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Logger.Warning($"Configured control key '{s}' was not found in System.Windows.Input.Key enum.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Windows Key verification failed: {ex.Message}");
        }
    }

    
}

public class HardwareConfig
{
    public string? PreferredGpuName { get; set; }
    public string PreferredGpuVendor { get; set; } = "Any";
    public string PreferredDeviceType { get; set; } = "DiscreteGpu";
    public bool PreferDiscreteGpu { get; set; } = true;
    public bool AllowIntegratedGpuFallback { get; set; } = true;
    public bool PreferMailboxPresentMode { get; set; } = true;
    public bool EnableTextureStreaming { get; set; } = false;
    public int TextureBudgetMB { get; set; } = 1024;
    public int MeshBudgetMB { get; set; } = 1024;
    public float MaxAnisotropy { get; set; } = 16.0f;
    public bool EnableFrustumCulling { get; set; } = true;
    public bool EnableOcclusionCulling { get; set; } = false;
    public bool DynamicResolutionEnabled { get; set; } = false;
    public float DynamicResolutionMinScale { get; set; } = 0.5f;
    public float DynamicResolutionMaxScale { get; set; } = 1.0f;
    public float TargetGpuUsagePercent { get; set; } = 92.0f;
    public float ThermalThrottleCpuCelsius { get; set; } = 90.0f;
    public float ThermalThrottleGpuCelsius { get; set; } = 88.0f;
    public string ProcessPriorityClass { get; set; } = "Normal";
    public string CpuAffinityMask { get; set; } = "Auto";
    public bool ForceCpuMetricsPolling { get; set; } = false;
    public bool ForceGpuMetricsPolling { get; set; } = false;
    public bool ForceVulkanValidationLayers { get; set; } = false;
}

public static class Config
{
    private const string ConfigPath = "config.toml";
    public static ConfigData Data { get; private set; } = new();

    static Config()
    {
        Load();
    }

    public static void Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string toml = File.ReadAllText(ConfigPath);
                var model = Tomlyn.TomlSerializer.Deserialize<ConfigData>(toml);
                if (model != null)
                {
                    Data = model;
                    Sanitize();
                    Logger.Configure(Data.Logging);
                    // Verify control names against Windows Key enum when possible (informational)
                    try { Data.Controls.VerifyAgainstWindowsKeyList(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load config: {ex.Message}. Using defaults.");
                Data = new ConfigData();
                Logger.Configure(Data.Logging);
            }
        }
        else
        {
            Logger.Configure(Data.Logging);
            Save();
        }
    }

    public static void Save()
    {
        try
        {
            var options = new Tomlyn.TomlSerializerOptions { WriteIndented = true };
            string toml = Tomlyn.TomlSerializer.Serialize(Data, options);
            File.WriteAllText(ConfigPath, toml);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save config: {ex.Message}");
        }
    }

    private static void Sanitize()
    {
        Data.Rendering.ChunkSize = Math.Clamp(Data.Rendering.ChunkSize, 4, 64);
        Data.Rendering.RenderDistanceThreshold = Math.Clamp(Data.Rendering.RenderDistanceThreshold, 2, 256);
        Data.Rendering.FieldOfViewRadians = Math.Clamp(Data.Rendering.FieldOfViewRadians, 0.3f, 2.6f);
        Data.Rendering.NearPlane = Math.Clamp(Data.Rendering.NearPlane, 0.01f, 10.0f);
        Data.Rendering.FarPlane = Math.Clamp(Data.Rendering.FarPlane, 100.0f, 5000.0f);

        Data.Performance.UpdatesPerSecond = Math.Clamp(Data.Performance.UpdatesPerSecond, 20, 240);
        Data.Performance.ChunkUpdateIntervalMs = Math.Clamp(Data.Performance.ChunkUpdateIntervalMs, 25, 1000);
        Data.Performance.ChunkUploadBudgetPerPass = Math.Clamp(Data.Performance.ChunkUploadBudgetPerPass, 1, 64);
        Data.Performance.MaxEffectiveRenderDistance = Math.Clamp(Data.Performance.MaxEffectiveRenderDistance, 2, 128);

        Data.Safety.CpuUsageSoftLimitPercent = Math.Clamp(Data.Safety.CpuUsageSoftLimitPercent, 20.0f, 100.0f);
        Data.Safety.RamUsageSoftLimitPercent = Math.Clamp(Data.Safety.RamUsageSoftLimitPercent, 20.0f, 100.0f);
        Data.Safety.GpuUsageSoftLimitPercent = Math.Clamp(Data.Safety.GpuUsageSoftLimitPercent, 20.0f, 100.0f);
        Data.Safety.MaxLoadedChunks = Math.Clamp(Data.Safety.MaxLoadedChunks, 64, 8192);
        Data.Safety.UnloadBatchSize = Math.Clamp(Data.Safety.UnloadBatchSize, 1, 256);
        Data.Safety.PressureCooldownMs = Math.Clamp(Data.Safety.PressureCooldownMs, 100, 5000);
        Data.Safety.MaxPendingUploadActions = Math.Clamp(Data.Safety.MaxPendingUploadActions, 16, 4096);

        Data.Logging.FlushIntervalMs = Math.Clamp(Data.Logging.FlushIntervalMs, 10, 5000);
        Data.Logging.MaxFileSizeMB = Math.Clamp(Data.Logging.MaxFileSizeMB, 1, 2048);
        Data.Logging.MaxRetainedFiles = Math.Clamp(Data.Logging.MaxRetainedFiles, 1, 200);
        Data.Logging.AsyncQueueCapacity = Math.Clamp(Data.Logging.AsyncQueueCapacity, 128, 1_000_000);
        Data.Logging.DuplicateSuppressionMs = Math.Clamp(Data.Logging.DuplicateSuppressionMs, 0, 60000);
        Data.Logging.MetricsSummaryIntervalMs = Math.Clamp(Data.Logging.MetricsSummaryIntervalMs, 250, 600000);

        Data.Hardware.TextureBudgetMB = Math.Clamp(Data.Hardware.TextureBudgetMB, 64, 16384);
        Data.Hardware.MeshBudgetMB = Math.Clamp(Data.Hardware.MeshBudgetMB, 64, 16384);
        Data.Hardware.MaxAnisotropy = Math.Clamp(Data.Hardware.MaxAnisotropy, 1.0f, 16.0f);
        Data.Hardware.DynamicResolutionMinScale = Math.Clamp(Data.Hardware.DynamicResolutionMinScale, 0.3f, 1.0f);
        Data.Hardware.DynamicResolutionMaxScale = Math.Clamp(Data.Hardware.DynamicResolutionMaxScale, 0.3f, 1.0f);
        if (Data.Hardware.DynamicResolutionMinScale > Data.Hardware.DynamicResolutionMaxScale)
        {
            Data.Hardware.DynamicResolutionMinScale = Data.Hardware.DynamicResolutionMaxScale;
        }
        Data.Hardware.TargetGpuUsagePercent = Math.Clamp(Data.Hardware.TargetGpuUsagePercent, 40.0f, 99.0f);
        Data.Hardware.ThermalThrottleCpuCelsius = Math.Clamp(Data.Hardware.ThermalThrottleCpuCelsius, 50.0f, 110.0f);
        Data.Hardware.ThermalThrottleGpuCelsius = Math.Clamp(Data.Hardware.ThermalThrottleGpuCelsius, 50.0f, 110.0f);

        Data.Camera.MouseSensitivity = Math.Clamp(Data.Camera.MouseSensitivity, 0.01f, 2.0f);
        Data.Camera.MinPitch = Math.Clamp(Data.Camera.MinPitch, -89.9f, 0.0f);
        Data.Camera.MaxPitch = Math.Clamp(Data.Camera.MaxPitch, 0.0f, 89.9f);
        if (Data.Camera.MinPitch >= Data.Camera.MaxPitch)
        {
            Data.Camera.MinPitch = -89.0f;
            Data.Camera.MaxPitch = 89.0f;
        }

        Data.Window.Width = Math.Clamp(Data.Window.Width, 640, 7680);
        Data.Window.Height = Math.Clamp(Data.Window.Height, 360, 4320);
        // Physics sane ranges
        // Limit base movement speed to a reasonable maximum to avoid extreme config values
        Data.Physics.BaseMovementSpeed = Math.Clamp(Data.Physics.BaseMovementSpeed, 0.1f, 20.0f);
        Data.Physics.SprintMultiplier = Math.Clamp(Data.Physics.SprintMultiplier, 1.0f, 3.0f);
        Data.Physics.CrouchMultiplier = Math.Clamp(Data.Physics.CrouchMultiplier, 0.05f, 1.0f);
        Data.Physics.JumpForce = Math.Clamp(Data.Physics.JumpForce, 1.0f, 20.0f);
        Data.Physics.Gravity = Math.Clamp(Data.Physics.Gravity, 1.0f, 200.0f);
    }
}

