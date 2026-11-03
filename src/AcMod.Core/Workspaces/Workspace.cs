namespace AcMod.Core.Workspaces;

/// <summary>
/// The Mod Workspace (WS-1): cars\, snapshots\, trash\, and exports\ under one root folder.
/// Every write must get its path from <see cref="Resolve"/>, so nothing lands outside the root (QS-6, hard rule 5).
/// </summary>
public sealed class Workspace
{
    public static readonly string[] Folders = ["cars", "snapshots", "trash", "exports"];

    /// <summary>The full path of the workspace folder, without a trailing separator (except for a drive root).</summary>
    public string Root { get; }

    private Workspace(string root) => Root = root;

    /// <summary>Opens a workspace, creating any missing workspace folders. Refuses a folder that overlaps the AC install (WS-2).</summary>
    /// <param name="acFolder">The AC install folder, or null when it isn't known yet.</param>
    public static (Workspace? Workspace, string? Error) Open(string root, string? acFolder)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (acFolder != null)
        {
            var ac = Path.TrimEndingDirectorySeparator(Path.GetFullPath(acFolder));
            if (IsSameOrInside(root, ac) || IsSameOrInside(ac, root))
            {
                return (null, $"The workspace {root} overlaps the AC folder {ac}. Choose a folder outside AC, such as D:\\ACModding.");
            }
        }

        foreach (var folder in Folders) Directory.CreateDirectory(Path.Combine(root, folder));
        return (new Workspace(root), null);
    }

    /// <summary>
    /// The full path for a path relative to the workspace root, or an error when it could lead outside the workspace:
    /// a rooted path, a ".." that climbs out, a ":" (a drive or an NTFS alternate stream), or a junction or symlink on the way.
    /// </summary>
    public (string? Path, string? Error) Resolve(string relativePath)
    {
        if (relativePath.Length == 0 || Path.IsPathRooted(relativePath) || relativePath.Contains(':') ||
            relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return (null, $"\"{relativePath}\" is not a plain relative path.");
        }

        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (full.Equals(Root, StringComparison.OrdinalIgnoreCase) || !IsSameOrInside(full, Root))
        {
            return (null, $"\"{relativePath}\" is outside the workspace.");
        }

        // A junction or symlink inside the workspace can point anywhere, so refuse one anywhere on the path.
        // ponytail: checked before the write, so a link created in between isn't caught; fine for one local user.
        for (var p = full; !p.Equals(Root, StringComparison.OrdinalIgnoreCase); p = Path.GetDirectoryName(p)!)
        {
            if (Path.Exists(p) && File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint))
            {
                return (null, $"\"{p}\" is a junction or symlink, which could lead outside the workspace.");
            }
        }

        return (full, null);
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or inside it. Both are full paths; Windows paths ignore case.</summary>
    private static bool IsSameOrInside(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
