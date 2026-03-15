using System;
using VulkanMC.Platform;
using System.Collections.Generic;
using System.IO;
using Silk.NET.Maths;
using SkiaSharp;
using AppConfig = VulkanMC.Config;
using VulkanMC;
// using VulkanMC.Platform; // Remove, as Platform is not a direct sub-namespace of VulkanMC
using VulkanMC.Core;

namespace VulkanMC.UI;

public class TextOverlay
{
    private double _timer;
    private int _fps;
    private int _frameCount;
    private int _visibleChunks;
    private long _visibleVertices;
    private readonly ISystemMetricsProvider _systemMetricsProvider;
    public string CurrentDebugString { get; private set; } = "Initializing...";

    private uint _atlasWidth = 512;
    private uint _atlasHeight = 512;
    private byte[] _atlasPayload = new byte[512 * 512];
    private Dictionary<char, GlyphInfo> _glyphs = new();

    public struct GlyphInfo
    {
        public Vector2D<float> Size;
        public Vector2D<float> Bearing;
        public uint Advance;
        public Vector2D<float> UVStart;
        public Vector2D<float> UVEnd;
    }

    public TextOverlay()
    {
        _systemMetricsProvider = SystemMetricsProviderFactory.Create();

        try
        {
            LoadFont();
        }
        catch (Exception ex)
        {
            Logger.Error($"Font atlas init failed: {ex}");
        }
    }

    private void LoadFont()
    {
        string? fontPath = ResolveFontPath();
        if (string.IsNullOrEmpty(fontPath) || !File.Exists(fontPath))
        {
            Logger.Error("Minecraft font not found in Fonts directory.");
            return;
        }

        using var typeface = SKTypeface.FromFile(fontPath);
        if (typeface == null)
        {
            Logger.Error($"Failed to load typeface: {fontPath}");
            return;
        }

        using var font = new SKFont
        {
            Typeface = typeface,
            Size = 24,
            Subpixel = true,
            Edging = SKFontEdging.Antialias
        };

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsStroke = false
        };

        using var atlas = new SKBitmap((int)_atlasWidth, (int)_atlasHeight, SKColorType.Alpha8, SKAlphaType.Premul);
        using var canvas = new SKCanvas(atlas);
        canvas.Clear(SKColors.Transparent);

        Array.Clear(_atlasPayload, 0, _atlasPayload.Length);
        _glyphs.Clear();

        int xOffset = 1;
        int yOffset = 1;
        int rowHeight = 0;

        for (char c = (char)32; c < 127; c++)
        {
            string s = c.ToString();
            SKRect bounds = default;
            float advance = font.MeasureText(s, out bounds, paint);

            int glyphWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            int glyphHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height));

            if (xOffset + glyphWidth + 1 >= _atlasWidth)
            {
                xOffset = 1;
                yOffset += rowHeight + 1;
                rowHeight = 0;
            }

            if (yOffset + glyphHeight + 1 >= _atlasHeight)
            {
                Logger.Warning("Text atlas is full; some glyphs were skipped.");
                break;
            }

            float drawX = xOffset - bounds.Left;
            float drawY = yOffset - bounds.Top;
            canvas.DrawText(s, drawX, drawY, font, paint);

            _glyphs[c] = new GlyphInfo
            {
                Size = new Vector2D<float>(glyphWidth, glyphHeight),
                Bearing = new Vector2D<float>(bounds.Left, -bounds.Top),
                Advance = (uint)Math.Max(1, (int)Math.Round(advance)),
                UVStart = new Vector2D<float>(xOffset / (float)_atlasWidth, yOffset / (float)_atlasHeight),
                UVEnd = new Vector2D<float>((xOffset + glyphWidth) / (float)_atlasWidth, (yOffset + glyphHeight) / (float)_atlasHeight)
            };

            xOffset += glyphWidth + 1;
            rowHeight = Math.Max(rowHeight, glyphHeight);
        }

        var pixmap = atlas.PeekPixels();
        if (pixmap == null)
        {
            Logger.Error("Failed to read text atlas pixels.");
            return;
        }

        IntPtr src = pixmap.GetPixels();
        int rowBytes = pixmap.RowBytes;
        int width = (int)_atlasWidth;
        int height = (int)_atlasHeight;

        for (int y = 0; y < height; y++)
        {
            IntPtr srcRow = IntPtr.Add(src, y * rowBytes);
            int dstOffset = y * width;
            System.Runtime.InteropServices.Marshal.Copy(srcRow, _atlasPayload, dstOffset, width);
        }

        Logger.Info($"Minecraft font loaded: {Path.GetFileName(fontPath)} ({_glyphs.Count} glyphs)");
    }

    private static string? ResolveFontPath()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "mc_regular.otf"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fonts", "mc_regular.otf"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fonts", "mc_regular.otf"),
            Path.Combine(Directory.GetCurrentDirectory(), "VulkanMC", "Fonts", "mc_regular.otf"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "mc_regular.otf"),
            Path.Combine(AppContext.BaseDirectory, "Fonts", "mc_bold.otf"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "mc_bold.otf")
        };

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    public void Update(double dt, Vector3D<float> pos, int chunkX, int chunkZ)
    {
        _timer += dt; _frameCount++;
        if (_timer >= 1.0)
        {
            SystemMetrics metrics = _systemMetricsProvider.GetMetrics();
            _fps = _frameCount; _frameCount = 0; _timer -= 1.0;

            var parts = new List<string> {
                $"FPS: {_fps}",
                $"CPU: {FormatPercent(metrics.CpuUsagePercent)}",
                $"GPU: {FormatPercent(metrics.GpuUsagePercent)}",
                $"RAM: {FormatPercent(metrics.RamUsagePercent)}",
                $"POS: {pos.X:F2}, {pos.Y:F2}, {pos.Z:F2}",
                $"CHUNK: {chunkX}, {chunkZ}"
            };

            CurrentDebugString = string.Join(" | ", parts);
        }
    }

    private static string FormatPercent(float? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "--";
    }

    public byte[] GetAtlasData() => _atlasPayload;
    public uint AtlasWidth => _atlasWidth;
    public uint AtlasHeight => _atlasHeight;
    public Dictionary<char, GlyphInfo> Glyphs => _glyphs;

    public void SetTerrainStats(int visibleChunks, long visibleVertices)
    {
        _visibleChunks = visibleChunks;
        _visibleVertices = visibleVertices;
    }
}