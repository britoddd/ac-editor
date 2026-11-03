using System.Text.Json;
using AcMod.Core.Acd;
using AcMod.Core.Formats;

namespace AcMod.Core.Workspaces;

// WS-13: a new car from an empty template, and copying data or sound into it.
public sealed partial class Workspace
{
    /// <summary>
    /// Creates a car from an empty template (WS-13): the folders data\, ui\, sfx\ and skins\default\, and a ui\ui_car.json holding only the name.
    /// Everything else, the model, collider, data and sound, is the first-car checklist's work, so its steps start at To do.
    /// </summary>
    public string? NewEmptyCar(string carId, string displayName)
    {
        if (carId.Any(c => char.IsUpper(c) || c == ' ' || c > 127)) return $"Use lowercase letters, digits and _ for the car ID (V-01), not \"{carId}\".";
        var (car, error) = CarFile(carId, ".");
        if (car == null) return error;
        if (Directory.Exists(car)) return $"A car {carId} is already in the workspace.";

        var (temp, tempError) = Resolve(Path.Combine("import.tmp", carId));
        if (temp == null) return tempError;
        var tempRoot = Path.GetDirectoryName(temp)!;
        try
        {
            if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot);
            foreach (var folder in new[] { "data", "ui", "sfx", Path.Combine("skins", "default") }) Directory.CreateDirectory(Path.Combine(temp, folder));
            File.WriteAllBytes(Path.Combine(temp, "ui", "ui_car.json"), JsonSerializer.SerializeToUtf8Bytes(new { name = displayName }, new JsonSerializerOptions { WriteIndented = true }));
            Directory.Move(temp, car);
            return null;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) DeleteFolder(tempRoot);
        }
    }

    /// <summary>
    /// Copies another car's physics data files into this car's data\, through <see cref="WriteCarFiles"/>, so a snapshot is taken first.
    /// The source can be a workspace car or Kunos's formula_k sample (owner decision, 2026-11-03). Refuses a car that uses data.acd,
    /// because AC would ignore the copied data\ (V-12).
    /// </summary>
    /// <returns>How many files were copied, or an error.</returns>
    public (int? Copied, string? Error) CopyDataFrom(string carId, string sourceFolder)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (File.Exists(Path.Combine(car, "data.acd"))) return (null, $"{carId} uses data.acd, so AC would ignore copied data\\ files (V-12).");
        if (!Directory.Exists(sourceFolder)) return (null, $"{sourceFolder} doesn't exist.");

        var source = CarData.Load(sourceFolder);
        if (source.Error != null) return (null, $"The source's data can't be read: {source.Error}");
        var (snapshot, writeError) = WriteCarFiles(carId, source.Files.ToDictionary(f => Path.Combine("data", f.Name), f => f.Data));
        return snapshot == null ? (null, writeError) : (source.Files.Count, null);
    }

    /// <summary>
    /// Copies another car's sound into this car: its sfx\&lt;source ID&gt;.bank as sfx\&lt;car ID&gt;.bank, and its GUIDs.txt lines moved to this car's ID.
    /// The lines come from the source's own sfx\GUIDs.txt, or else from AC's shared content\sfx\GUIDs.txt, which lists the Kunos cars; that is how the
    /// tutorial car got the cobra's sound in M0 (docs/VERIFIED_FACTS.md, "Car sound files"). Refuses when this car already has a bank, which a snapshot
    /// wouldn't keep.
    /// </summary>
    public string? CopySoundFrom(string carId, string sourceFolder)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return error;
        var sourceId = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder)));
        var bank = Path.Combine(sourceFolder, "sfx", sourceId + ".bank");
        if (!File.Exists(bank)) return $"{sourceId} has no sfx\\{sourceId}.bank to copy.";

        var (target, targetError) = CarFile(carId, Path.Combine("sfx", carId + ".bank"));
        if (target == null) return targetError;
        if (File.Exists(target)) return $"{carId} already has sfx\\{carId}.bank. Delete it yourself first if you want to replace it.";

        var own = Path.Combine(sourceFolder, "sfx", "GUIDs.txt");
        var shared = AcFolder == null ? null : Path.Combine(AcFolder, "content", "sfx", "GUIDs.txt");
        var guids = File.Exists(own) ? GuidsFile.Parse(File.ReadAllBytes(own))
            : shared != null && File.Exists(shared) ? OnlyLinesOf(GuidsFile.Parse(File.ReadAllBytes(shared)), sourceId)
            : null;
        if (guids == null || guids.Entries.Count == 0) return $"No GUIDs.txt names {sourceId}: neither its own sfx\\GUIDs.txt nor AC's shared one.";

        var (snapshot, writeError) = WriteCarFiles(carId, new Dictionary<string, byte[]> { [Path.Combine("sfx", "GUIDs.txt")] = guids.WithCarId(sourceId, carId).ToBytes() });
        if (snapshot == null) return writeError;
        File.Copy(bank, target); // a new file, so nothing is lost
        return null;
    }

    /// <summary>A GUIDs.txt with only one car's bank and event lines, taken from AC's shared file.</summary>
    private static GuidsFile OnlyLinesOf(GuidsFile shared, string carId)
    {
        var lines = shared.Entries
            .Where(e => (e.Kind == "bank" && e.Path.Equals(carId, StringComparison.OrdinalIgnoreCase)) ||
                        (e.Kind == "event" && e.Path.StartsWith($"cars/{carId}/", StringComparison.OrdinalIgnoreCase)))
            .Select(e => shared.Document.Lines[e.LineIndex].Content + "\r\n");
        return GuidsFile.Parse(System.Text.Encoding.ASCII.GetBytes(string.Concat(lines)));
    }
}
