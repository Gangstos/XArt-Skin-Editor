using System;
using System.Collections.Generic;
using System.Linq;

namespace XArtSkinEditor.Core;

/// <summary>One open skin (a tab): its pixels, its Steve/Alex + overlay settings and an unsaved-changes flag.</summary>
public sealed class SkinProject
{
    /// <summary>Stable identity, used as the file name of this tab in the autosaved session.</summary>
    public string Id { get; internal set; } = Guid.NewGuid().ToString("N");
    public string Name { get; internal set; }
    public SkinDocument Doc { get; } = new();
    public ModelSettings Model { get; } = new();

    /// <summary>True once the skin was edited after it was created, opened or saved.</summary>
    public bool Dirty { get; private set; }

    /// <summary>Raised when <see cref="Dirty"/> flips (not on every pixel).</summary>
    public event Action? StateChanged;

    internal SkinProject(string name)
    {
        Name = name;
        Doc.Changed += () =>
        {
            if (Dirty) return;
            Dirty = true;
            StateChanged?.Invoke();
        };
    }

    public void MarkClean()
    {
        if (!Dirty) return;
        Dirty = false;
        StateChanged?.Invoke();
    }
}

/// <summary>The open tabs. Shared by the UI and the MCP server (which always acts on the active tab). Thread-safe.</summary>
public sealed class Workspace
{
    private readonly object _gate = new();
    private readonly List<SkinProject> _tabs = new();
    private readonly Func<int, string> _defaultName;
    private int _counter;
    private SkinProject _active = null!;

    /// <summary>Tab list, order, names, dirty flags or the active tab changed.</summary>
    public event Action? TabsChanged;
    public event Action<SkinProject>? ProjectAdded;

    /// <param name="createInitial">False when tabs are about to be restored from a saved session.</param>
    public Workspace(Func<int, string> defaultName, bool createInitial = true)
    {
        _defaultName = defaultName;
        if (createInitial) New();
    }

    public SkinProject Active
    {
        get { lock (_gate) return _active; }
    }

    public IReadOnlyList<SkinProject> Snapshot()
    {
        lock (_gate) return _tabs.ToArray();
    }

    public SkinProject New(string? name = null, bool activate = true, string? id = null)
    {
        SkinProject p;
        lock (_gate)
        {
            string tabName;
            if (string.IsNullOrWhiteSpace(name))
            {
                // Default names ("Skin 3") must not collide with tabs that were restored or renamed.
                do tabName = _defaultName(++_counter);
                while (_tabs.Any(t => t.Name == tabName));
            }
            else tabName = name.Trim();

            p = new SkinProject(tabName);
            if (id is not null) p.Id = id;
            _tabs.Add(p);
            if (activate || _tabs.Count == 1) _active = p;
        }
        p.StateChanged += () => TabsChanged?.Invoke();
        ProjectAdded?.Invoke(p);
        TabsChanged?.Invoke();
        return p;
    }

    public void Activate(SkinProject p)
    {
        lock (_gate)
        {
            if (!_tabs.Contains(p) || ReferenceEquals(_active, p)) return;
            _active = p;
        }
        TabsChanged?.Invoke();
    }

    /// <summary>Closes a tab. Closing the last one leaves a fresh empty tab.</summary>
    public void Close(SkinProject p)
    {
        var needNew = false;
        lock (_gate)
        {
            var i = _tabs.IndexOf(p);
            if (i < 0) return;
            _tabs.RemoveAt(i);
            if (_tabs.Count == 0) needNew = true;
            else if (ReferenceEquals(_active, p)) _active = _tabs[Math.Max(0, i - 1)];
        }
        if (needNew) New();
        else TabsChanged?.Invoke();
    }

    public void Rename(SkinProject p, string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        lock (_gate)
        {
            if (!_tabs.Contains(p)) return;
            p.Name = name;
        }
        TabsChanged?.Invoke();
    }

    /// <summary>Finds a tab by 1-based number or by (case-insensitive) name.</summary>
    public SkinProject? Find(string key)
    {
        key = key.Trim();
        lock (_gate)
        {
            if (int.TryParse(key, out var n) && n >= 1 && n <= _tabs.Count) return _tabs[n - 1];
            return _tabs.FirstOrDefault(t => t.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
