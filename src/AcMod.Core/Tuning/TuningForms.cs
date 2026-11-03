using System.Globalization;
using AcMod.Core.Formats;

namespace AcMod.Core.Tuning;

/// <summary>
/// One field of a guided form (TU-2): an .ini entry with a hint taken from the file itself.
/// <paramref name="Id"/> is "[SECTION] KEY", with " #2" and so on for a repeated key, the same naming as <see cref="DataDiff"/>.
/// <paramref name="HintFrom"/> is null when the hint is the line's own comment, or the section it was borrowed from, such as "[FRONT]".
/// </summary>
public sealed record FormField(string Id, string Section, string Key, string Value, int LineIndex, string? Hint, string? HintFrom);

/// <summary>
/// The guided forms of TU-2. A form lists every field its file has, so no field is invented and none is out of reach.
/// The M4 step 8 survey of 230 installed cars found nearly every field in 90% or more of the cars, so there is no shorter list to pick.
/// </summary>
public static class TuningForms
{
    /// <summary>The seven groups of TU-2 and the data file each one edits.</summary>
    public static readonly IReadOnlyList<(string Group, string File)> Groups =
    [
        ("Car", "car.ini"), ("Engine", "engine.ini"), ("Drivetrain", "drivetrain.ini"), ("Brakes", "brakes.ini"),
        ("Suspension", "suspensions.ini"), ("Tyres", "tyres.ini"), ("Aero", "aero.ini"),
    ];

    /// <summary>
    /// Every field of an .ini file in file order. The hint is the line's own ';' or '//' comment. When the line has none, it is the comment
    /// on the same key in another section: real files usually comment [FRONT] only (SPRING_RATE: 212 of 229 cars in [FRONT], 9 in [REAR]).
    /// </summary>
    public static IReadOnlyList<FormField> Fields(byte[] iniBytes)
    {
        var ini = IniFile.Parse(iniBytes);
        var comments = ini.Entries.Select(e => Comment(ini.Document.Lines[e.LineIndex].Content, e.Value)).ToList();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var fields = new List<FormField>();
        for (var i = 0; i < ini.Entries.Count; i++)
        {
            var e = ini.Entries[i];
            var name = e.Section == "" ? e.Key : $"[{e.Section}] {e.Key}";
            var n = seen[name] = seen.GetValueOrDefault(name) + 1;
            var (hint, from) = (comments[i], (string?)null);
            if (hint == null)
            {
                var donor = Enumerable.Range(0, ini.Entries.Count).FirstOrDefault(j => j != i && ini.Entries[j].Key == e.Key && comments[j] != null, -1);
                if (donor >= 0) (hint, from) = (comments[donor], $"[{ini.Entries[donor].Section}]");
            }

            fields.Add(new FormField(n == 1 ? name : $"{name} #{n}", e.Section, e.Key, e.Value, e.LineIndex, hint, from));
        }

        return fields;
    }

    /// <summary>
    /// The text to store for a typed value. A field that holds a single number accepts ',' as the decimal mark, as Indonesian Windows types it,
    /// and stores '.' (QS-11). Other values are kept as typed, because AC uses ',' between the parts of a vector such as GRAPHICS_OFFSET=0,-0.65,0.05.
    /// </summary>
    public static string Normalize(string current, string typed)
    {
        typed = typed.Trim();
        var isNumber = double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        var commaDecimal = typed.Replace(',', '.');
        return isNumber && typed.Count(c => c == ',') == 1 && double.TryParse(commaDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? commaDecimal
            : typed;
    }

    /// <summary>The comment after an entry's value, without its ';' or '//', or null when there is none.</summary>
    private static string? Comment(string line, string value)
    {
        var rest = line[(line.IndexOf('=') + 1)..];
        var tail = rest[(rest.IndexOf(value, StringComparison.Ordinal) + value.Length)..].Trim();
        var text = tail.StartsWith(';') ? tail[1..] : tail.StartsWith("//", StringComparison.Ordinal) ? tail[2..] : null;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
