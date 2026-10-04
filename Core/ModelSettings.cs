using System;
using System.Linq;

namespace XArtSkinEditor.Core;

public enum BodyPart { Head, Body, RightArm, LeftArm, RightLeg, LeftLeg }

/// <summary>Preview-only state shared by the UI and the MCP server: Steve/Alex and per-part overlay visibility.</summary>
public sealed class ModelSettings
{
    private readonly object _gate = new();
    private bool _slim;
    private readonly bool[] _overlay = Enumerable.Repeat(true, Enum.GetValues<BodyPart>().Length).ToArray();
    private readonly bool[] _base = Enumerable.Repeat(true, Enum.GetValues<BodyPart>().Length).ToArray();

    public event Action? Changed;

    /// <summary>True = Alex (3px arms), false = Steve (4px arms).</summary>
    public bool Slim
    {
        get { lock (_gate) return _slim; }
        set
        {
            bool changed;
            lock (_gate) { changed = _slim != value; _slim = value; }
            if (changed) Changed?.Invoke();
        }
    }

    public bool GetOverlay(BodyPart part)
    {
        lock (_gate) return _overlay[(int)part];
    }

    public void SetOverlay(BodyPart part, bool visible)
    {
        bool changed;
        lock (_gate) { changed = _overlay[(int)part] != visible; _overlay[(int)part] = visible; }
        if (changed) Changed?.Invoke();
    }

    public bool[] OverlaySnapshot()
    {
        lock (_gate) return (bool[])_overlay.Clone();
    }

    // The body part itself (the base layer), as opposed to its overlay. Both can be hidden independently.
    public bool GetBase(BodyPart part)
    {
        lock (_gate) return _base[(int)part];
    }

    public void SetBase(BodyPart part, bool visible)
    {
        bool changed;
        lock (_gate) { changed = _base[(int)part] != visible; _base[(int)part] = visible; }
        if (changed) Changed?.Invoke();
    }

    public bool[] BaseSnapshot()
    {
        lock (_gate) return (bool[])_base.Clone();
    }

    /// <summary>Accepts head, body, right_arm, left_arm, right_leg, left_leg (spaces/dashes allowed).</summary>
    public static bool TryParsePart(string text, out BodyPart part)
    {
        var s = text.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        switch (s)
        {
            case "head": part = BodyPart.Head; return true;
            case "body": part = BodyPart.Body; return true;
            case "right_arm": part = BodyPart.RightArm; return true;
            case "left_arm": part = BodyPart.LeftArm; return true;
            case "right_leg": part = BodyPart.RightLeg; return true;
            case "left_leg": part = BodyPart.LeftLeg; return true;
            default: part = default; return false;
        }
    }
}
