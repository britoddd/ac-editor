using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AcMod.Core.Workspaces;

/// <summary>Finds the AC install (WS-1) by the route checked on BRITOD-DESK in docs/VERIFIED_FACTS.md ("Finding AC automatically").</summary>
public static partial class AcInstall
{
    /// <summary>AC's Steam app ID (docs/VERIFIED_FACTS.md).</summary>
    public const string SteamAppId = "244210";

    /// <summary>True when a folder has acs.exe and content\cars, as the AC install does (docs/VERIFIED_FACTS.md, "Contents of the AC folder").</summary>
    public static bool IsAcFolder(string folder) =>
        File.Exists(Path.Combine(folder, "acs.exe")) && Directory.Exists(Path.Combine(folder, "content", "cars"));

    /// <summary>The AC folder found through Steam's registry key, or null.</summary>
    public static string? Find()
    {
        if (!OperatingSystem.IsWindows()) return null;

        // HKCU holds forward slashes in lower case, such as c:/program files (x86)/steam; GetFullPath tidies that.
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        return steam == null ? null : FindInSteam(Path.GetFullPath(steam));
    }

    /// <summary>
    /// The AC folder in any Steam library listed in &lt;steam&gt;\steamapps\libraryfolders.vdf, or null. A library has AC when it holds
    /// steamapps\appmanifest_244210.acf, whose installdir names the folder under steamapps\common.
    /// </summary>
    public static string? FindInSteam(string steamFolder)
    {
        var vdf = Path.Combine(steamFolder, "steamapps", "libraryfolders.vdf");
        var libraries = File.Exists(vdf) ? Values(File.ReadAllText(vdf), "path") : [];
        // The Steam folder goes last: libraryfolders.vdf usually lists it too, with proper case, unlike the registry's lower case.
        foreach (var library in libraries.Append(steamFolder).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(manifest)) continue;

            var installDir = Values(File.ReadAllText(manifest), "installdir").FirstOrDefault();
            var ac = installDir == null ? null : Path.Combine(library, "steamapps", "common", installDir);
            if (ac != null && IsAcFolder(ac)) return ac;
        }

        return null;
    }

    /// <summary>
    /// Every value of a key in Steam's text format, where a line reads "key"  "value" and a backslash is written \\.
    /// ponytail: reads quoted pairs anywhere and ignores nesting, which is enough for "path" and "installdir".
    /// </summary>
    private static string[] Values(string text, string key) =>
        [.. QuotedPair().Matches(text).Where(m => m.Groups[1].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(m => Regex.Replace(m.Groups[2].Value, @"\\(.)", "$1"))];

    [GeneratedRegex("""
        "([^"\\]*)"[ \t]+"((?:[^"\\]|\\.)*)"
        """)]
    private static partial Regex QuotedPair();
}
