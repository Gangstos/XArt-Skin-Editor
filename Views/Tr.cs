using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using XArtSkinEditor.Core;

namespace XArtSkinEditor.Views;

/// <summary>
/// Declarative translations: <c>v:Tr.Key="menu.file"</c> sets the text/header/content of a control from
/// <see cref="Loc"/>, <c>v:Tr.Tip="tip.undo"</c> sets its tooltip. <see cref="Refresh"/> re-applies everything.
/// </summary>
public static class Tr
{
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Key", typeof(Tr));

    public static readonly AttachedProperty<string?> TipProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Tip", typeof(Tr));

    private static readonly List<WeakReference<AvaloniaObject>> Targets = new();

    static Tr()
    {
        KeyProperty.Changed.AddClassHandler<AvaloniaObject>((o, _) => { Register(o); Apply(o); });
        TipProperty.Changed.AddClassHandler<AvaloniaObject>((o, _) => { Register(o); Apply(o); });
    }

    public static string? GetKey(AvaloniaObject o) => o.GetValue(KeyProperty);
    public static void SetKey(AvaloniaObject o, string? v) => o.SetValue(KeyProperty, v);
    public static string? GetTip(AvaloniaObject o) => o.GetValue(TipProperty);
    public static void SetTip(AvaloniaObject o, string? v) => o.SetValue(TipProperty, v);

    private static void Register(AvaloniaObject o)
    {
        foreach (var w in Targets)
            if (w.TryGetTarget(out var t) && ReferenceEquals(t, o)) return;
        Targets.Add(new WeakReference<AvaloniaObject>(o));
    }

    private static void Apply(AvaloniaObject o)
    {
        if (o.GetValue(KeyProperty) is { } key)
        {
            var text = Loc.T(key);
            switch (o)
            {
                case TextBlock tb: tb.Text = text; break;
                case MenuItem mi: mi.Header = text; break;
                case ContentControl cc: cc.Content = text; break;
            }
        }
        if (o.GetValue(TipProperty) is { } tip && o is Control c)
            ToolTip.SetTip(c, Loc.T(tip));
    }

    public static void Refresh()
    {
        Targets.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in Targets.ToArray())
            if (w.TryGetTarget(out var t)) Apply(t);
    }
}
