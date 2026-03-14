using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using VulkanMC.Config;

namespace VulkanMC.Core;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public static class Logger
{
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    private static readonly object Sync = new();
    private static bool _enableConsoleColors = true;
    private static bool _includeTimestamp = true;
    private static bool _includeThreadId;
    private static bool _useUtcTimestamp;
    private static string _timestampFormat = "HH:mm:ss.fff";
    private static bool _enableFileLogging;
    private static string _filePath = "logs/vulkanmc.log";
    private static bool _appendToFile = true;
    private static int _flushIntervalMs = 250;
    private static int _maxFileSizeMB = 25;
    private static int _maxRetainedFiles = 5;
    private static int _duplicateSuppressionMs;
    private static DateTime _lastFlushUtc = DateTime.UtcNow;
    private static StreamWriter? _fileWriter;
    private static string? _lastMessage;
    private static LogLevel _lastMessageLevel;
    private static DateTime _lastMessageUtc = DateTime.MinValue;

    // ANSI Colors constants
    public const string RESET = "\u001b[0m";
    public const string BLACK = "\u001b[30m";
    public const string RED = "\u001b[31m";
    public const string GREEN = "\u001b[32m";
    public const string YELLOW = "\u001b[33m";
    public const string BLUE = "\u001b[34m";
    public const string MAGENTA = "\u001b[35m";
    public const string CYAN = "\u001b[36m";
    public const string WHITE = "\u001b[37m";
    public const string BOLD = "\u001b[1m";

    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Success(string message) => Log(LogLevel.Info, message, GREEN);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message) => Log(LogLevel.Error, message);

    public static void Configure(LoggingConfig config)
    {
        lock (Sync)
        {
            if (Enum.TryParse<LogLevel>(config.Level, true, out var level))
            {
                MinimumLevel = level;
            }

            _enableConsoleColors = config.EnableConsoleColors;
            _includeTimestamp = config.IncludeTimestamp;
            _includeThreadId = config.IncludeThreadId;
            _useUtcTimestamp = config.UseUtcTimestamp;
            _timestampFormat = string.IsNullOrWhiteSpace(config.TimestampFormat) ? "HH:mm:ss.fff" : config.TimestampFormat;
            _enableFileLogging = config.EnableFileLogging;
            _filePath = string.IsNullOrWhiteSpace(config.FilePath) ? "logs/vulkanmc.log" : config.FilePath;
            _appendToFile = config.AppendToFile;
            _flushIntervalMs = Math.Clamp(config.FlushIntervalMs, 10, 5000);
            _maxFileSizeMB = Math.Clamp(config.MaxFileSizeMB, 1, 2048);
            _maxRetainedFiles = Math.Clamp(config.MaxRetainedFiles, 1, 200);
            _duplicateSuppressionMs = Math.Clamp(config.DuplicateSuppressionMs, 0, 60000);

            if (_enableFileLogging)
            {
                EnsureFileWriter();
            }
            else
            {
                CloseFileWriter();
            }
        }
    }

    private static void Log(LogLevel level, string message, string? color = null)
    {
        if (level < MinimumLevel) return;

        lock (Sync)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (_duplicateSuppressionMs > 0 &&
                _lastMessage == message &&
                _lastMessageLevel == level &&
                (nowUtc - _lastMessageUtc).TotalMilliseconds < _duplicateSuppressionMs)
            {
                return;
            }

            _lastMessage = message;
            _lastMessageLevel = level;
            _lastMessageUtc = nowUtc;

            DateTime now = _useUtcTimestamp ? nowUtc : DateTime.Now;
            string timestampRaw = now.ToString(_timestampFormat, CultureInfo.InvariantCulture);
            string timestampText = _includeTimestamp ? $"[{timestampRaw}] " : string.Empty;
            string threadText = _includeThreadId ? $"[T{Environment.CurrentManagedThreadId}] " : string.Empty;
            string levelText = level.ToString().ToUpperInvariant().PadRight(7);

            if (_enableConsoleColors)
            {
                var levelColor = GetColorForLevel(level);
                var levelStr = $"{levelColor}{BOLD}{levelText}{RESET}";
                var textColor = color ?? levelColor;
                string ts = _includeTimestamp ? $"{WHITE}{timestampRaw}{RESET} " : string.Empty;
                string consoleLine = $"{ts}{threadText}[{levelStr}] {textColor}{message}{RESET}";
                Console.WriteLine(consoleLine);
            }
            else
            {
                Console.WriteLine($"{timestampText}{threadText}[{levelText}] {message}");
            }

            if (_enableFileLogging)
            {
                EnsureFileWriter();
                if (_fileWriter != null)
                {
                    RotateIfNeeded();
                    _fileWriter.WriteLine($"{timestampText}{threadText}[{levelText}] {message}");
                    if ((nowUtc - _lastFlushUtc).TotalMilliseconds >= _flushIntervalMs)
                    {
                        _fileWriter.Flush();
                        _lastFlushUtc = nowUtc;
                    }
                }
            }
        }
    }

    private static void EnsureFileWriter()
    {
        if (_fileWriter != null)
        {
            return;
        }

        string fullPath = Path.GetFullPath(_filePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var stream = new FileStream(fullPath, _appendToFile ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _fileWriter = new StreamWriter(stream) { AutoFlush = false };
        _filePath = fullPath;
        _lastFlushUtc = DateTime.UtcNow;
    }

    private static void CloseFileWriter()
    {
        if (_fileWriter == null)
        {
            return;
        }

        try
        {
            _fileWriter.Flush();
            _fileWriter.Dispose();
        }
        catch
        {
            // Ignore shutdown errors in logger cleanup.
        }
        finally
        {
            _fileWriter = null;
        }
    }

    private static void RotateIfNeeded()
    {
        if (_fileWriter?.BaseStream is not FileStream fs)
        {
            return;
        }

        long maxBytes = (long)_maxFileSizeMB * 1024L * 1024L;
        if (fs.Length < maxBytes)
        {
            return;
        }

        string fullPath = Path.GetFullPath(_filePath);
        string dir = Path.GetDirectoryName(fullPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(fullPath);
        string ext = Path.GetExtension(fullPath);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        int suffix = 0;
        string rotated;

        do
        {
            suffix++;
            rotated = Path.Combine(dir, $"{name}.{stamp}.{suffix}{ext}");
        }
        while (File.Exists(rotated));

        _fileWriter.Flush();
        _fileWriter.Dispose();
        _fileWriter = null;

        File.Move(fullPath, rotated);
        CleanupRotatedFiles(dir, name, ext);
        EnsureFileWriter();
    }

    private static void CleanupRotatedFiles(string dir, string name, string ext)
    {
        var pattern = $"{name}.*{ext}";
        var oldFiles = Directory.GetFiles(dir, pattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Skip(_maxRetainedFiles)
            .ToArray();

        foreach (var file in oldFiles)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Ignore file cleanup failures.
            }
        }
    }

    private static string GetColorForLevel(LogLevel level) => level switch
    {
        LogLevel.Debug => BLUE,
        LogLevel.Info => GREEN,
        LogLevel.Warning => YELLOW, // Orange is typically represented as Yellow in 8-color ANSI
        LogLevel.Error => RED,
        _ => WHITE
    };
}