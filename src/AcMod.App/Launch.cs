using System.Diagnostics;
using System.IO;

namespace AcMod.App;

/// <summary>Starts other programs for the user (WS-14). Each only starts something; nothing is sent anywhere.</summary>
public static class Launch
{
    /// <summary>Kunos's ksEditor from the AC SDK (sdk\editor\ksEditor.exe), started in its own folder. Returns why it couldn't, or null.</summary>
    public static string? KsEditor(string? acFolder)
    {
        var exe = acFolder == null ? null : Path.Combine(acFolder, "sdk", "editor", "ksEditor.exe");
        if (exe == null || !File.Exists(exe)) return $"ksEditor wasn't found{(exe == null ? "; set the AC folder first" : $" at {exe}")}.";
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = true });
        return null;
    }

    /// <summary>
    /// A folder in File Explorer. Close that window before you delete, rename or install the car: an Explorer window on a folder stops Windows from
    /// moving it (seen 2026-11-03, docs/VERIFIED_FACTS.md).
    /// </summary>
    public static void Folder(string folder) => Process.Start("explorer.exe", $"\"{folder}\"");

    /// <summary>A web page or a file in its usual program.</summary>
    public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
