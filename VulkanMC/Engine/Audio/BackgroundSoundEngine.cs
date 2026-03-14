using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VulkanMC.Engine.Audio;

public static class BackgroundSoundEngine
{
    private static Process? _player;

    public static void Init()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            var soundsDir = Path.Combine(baseDir, "Sounds");
            if (!Directory.Exists(soundsDir))
            {
                Logger.Info("No Sounds directory found; background audio disabled.");
                return;
            }

            // Prefer ambient subfolder if present
            var ambientDir = Path.Combine(soundsDir, "ambient");
            var searchDir = Directory.Exists(ambientDir) ? ambientDir : soundsDir;

            var files = Directory.EnumerateFiles(searchDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length == 0)
            {
                Logger.Info("No audio files found in Sounds folder; background audio disabled.");
                return;
            }

            // Choose a random ambient file to loop
            var rnd = new Random();
            var choice = files[rnd.Next(files.Length)];
            StartPlayerForFile(choice);
        }
        catch (Exception ex)
        {
            Logger.Warning($"BackgroundSoundEngine.Init failed: {ex.Message}");
        }
    }

    public static void PlayStepSound(VulkanMC.Terrain.BlockType type)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            var blockDir = Path.Combine(baseDir, "Sounds", "block");
            string key = type.ToString().ToLowerInvariant();

            // Prefer directory /Sounds/block/<type> if exists
            if (Directory.Exists(Path.Combine(blockDir, key)))
            {
                var opts = Directory.EnumerateFiles(Path.Combine(blockDir, key), "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (opts.Length > 0)
                {
                    var rnd = new Random();
                    StartOneShot(opts[rnd.Next(opts.Length)]);
                    return;
                }
            }

            // Fallback: search files in Sounds/block containing the key
            if (Directory.Exists(blockDir))
            {
                var opts = Directory.EnumerateFiles(blockDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => (f.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                                && (f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (opts.Length > 0)
                {
                    var rnd = new Random();
                    StartOneShot(opts[rnd.Next(opts.Length)]);
                    return;
                }
                // Fallback to any block sound
                var any = Directory.EnumerateFiles(blockDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (any.Length > 0)
                {
                    var rnd = new Random();
                    StartOneShot(any[rnd.Next(any.Length)]);
                    return;
                }
            }

            // No sound found
        }
        catch { }
    }

    private static void StartOneShot(string filePath)
    {
        try
        {
            // Prefer ffplay for one-shot playback
            if (IsCommandAvailable("ffplay"))
            {
                StartProcess("ffplay", $"-nodisp -loglevel panic -autoexit -volume 100 \"{filePath}\"");
                return;
            }
            if (IsCommandAvailable("paplay"))
            {
                StartProcess("paplay", $"\"{filePath}\"");
                return;
            }
            if (IsCommandAvailable("aplay") && filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                StartProcess("aplay", $"\"{filePath}\"");
                return;
            }
        }
        catch { }
    }

    private static void StartPlayerForFile(string filePath)
    {
        // Try ffplay, then paplay, then aplay
        if (IsCommandAvailable("ffplay"))
        {
            StartProcess("ffplay", $"-nodisp -loglevel panic -autoexit -loop 0 \"{filePath}\"");
            return;
        }

        if (IsCommandAvailable("paplay"))
        {
            // paplay does not support looping; we will background a simple loop via - also use paplay once and rely on ambient files being long
            StartProcess("paplay", $"\"{filePath}\"");
            return;
        }

        if (IsCommandAvailable("aplay") && filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            StartProcess("aplay", $"\"{filePath}\"");
            return;
        }

        Logger.Warning("No suitable audio player found (ffplay/paplay/aplay). Install ffmpeg or paplay for background audio.");
    }

    private static bool IsCommandAvailable(string cmd)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo("/usr/bin/which", cmd)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            p.Start();
            p.WaitForExit(500);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void StartProcess(string exe, string args)
    {
        try
        {
            Stop();
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            _player = Process.Start(psi);
            Logger.Info($"Background audio started: {exe} {args}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to start background audio player: {ex.Message}");
        }
    }

    public static void Stop()
    {
        try
        {
            if (_player != null && !_player.HasExited)
            {
                try { _player.Kill(); } catch { }
                _player.Dispose();
            }
        }
        catch { }
        finally { _player = null; }
    }
}
