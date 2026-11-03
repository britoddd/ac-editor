using AcMod.Core.Acd;

namespace AcMod.Core.Tuning;

/// <summary>One gear's ratio and the speed at the rev limiter on front and rear tyres.</summary>
public sealed record GearSpeed(int Gear, double Ratio, double FrontKmh, double RearKmh);

/// <summary>
/// Speed per gear at the rev limiter (TU-5): rpm ÷ (gear × final) wheel turns per minute × the tyre's circumference.
/// Fields, checked on 230 installed cars in M4 step 5: drivetrain.ini [GEARS] COUNT, GEAR_n, FINAL and [TRACTION] TYPE (RWD, FWD, AWD, AWD2);
/// engine.ini [ENGINE_DATA] LIMITER; tyres.ini [COMPOUND_DEFAULT] INDEX and the RADIUS (metres) of that compound's FRONT/REAR section.
/// Not verified in game: that INDEX n means the FRONT_n/REAR_n sections (0 means FRONT/REAR), and which radius sets the speed on an
/// all-wheel-drive car whose front and rear radii differ. Tyres flex and slip, so the speedometer may read a little differently.
/// </summary>
/// <param name="Traction">[TRACTION] TYPE as written, such as RWD.</param>
public sealed record GearTable(double LimiterRpm, double Final, string Traction, int CompoundIndex, double FrontRadius, double RearRadius, IReadOnlyList<GearSpeed> Gears)
{
    /// <summary>"front", "rear", or "both", from <see cref="Traction"/>. AWD2 counts as all-wheel drive.</summary>
    public string DrivenAxle => Traction.ToUpperInvariant() switch
    {
        "RWD" => "rear",
        "FWD" => "front",
        _ => "both",
    };

    public static double SpeedKmh(double rpm, double gearRatio, double finalRatio, double tyreRadius) =>
        rpm / (gearRatio * finalRatio) * 2 * Math.PI * tyreRadius * 60 / 1000;

    /// <summary>Reads the table from a car's data files, or says which field is missing.</summary>
    public static (GearTable? Table, string? Error) FromData(IReadOnlyList<AcdEntry> files)
    {
        var (drivetrain, engine, tyres) = (DataFiles.Ini(files, "drivetrain.ini"), DataFiles.Ini(files, "engine.ini"), DataFiles.Ini(files, "tyres.ini"));
        if (drivetrain == null || engine == null || tyres == null) return (null, "The car needs drivetrain.ini, engine.ini and tyres.ini.");

        var limiter = DataFiles.Number(engine, "ENGINE_DATA", "LIMITER");
        var count = DataFiles.Number(drivetrain, "GEARS", "COUNT");
        var final = DataFiles.Number(drivetrain, "GEARS", "FINAL");
        if (limiter is not > 0) return (null, "engine.ini [ENGINE_DATA] LIMITER is missing or 0.");
        if (count is not > 0 || final is not > 0) return (null, "drivetrain.ini [GEARS] COUNT or FINAL is missing.");

        var index = (int)(DataFiles.Number(tyres, "COMPOUND_DEFAULT", "INDEX") ?? 0);
        var suffix = index == 0 ? "" : $"_{index}";
        var front = DataFiles.Number(tyres, "FRONT" + suffix, "RADIUS");
        var rear = DataFiles.Number(tyres, "REAR" + suffix, "RADIUS");
        if (front is not > 0 || rear is not > 0) return (null, $"tyres.ini has no RADIUS in [FRONT{suffix}] and [REAR{suffix}] for default compound {index}.");

        var gears = new List<GearSpeed>();
        for (var g = 1; g <= count; g++)
        {
            var ratio = DataFiles.Number(drivetrain, "GEARS", $"GEAR_{g}");
            if (ratio is not > 0) return (null, $"drivetrain.ini [GEARS] GEAR_{g} is missing.");
            gears.Add(new GearSpeed(g, ratio.Value, SpeedKmh(limiter.Value, ratio.Value, final.Value, front.Value), SpeedKmh(limiter.Value, ratio.Value, final.Value, rear.Value)));
        }

        var traction = DataFiles.Value(drivetrain, "TRACTION", "TYPE") ?? "(missing)";
        return (new GearTable(limiter.Value, final.Value, traction, index, front.Value, rear.Value, gears), null);
    }
}
