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

    // WS-10: snapshots hold text data and UI files only, never KN5 models, sound banks, or textures.
    private static readonly string[] SnapshotFiles = ["data.acd", Path.Combine("sfx", "GUIDs.txt")];
    private static readonly string[] SnapshotFolders = ["data", "ui"];

    /// <summary>The snapshot IDs of a car, oldest first. IDs are UTC times, such as 20261103-142233-123.</summary>
    public string[] Snapshots(string carId)
    {
        var folder = Path.Combine(Root, "snapshots", carId);
        return CheckName(carId) == null && Directory.Exists(folder)
            ? [.. Directory.GetDirectories(folder).Select(Path.GetFileName).OfType<string>().Where(n => !n.EndsWith(".tmp")).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>Copies the car's snapshot scope (WS-10) to snapshots\&lt;car ID&gt;\&lt;snapshot ID&gt;. It appears only once complete.</summary>
    public (string? SnapshotId, string? Error) Snapshot(string carId)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (!Directory.Exists(car)) return (null, $"There is no car {carId} in the workspace.");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        var id = stamp;
        for (var n = 1; Directory.Exists(Path.Combine(Root, "snapshots", carId, id)); n++) id = $"{stamp}-{n}";

        // Copy into a .tmp folder and rename it, so a failed copy never looks like a complete snapshot (QS-8).
        var (temp, tempError) = Resolve(Path.Combine("snapshots", carId, id + ".tmp"));
        if (temp == null) return (null, tempError);
        if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        foreach (var folder in SnapshotFolders.Where(f => Directory.Exists(Path.Combine(car, f))))
        {
            Directory.CreateDirectory(Path.Combine(temp, folder)); // keeps an empty data\ too, because its existence matters (V-12)
        }

        foreach (var file in ScopeFiles(car))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(temp, file))!);
            File.Copy(Path.Combine(car, file), Path.Combine(temp, file));
            File.SetAttributes(Path.Combine(temp, file), FileAttributes.Normal); // a copied read-only flag would block cleaning up snapshots
        }

        Directory.CreateDirectory(temp); // a car with nothing in scope still gets a snapshot
        Directory.Move(temp, Path.Combine(Root, "snapshots", carId, id));
        return (id, null);
    }

    /// <summary>
    /// Writes files into a car after taking a snapshot (QS-3, WS-9). Paths are relative to the car folder and must be in the
    /// snapshot scope. Nothing is written when any path is refused.
    /// </summary>
    /// <returns>The ID of the snapshot that holds the files as they were before.</returns>
    public (string? SnapshotId, string? Error) WriteCarFiles(string carId, IReadOnlyDictionary<string, byte[]> files)
    {
        var targets = new List<(string Path, byte[] Data)>();
        foreach (var (path, data) in files)
        {
            var (full, error) = ScopedCarFile(carId, path);
            if (full == null) return (null, error);
            targets.Add((full, data));
        }

        var (snapshotId, snapshotError) = Snapshot(carId);
        if (snapshotId == null) return (null, snapshotError);
        WithRollback(carId, snapshotId, () => Replace(carId, targets));
        return (snapshotId, null);
    }

    /// <summary>
    /// Makes the car's snapshot scope match a snapshot exactly: files and folders the snapshot doesn't have are removed,
    /// so restoring a snapshot from before a pack also removes the newer data.acd (V-12). Takes a snapshot first, so a restore can be undone.
    /// </summary>
    /// <returns>The ID of the snapshot taken just before the restore.</returns>
    public (string? SnapshotId, string? Error) RestoreSnapshot(string carId, string snapshotId)
    {
        if (CheckName(snapshotId) != null || !Snapshots(carId).Contains(snapshotId))
        {
            return (null, $"Car {carId} has no snapshot {snapshotId}.");
        }

        var (beforeId, error) = Snapshot(carId);
        if (beforeId == null) return (null, error);
        WithRollback(carId, beforeId, () => MatchSnapshot(carId, snapshotId));
        return (beforeId, null);
    }

    /// <summary>Runs a change to a car. If it throws, puts the snapshot scope back to the given snapshot, then rethrows (QS-8).</summary>
    private void WithRollback(string carId, string snapshotId, Action change)
    {
        try
        {
            change();
        }
        catch (Exception failure)
        {
            try
            {
                MatchSnapshot(carId, snapshotId);
            }
            catch (Exception rollback)
            {
                throw new IOException(
                    $"Changing car {carId} failed ({failure.Message}), and putting its files back failed too ({rollback.Message}). Snapshot {snapshotId} still holds them.",
                    failure);
            }

            throw;
        }
    }

    /// <summary>
    /// Makes a car's snapshot scope match a snapshot: removes folders and files the snapshot doesn't have, and writes the files that differ.
    /// Unchanged files aren't touched, so a rollback still works when an unchanged file is read-only.
    /// </summary>
    private void MatchSnapshot(string carId, string snapshotId)
    {
        var car = Path.Combine(Root, "cars", carId);
        var snapshot = Path.Combine(Root, "snapshots", carId, snapshotId);
        string Checked((string? Path, string? Error) result) => result.Path ?? throw new IOException(result.Error);

        foreach (var folder in SnapshotFolders.Where(f => !Directory.Exists(Path.Combine(snapshot, f))))
        {
            var path = Checked(CarFile(carId, folder));
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }

        foreach (var file in ScopeFiles(car).Where(f => !File.Exists(Path.Combine(snapshot, f))))
        {
            File.Delete(Checked(ScopedCarFile(carId, file)));
        }

        var targets = new List<(string Path, byte[] Data)>();
        foreach (var file in ScopeFiles(snapshot))
        {
            var path = Checked(ScopedCarFile(carId, file));
            var data = File.ReadAllBytes(Path.Combine(snapshot, file));
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(data)) targets.Add((path, data));
        }

        foreach (var folder in SnapshotFolders.Where(f => Directory.Exists(Path.Combine(snapshot, f))))
        {
            Directory.CreateDirectory(Path.Combine(car, folder));
        }

        Replace(carId, targets);
    }

    /// <summary>
    /// Writes every file to a temp file first, then moves each over its target, so a failed write (a full disk, say) changes nothing.
    /// A move that fails midway is undone by <see cref="WithRollback"/>.
    /// </summary>
    private void Replace(string carId, List<(string Path, byte[] Data)> targets)
    {
        // Temp files live in snapshots\, not next to the target, so a crash can't leave a stray file that gets packed into data.acd.
        var temps = targets.Select((_, i) => Path.Combine(Root, "snapshots", carId, $"write-{i}.tmp")).ToArray();
        try
        {
            for (var i = 0; i < targets.Count; i++) File.WriteAllBytes(temps[i], targets[i].Data);
            for (var i = 0; i < targets.Count; i++)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targets[i].Path)!);
                File.Move(temps[i], targets[i].Path, overwrite: true);
            }
        }
        finally
        {
            foreach (var temp in temps) File.Delete(temp);
        }
    }

    /// <summary>The files in a car or snapshot folder that are in the snapshot scope, as paths relative to that folder.</summary>
    private static IEnumerable<string> ScopeFiles(string folder) =>
        SnapshotFiles.Where(f => File.Exists(Path.Combine(folder, f)))
            .Concat(SnapshotFolders.Where(f => Directory.Exists(Path.Combine(folder, f)))
                .SelectMany(f => Directory.EnumerateFiles(Path.Combine(folder, f), "*", SearchOption.AllDirectories))
                .Select(f => Path.GetRelativePath(folder, f)))
            .ToArray(); // a snapshot of the list, so callers can delete files while looping

    /// <summary>The full path of a file in a car's snapshot scope, or an error.</summary>
    private (string? Path, string? Error) ScopedCarFile(string carId, string relativePath)
    {
        var (full, error) = CarFile(carId, relativePath);
        if (full == null) return (null, error);

        var relative = Path.GetRelativePath(Path.Combine(Root, "cars", carId), full); // normalized, so data\..\x.kn5 is caught
        var inScope = SnapshotFiles.Any(f => relative.Equals(f, StringComparison.OrdinalIgnoreCase)) ||
                      SnapshotFolders.Any(f => relative.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        return inScope ? (full, null) : (null, $"\"{relativePath}\" is not a data or UI file, so it isn't snapshotted and can't be written (WS-10).");
    }

    /// <summary>The full path of a file in a car folder ("." for the folder itself), or an error for a bad car ID or a path that leaves the car folder.</summary>
    private (string? Path, string? Error) CarFile(string carId, string relativePath)
    {
        var nameError = CheckName(carId);
        if (nameError != null) return (null, $"Car ID: {nameError}");

        var (full, error) = Resolve(Path.Combine("cars", carId, relativePath));
        if (full == null) return (null, error);
        return IsSameOrInside(full, Path.Combine(Root, "cars", carId)) ? (full, null) : (null, $"\"{relativePath}\" is outside car {carId}.");
    }

    /// <summary>Null when a name can be one folder name, such as a car or snapshot ID; otherwise the reason it can't.</summary>
    private static string? CheckName(string name) =>
        name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ')
            ? $"\"{name}\" can't be a folder name. Use letters, digits, and _ with no \\ / : * ? \" < > |, and don't end with . or a space."
            : null;

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or inside it. Both are full paths; Windows paths ignore case.</summary>
    private static bool IsSameOrInside(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
