namespace AcMod.Core.Acd;

public enum DataSource
{
    None,
    Folder,
    Acd,
}

/// <summary>
/// The data AC actually uses for a car (TU-1): data.acd when it exists, otherwise the data\ folder.
/// AC ignores data\ while data.acd exists (V-12, verified in game), so an unreadable data.acd stays the source
/// and carries its <see cref="Error"/> (V-13) instead of falling back to data\.
/// </summary>
/// <param name="Files">The effective files in ordinal name order. Empty when there is an error.</param>
/// <param name="FolderIgnored">True when data\ exists next to data.acd, so edits to data\ do nothing in game (V-12 warning).</param>
public sealed record CarData(DataSource Source, IReadOnlyList<AcdEntry> Files, string? Error, bool FolderIgnored)
{
    /// <summary>Reads a car folder. The folder's name is the car ID, which gives the data.acd key.</summary>
    public static CarData Load(string carFolder)
    {
        carFolder = Path.GetFullPath(carFolder);
        var acdPath = Path.Combine(carFolder, "data.acd");
        var dataFolder = Path.Combine(carFolder, "data");
        var hasFolder = Directory.Exists(dataFolder);

        if (File.Exists(acdPath))
        {
            var acd = AcdFile.Parse(File.ReadAllBytes(acdPath), AcdKey.SourceName(acdPath));
            return new CarData(DataSource.Acd, acd.Entries, acd.Error, hasFolder);
        }

        return hasFolder
            ? new CarData(DataSource.Folder, ReadFolder(dataFolder), null, false)
            : new CarData(DataSource.None, [], "The car has no data.acd and no data folder.", false);
    }

    /// <summary>PK-1 for a car folder: packs its data\ folder for the car ID given by the folder's name, and verifies the result in memory.</summary>
    public static (byte[]? Bytes, string? Error) PackFolder(string carFolder)
    {
        var acdPath = Path.Combine(Path.GetFullPath(carFolder), "data.acd");
        return AcdFile.PackVerified(ReadFolder(Path.Combine(carFolder, "data")), AcdKey.SourceName(acdPath));
    }

    /// <summary>The top-level files of a data\ folder, in ordinal name order. No real data\ folder has subfolders (docs/VERIFIED_FACTS.md).</summary>
    public static AcdEntry[] ReadFolder(string dataFolder) =>
        Directory.GetFiles(dataFolder)
            .Select(f => new AcdEntry(Path.GetFileName(f), File.ReadAllBytes(f)))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();
}
