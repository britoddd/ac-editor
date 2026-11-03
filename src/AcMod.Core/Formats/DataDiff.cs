using System.Globalization;
using AcMod.Core.Acd;

namespace AcMod.Core.Formats;

/// <summary>
/// One difference between two sets of data files (TU-6, TU-7).
/// <paramref name="Field"/> is "[SECTION] KEY" for an .ini file, the row key (such as an rpm) for a .lut or .rto file,
/// "(whole file)" when a file exists on one side only, or "(other text)" when the text differs but no field does
/// (comments, spacing, line endings) or the file isn't an .ini, .lut or .rto. A repeated key gets " #2", " #3", and so on.
/// <paramref name="Before"/> is null for something added, and <paramref name="After"/> is null for something removed.
/// Both are null for "(other text)".
/// </summary>
public sealed record FieldChange(string File, string Field, string? Before, string? After);

public static class DataDiff
{
    /// <summary>Compares two sets of data files field by field. File names ignore case, like Windows; files are listed in name order.</summary>
    public static IReadOnlyList<FieldChange> Compare(IEnumerable<AcdEntry> before, IEnumerable<AcdEntry> after)
    {
        var left = before.ToDictionary(f => f.Name, f => f.Data, StringComparer.OrdinalIgnoreCase);
        var right = after.ToDictionary(f => f.Name, f => f.Data, StringComparer.OrdinalIgnoreCase);
        var changes = new List<FieldChange>();
        foreach (var name in left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            var (a, b) = (left.GetValueOrDefault(name), right.GetValueOrDefault(name));
            if (a == null || b == null)
            {
                changes.Add(new FieldChange(name, "(whole file)", a == null ? null : "present", b == null ? null : "present"));
                continue;
            }

            if (a.AsSpan().SequenceEqual(b)) continue;

            var (fieldsA, fieldsB) = (Fields(name, a), Fields(name, b));
            var fileChanges = fieldsA == null || fieldsB == null ? [] : CompareFields(name, fieldsA, fieldsB);
            changes.AddRange(fileChanges.Count > 0 ? fileChanges : [new FieldChange(name, "(other text)", null, null)]);
        }

        return changes;
    }

    private static List<FieldChange> CompareFields(string file, List<(string Field, string Value)> before, List<(string Field, string Value)> after)
    {
        var afterValues = after.ToDictionary(f => f.Field, f => f.Value, StringComparer.Ordinal);
        var beforeFields = before.Select(f => f.Field).ToHashSet(StringComparer.Ordinal);
        var changes = new List<FieldChange>();
        foreach (var (field, value) in before)
        {
            if (!afterValues.TryGetValue(field, out var newValue)) changes.Add(new FieldChange(file, field, value, null));
            else if (newValue != value) changes.Add(new FieldChange(file, field, value, newValue));
        }

        changes.AddRange(after.Where(f => !beforeFields.Contains(f.Field)).Select(f => new FieldChange(file, f.Field, null, f.Value)));
        return changes;
    }

    /// <summary>A file's fields in file order, or null when it isn't an .ini, .lut or .rto file.</summary>
    private static List<(string Field, string Value)>? Fields(string name, byte[] bytes)
    {
        var fields = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".ini" => IniFile.Parse(bytes).Entries.Select(e => (Field: e.Section == "" ? e.Key : $"[{e.Section}] {e.Key}", e.Value)),
            ".lut" or ".rto" => LutFile.Parse(bytes).Rows.Select(r => (Field: r.Key, Value: r.Value.ToString(CultureInfo.InvariantCulture))),
            _ => null,
        };
        if (fields == null) return null;

        // Number repeated keys, so the 2nd LIMITER in a section compares with the 2nd LIMITER on the other side.
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return [.. fields.Select(f =>
        {
            var n = seen[f.Field] = seen.GetValueOrDefault(f.Field) + 1;
            return (n == 1 ? f.Field : $"{f.Field} #{n}", f.Value);
        })];
    }
}
