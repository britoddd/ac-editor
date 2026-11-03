using System.Globalization;
using AcMod.Core.Acd;
using AcMod.Core.Formats;

namespace AcMod.Core.Tuning;

/// <summary>
/// Peak power and torque from engine.ini and its power curve (TU-4). Power is the curve's peak torque × rpm × 2π/60 × <see cref="BoostFactor"/>,
/// the formula that predicted the PW in AC's log to within 0.02% for the tutorial car (docs/VERIFIED_FACTS.md, V-12 and M2 entries).
/// </summary>
/// <param name="BoostFactor">1 + the MAX_BOOST of every [TURBO_n]. Verified in game for one turbo only; adding several is a guess.</param>
public sealed record PowerEstimate(double PeakPowerW, double PeakPowerRpm, double PeakTorqueNm, double PeakTorqueRpm, double BoostFactor, int TurboCount)
{
    /// <summary>AC's log turns 188,460 W into 252.728622 HP, which is mechanical horsepower (745.7 W).</summary>
    public double PeakPowerHp => PeakPowerW / 745.699872;

    /// <summary>
    /// Community estimate (GTPlanet, see docs/REQUIREMENTS.md "Facts and confidence"): rated engine torque ≈ power.lut torque ÷ 0.85, without a turbo.
    /// Null when the car has a turbo.
    /// </summary>
    public double? RatedTorqueEstimateNm => TurboCount == 0 ? PeakTorqueNm / 0.85 : null;

    /// <summary>Reads engine.ini's [HEADER] POWER_CURVE file and its [TURBO_n] MAX_BOOST values from a car's data files.</summary>
    public static (PowerEstimate? Estimate, string? Error) FromData(IReadOnlyList<AcdEntry> files)
    {
        var engine = DataFiles.Ini(files, "engine.ini");
        if (engine == null) return (null, "The car has no engine.ini.");

        var curveName = DataFiles.Value(engine, "HEADER", "POWER_CURVE");
        if (curveName == null) return (null, "engine.ini has no [HEADER] POWER_CURVE.");
        var curve = DataFiles.Find(files, curveName);
        if (curve == null) return (null, $"engine.ini names the power curve {curveName}, but the car has no such file.");

        var rows = LutFile.Parse(curve.Data).Rows.Where(r => r.KeyAsNumber != null).Select(r => (Rpm: r.KeyAsNumber!.Value, Torque: r.Value)).ToList();
        if (rows.Count == 0) return (null, $"{curveName} has no rpm|torque rows.");

        var boosts = engine.Sections.Select(s => s.Name).Where(n => n.StartsWith("TURBO_", StringComparison.Ordinal)).Distinct()
            .Select(n => DataFiles.Number(engine, n, "MAX_BOOST") ?? 0).ToList();
        return (FromCurve(rows, boosts), null);
    }

    /// <summary>The estimate for a power curve of (rpm, torque) points and each turbo's MAX_BOOST.</summary>
    public static PowerEstimate FromCurve(IReadOnlyList<(double Rpm, double Torque)> curve, IReadOnlyList<double> maxBoosts)
    {
        var factor = 1 + maxBoosts.Sum();
        var power = curve.MaxBy(p => p.Torque * p.Rpm);
        var torque = curve.MaxBy(p => p.Torque);
        return new PowerEstimate(power.Torque * factor * power.Rpm * 2 * Math.PI / 60, power.Rpm, torque.Torque * factor, torque.Rpm, factor, maxBoosts.Count);
    }
}

/// <summary>Small helpers to read a car's data files. The first of several duplicate keys is used; which one AC uses is not verified.</summary>
internal static class DataFiles
{
    public static AcdEntry? Find(IReadOnlyList<AcdEntry> files, string name) =>
        files.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static IniFile? Ini(IReadOnlyList<AcdEntry> files, string name) => Find(files, name) is { } f ? IniFile.Parse(f.Data) : null;

    public static string? Value(IniFile ini, string section, string key) => ini.Find(section, key).FirstOrDefault()?.Value;

    public static double? Number(IniFile ini, string section, string key) =>
        double.TryParse(Value(ini, section, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
}
