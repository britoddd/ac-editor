using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Tuning;

namespace AcMod.Core.Tests.Tuning;

public sealed class TuningTests
{
    private static AcdEntry File(string name, string text) => new(name, Encoding.UTF8.GetBytes(text));

    private static AcdEntry[] Car(string traction = "RWD", string compoundIndex = "1", string limiter = "6500", string gears = "GEAR_1=2.36\r\nGEAR_2=1.73\r\n") =>
    [
        File("drivetrain.ini", $"[TRACTION]\r\nTYPE={traction}\r\n[GEARS]\r\nCOUNT=2\r\n{gears}FINAL=3.10 ; final gear ratio\r\n"),
        File("engine.ini", $"[HEADER]\r\nPOWER_CURVE=power.lut\r\n[ENGINE_DATA]\r\nLIMITER={limiter}\r\n"),
        File("tyres.ini", $"[COMPOUND_DEFAULT]\r\nINDEX={compoundIndex}\r\n[FRONT]\r\nRADIUS=0.300\r\n[REAR]\r\nRADIUS=0.310\r\n" +
                          "[FRONT_1]\r\nRADIUS=0.275\r\n[REAR_1]\r\nRADIUS=0.285\r\n"),
    ];

    [Fact]
    public void Speed_is_wheel_turns_per_minute_times_the_tyre_circumference()
    {
        // 6500 rpm ÷ (2.36 × 3.10) = 888.46 wheel rpm; × 2π × 0.285 m = 1591.0 m/min = 95.46 km/h.
        Assert.Equal(95.46, GearTable.SpeedKmh(6500, 2.36, 3.10, 0.285), precision: 2);
    }

    [Fact]
    public void The_gear_table_uses_the_limiter_and_the_default_compounds_radius()
    {
        var (table, error) = GearTable.FromData(Car());

        Assert.Null(error);
        Assert.Equal(6500, table!.LimiterRpm);
        Assert.Equal((0.275, 0.285), (table.FrontRadius, table.RearRadius)); // INDEX=1 reads [FRONT_1] and [REAR_1]
        Assert.Equal([1, 2], table.Gears.Select(g => g.Gear));
        Assert.Equal(95.46, table.Gears[0].RearKmh, precision: 2);
        Assert.Equal(92.11, table.Gears[0].FrontKmh, precision: 2);
        Assert.Equal("rear", table.DrivenAxle);
    }

    [Fact]
    public void Compound_index_0_reads_FRONT_and_REAR()
    {
        var (table, _) = GearTable.FromData(Car(compoundIndex: "0"));

        Assert.Equal((0.300, 0.310), (table!.FrontRadius, table.RearRadius));
    }

    [Theory]
    [InlineData("RWD", "rear")]
    [InlineData("FWD", "front")]
    [InlineData("AWD", "both")]
    [InlineData("AWD2", "both")] // 16 installed cars use AWD2 (M4 step 5 survey)
    public void The_driven_axle_follows_the_traction_type(string traction, string axle)
    {
        Assert.Equal(axle, GearTable.FromData(Car(traction)).Table!.DrivenAxle);
    }

    [Fact]
    public void A_lower_limiter_lowers_every_gears_speed_in_proportion()
    {
        var full = GearTable.FromData(Car()).Table!;
        var lower = GearTable.FromData(Car(limiter: "5500")).Table!;

        Assert.Equal(full.Gears[0].RearKmh * 5500 / 6500, lower.Gears[0].RearKmh, precision: 6);
    }

    [Theory]
    [InlineData("0", "GEAR_1=2.36\r\nGEAR_2=1.73\r\n", "LIMITER")]
    [InlineData("6500", "GEAR_1=2.36\r\n", "GEAR_2")]
    [InlineData("6500", "GEAR_1=2.36\r\nGEAR_2=1.73\r\n", "RADIUS")]
    public void A_missing_field_is_named(string limiter, string gears, string missing)
    {
        var compound = missing == "RADIUS" ? "3" : "1"; // there's no [FRONT_3]
        var (table, error) = GearTable.FromData(Car(compoundIndex: compound, limiter: limiter, gears: gears));

        Assert.Null(table);
        Assert.Contains(missing, error);
    }

    [Fact]
    public void Power_is_peak_torque_times_rpm_times_the_boost_factor()
    {
        var estimate = PowerEstimate.FromCurve([(1000, 100), (4000, 200), (6000, 180)], [2.0]);

        Assert.Equal(3, estimate.BoostFactor);
        Assert.Equal(6000, estimate.PeakPowerRpm); // 180 × 6000 beats 200 × 4000
        Assert.Equal(180 * 3 * 6000 * 2 * Math.PI / 60, estimate.PeakPowerW, precision: 6);
        Assert.Equal((600, 4000), (estimate.PeakTorqueNm, estimate.PeakTorqueRpm));
        Assert.Null(estimate.RatedTorqueEstimateNm); // the ÷0.85 rule is for engines without a turbo
        Assert.Equal(200 / 0.85, PowerEstimate.FromCurve([(4000, 200)], []).RatedTorqueEstimateNm!.Value, precision: 6);
    }

    [Fact]
    public void Power_reads_the_curve_engine_ini_names_and_adds_every_turbo()
    {
        AcdEntry[] files =
        [
            File("engine.ini", "[HEADER]\r\nPOWER_CURVE=custom.lut\r\n[TURBO_0]\r\nMAX_BOOST=0.5\r\n[TURBO_1]\r\nMAX_BOOST=0.25\r\n"),
            File("custom.lut", "; rpm|torque\r\n1000|100\r\n5000|200\r\n"),
        ];

        var (estimate, error) = PowerEstimate.FromData(files);

        Assert.Null(error);
        Assert.Equal("custom.lut", PowerEstimate.CurveFile(files));
        Assert.Equal((1.75, 2), (estimate!.BoostFactor, estimate.TurboCount));
        Assert.Equal(PowerEstimate.PowerW(5000, 200, 1.75), estimate.PeakPowerW);
        Assert.Equal(350, estimate.PeakTorqueNm);
        Assert.Contains("no such file", PowerEstimate.FromData([files[0]]).Error);
    }

    [Fact]
    public void The_tutorial_cars_estimate_matches_the_PW_in_ACs_log()
    {
        var car = Path.Combine(Fixtures.LocalRoot, "acm_tutorial_basic");
        if (!Directory.Exists(car)) return; // gitignored real car, missing on a fresh clone

        var data = CarData.Load(car);
        var (estimate, _) = PowerEstimate.FromData(data.Files);
        var (table, _) = GearTable.FromData(data.Files);

        // The log said PW:188460 and 252.728622 HP with this power.lut (docs/VERIFIED_FACTS.md, M2 baseline and M3 install).
        Assert.InRange(estimate!.PeakPowerW, 188460 * 0.999, 188460 * 1.001);
        Assert.Equal(252.73, estimate.PeakPowerHp, precision: 0);
        Assert.Equal("AWD", table!.Traction);
        Assert.Equal(95.46, table.Gears[0].RearKmh, precision: 2); // 1st gear at 6500 rpm on the 0.285 m rear tyre
    }
}
