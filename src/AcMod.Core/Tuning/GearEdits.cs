using System.Globalization;
using AcMod.Core.Formats;

namespace AcMod.Core.Tuning;

/// <summary>
/// Adding and removing gears and power curve rows (TU-9). Each takes a file's bytes and returns new bytes, changing only the lines involved.
/// Not verified in game: whether AC needs setup.ini's [GEAR_n] section for a new gear, and how it treats LIMITER above the last power.lut row.
/// </summary>
public static class GearEdits
{
    /// <summary>drivetrain.ini with GEAR_&lt;COUNT+1&gt;=ratio added after the last gear line and COUNT raised by one.</summary>
    /// <returns>The new file and the new gear's number, or an error.</returns>
    public static (byte[]? Bytes, int Gear, string? Error) AddGear(byte[] drivetrain, double ratio)
    {
        if (!double.IsFinite(ratio) || ratio <= 0) return (null, 0, "A gear ratio must be a number above 0.");
        var (ini, count, error) = Gears(drivetrain);
        if (ini == null) return (null, 0, error);

        var last = ini.Find("GEARS", $"GEAR_{count}")[0];
        var added = IniFile.Parse(ini.Document.WithLinesInserted(last.LineIndex + 1, [$"GEAR_{count + 1}={Number(ratio)}"]).ToBytes());
        return (WithCount(added, count + 1), count + 1, null);
    }

    /// <summary>drivetrain.ini without the last GEAR_n line and with COUNT lowered by one. At least one gear stays.</summary>
    public static (byte[]? Bytes, int Gear, string? Error) RemoveLastGear(byte[] drivetrain)
    {
        var (ini, count, error) = Gears(drivetrain);
        if (ini == null) return (null, 0, error);
        if (count < 2) return (null, 0, "The car needs at least one gear.");

        var last = ini.Find("GEARS", $"GEAR_{count}")[0];
        return (WithCount(IniFile.Parse(ini.Document.WithLinesRemoved(last.LineIndex, 1).ToBytes()), count - 1), count, null);
    }

    /// <summary>
    /// setup.ini with a [GEAR_&lt;gear&gt;] section copied from [GEAR_&lt;gear-1&gt;] (its RATIOS list and the rest) and named "Gear &lt;gear&gt;",
    /// placed after that section. Null when there's nothing to copy or the section already exists.
    /// </summary>
    public static byte[]? AddSetupGear(byte[] setup, int gear)
    {
        var ini = IniFile.Parse(setup);
        if (ini.Sections.Any(s => s.Name == $"GEAR_{gear}") || SectionLines(ini, $"GEAR_{gear - 1}") is not var (start, end)) return null;

        var block = ini.Document.Lines.Skip(start).Take(end - start + 1).Select(l => l.Content).ToList();
        block[0] = $"[GEAR_{gear}]";
        var added = IniFile.Parse(ini.Document.WithLinesInserted(end + 1, ["", .. block]).ToBytes());
        var name = added.Find($"GEAR_{gear}", "NAME").FirstOrDefault();
        return (name == null ? added : added.WithValue(name, $"Gear {gear}")).ToBytes();
    }

    /// <summary>setup.ini without the [GEAR_&lt;gear&gt;] section (and one blank line before it). Null when there's no such section.</summary>
    public static byte[]? RemoveSetupGear(byte[] setup, int gear)
    {
        var ini = IniFile.Parse(setup);
        if (SectionLines(ini, $"GEAR_{gear}") is not var (start, end)) return null;
        if (start > 0 && string.IsNullOrWhiteSpace(ini.Document.Lines[start - 1].Content)) start--;
        return ini.Document.WithLinesRemoved(start, end - start + 1).ToBytes();
    }

    /// <summary>The power curve with a new "rpm|torque" row after its last row. The rpm must be above the highest one, so the engine can rev higher with power.</summary>
    public static (byte[]? Bytes, string? Error) AddCurveRow(byte[] curve, double rpm, double torque)
    {
        if (!double.IsFinite(rpm) || !double.IsFinite(torque)) return (null, "Rpm and torque must be numbers.");
        var lut = LutFile.Parse(curve);
        var last = lut.Rows.LastOrDefault(r => r.KeyAsNumber != null);
        if (last == null) return (null, "The power curve has no rpm|torque rows.");
        if (rpm <= last.KeyAsNumber) return (null, $"The new rpm must be above the highest one, {last.Key}.");

        return (lut.Document.WithLinesInserted(last.LineIndex + 1, [$"{Number(rpm)}|{Number(torque)}"]).ToBytes(), null);
    }

    /// <summary>The power curve without its last row. At least two rows stay, so it is still a curve.</summary>
    public static (byte[]? Bytes, string? Error) RemoveLastCurveRow(byte[] curve)
    {
        var lut = LutFile.Parse(curve);
        if (lut.Rows.Count < 3) return (null, "The power curve needs at least two rows.");
        return (lut.Document.WithLinesRemoved(lut.Rows[^1].LineIndex, 1).ToBytes(), null);
    }

    /// <summary>
    /// The ratios AC's garage setup offers for a gear: the values of the .rto file named by setup.ini [GEAR_n] RATIOS, or null when the car offers none.
    /// Whether AC snaps a drivetrain.ini ratio that isn't one of these is not verified, so the app only warns.
    /// </summary>
    public static IReadOnlyList<double>? RatioOptions(IReadOnlyList<Acd.AcdEntry> files, int gear)
    {
        if (DataFiles.Ini(files, "setup.ini") is not { } setup || DataFiles.Value(setup, $"GEAR_{gear}", "RATIOS") is not { } name) return null;
        return DataFiles.Find(files, name) is { } rto ? [.. LutFile.Parse(rto.Data).Rows.Select(r => r.Value)] : null;
    }

    private static (IniFile? Ini, int Count, string? Error) Gears(byte[] drivetrain)
    {
        var ini = IniFile.Parse(drivetrain);
        var count = ini.Find("GEARS", "COUNT").FirstOrDefault();
        if (count == null || !int.TryParse(count.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            return (null, 0, "drivetrain.ini has no usable [GEARS] COUNT.");
        }

        return ini.Find("GEARS", $"GEAR_{n}").Count == 0 ? (null, 0, $"drivetrain.ini has COUNT={n} but no GEAR_{n}.") : (ini, n, null);
    }

    private static byte[] WithCount(IniFile ini, int count) =>
        ini.WithValue(ini.Find("GEARS", "COUNT")[0], count.ToString(CultureInfo.InvariantCulture)).ToBytes();

    /// <summary>A section's header line and its last entry line, or null when the file has no such section.</summary>
    private static (int Start, int End)? SectionLines(IniFile ini, string name)
    {
        var header = ini.Sections.FirstOrDefault(s => s.Name == name);
        if (header == null) return null;
        var next = ini.Sections.Where(s => s.LineIndex > header.LineIndex).Select(s => s.LineIndex).DefaultIfEmpty(int.MaxValue).Min();
        var entries = ini.Entries.Where(e => e.LineIndex > header.LineIndex && e.LineIndex < next).Select(e => e.LineIndex).ToList();
        return (header.LineIndex, entries.Count == 0 ? header.LineIndex : entries.Max());
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
