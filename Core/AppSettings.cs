using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XArtSkinEditor.Core;

/// <summary>Tiny per-user settings file: UI language, colour theme and the saved colour palette.</summary>
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

    private static JsonObject Load()
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    /// <summary>Changes one key and keeps all the others, since everything lives in one file.</summary>
    private static void Set(string key, JsonNode? value)
    {
        try
        {
            var obj = Load();
            obj[key] = value;
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, obj.ToJsonString());
        }
        catch
        {
            // Settings are a convenience; a read-only profile must not break the editor.
        }
    }

    private static string? GetString(string key) =>
        Load().TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    public static string? LoadLanguage() => GetString("language");
    public static string? LoadTheme() => GetString("theme");
    public static void SaveLanguage(string code) => Set("language", code);
    public static void SaveTheme(string id) => Set("theme", id);

    /// <summary>The saved colours as hex strings, or null when the user has never saved a palette.</summary>
    public static List<string>? LoadPalette()
    {
        if (!Load().TryGetPropertyValue("palette", out var node) || node is not JsonArray arr) return null;
        return arr.Select(n => n?.GetValue<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();
    }

    public static void SavePalette(IEnumerable<string> colors) =>
        Set("palette", new JsonArray(colors.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()));
}
