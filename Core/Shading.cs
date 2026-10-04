using System;
using System.Collections.Generic;

namespace XArtSkinEditor.Core;

/// <summary>Lighten / darken brush: shifts a pixel's HSL lightness and keeps its hue, saturation and alpha.</summary>
public static class Shading
{
    /// <summary>Strength in percent of the lightness range (1-50).</summary>
    public static int Percent { get; set; } = 15;

    public static Rgba Apply(Rgba c, bool darken) => Apply(c, darken, Percent);

    public static Rgba Apply(Rgba c, bool darken, int percent)
    {
        if (c.A == 0) return c;                       // nothing to shade on an empty pixel
        var (h, s, l) = ToHsl(c.R, c.G, c.B);
        l = Math.Clamp(l + (darken ? -1 : 1) * percent / 100.0, 0, 1);
        var (r, g, b) = FromHsl(h, s, l);
        return new Rgba(r, g, b, c.A);
    }

    /// <summary>The pixels on a line between two points (inclusive), Bresenham.</summary>
    public static IEnumerable<(int x, int y)> LinePoints(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0); var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1; var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1) yield break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static (double h, double s, double l) ToHsl(byte r8, byte g8, byte b8)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max == min) return (0, 0, l);
        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static (byte r, byte g, byte b) FromHsl(double h, double s, double l)
    {
        if (s == 0) { var v = (byte)Math.Round(l * 255); return (v, v, v); }
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (Channel(p, q, h + 1.0 / 3), Channel(p, q, h), Channel(p, q, h - 1.0 / 3));
    }

    private static byte Channel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        var v = t < 1.0 / 6 ? p + (q - p) * 6 * t
              : t < 0.5 ? q
              : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6
              : p;
        return (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
    }
}
