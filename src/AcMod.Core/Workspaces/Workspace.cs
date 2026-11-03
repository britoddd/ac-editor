using System.IO.Compression;
using AcMod.Core.Formats;

namespace AcMod.Core.Workspaces;

/// <summary>A folder's total file size in bytes, and the latest change to it or anything inside it.</summary>
public sealed record FolderInfo(string Name, long Size, DateTime LastChangedUtc);

/// <summary>A car in the workspace (WS-3). <paramref name="DisplayName"/> is null when ui_car.json is missing or unreadable.</summary>
public sealed record CarInfo(string Id, string? DisplayName, long Size, DateTime LastChangedUtc);

/// <summary>
/// The Mod Workspace (WS-1): cars\, snapshots\, trash\, and exports\ under one root folder.
/// Every write must get its path from <see cref="Resolve"/>, so nothing lands outside the root (QS-6, hard rule 5).
/// </summary>
public sealed partial class Workspace
{
    public static readonly string[] Folders = ["cars", "snapshots", "trash", "exports"];

    /// <summary>The full path of the workspace folder, without a trailing separator (except for a drive root).</summary>
    public string Root { get; }

    /// <summary>The AC install folder given to <see cref="Open"/>, as a full path, or null when it isn't known.</summary>
    public string? AcFolder { get; }

    private Workspace(string root, string? acFolder) => (Root, AcFolder) = (root, acFolder);

    /// <summary>Opens a workspace, creating any missing workspace folders. Refuses a folder that overlaps the AC install (WS-2).</summary>
    /// <param name="acFolder">The AC install folder, or null when it isn't known yet.</param>
    public static (Workspace? Workspace, string? Error) Open(string root, string? acFolder)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var ac = acFolder == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(acFolder));
        if (ac != null && (IsSameOrInside(root, ac) || IsSameOrInside(ac, root)))
        {
            return (null, $"The workspace {root} overlaps the AC folder {ac}. Choose a folder outside AC, such as D:\\ACModding.");
        }

        foreach (var folder in Folders) Directory.CreateDirectory(Path.Combine(root, folder));
        return (new Workspace(root, ac), null);
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

    /// <summary>The cars in cars\ (WS-3), by ID. Validation status joins this in M5.</summary>
    public CarInfo[] Cars() =>
        [.. Subfolders("cars").Select(f => new CarInfo(f.Name, DisplayName(Path.Combine(Root, "cars", f.Name)), f.Size, f.LastChangedUtc))];

    /// <summary>Cars that haven't changed for at least <paramref name="unchangedFor"/> (WS-5).</summary>
    public CarInfo[] UnusedCars(TimeSpan unchangedFor) => [.. Cars().Where(c => DateTime.UtcNow - c.LastChangedUtc >= unchangedFor)];

    /// <summary>
    /// The folders inside one workspace folder, such as each car's snapshots or each trash entry, with their size (WS-5).
    /// Junctions and symlinks are skipped, so a link can't make a folder look bigger.
    /// </summary>
    /// <param name="workspaceFolder">One of <see cref="Folders"/>.</param>
    public FolderInfo[] Subfolders(string workspaceFolder)
    {
        if (!Folders.Contains(workspaceFolder)) throw new ArgumentException($"Not a workspace folder: {workspaceFolder}", nameof(workspaceFolder));

        return [.. new DirectoryInfo(Path.Combine(Root, workspaceFolder)).EnumerateDirectories("*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint })
            .Select(dir =>
            {
                // A folder's own time changes when it is created, so a car imported with old files still counts as recent.
                var (size, last) = (0L, dir.LastWriteTimeUtc);
                foreach (var entry in dir.EnumerateFileSystemInfos("*", NoLinks))
                {
                    if (entry is FileInfo file) size += file.Length;
                    if (entry.LastWriteTimeUtc > last) last = entry.LastWriteTimeUtc;
                }

                return new FolderInfo(dir.Name, size, last);
            })
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>ui\ui_car.json's "name", or null when the file is missing or isn't JSON even when read leniently (V-10).</summary>
    private static string? DisplayName(string car)
    {
        var path = Path.Combine(car, "ui", "ui_car.json");
        return File.Exists(path) ? UiCarJson.Parse(File.ReadAllBytes(path)).GetString("/name") : null;
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

        var id = UniqueName(Path.Combine(Root, "snapshots", carId), "");

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

    /// <summary>
    /// Imports one car from a folder or a .zip file into cars\ (WS-4). The car is the folder that holds data.acd or a data folder, at any
    /// depth, so content\cars\&lt;id&gt;\ inside a mod ZIP works. Its folder name is the car ID, or the ZIP's name when the car files sit at
    /// the top of the ZIP. Files outside the car folder, such as a readme, are left out. Files stream to disk one at a time (QS-7) into
    /// import.tmp\, and the car appears in cars\ only once complete (QS-8).
    /// </summary>
    /// <returns>The car ID.</returns>
    public (string? CarId, string? Error) ImportCar(string source)
    {
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var isZip = File.Exists(source);
        if (!isZip && !Directory.Exists(source)) return (null, $"{source} doesn't exist.");
        if (isZip && !source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return (null, "Only folders and .zip files can be imported. Extract other archives first.");
        // A loose car folder in the workspace root is fine to import; the workspace itself, or a folder the app manages, isn't.
        if (!isZip && (IsSameOrInside(Root, source) || Folders.Append("import.tmp").Any(f => IsSameOrInside(source, Path.Combine(Root, f)))))
        {
            return (null, "Choose a folder that isn't the workspace itself or inside its cars, snapshots, trash, or exports folders.");
        }

        try
        {
            using var zip = isZip ? ZipFile.OpenRead(source) : null;
            // Every file as its path relative to the source, with / separators, and a way to copy it to a destination.
            var files = zip != null
                ? [.. zip.Entries.Select(e => (Path: e.FullName.Replace('\\', '/'), Copy: (Action<string>)(dest => e.ExtractToFile(dest)))).Where(f => !f.Path.EndsWith('/'))]
                : Directory.EnumerateFiles(source, "*", NoLinks).Select(f => (Path: Path.GetRelativePath(source, f).Replace('\\', '/'), Copy: (Action<string>)(dest => File.Copy(f, dest)))).ToArray();

            var roots = CarRoots(files.Select(f => f.Path));
            if (roots.Length == 0) return (null, $"No car found in {Path.GetFileName(source)}. A car folder holds data.acd or a data folder.");
            if (roots.Length > 1)
            {
                return (null, $"{Path.GetFileName(source)} holds {roots.Length} cars ({string.Join(", ", roots.Select(r => r == "" ? "(top level)" : r))}). Import one car folder at a time.");
            }

            var root = roots[0];
            var carId = root != "" ? root[(root.LastIndexOf('/') + 1)..] : isZip ? Path.GetFileNameWithoutExtension(source) : Path.GetFileName(source);
            var (car, carError) = CarFile(carId, ".");
            if (car == null) return (null, carError);
            if (Directory.Exists(car)) return (null, $"A car {carId} is already in the workspace. Delete or rename it first.");
            var (temp, tempError) = Resolve(Path.Combine("import.tmp", carId));
            if (temp == null) return (null, tempError);

            var tempRoot = Path.GetDirectoryName(temp)!;
            try
            {
                if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot); // left over from an import that crashed
                var prefix = root == "" ? "" : root + "/";
                foreach (var (path, copy) in files.Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    // A ZIP entry can name any path, such as ../../evil.dll ("zip slip"), so each one must stay inside the car folder.
                    var (dest, destError) = Resolve(Path.Combine("import.tmp", carId, path[prefix.Length..]));
                    if (dest == null || dest == temp || !IsSameOrInside(dest, temp))
                    {
                        return (null, $"Refused {path}: {destError ?? "it points outside the car folder"}. The source may be unsafe, so nothing was imported.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    copy(dest);
                    File.SetAttributes(dest, File.GetAttributes(dest) & ~FileAttributes.ReadOnly); // cars in the workspace must stay editable
                }

                Directory.CreateDirectory(temp);
                Directory.Move(temp, car);
                return (carId, null);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot);
            }
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        {
            return (null, $"Can't read {Path.GetFileName(source)}: {e.Message}");
        }
    }

    /// <summary>The car folders among relative paths with / separators: each folder that holds data.acd or a data folder, minus any inside another.</summary>
    private static string[] CarRoots(IEnumerable<string> paths)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parts in paths.Select(p => p.Split('/')))
        {
            var data = Array.FindIndex(parts, s => s.Equals("data", StringComparison.OrdinalIgnoreCase));
            if (data >= 0 && data < parts.Length - 1) roots.Add(string.Join('/', parts[..data]));
            else if (parts[^1].Equals("data.acd", StringComparison.OrdinalIgnoreCase)) roots.Add(string.Join('/', parts[..^1]));
        }

        return [.. roots.Where(r => !roots.Any(o => o != r && (o == "" || r.StartsWith(o + "/", StringComparison.OrdinalIgnoreCase)))).Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Moves a car to trash\&lt;car ID&gt;_&lt;UTC time&gt; (WS-6). The move is one rename, so it either happens fully or not at all.</summary>
    /// <returns>The trash ID, which is the folder name in trash\.</returns>
    public (string? TrashId, string? Error) DeleteCar(string carId)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (!Directory.Exists(car)) return (null, $"There is no car {carId} in the workspace.");

        var trashId = UniqueName(Path.Combine(Root, "trash"), carId + "_");
        var (target, targetError) = Resolve(Path.Combine("trash", trashId));
        if (target == null) return (null, targetError);

        Directory.Move(car, target);
        return (trashId, null);
    }

    /// <summary>Moves a trash entry back to cars\. Refuses when a car with that ID exists again.</summary>
    /// <returns>The car ID.</returns>
    public (string? CarId, string? Error) RestoreFromTrash(string trashId)
    {
        var (entry, carId, error) = TrashEntry(trashId);
        if (entry == null) return (null, error);

        var (car, carError) = CarFile(carId!, ".");
        if (car == null) return (null, carError);
        if (Directory.Exists(car)) return (null, $"A car {carId} is already in the workspace. Delete or rename it first.");

        Directory.Move(entry, car);
        return (carId, null);
    }

    /// <summary>The warning every front end shows before <see cref="DeletePermanently"/>, naming what will be lost (WS-6).</summary>
    public (string? Warning, string? Error) PermanentDeleteWarning(string trashId)
    {
        var (entry, carId, error) = TrashEntry(trashId);
        if (entry == null) return (null, error);

        var size = Subfolders("trash").Single(f => f.Name == trashId).Size;
        var snapshots = Snapshots(carId!);
        var snapshotSize = Subfolders("snapshots").FirstOrDefault(f => f.Name.Equals(carId, StringComparison.OrdinalIgnoreCase))?.Size ?? 0;
        var what = snapshots.Length == 0 ? "" :
            SnapshotsGoToo(trashId, carId!) ? $" and its {snapshots.Length} snapshot(s) ({Megabytes(snapshotSize)})" :
            $". Its {snapshots.Length} snapshot(s) are kept, because another car {carId} is still in the workspace or the trash";
        return ($"WARNING: this permanently deletes {trashId} ({Megabytes(size)}){what}. " +
                $"This can't be undone, and the files don't go to the Windows Recycle Bin. Type {carId} to confirm.", null);
    }

    /// <summary>
    /// Deletes a trash entry for good, with the car's snapshots (WS-6, hard rule 4). Show <see cref="PermanentDeleteWarning"/> first.
    /// <paramref name="typedCarId"/> is what the user typed to confirm, and it must match the car ID exactly.
    /// The snapshots are kept while another car with the same ID is in cars\ or the trash, because they may be that car's history.
    /// </summary>
    public string? DeletePermanently(string trashId, string typedCarId)
    {
        var (entry, carId, error) = TrashEntry(trashId);
        if (entry == null || carId == null) return error;
        if (typedCarId != carId) return $"Type the car ID {carId} exactly to delete it permanently.";

        var (snapshots, snapshotsError) = Resolve(Path.Combine("snapshots", carId));
        if (snapshots == null) return snapshotsError;
        var snapshotsGo = SnapshotsGoToo(trashId, carId);
        DeleteFolder(entry);
        if (snapshotsGo && Directory.Exists(snapshots)) DeleteFolder(snapshots);
        return null;
    }

    /// <summary>True when no other car with this ID is in cars\ or the trash. Car IDs ignore case, like Windows folder names.</summary>
    private bool SnapshotsGoToo(string trashId, string carId) =>
        !Directory.Exists(Path.Combine(Root, "cars", carId)) &&
        !Directory.GetDirectories(Path.Combine(Root, "trash")).Select(Path.GetFileName).Any(n =>
            n != trashId && n!.LastIndexOf('_') > 0 && n[..n.LastIndexOf('_')].Equals(carId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Deletes a folder and everything in it, read-only files included (some mods ship them). Junctions and symlinks inside are removed
    /// as links, and nothing they point to is touched. Directory.Delete(recursive) isn't used, because without admin rights it throws on
    /// a junction (it first tries to unmount it as a volume mount point).
    /// </summary>
    private static void DeleteFolder(string folder)
    {
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0 }))
        {
            var isLink = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
            if (entry is DirectoryInfo && !isLink)
            {
                DeleteFolder(entry.FullName);
                continue;
            }

            if (!isLink) entry.Attributes = FileAttributes.Normal;
            entry.Delete();
        }

        Directory.Delete(folder);
    }

    /// <summary>Everything in a folder tree, hidden files included, without following junctions or symlinks out of it.</summary>
    private static readonly EnumerationOptions NoLinks = new() { AttributesToSkip = FileAttributes.ReparsePoint, RecurseSubdirectories = true };

    private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";

    /// <summary>The full path and car ID of a trash entry, or an error.</summary>
    private (string? Path, string? CarId, string? Error) TrashEntry(string trashId)
    {
        // The time part has no "_", so the car ID is everything before the last one.
        var split = trashId.LastIndexOf('_');
        var (entry, error) = CheckName(trashId) == null && split > 0 ? Resolve(Path.Combine("trash", trashId)) : (null, null);
        return entry != null && Directory.Exists(entry)
            ? (entry, trashId[..split], null)
            : (null, null, error ?? $"There is no {trashId} in the trash.");
    }

    /// <summary>A new folder name in <paramref name="folder"/>: the prefix plus the UTC time, such as 20261103-142233-123, with -1, -2, ... on a clash.</summary>
    private static string UniqueName(string folder, string prefix)
    {
        var stamp = prefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        var name = stamp;
        for (var n = 1; Directory.Exists(Path.Combine(folder, name)); n++) name = $"{stamp}-{n}";
        return name;
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
            if (Directory.Exists(path)) DeleteFolder(path);
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
