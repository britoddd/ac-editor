using AcMod.Core.Acd;
using AcMod.Core.Formats;
using AcMod.Core.Text;

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

    /// <summary>Runs the validator on the car's saved files (VA-1). Unsaved changes aren't included.</summary>
    public IReadOnlyList<Validation.Finding> Validate() => _workspace.Validate(CarId).Findings ?? [];

    /// <summary>Every data file with any unsaved change applied, for live calculations such as the power estimate.</summary>
    public IReadOnlyList<AcdEntry> CurrentFiles => [.. Data.Files.Select(f => _changes.TryGetValue(f.Name, out var changed) ? f with { Data = changed } : f)];

    /// <summary>Every unsaved change, field by field, to show before saving (TU-7).</summary>
    public IReadOnlyList<FieldChange> Changes => DataDiff.Compare(Data.Files, CurrentFiles);

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

    /// <summary>Changes one row's value in a .lut or .rto file and nothing else in it (TU-4, QS-2). The value is written in invariant culture.</summary>
    public void SetLutValue(string name, int rowIndex, double value) => Set(name, LutFile.Parse(Current(name)).WithValue(rowIndex, value).ToBytes());

    /// <summary>Adds a gear with this ratio to drivetrain.ini, and its section to setup.ini when that file has the previous gear's (TU-9).</summary>
    /// <returns>Why it couldn't, or null.</returns>
    public string? AddGear(double ratio)
    {
        if (FileName("drivetrain.ini") is not { } drivetrain) return "The car has no drivetrain.ini.";
        var (bytes, gear, error) = Tuning.GearEdits.AddGear(Current(drivetrain), ratio);
        if (bytes == null) return error;
        Set(drivetrain, bytes);
        if (FileName("setup.ini") is { } setup && Tuning.GearEdits.AddSetupGear(Current(setup), gear) is { } setupBytes) Set(setup, setupBytes);
        return null;
    }

    /// <summary>Removes the last gear from drivetrain.ini, and its section from setup.ini (TU-9).</summary>
    /// <returns>Why it couldn't, or null.</returns>
    public string? RemoveLastGear()
    {
        if (FileName("drivetrain.ini") is not { } drivetrain) return "The car has no drivetrain.ini.";
        var (bytes, gear, error) = Tuning.GearEdits.RemoveLastGear(Current(drivetrain));
        if (bytes == null) return error;
        Set(drivetrain, bytes);
        if (FileName("setup.ini") is { } setup && Tuning.GearEdits.RemoveSetupGear(Current(setup), gear) is { } setupBytes) Set(setup, setupBytes);
        return null;
    }

    /// <summary>Applies a whole-file edit, such as adding a power curve row, to one data file (TU-9).</summary>
    /// <returns>The edit's error, or null.</returns>
    public string? Apply(string name, Func<byte[], (byte[]? Bytes, string? Error)> edit)
    {
        var (bytes, error) = edit(Current(name));
        if (bytes == null) return error;
        Set(name, bytes);
        return null;
    }

    /// <summary>The car's own spelling of a data file name (names can differ in case), or null when it has no such file.</summary>
    public string? FileName(string name) => Data.Files.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>Changes several rows of a .lut or .rto file as one change (TU-8), and nothing else in it (QS-2).</summary>
    public void SetLutValues(string name, IReadOnlyDictionary<int, double> values)
    {
        var lut = LutFile.Parse(Current(name));
        foreach (var (row, value) in values) lut = lut.WithValue(row, value);
        Set(name, lut.ToBytes());
    }

    /// <summary>
    /// Changes one .ini value, found by its line, and nothing else in the file (TU-2, QS-2). A single-number field accepts ',' as the decimal mark
    /// (<see cref="Tuning.TuningForms.Normalize"/>).
    /// </summary>
    /// <returns>Why the value can't be stored, such as a ';' in it, or null.</returns>
    public string? SetIniValue(string name, int lineIndex, string value)
    {
        var ini = IniFile.Parse(Current(name));
        var entry = ini.Entries.FirstOrDefault(e => e.LineIndex == lineIndex) ?? throw new ArgumentException($"{name} has no entry on line {lineIndex + 1}.", nameof(lineIndex));
        try
        {
            Set(name, ini.WithValue(entry, Tuning.TuningForms.Normalize(entry.Value, value)).ToBytes());
            return null;
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }
    }

    /// <summary>A data file as text, with any unsaved change (TU-3).</summary>
    public string Text(string name) => TextDocument.Parse(Current(name)).Text;

    /// <summary>
    /// Changes a data file from edited text (TU-3), keeping its encoding and BOM. A file with no "\r\n" keeps plain "\n" line breaks,
    /// because a Windows text box types "\r\n" for Enter.
    /// </summary>
    /// <returns>Why the text can't be stored in this file, or null.</returns>
    public string? SetText(string name, string text)
    {
        var saved = TextDocument.Parse(Original(name));
        if (!saved.Text.Contains("\r\n")) text = text.Replace("\r\n", "\n");
        try
        {
            Set(name, saved.WithText(text).ToBytes());
            return null;
        }
        catch (ArgumentException e)
        {
            return e.Message; // a character the file's encoding can't hold
        }
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
            var (packed, packError) = AcdFile.PackVerified(CurrentFiles, CarId);
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
