using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using XArtSkinEditor.Core;

namespace XArtSkinEditor.Views;

/// <summary>
/// Colour themes. Every colour of the UI is a "T.*" brush resource that the XAML refers to with
/// DynamicResource; switching a theme replaces those brushes and the Fluent accent, so it applies instantly.
/// </summary>
public static class AppTheme
{
    public sealed record Def(string Id, string Name, bool Light, string[] Colors);

    // Order matches Keys.
    private static readonly string[] Keys =
    {
        "Bg", "Fg", "Muted", "Icon", "IconOn", "Bar", "BarLine", "Panel", "Options", "TabStrip", "View3D", "MapBg",
        "Line", "Head", "Sep", "Hover", "Btn", "BtnHover", "Pill", "PillHover", "PillHoverLine",
        "Accent", "AccentBtn", "AccentChk", "AccentPress", "SegOn", "SegOnLine", "LayerOn", "LayerIcon",
        "TabHover", "SwatchLine", "DialogMuted",
    };

    public static readonly Def[] All =
    {
        new("classic", "Classic Purple", false, new[]
        {
            "#0b0c18", "#e7e9f5", "#8a8fb5", "#aab0d6", "#ffffff", "#090a14", "#1c1f3d", "#10112a", "#12132a", "#0e0f20", "#121326", "#0d0e1a",
            "#23264a", "#181a33", "#2a2d55", "#1f2247", "#1b1e3a", "#252950", "#141628", "#1d2042", "#3a3e78",
            "#8b5cf6", "#7c3aed", "#4a2fb0", "#33288a", "#5b34c9", "#a78bfa", "#241f55", "#c4b5fd",
            "#14162d", "#4a4f85", "#a9aed2",
        }),
        new("pinky", "Dark Pinky", false, new[]
        {
            "#0a0a0b", "#f4e9ef", "#9a8a95", "#c9b3c1", "#ffffff", "#050505", "#1f1a1d", "#0e0d0f", "#100f11", "#0a090b", "#131113", "#0c0b0d",
            "#2a2226", "#171416", "#342a30", "#231c20", "#1c1719", "#2a2126", "#121012", "#1d171a", "#4a3640",
            "#ec4899", "#db2777", "#9d174d", "#6b1135", "#be185d", "#f472b6", "#3a1428", "#f9a8d4",
            "#131012", "#5c4753", "#cdb8c4",
        }),
        new("sun", "Sun White", true, new[]
        {
            "#fffaf0", "#2b2118", "#7a6a58", "#6b5a48", "#2b1a05", "#fff1d6", "#ecd9b0", "#fff4e0", "#fff6e6", "#fdecc8", "#f7e7c4", "#fbefd5",
            "#ecd9b0", "#fffdf7", "#e2cd9c", "#fde8bd", "#fff8ea", "#fde8bd", "#fffdf7", "#fdeccb", "#d9bd7d",
            "#f59e0b", "#d97706", "#fcd34d", "#f6b93a", "#fbbf24", "#d97706", "#fde9b6", "#b45309",
            "#fdecc8", "#c9a96a", "#6b5a48",
        }),
    };

    public static Def Current { get; private set; } = All[0];

    /// <summary>Colour of the 3D viewport background, used by the CPU renderer.</summary>
    public static Rgba ViewportColor { get; private set; } = Rgba.Parse("#121326");

    public static event Action? Changed;

    public static Def Find(string? id) => Array.Find(All, t => t.Id == id) ?? All[0];

    public static IBrush Brush(string key) =>
        Application.Current?.Resources.TryGetResource("T." + key, null, out var v) == true && v is IBrush b
            ? b
            : Brushes.Gray;

    public static void Apply(Def t)
    {
        var app = Application.Current;
        if (app is null) return;
        Current = t;

        for (var i = 0; i < Keys.Length; i++)
            app.Resources["T." + Keys[i]] = new SolidColorBrush(Color.Parse(t.Colors[i]));
        ViewportColor = Rgba.Parse(t.Colors[Array.IndexOf(Keys, "View3D")]);

        // Fluent controls (text boxes, menus, the colour picker...) follow the accent and the light/dark variant.
        var variant = t.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        foreach (var style in app.Styles)
        {
            if (style is not FluentTheme fluent) continue;
            if (fluent.Palettes.TryGetValue(variant, out var pal))
            {
                pal.Accent = Color.Parse(t.Colors[Array.IndexOf(Keys, "Accent")]);
                pal.RegionColor = Color.Parse(t.Colors[Array.IndexOf(Keys, "Bg")]);
            }
        }
        app.RequestedThemeVariant = variant;
        Changed?.Invoke();
    }
}
