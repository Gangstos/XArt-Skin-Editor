using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace XArtSkinEditor.Core;

/// <summary>
/// Keeps the open tabs on disk so an accidental close, a crash or a power cut loses at most a few seconds of work.
/// Each tab is a 64x64 PNG plus a small manifest (names, Steve/Alex, overlay visibility, active tab).
/// Files are written to a temp name and renamed, so a cut in the middle of a save never corrupts the last good copy.
/// </summary>
public sealed class SessionStore : IDisposable
{
    private const int QuietMs = 1500;       // save this long after the last edit...
    private const int MaxDelayMs = 6000;    // ...but never wait longer than this while editing continuously
    private static readonly Regex IdPattern = new("^[0-9a-f]{32}$", RegexOptions.Compiled);

    public static string Dir => Path.Combine(AppSettings.DataDir, "session");
    private static string ManifestPath => Path.Combine(Dir, "session.json");

    private readonly Workspace _ws;
    private readonly object _gate = new();
    private readonly object _writeGate = new();
    private Timer? _timer;
    private DateTime? _firstPending;
    private bool _disposed;

    public SessionStore(Workspace ws)
    {
        _ws = ws;
        foreach (var p in ws.Snapshot()) Hook(p);
        ws.ProjectAdded += Hook;
        ws.TabsChanged += RequestSave;
        RequestSave();
    }

    private void Hook(SkinProject p)
    {
        p.Doc.Changed += RequestSave;
        p.Model.Changed += RequestSave;
        RequestSave();
    }

    /// <summary>Schedules a save; cheap enough to call on every pixel.</summary>
    public void RequestSave()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var now = DateTime.UtcNow;
            _firstPending ??= now;
            var due = (now - _firstPending.Value).TotalMilliseconds >= MaxDelayMs ? 0 : QuietMs;
            if (_timer is null) _timer = new Timer(_ => SaveNow(), null, due, Timeout.Infinite);
            else _timer.Change(due, Timeout.Infinite);
        }
    }

    private void SaveNow()
    {
        lock (_gate) _firstPending = null;
        lock (_writeGate)
        {
            try { Write(); }
            catch
            {
                // Autosave is best effort; a locked or full disk must never take the editor down.
            }
        }
    }

    private void Write()
    {
        Directory.CreateDirectory(Dir);
        var tabs = _ws.Snapshot();
        var active = _ws.Active;

        var entries = new List<object>();
        foreach (var t in tabs)
        {
            WriteAtomic(Path.Combine(Dir, t.Id + ".png"), t.Doc.EncodePng());
            entries.Add(new
            {
                id = t.Id,
                name = t.Name,
                slim = t.Model.Slim,
                overlay = t.Model.OverlaySnapshot(),
                parts = t.Model.BaseSnapshot(),
                dirty = t.Dirty,
            });
        }
        var manifest = JsonSerializer.Serialize(new { version = 1, active = active.Id, tabs = entries });
        WriteAtomic(ManifestPath, Encoding.UTF8.GetBytes(manifest));

        // Tabs that were closed no longer need their file.
        var keep = tabs.Select(t => t.Id + ".png").ToHashSet();
        foreach (var f in Directory.GetFiles(Dir, "*.png"))
        {
            if (keep.Contains(Path.GetFileName(f))) continue;
            try { File.Delete(f); } catch { /* ignore */ }
        }
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Saves immediately (used on exit) and stops the background timer.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
        SaveNow();
    }

    /// <summary>Re-creates the saved tabs in an (empty) workspace. Returns how many were restored.</summary>
    public static int Restore(Workspace ws)
    {
        try
        {
            if (!File.Exists(ManifestPath)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(ManifestPath));
            var root = doc.RootElement;
            var activeId = root.TryGetProperty("active", out var a) ? a.GetString() : null;

            SkinProject? toActivate = null;
            var count = 0;
            foreach (var t in root.GetProperty("tabs").EnumerateArray())
            {
                try
                {
                    var id = t.GetProperty("id").GetString() ?? "";
                    if (!IdPattern.IsMatch(id)) continue;              // the id becomes a file name: accept only what we wrote
                    var png = Path.Combine(Dir, id + ".png");
                    if (!File.Exists(png)) continue;

                    var p = ws.New(t.GetProperty("name").GetString(), activate: false, id: id);
                    p.Doc.LoadPng(File.ReadAllBytes(png));
                    p.Doc.ClearHistory();

                    p.Model.Slim = t.TryGetProperty("slim", out var slim) && slim.GetBoolean();
                    if (t.TryGetProperty("overlay", out var ov))
                    {
                        var i = 0;
                        foreach (var v in ov.EnumerateArray())
                        {
                            if (i < Enum.GetValues<BodyPart>().Length) p.Model.SetOverlay((BodyPart)i, v.GetBoolean());
                            i++;
                        }
                    }

                    // Hidden body parts (older sessions have no "parts": everything stays visible).
                    if (t.TryGetProperty("parts", out var parts))
                    {
                        var i = 0;
                        foreach (var v in parts.EnumerateArray())
                        {
                            if (i < Enum.GetValues<BodyPart>().Length) p.Model.SetBase((BodyPart)i, v.GetBoolean());
                            i++;
                        }
                    }

                    // Loading marks the tab dirty; give back the state it had when it was saved.
                    if (!(t.TryGetProperty("dirty", out var d) && d.GetBoolean())) p.MarkClean();
                    if (id == activeId) toActivate = p;
                    count++;
                }
                catch
                {
                    // One damaged tab must not stop the others from coming back.
                }
            }
            if (toActivate is not null) ws.Activate(toActivate);
            return count;
        }
        catch
        {
            return 0;
        }
    }
}
