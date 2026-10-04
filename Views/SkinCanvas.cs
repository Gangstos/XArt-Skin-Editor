using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using XArtSkinEditor.Core;

namespace XArtSkinEditor.Views;

public enum Tool { Pencil, Eraser, Fill, Picker, Shade }

public sealed class SkinCanvas : Control
{
    private static readonly IBrush CheckerA = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly IBrush CheckerB = new SolidColorBrush(Color.FromRgb(0x6a, 0x6a, 0x6a));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(48, 0, 0, 0)), 1);

    private readonly WriteableBitmap _bmp = new(
        new PixelSize(SkinDocument.Width, SkinDocument.Height), new Vector(96, 96),
        PixelFormat.Bgra8888, AlphaFormat.Unpremul);
    private SkinDocument? _doc;
    private bool _refreshQueued;
    private (int x, int y)? _last;
    private (int x, int y)? _hover;

    public Tool Tool { get; set; } = Tool.Pencil;
    public Color Color { get; set; } = Colors.Black;
    public bool ShowGrid { get; set; } = true;

    private (int x, int y)? _highlight;

    /// <summary>A skin pixel to outline, e.g. the one under the cursor in the 3D view.</summary>
    public (int x, int y)? Highlight
    {
        get => _highlight;
        set { if (_highlight == value) return; _highlight = value; InvalidateVisual(); }
    }

    /// <summary>Raised when the eyedropper picks a color.</summary>
    public event Action<Color>? ColorPicked;
    public event Action<int, int>? HoverChanged;

    public SkinDocument? Document
    {
        get => _doc;
        set
        {
            if (_doc is not null) _doc.Changed -= OnChanged;
            _doc = value;
            if (_doc is not null) _doc.Changed += OnChanged;
            Refresh();
        }
    }

    public SkinCanvas()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    private void OnChanged()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() => { _refreshQueued = false; Refresh(); });
    }

    private void Refresh()
    {
        if (_doc is null) return;
        var rgba = _doc.SnapshotRgba();
        using (var fb = _bmp.Lock())
        {
            var bgra = new byte[rgba.Length];
            for (var i = 0; i < rgba.Length; i += 4)
            {
                bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = rgba[i + 3];
            }
            for (var y = 0; y < SkinDocument.Height; y++)
                Marshal.Copy(bgra, y * SkinDocument.Width * 4, fb.Address + y * fb.RowBytes, SkinDocument.Width * 4);
        }
        InvalidateVisual();
    }

    private (double scale, Point origin) Fit()
    {
        var scale = Math.Max(1, Math.Floor(Math.Min(Bounds.Width / SkinDocument.Width, Bounds.Height / SkinDocument.Height)));
        var size = scale * SkinDocument.Width;
        return (scale, new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2));
    }

    public override void Render(DrawingContext ctx)
    {
        var (s, o) = Fit();
        for (var y = 0; y < SkinDocument.Height; y++)
            for (var x = 0; x < SkinDocument.Width; x++)
                ctx.FillRectangle(((x + y) & 1) == 0 ? CheckerA : CheckerB, new Rect(o.X + x * s, o.Y + y * s, s, s));

        var dest = new Rect(o.X, o.Y, SkinDocument.Width * s, SkinDocument.Height * s);
        ctx.DrawImage(_bmp, new Rect(0, 0, SkinDocument.Width, SkinDocument.Height), dest);

        if (ShowGrid && s >= 6)
            for (var i = 0; i <= SkinDocument.Width; i++)
            {
                ctx.DrawLine(GridPen, new Point(o.X + i * s, o.Y), new Point(o.X + i * s, o.Y + dest.Height));
                ctx.DrawLine(GridPen, new Point(o.X, o.Y + i * s), new Point(o.X + dest.Width, o.Y + i * s));
            }

        if (_hover is { } h)
            ctx.DrawRectangle(null, new Pen(Brushes.White, 1), new Rect(o.X + h.x * s, o.Y + h.y * s, s, s));
        if (_highlight is { } k)
            ctx.DrawRectangle(null, new Pen(Brushes.Yellow, 2), new Rect(o.X + k.x * s, o.Y + k.y * s, s, s));
    }

    private (int x, int y)? ToPixel(Point p)
    {
        var (s, o) = Fit();
        var x = (int)Math.Floor((p.X - o.X) / s);
        var y = (int)Math.Floor((p.Y - o.Y) / s);
        return SkinDocument.InBounds(x, y) ? (x, y) : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_doc is null || ToPixel(e.GetPosition(this)) is not { } px) return;
        var props = e.GetCurrentPoint(this).Properties;
        var erase = props.IsRightButtonPressed;
        if (!props.IsLeftButtonPressed && !erase) return;

        e.Pointer.Capture(this);
        // Right button erases, except with the lighten/darken brush where it darkens.
        var tool = erase && Tool != Tool.Shade ? Tool.Eraser : Tool;
        _darken = erase && Tool == Tool.Shade;
        if (tool == Tool.Picker)
        {
            var c = _doc.GetPixel(px.x, px.y);
            ColorPicked?.Invoke(Color.FromArgb(c.A, c.R, c.G, c.B));
            return;
        }
        if (tool == Tool.Fill)
        {
            var c = ToRgba(Color);
            _doc.Edit(ed => ed.Flood(px.x, px.y, c));
            return;
        }
        _doc.BeginStroke();
        _active = tool;
        // Shading works from the pixels as they were when the stroke began, one change per pixel,
        // so dragging over the same pixel twice does not shade it twice.
        _strokeSnap = tool == Tool.Shade ? _doc.SnapshotRgba() : null;
        _shaded.Clear();
        Paint(px, null);
        _last = px;
    }

    private Tool _active;
    private byte[]? _strokeSnap;
    private bool _darken;
    private readonly System.Collections.Generic.HashSet<(int, int)> _shaded = new();

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var px = ToPixel(e.GetPosition(this));
        if (px != _hover) { _hover = px; InvalidateVisual(); if (px is { } p) HoverChanged?.Invoke(p.x, p.y); }
        if (_last is null || _doc is null || px is not { } cur) return;
        Paint(cur, _last);
        _last = cur;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_last is null) return;
        _last = null;
        _doc?.EndStroke();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    private void Paint((int x, int y) to, (int x, int y)? from)
    {
        if (_active == Tool.Shade && _strokeSnap is { } snap)
        {
            var pts = from is { } a ? Shading.LinePoints(a.x, a.y, to.x, to.y) : new[] { to };
            _doc!.Edit(ed =>
            {
                foreach (var (x, y) in pts)
                {
                    if (!_shaded.Add((x, y))) continue;
                    var o = (y * SkinDocument.Width + x) * 4;
                    ed.Set(x, y, Shading.Apply(new Rgba(snap[o], snap[o + 1], snap[o + 2], snap[o + 3]), _darken));
                }
            });
            return;
        }
        var c = _active == Tool.Eraser ? Rgba.Transparent : ToRgba(Color);
        _doc!.Edit(ed =>
        {
            if (from is { } f) ed.Line(f.x, f.y, to.x, to.y, c);
            else ed.Set(to.x, to.y, c);
        });
    }

    private static Rgba ToRgba(Color c) => new(c.R, c.G, c.B, c.A);
}
