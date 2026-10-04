using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XArtSkinEditor.Core;

/// <summary>Standard Minecraft 64x64 skin UV layout (1.8+), so an agent knows where each body part lives.</summary>
public static class SkinLayout
{
    public sealed record Face(string Name, int X, int Y, int W, int H);

    public sealed record Part(string Name, string Layer, Face[] Faces);

    public static readonly IReadOnlyList<Part> Parts = Build();

    private static Part Box(string name, string layer, int ox, int oy, int w, int h, int d)
    {
        // Standard box unwrap: top/bottom over the sides, sides in a row: right, front, left, back.
        return new Part(name, layer, new[]
        {
            new Face("top", ox + d, oy, w, d),
            new Face("bottom", ox + d + w, oy, w, d),
            new Face("right", ox, oy + d, d, h),
            new Face("front", ox + d, oy + d, w, h),
            new Face("left", ox + d + w, oy + d, d, h),
            new Face("back", ox + d + w + d, oy + d, w, h),
        });
    }

    private static List<Part> Build() => new()
    {
        Box("head", "base", 0, 0, 8, 8, 8),
        Box("head", "overlay", 32, 0, 8, 8, 8),
        Box("body", "base", 16, 16, 8, 12, 4),
        Box("body", "overlay", 16, 32, 8, 12, 4),
        Box("right_arm", "base", 40, 16, 4, 12, 4),
        Box("right_arm", "overlay", 40, 32, 4, 12, 4),
        Box("right_leg", "base", 0, 16, 4, 12, 4),
        Box("right_leg", "overlay", 0, 32, 4, 12, 4),
        Box("left_arm", "base", 32, 48, 4, 12, 4),
        Box("left_arm", "overlay", 48, 48, 4, 12, 4),
        Box("left_leg", "base", 16, 48, 4, 12, 4),
        Box("left_leg", "overlay", 0, 48, 4, 12, 4),
    };

    public static string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Minecraft 64x64 skin layout. Coordinates: x,y = top-left pixel of the region, then width x height.");
        sb.AppendLine("The skin is an UNWRAPPED texture: 'front' is what you see from the front of the character.");
        sb.AppendLine("'base' layer is the body itself; 'overlay' (hat/jacket/sleeves/pants) is drawn slightly outside it and may be transparent.");
        sb.AppendLine("Everything not listed is unused. Slim (Alex) model: arms are 3 px wide instead of 4 (front/back/top/bottom lose 1 px; right/left stay 4 wide).");
        sb.AppendLine();
        foreach (var part in Parts)
        {
            sb.Append($"{part.Name} [{part.Layer}]: ");
            sb.AppendLine(string.Join("; ", part.Faces.Select(f => $"{f.Name} ({f.X},{f.Y}) {f.W}x{f.H}")));
        }
        return sb.ToString();
    }
}
