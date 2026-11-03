using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Formats;

namespace AcMod.Core.Workspaces;

/// <summary>The new car's ID, and what the user should look at: files that still mention the old ID, and anything that couldn't be done.</summary>
public sealed record SaveAsResult(string CarId, IReadOnlyList<string> MentionsOfOldId, IReadOnlyList<string> Notes);

// WS-7: Save as new car.
public sealed partial class Workspace
{
    private static readonly string[] TextExtensions = [".ini", ".json", ".txt", ".lut", ".rto", ".cfg"];

    /// <summary>
    /// Copies a workspace car to a new car ID (WS-7). In the copy: the folder takes the new ID, sfx\&lt;old&gt;.bank becomes &lt;new&gt;.bank,
    /// sfx\GUIDs.txt's own bank and event lines move to the new ID (other cars' lines stay), data.acd is repacked and verified with the new ID's key
    /// (the key comes from the folder name), ui_car.json gets the display name, and the app's install marker is dropped.
    /// KN5 files and lods.ini stay as they are: a model's name needn't match the car ID (V-03 dropped in M0).
    /// Other files that mention the old ID are listed, not rewritten, because what those mentions do isn't verified (M6 step 1 survey).
    /// Refuses unreadable data (V-13), a model that may be encrypted (V-14), an ID that isn't lowercase ASCII (V-01), and a taken ID.
    /// The copy is built in import.tmp\ and appears in cars\ only once complete (QS-8).
    /// </summary>
    public (SaveAsResult? Result, string? Error) SaveAsNewCar(string carId, string newId, string? displayName)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (!Directory.Exists(car)) return (null, $"There is no car {carId} in the workspace.");
        if (newId.Any(c => char.IsUpper(c) || c == ' ' || c > 127)) return (null, $"Use lowercase letters, digits and _ for the new ID (V-01), not \"{newId}\".");
        var (target, targetError) = CarFile(newId, ".");
        if (target == null) return (null, targetError);
        if (Directory.Exists(target)) return (null, $"A car {newId} is already in the workspace.");

        var data = CarData.Load(car);
        if (data.Error != null) return (null, $"{carId} can't be copied, because its data can't be read: {data.Error}");
        if (Validation.Validator.Run(car, AcFolder).Any(f => f.Rule == "V-14"))
        {
            return (null, $"{carId} has a model that may be encrypted (V-14), so it isn't copied to a new ID. This tool never works around protection.");
        }

        var (temp, tempError) = Resolve(Path.Combine("import.tmp", newId));
        if (temp == null) return (null, tempError);
        var tempRoot = Path.GetDirectoryName(temp)!;
        var notes = new List<string>();
        try
        {
            if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot); // left over from a crash
            CopyFolder(car, temp);
            File.Delete(Path.Combine(temp, InstallMarker));

            var sfx = Path.Combine(temp, "sfx");
            var bank = Directory.Exists(sfx) ? Directory.GetFiles(sfx, carId + ".bank").FirstOrDefault() : null; // Windows ignores case here
            if (bank != null) File.Move(bank, Path.Combine(sfx, newId + ".bank"));
            else notes.Add($"There was no sfx\\{carId}.bank to rename, so the new car has no sound bank of its own (V-09).");

            var guids = Path.Combine(sfx, "GUIDs.txt");
            if (File.Exists(guids)) File.WriteAllBytes(guids, GuidsFile.Parse(File.ReadAllBytes(guids)).WithCarId(carId, newId).ToBytes());

            var acd = Path.Combine(temp, "data.acd");
            if (File.Exists(acd))
            {
                var (packed, packError) = AcdFile.PackVerified(data.Files, newId);
                if (packed == null) return (null, $"data.acd couldn't be repacked for {newId}: {packError}");
                File.WriteAllBytes(acd, packed);
            }

            if (displayName != null) SetDisplayName(Path.Combine(temp, "ui", "ui_car.json"), displayName, notes);

            var mentions = MentionsOf(temp, carId, newId);
            Directory.Move(temp, target);
            if (SetCopiedFrom(newId, carId) is { } recordError) notes.Add($"The checklist couldn't record the source car: {recordError}");
            return (new SaveAsResult(newId, mentions, notes), null);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot);
        }
    }

    /// <summary>
    /// WS-8: moves the old car to the trash, but only when the new copy validates without errors, so the old one is never lost for a broken copy.
    /// </summary>
    /// <returns>The old car's trash ID.</returns>
    public (string? TrashId, string? Error) ReplacePrevious(string oldId, string newId)
    {
        if (oldId.Equals(newId, StringComparison.OrdinalIgnoreCase)) return (null, "The new car and the previous one are the same.");
        var (findings, error) = Validate(newId);
        if (findings == null) return (null, error);

        var errors = findings.Where(f => f.Severity == Validation.Severity.Error).ToList();
        if (errors.Count > 0)
        {
            return (null, $"{newId} has {errors.Count} validation error(s), so {oldId} is kept: " + string.Join("; ", errors.Take(3).Select(f => $"{f.Rule} {f.Message}")));
        }

        return DeleteCar(oldId);
    }

    private static void SetDisplayName(string json, string displayName, List<string> notes)
    {
        var ui = File.Exists(json) ? UiCarJson.Parse(File.ReadAllBytes(json)) : null;
        if (ui is { Error: null } && ui.GetString("/name") != null) File.WriteAllBytes(json, ui.WithString("/name", displayName).ToBytes());
        else notes.Add("ui\\ui_car.json has no readable \"name\", so the display name wasn't set. Set it on the Raw files tab.");
    }

    /// <summary>
    /// Text files in the new car, and entries in its data.acd, that still mention the old ID. lods.ini is left out: it names model files,
    /// which keep their names. sfx\GUIDs.txt is checked after its rewrite, so only lines for other uses of the old ID remain.
    /// </summary>
    private static List<string> MentionsOf(string folder, string oldId, string newId)
    {
        bool Mentions(byte[] bytes) => Encoding.Latin1.GetString(bytes).Contains(oldId, StringComparison.OrdinalIgnoreCase);
        var mentions = Directory.EnumerateFiles(folder, "*", NoLinks)
            .Where(f => TextExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) && new FileInfo(f).Length < 2_000_000)
            .Select(f => Path.GetRelativePath(folder, f))
            .Where(f => !f.Equals(Path.Combine("data", "lods.ini"), StringComparison.OrdinalIgnoreCase) && Mentions(File.ReadAllBytes(Path.Combine(folder, f))))
            .ToList();
        var acd = CarData.Load(folder, newId);
        if (acd.Source == DataSource.Acd && acd.Error == null)
        {
            mentions.AddRange(acd.Files.Where(f => !f.Name.Equals("lods.ini", StringComparison.OrdinalIgnoreCase) && Mentions(f.Data)).Select(f => $"data.acd: {f.Name}"));
        }

        return [.. mentions.Order(StringComparer.OrdinalIgnoreCase)];
    }
}
