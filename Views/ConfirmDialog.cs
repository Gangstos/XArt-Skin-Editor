using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace XArtSkinEditor.Views;

/// <summary>Small modal Yes/No dialog in the app's dark style. Cancel is focused so Enter never confirms by accident.</summary>
public static class ConfirmDialog
{
    public static async Task<bool> AskAsync(Window owner, string title, string message, string confirmText, bool danger = true)
    {
        var result = false;
        var dlg = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#141628")),
            Foreground = new SolidColorBrush(Color.Parse("#e7e9f5")),
            Icon = owner.Icon,
        };

        var cancel = new Button
        {
            Content = Core.Loc.T("dlg.cancel"),
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#1b1e3a")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2a2d55")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8),
        };
        var confirm = new Button
        {
            Content = confirmText,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse(danger ? "#b42335" : "#7c3aed")),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8),
        };
        cancel.Click += (_, _) => dlg.Close();
        confirm.Click += (_, _) => { result = true; dlg.Close(); };

        dlg.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#a9aed2")) },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, confirm },
                },
            },
        };
        dlg.KeyDown += (_, e) => { if (e.Key == Key.Escape) dlg.Close(); };
        dlg.Opened += (_, _) => cancel.Focus();

        await dlg.ShowDialog(owner);
        return result;
    }
}
