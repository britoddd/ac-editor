using AcMod.Core.Acd;

namespace AcMod.Core.Workspaces;

public sealed partial class Workspace
{
    /// <summary>Opens a workspace car's effective data for editing (TU-1): data.acd when it exists, otherwise data\ (V-12).</summary>
    public (CarEdit? Edit, string? Error) OpenCar(string carId)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        return Directory.Exists(car) ? (new CarEdit(this, carId, car), null) : (null, $"There is no car {carId} in the workspace.");
    }
}

/// <summary>
/// An editing session on one car's effective data (TU-1, TU-7). Changes are held in memory per data file until <see cref="Save"/>,
/// which writes through <see cref="Workspace.WriteCarFiles"/>, so it snapshots first and rolls back on failure.
/// A car that uses data\ gets those files written; a car that uses data.acd gets it repacked and verified (PK-1).
/// Unreadable data (V-13) opens read-only.
/// </summary>
public sealed class CarEdit
{
    private readonly Workspace _workspace;
    private readonly string _folder;
    private readonly Dictionary<string, byte[]> _changes = new(StringComparer.Ordinal);

    internal CarEdit(Workspace workspace, string carId, string folder)
    {
        (_workspace, CarId, _folder) = (workspace, carId, folder);
        Data = CarData.Load(folder);
    }

    public string CarId { get; }

    /// <summary>The data as last loaded or saved. <see cref="CarData.FolderIgnored"/> means data\ also exists but AC ignores it (V-12).</summary>
    public CarData Data { get; private set; }

    /// <summary>True when the data can't be read (V-13), or the car has no data. Nothing can be changed or saved.</summary>
    public bool ReadOnly => Data.Error != null;

    /// <summary>The data files with unsaved changes, in name order (TU-7).</summary>
    public IReadOnlyList<string> ChangedFiles => [.. _changes.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The car files a save writes, relative to the car folder (TU-7): data\&lt;name&gt; for each changed file,
    /// or data.acd alone when the car uses data.acd.
    /// </summary>
    public IReadOnlyList<string> WriteTargets =>
        _changes.Count == 0 ? [] : Data.Source == DataSource.Acd ? ["data.acd"] : [.. ChangedFiles.Select(n => Path.Combine("data", n))];

    /// <summary>A data file's bytes, with any unsaved change.</summary>
    public byte[] Current(string name) => _changes.TryGetValue(name, out var changed) ? changed : Original(name);

    /// <summary>
    /// Changes a data file in memory. Setting it back to its saved bytes clears the change, so <see cref="ChangedFiles"/> stays exact.
    /// Only existing data files can be changed.
    /// </summary>
    public void Set(string name, byte[] bytes)
    {
        if (ReadOnly) throw new InvalidOperationException($"{CarId} is read-only: {Data.Error}");
        if (Original(name).AsSpan().SequenceEqual(bytes)) _changes.Remove(name);
        else _changes[name] = bytes;
    }

    /// <summary>Drops every unsaved change.</summary>
    public void Discard() => _changes.Clear();

    /// <summary>Writes the unsaved changes, then reloads the data. Nothing is written when there are none.</summary>
    /// <returns>The ID of the snapshot taken before the write, or null when there was nothing to save.</returns>
    public (string? SnapshotId, string? Error) Save()
    {
        if (ReadOnly) return (null, $"{CarId} is read-only: {Data.Error}");
        if (_changes.Count == 0) return (null, null);

        var files = new Dictionary<string, byte[]>();
        if (Data.Source == DataSource.Acd)
        {
            var entries = Data.Files.Select(f => _changes.TryGetValue(f.Name, out var changed) ? f with { Data = changed } : f).ToArray();
            var (packed, packError) = AcdFile.PackVerified(entries, CarId);
            if (packed == null) return (null, packError);
            files["data.acd"] = packed;
        }
        else
        {
            foreach (var (name, bytes) in _changes) files[Path.Combine("data", name)] = bytes;
        }

        var (snapshotId, error) = _workspace.WriteCarFiles(CarId, files);
        if (snapshotId == null) return (null, error);

        _changes.Clear();
        Data = CarData.Load(_folder);
        return (snapshotId, null);
    }

    private byte[] Original(string name) =>
        Data.Files.FirstOrDefault(f => f.Name == name)?.Data ?? throw new ArgumentException($"{CarId} has no data file {name}.", nameof(name));
}
