using System;
using System.Collections.Generic;
using SkiaSharp;

namespace XArtSkinEditor.Core;

public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    /// <summary>Parses #RGB, #RRGGBB, #RRGGBBAA (the # is optional) or "transparent".</summary>
    public static Rgba Parse(string text)
    {
        var s = text.Trim().TrimStart('#');
        if (s.Equals("transparent", StringComparison.OrdinalIgnoreCase) || s.Equals("none", StringComparison.OrdinalIgnoreCase))
            return Transparent;
        if (s.Length == 3)
            s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length is not (6 or 8) || !uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v))
            throw new ArgumentException($"Invalid color '{text}'. Use #RRGGBB, #RRGGBBAA or 'transparent'.");
        return s.Length == 6
            ? new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255)
            : new Rgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

/// <summary>
/// A 64x64 RGBA skin. Thread-safe: the UI and the MCP server mutate it concurrently.
/// Pixels are stored unpremultiplied; writing a pixel replaces it (alpha 0 erases).
/// </summary>
public sealed class SkinDocument
{
    public const int Width = 64;
    public const int Height = 64;
    private const int MaxHistory = 100;

    private readonly object _gate = new();
    private readonly byte[] _px = new byte[Width * Height * 4];
    private readonly List<byte[]> _undo = new();
    private readonly List<byte[]> _redo = new();
    private bool _inStroke;

    public event Action? Changed;

    public static bool InBounds(int x, int y) => x is >= 0 and < Width && y is >= 0 and < Height;

    public Rgba GetPixel(int x, int y)
    {
        CheckBounds(x, y);
        lock (_gate)
        {
            var i = (y * Width + x) * 4;
            return new Rgba(_px[i], _px[i + 1], _px[i + 2], _px[i + 3]);
        }
    }

    /// <summary>Runs <paramref name="edit"/> as one undoable step and raises Changed once.</summary>
    public void Edit(Action<Editor> edit)
    {
        lock (_gate)
        {
            if (!_inStroke) PushUndo();
            edit(new Editor(this));
        }
        Changed?.Invoke();
    }

    /// <summary>Groups many UI edits (a mouse drag) into a single undo step.</summary>
    public void BeginStroke()
    {
        lock (_gate)
        {
            if (_inStroke) return;
            PushUndo();
            _inStroke = true;
        }
    }

    public void EndStroke()
    {
        lock (_gate) _inStroke = false;
    }

    public void SetPixel(int x, int y, Rgba c) => Edit(e => e.Set(x, y, c));

    public void Clear() => Edit(e => e.Fill(0, 0, Width, Height, Rgba.Transparent));

    /// <summary>Forgets undo/redo, e.g. after opening a file so Ctrl+Z cannot wipe the freshly loaded skin.</summary>
    public void ClearHistory()
    {
        lock (_gate)
        {
            _undo.Clear();
            _redo.Clear();
        }
    }

    public bool Undo() => Swap(_undo, _redo);
    public bool Redo() => Swap(_redo, _undo);

    /// <summary>True when every pixel is fully transparent.</summary>
    public bool IsEmpty()
    {
        lock (_gate)
        {
            for (var i = 3; i < _px.Length; i += 4)
                if (_px[i] != 0) return false;
            return true;
        }
    }

    public byte[] SnapshotRgba()
    {
        lock (_gate) return (byte[])_px.Clone();
    }

    public byte[] EncodePng(int scale = 1)
    {
        var rgba = SnapshotRgba();
        using var bmp = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);
        using var img = scale == 1
            ? SKImage.FromBitmap(bmp)
            : SKImage.FromBitmap(bmp.Resize(new SKImageInfo(Width * scale, Height * scale, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Nearest)));
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Alex (slim) skins leave the 4th arm column unused, so those pixels stay fully transparent: x 54-55 / y 20-31
    /// for the right arm and x 46-47 / y 52-63 for the left arm. Steve skins normally draw there.
    /// </summary>
    public bool LooksSlim()
    {
        var px = SnapshotRgba();
        static bool Clear(byte[] p, int x0, int y0, int w, int h)
        {
            for (var y = y0; y < y0 + h; y++)
                for (var x = x0; x < x0 + w; x++)
                    if (p[(y * Width + x) * 4 + 3] != 0) return false;
            return true;
        }
        return Clear(px, 54, 20, 2, 12) && Clear(px, 46, 52, 2, 12);
    }

    /// <summary>
    /// Loads a PNG and returns true if the skin looks like the slim (Alex) model.
    /// Images other than 64x64 are placed at the top-left corner and cropped.
    /// </summary>
    public bool LoadPng(byte[] png)
    {
        using var src = SKBitmap.Decode(png) ?? throw new ArgumentException("Not a valid image.");
        using var conv = new SKBitmap(new SKImageInfo(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (!src.CopyTo(conv, SKColorType.Rgba8888))
            throw new ArgumentException("Unsupported image format.");
        var data = conv.Bytes;
        Edit(e =>
        {
            e.Fill(0, 0, Width, Height, Rgba.Transparent);
            for (var y = 0; y < Math.Min(Height, conv.Height); y++)
                for (var x = 0; x < Math.Min(Width, conv.Width); x++)
                {
                    var i = (y * conv.Width + x) * 4;
                    e.Set(x, y, new Rgba(data[i], data[i + 1], data[i + 2], data[i + 3]));
                }
        });
        // Legacy 64x32 skins only exist for the Steve model.
        return conv.Height >= Height && LooksSlim();
    }

    private bool Swap(List<byte[]> from, List<byte[]> to)
    {
        lock (_gate)
        {
            if (from.Count == 0) return false;
            to.Add((byte[])_px.Clone());
            var state = from[^1];
            from.RemoveAt(from.Count - 1);
            Buffer.BlockCopy(state, 0, _px, 0, _px.Length);
        }
        Changed?.Invoke();
        return true;
    }

    private void PushUndo()
    {
        _undo.Add((byte[])_px.Clone());
        if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
        _redo.Clear();
    }

    private static void CheckBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException(null, $"Pixel ({x},{y}) is outside the {Width}x{Height} canvas.");
    }

    /// <summary>Mutation API valid only inside <see cref="Edit"/>; out-of-range pixels are clipped.</summary>
    public readonly struct Editor
    {
        private readonly SkinDocument _d;
        internal Editor(SkinDocument d) => _d = d;

        public void Set(int x, int y, Rgba c)
        {
            if (!InBounds(x, y)) return;
            var i = (y * Width + x) * 4;
            _d._px[i] = c.R; _d._px[i + 1] = c.G; _d._px[i + 2] = c.B; _d._px[i + 3] = c.A;
        }

        public Rgba Get(int x, int y)
        {
            var i = (y * Width + x) * 4;
            return new Rgba(_d._px[i], _d._px[i + 1], _d._px[i + 2], _d._px[i + 3]);
        }

        public void Fill(int x, int y, int w, int h, Rgba c)
        {
            for (var yy = y; yy < y + h; yy++)
                for (var xx = x; xx < x + w; xx++)
                    Set(xx, yy, c);
        }

        public void Line(int x0, int y0, int x1, int y1, Rgba c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                var e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Flood fill of the contiguous area of identical color (4-connected).</summary>
        public void Flood(int x, int y, Rgba c, (int x, int y, int w, int h)? clip = null)
        {
            if (!InBounds(x, y)) return;
            var target = Get(x, y);
            if (target == c) return;
            var stack = new Stack<(int, int)>();
            stack.Push((x, y));
            while (stack.Count > 0)
            {
                var (px, py) = stack.Pop();
                if (!InBounds(px, py) || Get(px, py) != target) continue;
                if (clip is { } k && (px < k.x || py < k.y || px >= k.x + k.w || py >= k.y + k.h)) continue;
                Set(px, py, c);
                stack.Push((px + 1, py)); stack.Push((px - 1, py));
                stack.Push((px, py + 1)); stack.Push((px, py - 1));
            }
        }
    }
}
