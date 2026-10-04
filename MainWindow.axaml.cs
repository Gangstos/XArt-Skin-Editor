using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XArtSkinEditor.Core;
using XArtSkinEditor.Mcp;
using XArtSkinEditor.Views;

namespace XArtSkinEditor;

public partial class MainWindow : Window
{
    private static readonly IBrush Off = new SolidColorBrush(Color.Parse("#ef4444"));
    private static readonly IBrush On = new SolidColorBrush(Color.Parse("#22c55e"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#8a8fb5"));
    private const string EyeIcon = "M2,12 C5,7 8.5,5 12,5 C15.5,5 19,7 22,12 C19,17 15.5,19 12,19 C8.5,19 5,17 2,12 Z M12,9 A3,3 0 1 1 11.99,9 Z";
    private const string CloseIcon = "M2,2 L10,10 M10,2 L2,10";

    private static readonly Dictionary<BodyPart, string> PartKeys = new()
    {
        [BodyPart.Head] = "part.head",
        [BodyPart.Body] = "part.body",
        [BodyPart.RightArm] = "part.rarm",
        [BodyPart.LeftArm] = "part.larm",
        [BodyPart.RightLeg] = "part.rleg",
        [BodyPart.LeftLeg] = "part.lleg",
    };

    private static readonly Dictionary<BodyPart, string> LayerKeys = new()
    {
        [BodyPart.Head] = "layer.head",
        [BodyPart.Body] = "layer.body",
        [BodyPart.RightArm] = "layer.rarm",
        [BodyPart.LeftArm] = "layer.larm",
        [BodyPart.RightLeg] = "layer.rleg",
        [BodyPart.LeftLeg] = "layer.lleg",
    };

    private Workspace _ws = null!;
    private SessionStore _session = null!;
    private int _restoredTabs;
    private SkinProject? _attached;
    private readonly McpHost _mcp = new();
    // Layers matrix: one row per body part, one eye for the part itself and one for its overlay.
    private readonly Dictionary<BodyPart, ToggleButton> _baseEyes = new();
    private readonly Dictionary<BodyPart, ToggleButton> _overlayEyes = new();
    private readonly Dictionary<BodyPart, TextBlock> _partLabels = new();
    private TextBlock _colBase = null!, _colOverlay = null!;
    private bool _syncing, _syncingMap, _statusIsHint = true;
    private string _toolKey = "tool.pencil";

    // Everything below acts on the active tab.
    private SkinDocument _doc => _ws.Active.Doc;
    private ModelSettings _model => _ws.Active.Model;

    public MainWindow()
    {
        Loc.Init(AppSettings.LoadLanguage());
        // Bring back the tabs of the previous session (also after a crash or power loss), then keep saving them.
        _ws = new Workspace(n => Loc.T("tab.default", n), createInitial: false);
        _restoredTabs = SessionStore.Restore(_ws);
        if (_ws.Snapshot().Count == 0) _ws.New();
        _session = new SessionStore(_ws);
        InitializeComponent();
        SetColor(ColorPick.Color);

        ColorPick.ColorChanged += (_, e) => SetColor(e.NewColor);
        Canvas.ColorPicked += c => ColorPick.Color = c;
        Model.ColorPicked += c => ColorPick.Color = c;
        Canvas.HoverChanged += (x, y) => CursorText.Text = $"{x}, {y}  {_doc.GetPixel(x, y)}";
        Model.TexelHovered += h =>
        {
            Canvas.Highlight = h is { } v ? (v.x, v.y) : null;
            CursorText.Text = h?.text ?? "";
        };

        ToolPencil.IsCheckedChanged += (_, _) => SetTool(ToolPencil, Tool.Pencil, "tool.pencil");
        ToolEraser.IsCheckedChanged += (_, _) => SetTool(ToolEraser, Tool.Eraser, "tool.eraser");
        ToolFill.IsCheckedChanged += (_, _) => SetTool(ToolFill, Tool.Fill, "tool.fill");
        ToolPicker.IsCheckedChanged += (_, _) => SetTool(ToolPicker, Tool.Picker, "tool.picker");

        GridCheck.IsCheckedChanged += (_, _) => SetGrid(GridCheck.IsChecked == true);
        MiGrid.PropertyChanged += (_, e) => { if (e.Property == MenuItem.IsCheckedProperty) SetGrid(MiGrid.IsChecked); };
        MapToggle.IsCheckedChanged += (_, _) => SetMapVisible(MapToggle.IsChecked == true);
        MiMap.PropertyChanged += (_, e) => { if (e.Property == MenuItem.IsCheckedProperty) SetMapVisible(MiMap.IsChecked); };

        UndoBtn.Click += (_, _) => _doc.Undo();
        RedoBtn.Click += (_, _) => _doc.Redo();
        ClearBtn.Click += (_, _) => ConfirmClear();
        OpenBtn.Click += OnOpen;
        SaveBtn.Click += OnSave;
        MiNew.Click += (_, _) => _ws.New();
        MiClose.Click += (_, _) => _ = CloseTabAsync(_ws.Active);
        MiOpen.Click += OnOpen;
        MiSave.Click += OnSave;
        MiExit.Click += (_, _) => Close();
        MiUndo.Click += (_, _) => _doc.Undo();
        MiRedo.Click += (_, _) => _doc.Redo();
        MiClear.Click += (_, _) => ConfirmClear();
        MiReset.Click += (_, _) => Model.ResetView();
        NewTabBtn.Click += (_, _) => _ws.New();

        SwatchHex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitHex(); Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { ShowCurrentHex(); Focus(); e.Handled = true; }
        };
        SwatchHex.LostFocus += (_, _) => CommitHex();

        BuildModelControls();
        BuildMcpControls();
        BuildTabs();
        BuildLanguageControls();

        // Optional: `XArtSkinEditor --mcp-port 5199` starts the MCP server right away (it is off by default).
        Opened += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "--mcp-port");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
            {
                PortBox.Value = port;
                await _mcp.StartAsync(_ws, port);
                UpdateMcpUi();
            }
        };

        KeyDown += OnKeyDown;
        Closed += async (_, _) =>
        {
            _session.Dispose();   // final save: nothing is lost when the window is closed
            await _mcp.DisposeAsync();
        };

        if (_restoredTabs > 0) ShowStatus(Loc.T("msg.restored", _restoredTabs));
    }

    // ---- Tabs ------------------------------------------------------------------------------

    private void BuildTabs()
    {
        foreach (var p in _ws.Snapshot()) HookProject(p);
        _ws.ProjectAdded += p => Dispatcher.UIThread.Post(() => HookProject(p));
        _ws.TabsChanged += () => Dispatcher.UIThread.Post(OnTabsChanged);
        OnTabsChanged();
    }

    /// <summary>Model settings can be changed by the MCP server; mirror them while that tab is active.</summary>
    private void HookProject(SkinProject p)
    {
        p.Model.Changed += () =>
        {
            if (ReferenceEquals(p, _ws.Active)) Dispatcher.UIThread.Post(SyncModelControls);
        };
    }

    private void OnTabsChanged()
    {
        RebuildTabs();
        var active = _ws.Active;
        if (!ReferenceEquals(active, _attached)) AttachActive(active);
    }

    /// <summary>Points the canvas, the 3D view and the model controls at another tab.</summary>
    private void AttachActive(SkinProject p)
    {
        _attached = p;
        Canvas.Document = p.Doc;
        Model.Document = p.Doc;
        Model.Settings = p.Model;
        Canvas.Highlight = null;
        CursorText.Text = "";
        SyncModelControls();
    }

    private void RebuildTabs()
    {
        for (var i = TabStrip.Children.Count - 1; i >= 0; i--)
            if (!ReferenceEquals(TabStrip.Children[i], NewTabBtn)) TabStrip.Children.RemoveAt(i);

        var active = _ws.Active;
        var index = 0;
        foreach (var p in _ws.Snapshot())
            TabStrip.Children.Insert(index++, BuildTab(p, ReferenceEquals(p, active)));
    }

    private Control BuildTab(SkinProject p, bool isActive)
    {
        var title = new TextBlock
        {
            Text = (p.Dirty ? "• " : "") + p.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 170,
        };
        var x = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(CloseIcon) };
        x.Classes.Add("x");
        var close = new Button { Content = x };
        close.Classes.Add("tabclose");
        ToolTip.SetTip(close, Loc.T("tab.close"));
        close.Click += (_, _) => _ = CloseTabAsync(p);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { title, close } };
        var tab = new Border { Child = row };
        tab.Classes.Add("tab");
        tab.Classes.Set("active", isActive);

        tab.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) _ws.Activate(p);
        };
        tab.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Middle) _ = CloseTabAsync(p);
        };
        tab.DoubleTapped += (_, _) => BeginRename(p, row);
        return tab;
    }

    private void BeginRename(SkinProject p, StackPanel row)
    {
        var box = new TextBox { Text = p.Name, MinWidth = 110, MaxWidth = 170, MinHeight = 0, Padding = new Thickness(4, 1) };
        row.Children[0] = box;
        Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); });

        var done = false;
        void Commit()
        {
            if (done) return;
            done = true;
            _ws.Rename(p, box.Text ?? p.Name);
            RebuildTabs();
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { done = true; RebuildTabs(); e.Handled = true; }
        };
        box.LostFocus += (_, _) => Commit();
    }

    private async System.Threading.Tasks.Task CloseTabAsync(SkinProject p)
    {
        if (p.Dirty && !p.Doc.IsEmpty())
        {
            var ok = await ConfirmDialog.AskAsync(this, Loc.T("dlg.close.title", p.Name), Loc.T("dlg.close.msg"), Loc.T("dlg.close.ok"));
            if (!ok) return;
        }
        _ws.Close(p);
    }

    // ---- Language --------------------------------------------------------------------------

    private void BuildLanguageControls()
    {
        foreach (var lang in Loc.Languages)
            LanguageCombo.Items.Add(new ComboBoxItem { Content = lang.NativeName });
        LanguageCombo.SelectedIndex = IndexOfLanguage(Loc.Current);
        LanguageCombo.SelectionChanged += (_, _) =>
        {
            if (LanguageCombo.SelectedIndex < 0) return;
            Loc.SetLanguage(Loc.Languages[LanguageCombo.SelectedIndex].Code);
        };
        Loc.Changed += () => Dispatcher.UIThread.Post(() =>
        {
            AppSettings.SaveLanguage(Loc.Current);
            ApplyLanguage();
        });
        ApplyLanguage();
    }

    private static int IndexOfLanguage(string code)
    {
        for (var i = 0; i < Loc.Languages.Count; i++)
            if (Loc.Languages[i].Code == code) return i;
        return 1;
    }

    /// <summary>Re-applies every translated string, including the ones built in code.</summary>
    private void ApplyLanguage()
    {
        Tr.Refresh();
        ToolName.Text = Loc.T(_toolKey);
        RefreshLayerTexts();
        if (_statusIsHint) StatusText.Text = Loc.T("status.hint");
        RebuildTabs();   // close-button tooltips
        UpdateMcpUi();
    }

    private void ShowStatus(string text)
    {
        _statusIsHint = false;
        StatusText.Text = text;
    }

    // ---- Workspace -------------------------------------------------------------------------

    private void SetMapVisible(bool on)
    {
        if (_syncingMap) return;
        _syncingMap = true;
        try
        {
            MapToggle.IsChecked = on;
            MiMap.IsChecked = on;
            MapPanel.IsVisible = on;
            MapSplitter.IsVisible = on;
            GridCheck.IsVisible = on;
            var cols = Workspace.ColumnDefinitions;
            cols[0].Width = new GridLength(1, GridUnitType.Star);
            cols[1].Width = on ? GridLength.Auto : new GridLength(0);
            cols[2].Width = on ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }
        finally { _syncingMap = false; }
    }

    private void SetGrid(bool on)
    {
        Canvas.ShowGrid = on;
        Canvas.InvalidateVisual();
        if (GridCheck.IsChecked != on) GridCheck.IsChecked = on;
        if (MiGrid.IsChecked != on) MiGrid.IsChecked = on;
    }

    // ---- MCP server ------------------------------------------------------------------------

    private void BuildMcpControls()
    {
        PortBox.Value = McpHost.DefaultPort;
        McpToggleBtn.Click += async (_, _) =>
        {
            McpToggleBtn.IsEnabled = false;
            try
            {
                if (_mcp.IsRunning) await _mcp.StopAsync();
                else await _mcp.StartAsync(_ws, (int)(PortBox.Value ?? McpHost.DefaultPort));
            }
            finally
            {
                McpToggleBtn.IsEnabled = true;
                UpdateMcpUi();
            }
        };
        CopyCmdBtn.Click += async (_, _) =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            await clipboard.SetTextAsync(McpCmdBox.Text ?? "");
            CopyCmdBtn.Content = Loc.T("mcp.copied");
            await System.Threading.Tasks.Task.Delay(1200);
            CopyCmdBtn.Content = Loc.T("mcp.copy");
        };
        foreach (var c in McpClients) McpClientCombo.Items.Add(c.Name);
        McpClientCombo.SelectedIndex = 0;
        McpClientCombo.SelectionChanged += (_, _) => UpdateMcpSnippet();
        _mcp.StateChanged += () => Dispatcher.UIThread.Post(UpdateMcpUi);
        UpdateMcpUi();
    }

    // How to attach each agent. File = where the JSON goes; null = a terminal command; ChatGPT needs a tunnel.
    private sealed record McpClient(string Name, string? File, Func<string, string> Snippet, bool Tunnel = false);

    private static readonly McpClient[] McpClients =
    {
        new("Claude Code", null, u => $"claude mcp add --transport http xart-skin {u}"),
        new("Cursor", "~/.cursor/mcp.json", u => Json("mcpServers", $"\"url\": \"{u}\"")),
        new("GitHub Copilot (VS Code)", ".vscode/mcp.json", u => Json("servers", $"\"type\": \"http\", \"url\": \"{u}\"")),
        new("Gemini CLI", null, u => $"gemini mcp add --transport http xart-skin {u}"),
        new("Antigravity", "mcp_config.json", u => Json("mcpServers", $"\"serverUrl\": \"{u}\"")),
        new("ChatGPT", null, u => $"cloudflared tunnel --url {u.Replace("/mcp", "")}", Tunnel: true),
    };

    private static string Json(string root, string body) =>
        $"{{\n  \"{root}\": {{\n    \"xart-skin\": {{ {body} }}\n  }}\n}}";

    private void UpdateMcpSnippet()
    {
        var i = Math.Max(0, McpClientCombo.SelectedIndex);
        var c = McpClients[i];
        McpCmdBox.Text = c.Snippet(_mcp.Url);
        McpWhere.Text = c.Tunnel ? Loc.T("mcp.chatgpt")
            : c.File is { } f ? Loc.T("mcp.where_file", f) : Loc.T("mcp.where");
    }

    private void UpdateMcpUi()
    {
        var running = _mcp.IsRunning;
        var brush = running ? On : Off;
        McpDot.Fill = brush;
        McpLabel.Foreground = brush;
        McpState.Text = running ? $":{_mcp.Port}" : Loc.T("mcp.off");

        PortBox.IsEnabled = !running;
        McpToggleBtn.Content = Loc.T(running ? "mcp.stop" : "mcp.start");
        McpToggleBtn.Classes.Set("primary", !running);
        McpToggleBtn.Classes.Set("danger", running);
        McpInfo.IsVisible = running;
        McpUrlBox.Text = _mcp.Url;
        UpdateMcpSnippet();

        if (_mcp.Error is { } err)
        {
            McpStatus.Text = Loc.T("mcp.err", err);
            McpStatus.Foreground = Off;
        }
        else
        {
            McpStatus.Text = Loc.T(running ? "mcp.status_on" : "mcp.status_off");
            McpStatus.Foreground = Muted;
        }
    }

    // ---- Model / layers --------------------------------------------------------------------

    private void BuildModelControls()
    {
        BuildLayerGrid();

        ShowAllBtn.Click += (_, _) => SetAllLayers(true);
        HideAllBtn.Click += (_, _) => SetAllLayers(false);
        ModelSteve.IsCheckedChanged += (_, _) => { if (!_syncing && ModelSteve.IsChecked == true) _model.Slim = false; };
        ModelAlex.IsCheckedChanged += (_, _) => { if (!_syncing && ModelAlex.IsChecked == true) _model.Slim = true; };
        LayerCombo.SelectionChanged += (_, _) => Model.Layer = (PaintLayer)Math.Max(0, LayerCombo.SelectedIndex);
        ResetViewBtn.Click += (_, _) => Model.ResetView();
    }

    private void BuildLayerGrid()
    {
        var parts = Enum.GetValues<BodyPart>();
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,78,78"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", parts.Length + 1))),
            Margin = new Thickness(2, 0),
        };

        TextBlock Header(int col)
        {
            var t = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            t.Classes.Add("h");
            Grid.SetColumn(t, col);
            grid.Children.Add(t);
            return t;
        }
        _colBase = Header(1);
        _colOverlay = Header(2);

        ToggleButton Eye(int row, int col, Action<bool> apply)
        {
            var icon = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(EyeIcon) };
            icon.Classes.Add("ic");
            var eye = new ToggleButton { IsChecked = true, Content = icon, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(1) };
            eye.Classes.Add("layer");
            eye.IsCheckedChanged += (_, _) =>
            {
                if (!_syncing) apply(eye.IsChecked == true);
            };
            Grid.SetRow(eye, row);
            Grid.SetColumn(eye, col);
            grid.Children.Add(eye);
            return eye;
        }

        var r = 1;
        foreach (var part in parts)
        {
            var p = part;
            var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetRow(label, r);
            grid.Children.Add(label);
            _partLabels[part] = label;
            _baseEyes[part] = Eye(r, 1, on => _model.SetBase(p, on));
            _overlayEyes[part] = Eye(r, 2, on => _model.SetOverlay(p, on));
            r++;
        }
        OverlayPanel.Children.Add(grid);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Part names, column headers and eye tooltips in the current language.</summary>
    private void RefreshLayerTexts()
    {
        if (_colBase is null) return;
        _colBase.Text = Loc.T("col.base");
        _colOverlay.Text = Loc.T("col.overlay");
        foreach (var part in Enum.GetValues<BodyPart>())
        {
            var name = Cap(Loc.T(PartKeys[part]));
            _partLabels[part].Text = name;
            ToolTip.SetTip(_baseEyes[part], name);
            ToolTip.SetTip(_overlayEyes[part], Loc.T(LayerKeys[part]));
        }
    }

    private void SetAllLayers(bool visible)
    {
        foreach (var part in Enum.GetValues<BodyPart>())
        {
            _model.SetBase(part, visible);
            _model.SetOverlay(part, visible);
        }
    }

    private void SyncModelControls()
    {
        _syncing = true;
        try
        {
            ModelSteve.IsChecked = !_model.Slim;
            ModelAlex.IsChecked = _model.Slim;
            foreach (var part in Enum.GetValues<BodyPart>())
            {
                _baseEyes[part].IsChecked = _model.GetBase(part);
                _overlayEyes[part].IsChecked = _model.GetOverlay(part);
            }
        }
        finally { _syncing = false; }
    }

    // ---- Tools, keyboard, files ------------------------------------------------------------

    private void SetColor(Color c)
    {
        Canvas.Color = c;
        Model.Color = c;
        Swatch.Background = new SolidColorBrush(c);
        ShowCurrentHex();
    }

    private void ShowCurrentHex()
    {
        var c = Canvas.Color;
        SwatchHex.Text = new Rgba(c.R, c.G, c.B, c.A).ToString();
    }

    /// <summary>Applies the typed hex color; anything unparseable is discarded and the current color is shown again.</summary>
    private void CommitHex()
    {
        try
        {
            var c = Rgba.Parse(SwatchHex.Text ?? "");
            ColorPick.Color = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch (ArgumentException)
        {
            ShowStatus(Loc.T("msg.bad_color"));
        }
        ShowCurrentHex();
    }

    private async void ConfirmClear()
    {
        var ok = await ConfirmDialog.AskAsync(this, Loc.T("dlg.clear.title"), Loc.T("dlg.clear.msg"), Loc.T("dlg.clear.ok"));
        if (ok) _doc.Clear();
    }

    private void SetTool(RadioButton rb, Tool tool, string key)
    {
        if (rb.IsChecked != true) return;
        Canvas.Tool = tool;
        Model.Tool = tool;
        _toolKey = key;
        ToolName.Text = Loc.T(key);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.Z: _doc.Undo(); e.Handled = true; break;
                case Key.Y: _doc.Redo(); e.Handled = true; break;
                case Key.O: OnOpen(null, new RoutedEventArgs()); e.Handled = true; break;
                case Key.S: OnSave(null, new RoutedEventArgs()); e.Handled = true; break;
                case Key.T: _ws.New(); e.Handled = true; break;
                case Key.W: _ = CloseTabAsync(_ws.Active); e.Handled = true; break;
            }
            return;
        }
        var tool = e.Key switch { Key.P => ToolPencil, Key.E => ToolEraser, Key.F => ToolFill, Key.I => ToolPicker, _ => null };
        if (tool is null) return;
        tool.IsChecked = true;
        e.Handled = true;
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("file.open_title"),
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.ImagePng },
        });

        foreach (var file in files)
        {
            try
            {
                await using var s = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms);

                // Each opened skin gets its own tab; an untouched empty tab is reused instead of left behind.
                var name = Path.GetFileNameWithoutExtension(file.Name);
                var target = _ws.Active.Doc.IsEmpty() ? _ws.Active : _ws.New(name);
                _ws.Rename(target, name);
                target.Model.Slim = target.Doc.LoadPng(ms.ToArray());
                target.Doc.ClearHistory();   // a freshly opened file has no history: Ctrl+Z must not blank it
                target.MarkClean();
                ShowStatus(Loc.T("msg.opened", file.Name, target.Model.Slim ? "Alex" : "Steve"));
            }
            catch (Exception ex)
            {
                ShowStatus(Loc.T("msg.open_failed", ex.Message));
            }
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var project = _ws.Active;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.T("file.save_title"),
            SuggestedFileName = project.Name + ".png",
            DefaultExtension = "png",
            FileTypeChoices = new[] { FilePickerFileTypes.ImagePng },
        });
        if (file is null) return;
        await using var s = await file.OpenWriteAsync();
        s.SetLength(0);
        await s.WriteAsync(project.Doc.EncodePng());
        project.MarkClean();
        ShowStatus(Loc.T("msg.saved", file.Name));
    }
}
