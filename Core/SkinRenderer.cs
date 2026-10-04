using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;

namespace XArtSkinEditor.Core;

public sealed class Camera
{
    public float Yaw = 0.6f;       // radians, 0 = looking at the front
    public float Pitch = 0.25f;    // radians, positive = from above
    public float Distance = 80f;

    public void Reset() { Yaw = 0.6f; Pitch = 0.25f; Distance = 80f; }
}

/// <summary>A texel hit on the model: which box/face, the skin pixel, and that face's rectangle on the skin.</summary>
public readonly record struct Hit(int Box, int Face, int Tx, int Ty, int Rx, int Ry, int Rw, int Rh);

/// <summary>
/// Tiny CPU ray caster for the player model. The same trace is used to draw the preview and to find the skin
/// pixel under the mouse, so what you click is exactly what you see.
/// </summary>
public static class SkinRenderer
{
    // Faces: 0:+x (left side) 1:-x (right side) 2:+y top 3:-y bottom 4:+z front 5:-z back
    public static readonly string[] FaceNames = { "left", "right", "top", "bottom", "front", "back" };
    private static readonly float[] Shade = { 0.72f, 0.72f, 1.0f, 0.55f, 0.88f, 0.88f };
    private const float TanHalfFov = 0.3153f; // tan(17.5 deg)
    private const float TargetY = 16f;

    public readonly struct Ray
    {
        public readonly float Ox, Oy, Oz, Dx, Dy, Dz;
        public Ray(float ox, float oy, float oz, float dx, float dy, float dz) { Ox = ox; Oy = oy; Oz = oz; Dx = dx; Dy = dy; Dz = dz; }
    }

    public readonly struct Basis
    {
        private readonly float _cx, _cy, _cz, _fx, _fy, _fz, _rx, _ry, _rz, _ux, _uy, _uz, _aspect;

        public Basis(Camera cam, float aspect)
        {
            var cp = MathF.Cos(cam.Pitch);
            _cx = cam.Distance * MathF.Sin(cam.Yaw) * cp;
            _cy = TargetY + cam.Distance * MathF.Sin(cam.Pitch);
            _cz = cam.Distance * MathF.Cos(cam.Yaw) * cp;
            var fx = -_cx; var fy = TargetY - _cy; var fz = -_cz;
            var fl = MathF.Sqrt(fx * fx + fy * fy + fz * fz);
            _fx = fx / fl; _fy = fy / fl; _fz = fz / fl;
            // right = forward x up(0,1,0)
            var rx = _fy * 0 - _fz * 1; var ry = _fz * 0 - _fx * 0; var rz = _fx * 1 - _fy * 0;
            var rl = MathF.Sqrt(rx * rx + ry * ry + rz * rz);
            _rx = rx / rl; _ry = ry / rl; _rz = rz / rl;
            _ux = _ry * _fz - _rz * _fy; _uy = _rz * _fx - _rx * _fz; _uz = _rx * _fy - _ry * _fx;
            _aspect = aspect;
        }

        /// <summary>nx, ny in [0,1] across the viewport, origin top-left.</summary>
        public Ray At(float nx, float ny)
        {
            var sx = (2 * nx - 1) * _aspect * TanHalfFov;
            var sy = (1 - 2 * ny) * TanHalfFov;
            return new Ray(_cx, _cy, _cz,
                _fx + sx * _rx + sy * _ux, _fy + sx * _ry + sy * _uy, _fz + sx * _rz + sy * _uz);
        }
    }

    private static bool Slab(float o, float d, float mn, float mx, int fNeg, int fPos, ref float tn, ref float tf, ref int face)
    {
        if (MathF.Abs(d) < 1e-9f) return o >= mn && o <= mx;
        var inv = 1f / d;
        var t1 = (mn - o) * inv;
        var t2 = (mx - o) * inv;
        int f;
        if (t1 > t2) { (t1, t2) = (t2, t1); f = fPos; } else f = fNeg;
        if (t1 > tn) { tn = t1; face = f; }
        if (t2 < tf) tf = t2;
        return tn <= tf;
    }

    private static bool Intersect(in ModelBox b, in Ray r, out float t, out int face)
    {
        float tn = -1e30f, tf = 1e30f;
        face = -1; t = 0;
        if (!Slab(r.Ox, r.Dx, b.X0, b.X1, 1, 0, ref tn, ref tf, ref face)) return false;
        if (!Slab(r.Oy, r.Dy, b.Y0, b.Y1, 3, 2, ref tn, ref tf, ref face)) return false;
        if (!Slab(r.Oz, r.Dz, b.Z0, b.Z1, 5, 4, ref tn, ref tf, ref face)) return false;
        t = tn;
        return face >= 0;
    }

    private static Hit MakeHit(int index, in ModelBox b, int face, float px, float py, float pz)
    {
        float Frac(float v, float lo, float hi) => Math.Clamp((v - lo) / (hi - lo), 0f, 0.9999f);
        int rx, ry, rw, rh;
        float fu, fv;
        switch (face)
        {
            case 4: // front (+z)
                rx = b.U + b.D; ry = b.V + b.D; rw = b.W; rh = b.H;
                fu = Frac(px, b.X0, b.X1); fv = Frac(b.Y1 - (py - b.Y0), b.Y0, b.Y1); break;
            case 5: // back (-z)
                rx = b.U + b.D + b.W + b.D; ry = b.V + b.D; rw = b.W; rh = b.H;
                fu = 1f - Frac(px, b.X0, b.X1); fv = Frac(b.Y1 - (py - b.Y0), b.Y0, b.Y1); break;
            case 1: // right side (-x)
                rx = b.U; ry = b.V + b.D; rw = b.D; rh = b.H;
                fu = Frac(pz, b.Z0, b.Z1); fv = Frac(b.Y1 - (py - b.Y0), b.Y0, b.Y1); break;
            case 0: // left side (+x)
                rx = b.U + b.D + b.W; ry = b.V + b.D; rw = b.D; rh = b.H;
                fu = 1f - Frac(pz, b.Z0, b.Z1); fv = Frac(b.Y1 - (py - b.Y0), b.Y0, b.Y1); break;
            case 2: // top
                rx = b.U + b.D; ry = b.V; rw = b.W; rh = b.D;
                fu = Frac(px, b.X0, b.X1); fv = Frac(pz, b.Z0, b.Z1); break;
            default: // bottom
                rx = b.U + b.D + b.W; ry = b.V; rw = b.W; rh = b.D;
                fu = Frac(px, b.X0, b.X1); fv = 1f - Frac(pz, b.Z0, b.Z1); break;
        }
        var tx = rx + Math.Min(rw - 1, (int)(fu * rw));
        var ty = ry + Math.Min(rh - 1, (int)(fv * rh));
        return new Hit(index, face, tx, ty, rx, ry, rw, rh);
    }

    /// <summary>
    /// Finds the first visible texel along the ray. With <paramref name="skipTransparentOverlay"/> fully transparent
    /// overlay texels are see-through (as in the game); base texels always count so empty skin areas can be painted.
    /// </summary>
    public static bool Trace(byte[] rgba, ModelBox[] boxes, in Ray ray, bool skipTransparentOverlay, out Hit hit)
    {
        hit = default;
        var tMin = 0f;
        for (var iter = 0; iter < 8; iter++)
        {
            var best = float.MaxValue; var bi = -1; var bf = -1;
            for (var i = 0; i < boxes.Length; i++)
                if (Intersect(boxes[i], ray, out var t, out var f) && t > tMin && t < best) { best = t; bi = i; bf = f; }
            if (bi < 0) return false;

            ref readonly var b = ref boxes[bi];
            var h = MakeHit(bi, b, bf, ray.Ox + ray.Dx * best, ray.Oy + ray.Dy * best, ray.Oz + ray.Dz * best);
            if (!(skipTransparentOverlay && b.Overlay && rgba[(h.Ty * SkinDocument.Width + h.Tx) * 4 + 3] == 0))
            {
                hit = h;
                return true;
            }
            tMin = best + 1e-3f;
        }
        return false;
    }

    /// <summary>Renders into <paramref name="bgra"/> (w*h*4, fully opaque). Hover texel is lightly highlighted.</summary>
    public static void Render(byte[] rgba, ModelBox[] boxes, Camera cam, int w, int h, byte[] bgra, Rgba bg, (int x, int y)? hover = null)
    {
        var basis = new Basis(cam, (float)w / h);
        float minX = 1e9f, minY = 1e9f, minZ = 1e9f, maxX = -1e9f, maxY = -1e9f, maxZ = -1e9f;
        foreach (var b in boxes)
        {
            minX = Math.Min(minX, b.X0); minY = Math.Min(minY, b.Y0); minZ = Math.Min(minZ, b.Z0);
            maxX = Math.Max(maxX, b.X1); maxY = Math.Max(maxY, b.Y1); maxZ = Math.Max(maxZ, b.Z1);
        }
        var bounds = new ModelBox("", BodyPart.Head, false, minX, minY, minZ, maxX, maxY, maxZ, 0, 0, 1, 1, 1);
        var empty = boxes.Length == 0;

        Parallel.For(0, h, j =>
        {
            var row = j * w * 4;
            for (var i = 0; i < w; i++)
            {
                byte r = bg.R, g = bg.G, bl = bg.B;
                if (!empty)
                {
                    var ray = basis.At((i + 0.5f) / w, (j + 0.5f) / h);
                    if (Intersect(bounds, ray, out _, out _) && Trace(rgba, boxes, ray, true, out var hit))
                    {
                        var o = (hit.Ty * SkinDocument.Width + hit.Tx) * 4;
                        float cr, cg, cb;
                        if (rgba[o + 3] == 0) { cr = cg = cb = 130; }   // empty base texel: neutral grey so the model reads as solid
                        else { cr = rgba[o]; cg = rgba[o + 1]; cb = rgba[o + 2]; }
                        var s = Shade[hit.Face];
                        cr *= s; cg *= s; cb *= s;
                        if (hover is { } hv && hv.x == hit.Tx && hv.y == hit.Ty)
                        {
                            cr = cr * 0.6f + 255 * 0.4f; cg = cg * 0.6f + 255 * 0.4f; cb = cb * 0.6f + 255 * 0.4f;
                        }
                        r = (byte)Math.Min(255f, cr); g = (byte)Math.Min(255f, cg); bl = (byte)Math.Min(255f, cb);
                    }
                }
                var p = row + i * 4;
                bgra[p] = bl; bgra[p + 1] = g; bgra[p + 2] = r; bgra[p + 3] = 255;
            }
        });
    }

    public static byte[] RenderPng(byte[] rgba, ModelBox[] boxes, Camera cam, int w, int h, Rgba bg)
    {
        var buf = new byte[w * h * 4];
        Render(rgba, boxes, cam, w, h, buf, bg);
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque));
        Marshal.Copy(buf, 0, bmp.GetPixels(), buf.Length);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
