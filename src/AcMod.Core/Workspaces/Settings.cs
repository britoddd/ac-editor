using System.Text.Json;

namespace AcMod.Core.Workspaces;

/// <summary>
/// What the app remembers between runs (WS-1): the workspace folder and the AC folder. It is stored in
/// %LOCALAPPDATA%\AcMod\settings.json, the one write outside the workspace besides WS-11 installs (QS-6, owner decision 2026-11-03).
/// It never holds car data.
/// </summary>
/// <param name="ContentManagerPath">Content Manager.exe, picked once by the user (WS-14), or null.</param>
public sealed record Settings(string? WorkspaceFolder, string? AcFolder, string? ContentManagerPath = null)
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcMod", "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>The saved settings. A missing file gives empty settings; an unreadable one gives empty settings and the reason.</summary>
    public static (Settings Settings, string? Error) Load(string path)
    {
        if (!File.Exists(path)) return (new Settings(null, null), null);
        try
        {
            return (JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(path)) ?? new Settings(null, null), null);
        }
        catch (JsonException e)
        {
            return (new Settings(null, null), $"{path} can't be read ({e.Message}), so the workspace and AC folders must be chosen again.");
        }
    }

    /// <summary>Writes the settings to a temp file, then renames it, so a crash never leaves half a file.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path + ".tmp", JsonSerializer.SerializeToUtf8Bytes(this, Options));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
