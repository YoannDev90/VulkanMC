using System;
using System.IO;
using Tomlyn;
using Silk.NET.Input;

namespace VulkanMC;

public class ConfigData
{
    public RenderingConfig Rendering { get; set; } = new();
    public PhysicsConfig Physics { get; set; } = new();
    public WindowConfig Window { get; set; } = new();
    public ControlsConfig Controls { get; set; } = new();
    public HardwareConfig Hardware { get; set; } = new();
}

public class RenderingConfig
{
    public int RenderDistanceThreshold { get; set; } = 24;
    public int ChunkSize { get; set; } = 16;
    public int WorldSeed { get; set; } = 42;
}

public class PhysicsConfig
{
    public float BaseMovementSpeed { get; set; } = 150.0f;
    public float SprintMultiplier { get; set; } = 3.0f;
    public float JumpForce { get; set; } = 8.0f;
    public float Gravity { get; set; } = 25.0f;
    public float Drag { get; set; } = 10.0f;
    public bool GravityEnabled { get; set; } = true;
    public bool AutoJump { get; set; } = true;
}

public class WindowConfig
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public string Title { get; set; } = "VulkanMC Engine";
}

public class ControlsConfig
{
    public Key Forward { get; set; } = Key.W;
    public Key Backward { get; set; } = Key.S;
    public Key Left { get; set; } = Key.A;
    public Key Right { get; set; } = Key.D;
    public Key Jump { get; set; } = Key.Space;
    public Key Sprint { get; set; } = Key.ShiftLeft;
    public Key Escape { get; set; } = Key.Escape;
    public Key QuickExit { get; set; } = Key.Delete;
}

public class HardwareConfig
{
    public string? PreferredGpuName { get; set; }
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
                if (model != null) Data = model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load config: {ex.Message}. Using defaults.");
                Data = new ConfigData();
            }
        }
        else
        {
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
            Console.WriteLine($"[ERROR] Failed to save config: {ex.Message}");
        }
    }
}

