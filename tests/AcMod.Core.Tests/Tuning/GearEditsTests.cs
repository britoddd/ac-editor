using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Text;
using AcMod.Core.Tuning;

namespace AcMod.Core.Tests.Tuning;

public sealed class GearEditsTests
{
    private const string Drivetrain =
        "[TRACTION]\r\nTYPE=RWD\r\n\r\n[GEARS]\r\nCOUNT=2\t\t; forward gears number\r\nGEAR_R=-3.8\r\nGEAR_1=2.36\r\nGEAR_2=1.73\r\n\r\nFINAL=3.10\r\n";

    private const string Setup =
        "[GEARS]\r\nUSE_GEARSET=0\r\n\r\n;GEARS\r\n[GEAR_1]\r\nRATIOS=ratios.rto\r\nNAME=First Gear\r\n\r\n[GEAR_2]\r\nRATIOS=ratios.rto\r\nNAME=Second Gear\r\nHELP=HELP_REAR_GEAR\r\n\r\n;DIFF\r\n[DIFF_POWER]\r\nMIN=10\r\n";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(byte[]? bytes) => Encoding.UTF8.GetString(bytes!);

    [Fact]
    public void Inserted_lines_copy_the_line_break_above_and_appending_keeps_a_missing_final_break_missing()
    {
        var lf = TextDocument.Parse(Bytes("a\nb\n"));
        var noFinalBreak = TextDocument.Parse(Bytes("a\r\nb"));

        Assert.Equal("a\nx\nb\n", lf.WithLinesInserted(1, ["x"]).Text);
        Assert.Equal("a\r\nb\r\nx", noFinalBreak.WithLinesInserted(2, ["x"]).Text);
        Assert.Equal("a\r\n", noFinalBreak.WithLinesRemoved(1, 1).Text);
    }

    [Fact]
    public void Adding_a_gear_adds_one_line_after_the_last_gear_and_raises_COUNT()
    {
        var (bytes, gear, error) = GearEdits.AddGear(Bytes(Drivetrain), 1.52);

        Assert.Null(error);
        Assert.Equal(3, gear);
        Assert.Equal(Drivetrain.Replace("COUNT=2", "COUNT=3").Replace("GEAR_2=1.73\r\n", "GEAR_2=1.73\r\nGEAR_3=1.52\r\n"), Text(bytes));
    }

    [Fact]
    public void Removing_the_last_gear_undoes_adding_it()
    {
        var added = GearEdits.AddGear(Bytes(Drivetrain), 1.52).Bytes!;

        Assert.Equal(Drivetrain, Text(GearEdits.RemoveLastGear(added).Bytes));
        Assert.Contains("at least one gear", GearEdits.RemoveLastGear(Bytes("[GEARS]\r\nCOUNT=1\r\nGEAR_1=3\r\n")).Error);
    }

    [Theory]
    [InlineData("[GEARS]\r\nGEAR_1=2\r\n", "COUNT")]
    [InlineData("[GEARS]\r\nCOUNT=3\r\nGEAR_1=2\r\n", "no GEAR_3")]
    public void A_drivetrain_without_usable_gears_is_refused(string drivetrain, string message)
    {
        Assert.Contains(message, GearEdits.AddGear(Bytes(drivetrain), 1.5).Error);
        Assert.Contains("above 0", GearEdits.AddGear(Bytes(Drivetrain), 0).Error);
    }

    [Fact]
    public void A_new_gear_gets_a_setup_section_copied_from_the_previous_gear()
    {
        var setup = Text(GearEdits.AddSetupGear(Bytes(Setup), 3));

        Assert.Equal(
            Setup.Replace("HELP=HELP_REAR_GEAR\r\n", "HELP=HELP_REAR_GEAR\r\n\r\n[GEAR_3]\r\nRATIOS=ratios.rto\r\nNAME=Gear 3\r\nHELP=HELP_REAR_GEAR\r\n"),
            setup);
        Assert.Equal(Setup, Text(GearEdits.RemoveSetupGear(Bytes(setup), 3)));
        Assert.Null(GearEdits.AddSetupGear(Bytes(Setup), 2)); // already there
        Assert.Null(GearEdits.AddSetupGear(Bytes(Setup), 5)); // no [GEAR_4] to copy
    }

    [Fact]
    public void A_power_curve_row_goes_after_the_last_row_and_must_be_higher()
    {
        const string curve = "; rpm|torque\r\n0|100\r\n8000|150\r\n; end\r\n";

        Assert.Equal("; rpm|torque\r\n0|100\r\n8000|150\r\n9000|140.5\r\n; end\r\n", Text(GearEdits.AddCurveRow(Bytes(curve), 9000, 140.5).Bytes));
        Assert.Contains("above the highest one, 8000", GearEdits.AddCurveRow(Bytes(curve), 8000, 140).Error);
        Assert.Contains("at least two rows", GearEdits.RemoveLastCurveRow(Bytes(curve)).Error);
        Assert.Equal(curve, Text(GearEdits.RemoveLastCurveRow(GearEdits.AddCurveRow(Bytes(curve), 9000, 140.5).Bytes!).Bytes));
    }

    [Fact]
    public void Adding_then_removing_a_gear_leaves_the_tutorial_cars_files_byte_identical()
    {
        var car = Path.Combine(Fixtures.LocalRoot, "acm_tutorial_basic");
        if (!Directory.Exists(car)) return; // gitignored real car, missing on a fresh clone

        var files = CarData.Load(car).Files;
        var drivetrain = files.Single(f => f.Name == "drivetrain.ini").Data;
        var setup = files.Single(f => f.Name == "setup.ini").Data;

        var (added, gear, _) = GearEdits.AddGear(drivetrain, 0.78);
        var setupAdded = GearEdits.AddSetupGear(setup, gear);

        Assert.Equal(7, gear);
        Assert.Contains(2.36, GearEdits.RatioOptions(files, "GEAR_1")!); // GEAR_1=2.36 is the ratios.rto option 14//33
        Assert.Equal([3.10, 4.10, 5.10], GearEdits.RatioOptions(files, "FINAL")!); // [FINAL_GEAR_RATIO] RATIOS=final.rto
        Assert.Null(GearEdits.RatioOptions(files, "GEAR_9"));
        Assert.NotNull(setupAdded);
        Assert.Contains("GEAR_7=0.78", Text(added));
        Assert.Equal(drivetrain, GearEdits.RemoveLastGear(added!).Bytes);
        Assert.Equal(setup, GearEdits.RemoveSetupGear(setupAdded!, 7));
    }
}
