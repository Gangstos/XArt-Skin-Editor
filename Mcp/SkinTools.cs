using System;
using ModelContextProtocol;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using XArtSkinEditor.Core;

namespace XArtSkinEditor.Mcp;

[McpServerToolType]
public sealed class SkinTools(Workspace ws)
{
    private const string ColorHelp = "Color as #RRGGBB, #RRGGBBAA or 'transparent' (erases).";

    // Every drawing tool acts on whichever tab is active at the moment of the call.
    private SkinDocument doc => ws.Active.Doc;
    private ModelSettings model => ws.Active.Model;

    // McpException messages reach the agent; other exceptions are reduced to a generic "error occurred".
    private static Rgba ParseColor(string text)
    {
        try { return Rgba.Parse(text); }
        catch (ArgumentException ex) { throw new McpException(ex.Message); }
    }

    private static void CheckPixel(int x, int y)
    {
        if (!SkinDocument.InBounds(x, y))
            throw new McpException($"Pixel ({x},{y}) is outside the 64x64 canvas (valid range 0-63).");
    }

    [McpServerTool(Name = "list_tabs", ReadOnly = true)]
    [Description("Lists the open skin tabs (numbered from 1). The active tab is marked; all drawing tools act on the active tab. A trailing * means unsaved changes.")]
    public string ListTabs()
    {
        var active = ws.Active;
        return string.Join("\n", ws.Snapshot().Select((t, i) =>
            $"{i + 1}. {t.Name}{(ReferenceEquals(t, active) ? " (active)" : "")}{(t.Dirty ? " *" : "")}"));
    }

    [McpServerTool(Name = "new_tab")]
    [Description("Opens a new empty skin tab and makes it active. Use it to work on a second skin without touching the current one.")]
    public string NewTab([Description("Optional tab name.")] string? name = null)
    {
        var p = ws.New(name);
        return $"created tab {ws.Snapshot().ToList().IndexOf(p) + 1}: {p.Name} (active)";
    }

    [McpServerTool(Name = "switch_tab")]
    [Description("Makes another tab active so drawing tools act on it. Pass the tab number (see list_tabs) or its name.")]
    public string SwitchTab([Description("Tab number from 1, or tab name.")] string tab)
    {
        var p = ws.Find(tab) ?? throw new McpException($"No tab '{tab}'. Call list_tabs to see the open tabs.");
        ws.Activate(p);
        return $"active tab: {p.Name}";
    }

    [McpServerTool(Name = "rename_tab")]
    [Description("Renames the active tab.")]
    public string RenameTab(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new McpException("Name must not be empty.");
        ws.Rename(ws.Active, name);
        return $"renamed to {ws.Active.Name}";
    }

    [McpServerTool(Name = "get_model_preview", ReadOnly = true)]
    [Description("Renders the skin on the 3D player model (respecting Steve/Alex and hidden overlay layers) and returns it as a PNG. Use it to check how the skin really looks. yaw 0 = front, 90 = the character's left side, 180 = back, -90 = right side; pitch > 0 looks from above.")]
    public ContentBlock[] GetModelPreview(
        [Description("Horizontal camera angle in degrees.")] float yaw = 30,
        [Description("Vertical camera angle in degrees (-85..85).")] float pitch = 15,
        [Description("Image width in px (64-1024).")] int width = 400,
        [Description("Image height in px (64-1024).")] int height = 500)
    {
        var cam = new Camera
        {
            Yaw = yaw * MathF.PI / 180f,
            Pitch = Math.Clamp(pitch, -85f, 85f) * MathF.PI / 180f,
        };
        var boxes = PlayerModel.Filter(PlayerModel.Build(model.Slim), model.OverlaySnapshot(), null, model.BaseSnapshot());
        var png = SkinRenderer.RenderPng(doc.SnapshotRgba(), boxes, cam,
            Math.Clamp(width, 64, 1024), Math.Clamp(height, 64, 1024), new Rgba(0x12, 0x13, 0x26, 255));
        return new ContentBlock[] { ImageContentBlock.FromBytes(png, "image/png") };
    }

    [McpServerTool(Name = "set_model_type")]
    [Description("Selects the player model: 'steve' (4px wide arms) or 'alex' (3px wide slim arms). Changes the 3D preview and which arm pixels are used.")]
    public string SetModelType([Description("'steve' or 'alex'.")] string type)
    {
        switch (type.Trim().ToLowerInvariant())
        {
            case "steve": model.Slim = false; break;
            case "alex": model.Slim = true; break;
            default: throw new McpException("type must be 'steve' or 'alex'.");
        }
        return $"model: {(model.Slim ? "alex" : "steve")}";
    }

    [McpServerTool(Name = "set_part_visibility")]
    [Description("Shows or hides a whole body part (its base layer) in the 3D preview and in get_model_preview, e.g. to see the legs without the torso. Does not change the texture; the part's overlay is controlled separately by set_overlay_visibility. Parts: head, body, right_arm, left_arm, right_leg, left_leg or 'all'.")]
    public string SetPartVisibility(string part, bool visible)
    {
        if (part.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            foreach (var p in Enum.GetValues<BodyPart>()) model.SetBase(p, visible);
        else if (ModelSettings.TryParsePart(part, out var bp))
            model.SetBase(bp, visible);
        else
            throw new McpException("Unknown part. Use head, body, right_arm, left_arm, right_leg, left_leg or all.");
        var state = model.BaseSnapshot();
        return string.Join(", ", Enum.GetValues<BodyPart>().Select(p => $"{p}: {(state[(int)p] ? "on" : "off")}"));
    }

    [McpServerTool(Name = "set_overlay_visibility")]
    [Description("Shows or hides the overlay (second) layer of a body part in the 3D preview and in get_model_preview. Does not change the texture. Parts: head (hat), body (jacket), right_arm, left_arm (sleeves), right_leg, left_leg (pants) or 'all'.")]
    public string SetOverlayVisibility(string part, bool visible)
    {
        if (part.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            foreach (var p in Enum.GetValues<BodyPart>()) model.SetOverlay(p, visible);
        else if (ModelSettings.TryParsePart(part, out var bp))
            model.SetOverlay(bp, visible);
        else
            throw new McpException("Unknown part. Use head, body, right_arm, left_arm, right_leg, left_leg or all.");
        var state = model.OverlaySnapshot();
        return string.Join(", ", Enum.GetValues<BodyPart>().Select(p => $"{p}: {(state[(int)p] ? "on" : "off")}"));
    }

    [McpServerTool(Name = "get_skin_layout", ReadOnly = true)]
    [Description("Describes the Minecraft 64x64 skin UV layout: where head, body, arms and legs (base + overlay layers) are on the canvas. Call this first.")]
    public string GetSkinLayout() => SkinLayout.Describe();

    [McpServerTool(Name = "get_canvas_image", ReadOnly = true)]
    [Description("Returns the current skin as a PNG image so you can look at it. Use scale 8 or more to see individual pixels (output is 64*scale px wide).")]
    public ContentBlock[] GetCanvasImage([Description("Integer upscale factor 1-16 (nearest neighbour).")] int scale = 8)
    {
        scale = Math.Clamp(scale, 1, 16);
        return new ContentBlock[]
        {
            ImageContentBlock.FromBytes(doc.EncodePng(scale), "image/png"),
        };
    }

    [McpServerTool(Name = "get_pixel", ReadOnly = true)]
    [Description("Reads one pixel and returns its color.")]
    public string GetPixel(int x, int y)
    {
        CheckPixel(x, y);
        return doc.GetPixel(x, y).ToString();
    }

    [McpServerTool(Name = "set_pixel")]
    [Description("Sets one pixel. Replaces the previous value (no blending). " + ColorHelp)]
    public string SetPixel(int x, int y, string color)
    {
        CheckPixel(x, y);
        doc.SetPixel(x, y, ParseColor(color));
        return "ok";
    }

    [McpServerTool(Name = "set_pixels")]
    [Description("Sets many pixels in one undo step. Prefer this over repeated set_pixel calls. Each entry is 'x,y,#color', e.g. [\"3,4,#ff0000\",\"3,5,#00ff00\"]. Out-of-range entries are rejected before anything is drawn.")]
    public string SetPixels([Description("Entries formatted 'x,y,color'.")] string[] pixels)
    {
        var parsed = pixels.Select(p =>
        {
            var parts = p.Split(',', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
                throw new McpException($"Bad entry '{p}', expected 'x,y,color'.");
            if (!SkinDocument.InBounds(x, y))
                throw new McpException($"Entry '{p}' is outside the canvas (0-63).");
            return (x, y, c: ParseColor(parts[2]));
        }).ToArray();
        doc.Edit(e => { foreach (var (x, y, c) in parsed) e.Set(x, y, c); });
        return $"set {parsed.Length} pixels";
    }

    [McpServerTool(Name = "fill_rect")]
    [Description("Fills a rectangle (clipped to the canvas) with one color. " + ColorHelp)]
    public string FillRect(int x, int y, int width, int height, string color)
    {
        var c = ParseColor(color);
        doc.Edit(e => e.Fill(x, y, width, height, c));
        return "ok";
    }

    [McpServerTool(Name = "draw_line")]
    [Description("Draws a 1px line between two pixels (inclusive). " + ColorHelp)]
    public string DrawLine(int x0, int y0, int x1, int y1, string color)
    {
        var c = ParseColor(color);
        doc.Edit(e => e.Line(x0, y0, x1, y1, c));
        return "ok";
    }

    [McpServerTool(Name = "flood_fill")]
    [Description("Paint-bucket: replaces the contiguous area of identical color around (x,y). " + ColorHelp)]
    public string FloodFill(int x, int y, string color)
    {
        var c = ParseColor(color);
        doc.Edit(e => e.Flood(x, y, c));
        return "ok";
    }

    [McpServerTool(Name = "clear_canvas", Destructive = true)]
    [Description("Makes the whole canvas transparent. Can be undone with undo.")]
    public string ClearCanvas()
    {
        doc.Clear();
        return "ok";
    }

    [McpServerTool(Name = "undo")]
    [Description("Undoes the last edit (a set_pixels / fill / etc. call is one step).")]
    public string Undo() => doc.Undo() ? "undone" : "nothing to undo";

    [McpServerTool(Name = "redo")]
    [Description("Redoes the last undone edit.")]
    public string Redo() => doc.Redo() ? "redone" : "nothing to redo";

    [McpServerTool(Name = "load_png", Destructive = true)]
    [Description("Replaces the canvas with a PNG file from disk (absolute path). Non-64x64 images are cropped/placed at the top-left. Also detects the model type (steve/alex) from the skin and switches the preview to it.")]
    public string LoadPng(string path)
    {
        model.Slim = doc.LoadPng(File.ReadAllBytes(path));
        return $"loaded, detected model: {(model.Slim ? "alex" : "steve")}";
    }

    [McpServerTool(Name = "export_png")]
    [Description("Saves the skin as a 64x64 PNG to an absolute path, ready to use in Minecraft. Overwrites an existing file.")]
    public string ExportPng(string path)
    {
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new McpException("Path must end with .png");
        File.WriteAllBytes(path, doc.EncodePng());
        return $"saved {path}";
    }
}
