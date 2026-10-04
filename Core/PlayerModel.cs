using System.Collections.Generic;

namespace XArtSkinEditor.Core;

/// <summary>
/// One cuboid of the player model. Geometry is in skin pixels, y up, feet at y=0, the character faces +Z
/// (its right arm is at -X). W/H/D are the *texture* dimensions; overlay boxes are geometrically inflated
/// but keep the dimensions of the box they wrap.
/// </summary>
public readonly record struct ModelBox(
    string Name, BodyPart Part, bool Overlay,
    float X0, float Y0, float Z0, float X1, float Y1, float Z1,
    int U, int V, int W, int H, int D);

/// <summary>Which layer a click on the 3D model paints: the visible one (Auto), the base body, or the overlay.</summary>
public enum PaintLayer { Auto, Base, Overlay }

public static class PlayerModel
{
    public static ModelBox[] Build(bool slim)
    {
        var aw = slim ? 3 : 4;
        var list = new List<ModelBox>();

        void Add(string name, BodyPart part, float x0, float y0, float z0, float x1, float y1, float z1,
                 int u, int v, int w, int h, int d, int ou, int ov, float grow)
        {
            list.Add(new ModelBox(name, part, false, x0, y0, z0, x1, y1, z1, u, v, w, h, d));
            list.Add(new ModelBox(name, part, true, x0 - grow, y0 - grow, z0 - grow, x1 + grow, y1 + grow, z1 + grow, ou, ov, w, h, d));
        }

        Add("head", BodyPart.Head, -4, 24, -4, 4, 32, 4, 0, 0, 8, 8, 8, 32, 0, 0.5f);
        Add("body", BodyPart.Body, -4, 12, -2, 4, 24, 2, 16, 16, 8, 12, 4, 16, 32, 0.25f);
        Add("right arm", BodyPart.RightArm, -4 - aw, 12, -2, -4, 24, 2, 40, 16, aw, 12, 4, 40, 32, 0.25f);
        Add("left arm", BodyPart.LeftArm, 4, 12, -2, 4 + aw, 24, 2, 32, 48, aw, 12, 4, 48, 48, 0.25f);
        Add("right leg", BodyPart.RightLeg, -4, 0, -2, 0, 12, 2, 0, 16, 4, 12, 4, 0, 32, 0.25f);
        Add("left leg", BodyPart.LeftLeg, 0, 0, -2, 4, 12, 2, 16, 48, 4, 12, 4, 0, 48, 0.25f);
        return list.ToArray();
    }

    /// <summary>Boxes to draw (layer = null) or to paint on, given which overlays are visible.</summary>
    public static ModelBox[] Filter(ModelBox[] all, bool[] overlayVisible, PaintLayer? layer, bool[]? baseVisible = null)
    {
        var res = new List<ModelBox>(all.Length);
        foreach (var b in all)
        {
            var overlayOn = overlayVisible[(int)b.Part];
            var baseOn = baseVisible is null || baseVisible[(int)b.Part];
            var keep = layer switch
            {
                PaintLayer.Base => !b.Overlay && baseOn,
                PaintLayer.Overlay => b.Overlay && overlayOn,
                _ => b.Overlay ? overlayOn : baseOn,
            };
            if (keep) res.Add(b);
        }
        return res.ToArray();
    }
}
