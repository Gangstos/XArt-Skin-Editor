using System;
using System.Collections.Generic;
using System.Linq;

namespace XArtSkinEditor.Core;

public enum PaletteAdd { Added, AlreadyThere, Full }

/// <summary>
/// The user's saved colors. One instance is shared by the window and the MCP tools, so the list may be changed
/// from a background thread; <see cref="Changed"/> may therefore also arrive on one.
/// </summary>
public sealed class Palette
{
    public const int Max = 48;

    private static readonly string[] Defaults =
    {
        "#000000", "#ffffff", "#7f7f7f", "#3b2a20", "#6b4226", "#c68642", "#e0ac69", "#f5d0b0",
        "#e74c3c", "#e67e22", "#f1c40f", "#2ecc71", "#1abc9c", "#3498db", "#8b5cf6", "#e84393",
    };

    private readonly object _lock = new();
    private readonly List<Rgba> _colors = new();

    public event Action? Changed;

    /// <summary>Loads the saved palette; a first run gets a starter set.</summary>
    public Palette()
    {
        foreach (var hex in AppSettings.LoadPalette() ?? Defaults.ToList())
        {
            try { _colors.Add(Rgba.Parse(hex)); }
            catch (ArgumentException) { /* skip a damaged entry */ }
        }
    }

    public List<Rgba> Snapshot()
    {
        lock (_lock) return new List<Rgba>(_colors);
    }

    public PaletteAdd Add(Rgba color)
    {
        lock (_lock)
        {
            if (_colors.Contains(color)) return PaletteAdd.AlreadyThere;
            if (_colors.Count >= Max) return PaletteAdd.Full;
            _colors.Add(color);
            Save();
        }
        Changed?.Invoke();
        return PaletteAdd.Added;
    }

    public bool Remove(Rgba color)
    {
        lock (_lock)
        {
            if (!_colors.Remove(color)) return false;
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    private void Save() => AppSettings.SavePalette(_colors.Select(c => c.ToString()));
}
