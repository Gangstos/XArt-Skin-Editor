using System;
using System.Collections.Generic;
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

/// <summary>
/// 3D player preview. Left mouse paints with the current tool, right/middle mouse orbits, wheel zooms.
/// </summary>
public sealed class ModelView : Control
{
    private static Rgba Background => AppTheme.ViewportColor;

    private SkinDocument? _doc;
    private ModelSettings? _settings;
    private readonly Camera _cam = new();
    private WriteableBitmap? _bmp;
    private byte[] _buf = Array.Empty<byte>();
    private byte[]? _rgba, _strokeRgba;
    private ModelBox[]? _strokeBoxes;
    private ModelBox[]? _viewBoxes, _paintBoxes;
    private bool _frameQueued, _orbit, _painting;
    private Point _last;
    private (int x, int y)? _hover;
    private PaintLayer _layer = PaintLayer.Auto;

    public Tool Tool { get; set; } = Tool.Pencil;
    public Color Color { get; set; } = Colors.Black;

    public PaintLayer Layer
    {
        get => _layer;
        set { _layer = value; _paintBoxes = null; }
    }

    public event Action<Color>? ColorPicked;
    /// <summary>Skin pixel under the cursor and a description, or null when the cursor leaves the model.</summary>
    public event Action<(int x, int y, string text)?>? TexelHovered;

    public SkinDocument? Document
    {
        get => _doc;
        set
        {
            if (_doc is not null) _doc.Changed -= OnDocChanged;
            _doc = value;
            if (_doc is not null) _doc.Changed += OnDocChanged;
            _rgba = null;     // cached pixels belong to the previous document
            _hover = null;
            RequestFrame();
        }
    }

    public ModelSettings? Settings
    {
        get => _settings;
        set
        {
            if (_settings is not null) _settings.Changed -= OnSettingsChanged;
            _settings = value;
            if (_settings is not null) _settings.Changed += OnSettingsChanged;
            OnSettingsChanged();
        }
    }

    public ModelView()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        AppTheme.Changed += RequestFrame;   // the viewport background belongs to the theme
    }

    public void ResetView()
    {
        _cam.Reset();
        RequestFrame();
    }

    private void OnDocChanged()
    {
        _rgba = null;
        RequestFrame();
    }

    private void OnSettingsChanged()
    {
        _viewBoxes = null;
        _paintBoxes = null;
        RequestFrame();
    }

    private ModelBox[] ViewBoxes() => _viewBoxes ??= Boxes(null);
    private ModelBox[] PaintBoxes() => _paintBoxes ??= Boxes(_layer);

    private ModelBox[] Boxes(PaintLayer? layer)
    {
        if (_settings is null) return Array.Empty<ModelBox>();
        return PlayerModel.Filter(PlayerModel.Build(_settings.Slim), _settings.OverlaySnapshot(), layer, _settings.BaseSnapshot());
    }

    private void RequestFrame()
    {
        if (_frameQueued) return;
        _frameQueued = true;
        Dispatcher.UIThread.Post(Frame, DispatcherPriority.Background);
    }

    private void Frame()
    {
        _frameQueued = false;
        if (_doc is null || Bounds.Width < 2 || Bounds.Height < 2) return;
        // Render at physical pixels (125% / 150% display scaling) so the preview stays sharp, also while rotating.
        var k = (TopLevel.GetTopLevel(this)?.RenderScaling) ?? 1.0;
        var w = Math.Max(1, (int)(Bounds.Width * k));
        var h = Math.Max(1, (int)(Bounds.Height * k));
        if (_bmp is null || _bmp.PixelSize.Width != w || _bmp.PixelSize.Height != h)
        {
            _bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            _buf = new byte[w * h * 4];
        }

        _rgba ??= _doc.SnapshotRgba();
        SkinRenderer.Render(_rgba, ViewBoxes(), _cam, w, h, _buf, Background, _hover);
        using (var fb = _bmp.Lock())
            for (var y = 0; y < h; y++)
                Marshal.Copy(_buf, y * w * 4, fb.Address + y * fb.RowBytes, w * 4);
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        if (_bmp is null) return;
        ctx.DrawImage(_bmp, new Rect(0, 0, _bmp.PixelSize.Width, _bmp.PixelSize.Height), new Rect(0, 0, Bounds.Width, Bounds.Height));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestFrame();
    }

    private Hit? Pick(Point p)
    {
        if (_doc is null || Bounds.Width < 1 || Bounds.Height < 1) return null;
        var locked = _painting && _strokeBoxes is not null;
        var boxes = locked ? _strokeBoxes! : PaintBoxes();
        if (boxes.Length == 0) return null;
        _rgba ??= _doc.SnapshotRgba();
        var basis = new SkinRenderer.Basis(_cam, (float)(Bounds.Width / Bounds.Height));
        var ray = basis.At((float)(p.X / Bounds.Width), (float)(p.Y / Bounds.Height));
        return SkinRenderer.Trace(_painting && _strokeRgba is not null ? _strokeRgba : _rgba, boxes, ray,
            !locked && _layer == PaintLayer.Auto, out var hit) ? hit : null;
    }

    private string Describe(Hit h)
    {
        var b = PaintBoxes()[h.Box];
        var part = Loc.T(b.Part switch
        {
            BodyPart.Head => "part.head",
            BodyPart.Body => "part.body",
            BodyPart.RightArm => "part.rarm",
            BodyPart.LeftArm => "part.larm",
            BodyPart.RightLeg => "part.rleg",
            _ => "part.lleg",
        });
        var overlay = b.Overlay ? " " + Loc.T("overlay.word") : "";
        return $"{part}{overlay}, {Loc.T("face." + SkinRenderer.FaceNames[h.Face])}  ({h.Tx}, {h.Ty})";
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // The lighten/darken brush darkens with the right button when it is pressed on the model,
        // ...but a right click on empty space (nothing to darken there) still rotates the view.
        var shadeRight = Tool == Tool.Shade && props.IsRightButtonPressed && _doc is not null && Pick(pos) is not null;
        if (props.IsMiddleButtonPressed || (props.IsRightButtonPressed && !shadeRight))
        {
            _orbit = true; _last = pos;
            e.Pointer.Capture(this);
            return;
        }
        if (!(props.IsLeftButtonPressed || shadeRight) || _doc is null || Pick(pos) is not { } hit) return;
        _darken = shadeRight;

        if (Tool == Tool.Picker)
        {
            var c = _doc.GetPixel(hit.Tx, hit.Ty);
            ColorPicked?.Invoke(Color.FromArgb(c.A, c.R, c.G, c.B));
            return;
        }
        if (Tool == Tool.Fill)
        {
            var c = new Rgba(Color.R, Color.G, Color.B, Color.A);
            _doc.Edit(ed => ed.Flood(hit.Tx, hit.Ty, c, (hit.Rx, hit.Ry, hit.Rw, hit.Rh)));
            return;
        }

        _painting = true; _last = pos;
        // Pick against the pixels as they were when the stroke began: otherwise an eraser that just cleared an
        // overlay texel would see it as transparent on the next sample and go on to erase the base beneath it.
        _strokeRgba = (byte[])_rgba!.Clone();
        _shaded.Clear();
        // In "visible layer" mode the stroke stays on the layer it started on, so drifting over a transparent
        // overlay texel does not suddenly paint or erase the base.
        _strokeBoxes = _layer == PaintLayer.Auto
            ? Boxes(PaintBoxes()[hit.Box].Overlay ? PaintLayer.Overlay : PaintLayer.Base)
            : null;
        e.Pointer.Capture(this);
        _doc.BeginStroke();
        Stroke(new List<(int, int)> { (hit.Tx, hit.Ty) });
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_orbit)
        {
            _cam.Yaw -= (float)(pos.X - _last.X) * 0.01f;
            _cam.Pitch = Math.Clamp(_cam.Pitch + (float)(pos.Y - _last.Y) * 0.01f, -1.5f, 1.5f);
            _last = pos;
            RequestFrame();
            return;
        }

        if (_painting && _doc is not null)
        {
            var dx = pos.X - _last.X; var dy = pos.Y - _last.Y;
            var n = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy))));
            var texels = new List<(int, int)>();
            for (var i = 1; i <= n; i++)
                if (Pick(new Point(_last.X + dx * i / n, _last.Y + dy * i / n)) is { } h)
                    texels.Add((h.Tx, h.Ty));
            _last = pos;
            if (texels.Count > 0) Stroke(texels);
            return;
        }

        var hover = Pick(pos);
        var texel = hover is { } hv ? (hv.Tx, hv.Ty) : ((int, int)?)null;
        if (texel == _hover) return;
        _hover = texel;
        TexelHovered?.Invoke(hover is { } hh ? (hh.Tx, hh.Ty, Describe(hh)) : null);
        RequestFrame();
    }

    private readonly HashSet<(int, int)> _shaded = new();
    private bool _darken;   // the current lighten/darken stroke was started with the right button

    private void Stroke(List<(int x, int y)> texels)
    {
        if (Tool == Tool.Shade && _strokeRgba is { } snap)
        {
            // One change per texel, computed from the pixels at the start of the stroke.
            _doc!.Edit(ed =>
            {
                foreach (var (x, y) in texels)
                {
                    if (!_shaded.Add((x, y))) continue;
                    var o = (y * SkinDocument.Width + x) * 4;
                    ed.Set(x, y, Shading.Apply(new Rgba(snap[o], snap[o + 1], snap[o + 2], snap[o + 3]), _darken));
                }
            });
            return;
        }
        var c = Tool == Tool.Eraser ? Rgba.Transparent : new Rgba(Color.R, Color.G, Color.B, Color.A);
        _doc!.Edit(ed => { foreach (var (x, y) in texels) ed.Set(x, y, c); });
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_orbit) _orbit = false;
        if (_painting) { _painting = false; _strokeRgba = null; _strokeBoxes = null; _doc?.EndStroke(); }
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover is null) return;
        _hover = null;
        TexelHovered?.Invoke(null);
        RequestFrame();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _cam.Distance = Math.Clamp(_cam.Distance * MathF.Exp(-(float)e.Delta.Y * 0.1f), 30f, 200f);
        RequestFrame();
    }
}
