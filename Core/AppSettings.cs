using System;
using System.IO;
using System.Text.Json;

namespace XArtSkinEditor.Core;

/// <summary>Tiny per-user settings file (UI language and colour theme).</summary>
public static class AppSettings
{
    /// <summary>
    /// Where settings and the autosaved session live. The XART_DATA_DIR environment variable overrides it,
    /// which lets tests run against a throw-away folder instead of the user's real data.
    /// </summary>
    public static string DataDir
    {
        get
        {
            var overrideDir = Environment.GetEnvironmentVariable("XART_DATA_DIR");
            return !string.IsNullOrWhiteSpace(overrideDir)
                ? overrideDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XArtSkinEditor");
        }
    }

    private static string Dir => DataDir;
    private static string FilePath => Path.Combine(Dir, "settings.json");

    private static string? Read(string name)
    {
        try
        {
            using var s = File.OpenRead(FilePath);
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.TryGetProperty(name, out var v) ? v.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void Write(string? language, string? theme)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new { language, theme }));
        }
        catch
        {
            // Settings are a convenience; a read-only profile must not break the editor.
        }
    }

    public static string? LoadLanguage() => Read("language");
    public static string? LoadTheme() => Read("theme");

    // Each setter keeps the other value, since both live in one file.
    public static void SaveLanguage(string code) => Write(code, LoadTheme());
    public static void SaveTheme(string id) => Write(LoadLanguage(), id);
}
