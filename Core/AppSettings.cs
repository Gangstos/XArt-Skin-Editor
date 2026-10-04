using System;
using System.IO;
using System.Text.Json;

namespace XArtSkinEditor.Core;

/// <summary>Tiny per-user settings file (currently just the UI language).</summary>
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

    public static string? LoadLanguage()
    {
        try
        {
            using var s = File.OpenRead(FilePath);
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.TryGetProperty("language", out var v) ? v.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveLanguage(string code)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new { language = code }));
        }
        catch
        {
            // Settings are a convenience; a read-only profile must not break the editor.
        }
    }
}
