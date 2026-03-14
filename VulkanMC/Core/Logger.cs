using System;

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

    private static void Log(LogLevel level, string message, string? color = null)
    {
        if (level < MinimumLevel) return;

        var timestamp = $"{WHITE}{DateTime.Now:HH:mm:ss.fff}{RESET}";
        var levelColor = GetColorForLevel(level);
        var levelStr = $"{levelColor}{BOLD}{level.ToString().ToUpper().PadRight(7)}{RESET}";
        
        var textColor = color ?? levelColor;
        
        Console.WriteLine($"[{timestamp}] [{levelStr}] {textColor}{message}{RESET}");
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