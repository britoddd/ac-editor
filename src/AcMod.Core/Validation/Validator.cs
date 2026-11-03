using AcMod.Core.Acd;
using AcMod.Core.Formats;

namespace AcMod.Core.Validation;

public enum Severity { Info, Warning, Error }

/// <summary>How well a rule is supported (VA-3), from its M0 verdict in docs/REQUIREMENTS.md.</summary>
public enum Evidence
{
    /// <summary>Checked against real cars or in game.</summary>
    Verified,

    /// <summary>From a community source or the owner's decision, not checked here.</summary>
    Community,

    /// <summary>A guess the app can't confirm, such as "may be encrypted".</summary>
    Unverified,
}

/// <summary>One validator result (VA-2): which rule, how bad, which file (relative to the car folder, or null for the car), what's wrong, and how to fix it.</summary>
public sealed record Finding(string Rule, Severity Severity, string? File, string Message, string Fix, Evidence Evidence);

/// <summary>
/// The car validator (VA-1 to VA-3). Rules and severities are the "M0 verdicts" in docs/REQUIREMENTS.md; V-03 was dropped there.
/// "Effective data" is data.acd when it exists and data\ otherwise, because AC ignores data\ while data.acd exists (V-12).
/// </summary>
public static class Validator
{
    private static readonly string[] Mandatory = ["aero.ini", "cameras.ini", "car.ini", "driver3d.ini", "engine.ini", "lods.ini", "suspensions.ini", "tyres.ini", "power.lut"];
    private static readonly string[] Behaviour = ["brakes.ini", "colliders.ini", "drivetrain.ini", "setup.ini"];
    private static readonly string[] DevExtensions = [".blend", ".blend1", ".fbx", ".psd", ".tmp", ".bak"];
    private static readonly EnumerationOptions AllFiles = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };

    /// <summary>Runs every rule on a car folder. The folder's name is the car ID. Read-only.</summary>
    public static IReadOnlyList<Finding> Run(string carFolder)
    {
        carFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(carFolder));
        var carId = Path.GetFileName(carFolder);
        var data = CarData.Load(carFolder);
        var findings = new List<Finding>();
        void Add(string rule, Severity severity, string? file, string message, string fix, Evidence evidence) =>
            findings.Add(new Finding(rule, severity, file, message, fix, evidence));

        // V-01: lowercase folder names (owner decision; the CSP wiki says encrypted cars need them; tools disagree on keys for non-ASCII names).
        if (carId.Any(c => char.IsUpper(c) || c == ' ' || c > 127))
        {
            Add("V-01", Severity.Warning, null, $"The car ID \"{carId}\" has capital letters, spaces, or non-ASCII characters.",
                "Use Save as new car (M6) to give it an ID of lowercase letters, digits, and _.", Evidence.Community);
        }

        // V-04: every installed car has collider.kn5, and AC's log loads it.
        if (!File.Exists(Path.Combine(carFolder, "collider.kn5")))
        {
            Add("V-04", Severity.Error, "collider.kn5", "collider.kn5 is missing.",
                "Export the collision mesh from Blender/ksEditor as collider.kn5 into the car folder.", Evidence.Verified);
        }

        // V-13: an unreadable data.acd makes the car read-only, and the data rules can't run.
        if (data.Error != null)
        {
            Add("V-13", Severity.Error, data.Source == DataSource.Acd ? "data.acd" : null, $"The car's data can't be read: {data.Error}",
                data.Source == DataSource.Acd ? "The car opens read-only. This tool never bypasses protection." : "Add a data folder with the car's data files.",
                Evidence.Verified);
        }
        else
        {
            var names = data.Files.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var where = data.Source == DataSource.Acd ? "data.acd" : "the data folder";
            foreach (var file in Mandatory.Where(f => !names.Contains(f)))
            {
                Add("V-05", Severity.Error, file, $"{file} is missing from {where}.", $"Add {file}; copy one from a similar car and adjust it.", Evidence.Verified);
            }

            foreach (var file in Behaviour.Where(f => !names.Contains(f)))
            {
                Add("V-06", Severity.Warning, file, $"{file} is missing from {where}.", $"Add {file}, so the car's {Path.GetFileNameWithoutExtension(file)} behaves as intended.", Evidence.Community);
            }

            CheckReferences(carFolder, data, names, Add);
        }

        // V-12: in game, AC used data.acd and ignored data\ while both existed.
        if (data.FolderIgnored)
        {
            Add("V-12", Severity.Warning, "data", "data\\ is ignored while data.acd exists.",
                "Edit through this app (it edits data.acd), or remove data.acd if you meant to use data\\.", Evidence.Verified);
        }

        // V-11: every installed car has at least one skin folder.
        var skins = Path.Combine(carFolder, "skins");
        if (!Directory.Exists(skins) || !Directory.EnumerateDirectories(skins).Any())
        {
            Add("V-11", Severity.Warning, "skins", "The car has no skin folder.", "Add skins\\<name>\\ with the car's textures and a preview.jpg.", Evidence.Verified);
        }

        // V-15: development files load fine in game but don't belong in a release; exports leave them out (PK-3).
        foreach (var file in Directory.EnumerateFiles(carFolder, "*", AllFiles).Select(f => Path.GetRelativePath(carFolder, f))
                     .Where(f => DevExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                                 || f.Equals(Path.Combine("texture", "NULL.dds"), StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            Add("V-15", Severity.Info, file, $"{file} is a development file.", "Keep it in your work folder; Export ZIP leaves .blend, .fbx, .psd, .tmp and .bak files out.", Evidence.Verified);
        }

        return findings;
    }

    /// <summary>
    /// V-02: every .kn5 that lods.ini names exists in the car folder (all 781 such references on BRITOD-DESK exist when case is ignored).
    /// V-07: every .lut or .rto that a standard data file names exists in the effective data, ignoring case. Skipped: [COAST_CURVE] FILENAME, which AC
    /// ignores (M0), Content Manager's [__CM_*] backup sections, and files AC doesn't read, such as "tyres - copy.ini" (M5 step 2 survey).
    /// </summary>
    private static void CheckReferences(string carFolder, CarData data, HashSet<string> names, Action<string, Severity, string?, string, string, Evidence> add)
    {
        foreach (var file in data.Files.Where(f => Mandatory.Concat(Behaviour).Contains(f.Name, StringComparer.OrdinalIgnoreCase) && f.Name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)))
        {
            var isLods = file.Name.Equals("lods.ini", StringComparison.OrdinalIgnoreCase);
            var isEngine = file.Name.Equals("engine.ini", StringComparison.OrdinalIgnoreCase);
            foreach (var entry in IniFile.Parse(file.Data).Entries.Where(e => !e.Section.StartsWith("__CM_", StringComparison.Ordinal)))
            {
                var value = entry.Value;
                var extension = Path.GetExtension(value).ToLowerInvariant();
                if (isLods && extension == ".kn5" && !File.Exists(Path.Combine(carFolder, value)))
                {
                    add("V-02", Severity.Error, "lods.ini", $"lods.ini [{entry.Section}] {entry.Key} names {value}, which isn't in the car folder.",
                        $"Export {value} from ksEditor into the car folder, or fix the name in lods.ini.", Evidence.Verified);
                }
                else if (extension is ".lut" or ".rto" && !(isEngine && entry.Section == "COAST_CURVE" && entry.Key == "FILENAME") && !names.Contains(value)
                         && !Mandatory.Contains(value, StringComparer.OrdinalIgnoreCase)) // a missing power.lut is already V-05
                {
                    add("V-07", Severity.Error, file.Name, $"{file.Name} [{entry.Section}] {entry.Key} names {value}, which isn't in the data.",
                        $"Add {value} to the data, or point {entry.Key} at a table the car has.", Evidence.Verified);
                }
            }
        }
    }
}
