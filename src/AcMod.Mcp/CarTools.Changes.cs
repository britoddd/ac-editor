using System.ComponentModel;
using System.Globalization;
using AcMod.Core.Formats;
using AcMod.Core.Workspaces;
using ModelContextProtocol.Server;

namespace AcMod.Mcp;

/// <summary>One single-value edit (QS-2): an .ini value by section and key, or a .lut/.rto row's value.</summary>
public sealed record DataEdit(
    [property: Description("The data file, such as engine.ini or power.lut.")] string File,
    [property: Description("The new value. Numbers use '.' as the decimal mark.")] string Value,
    [property: Description("For an .ini file: the section without brackets, such as ENGINE_DATA.")] string? Section = null,
    [property: Description("For an .ini file: the key, such as LIMITER.")] string? Key = null,
    [property: Description("For a .lut or .rto file: the row, counting data rows from 0.")] int? Row = null);

// CC-4, CC-5: preview and apply single-value changes through the same CarEdit session and save path as the app.
public static partial class CarTools
{
    [McpServerTool(Name = "preview_change"), Description(
        "Shows what a set of single-value edits would change in a car's data, field by field, and which files a save would write. Writes nothing. " +
        "Call it before apply_change and show the owner the result.")]
    public static string PreviewChange([Description("The car ID.")] string carId, [Description("The edits, applied in order.")] DataEdit[] edits) =>
        RunCar(carId, edit => Stage(edit, edits) is { } error ? new { error } : new { carId, changes = edit.Changes, writes = edit.WriteTargets });

    [McpServerTool(Name = "apply_change"), Description(
        "Applies a set of single-value edits to a car, as the app's Save does: a snapshot is taken first (restore it with its snapshot ID), data.acd is repacked " +
        "and verified when the car uses it, and the car is validated again afterwards. Only for edits the owner approved after preview_change.")]
    public static string ApplyChange([Description("The car ID.")] string carId, [Description("The edits, applied in order. Use the ones from preview_change.")] DataEdit[] edits) =>
        RunCar(carId, edit =>
        {
            var before = edit.Validate();
            if (before.Any(f => f.Rule == "V-14")) return new { error = $"{carId} has a model that may be encrypted (V-14). The app asks before saving such a car, so apply_change refuses; save it in the app." };
            if (Stage(edit, edits) is { } stageError) return new { error = stageError };
            var changes = edit.Changes;
            if (changes.Count == 0) return new { error = "The edits change nothing." };

            var (snapshotId, saveError) = edit.Save();
            if (snapshotId == null) return new { error = saveError };
            var after = edit.Validate();
            static string Key(AcMod.Core.Validation.Finding f) => $"{f.Rule}|{f.File}|{f.Message}";
            var known = before.Select(Key).ToHashSet();
            return new
            {
                carId, snapshotId, changes,
                errorsNow = after.Count(f => f.Severity == AcMod.Core.Validation.Severity.Error),
                newFindings = after.Where(f => !known.Contains(Key(f))).Select(f => new { f.Rule, severity = f.Severity.ToString(), f.File, f.Message, f.Fix }),
            };
        });

    /// <summary>Stages the edits in the session, in memory. Returns the first problem, or null when every edit applied.</summary>
    private static string? Stage(CarEdit edit, IReadOnlyList<DataEdit> edits)
    {
        if (edits.Count == 0) return "No edits given.";
        foreach (var e in edits)
        {
            var name = edit.FileName(e.File);
            if (name == null) return $"{edit.CarId} has no data file {e.File}.";
            var extension = Path.GetExtension(name).ToLowerInvariant();

            if (extension == ".ini")
            {
                if (e.Section == null || e.Key == null) return $"{name}: an .ini edit needs a section and a key.";
                var matches = IniFile.Parse(edit.Current(name)).Find(e.Section, e.Key);
                if (matches.Count != 1)
                {
                    return matches.Count == 0 ? $"{name} has no [{e.Section}] {e.Key}."
                        : $"{name} has [{e.Section}] {e.Key} {matches.Count} times. Which one AC uses is unverified, so change it in the app's Raw files tab.";
                }

                if (edit.SetIniValue(name, matches[0].LineIndex, e.Value) is { } error) return $"{name} [{e.Section}] {e.Key}: {error}";
            }
            else if (extension is ".lut" or ".rto")
            {
                if (e.Row is not { } row) return $"{name}: a table edit needs a row.";
                if (!double.TryParse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
                {
                    return $"{name} row {row}: \"{e.Value}\" isn't a number.";
                }

                var rows = LutFile.Parse(edit.Current(name)).Rows.Count;
                if (row < 0 || row >= rows) return $"{name} has rows 0 to {rows - 1}, not {row}.";
                edit.SetLutValue(name, row, value);
            }
            else
            {
                return $"{name}: only .ini, .lut and .rto files can be edited here.";
            }
        }

        return null;
    }
}
