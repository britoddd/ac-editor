using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Formats;

namespace AcMod.Core.Tests.Formats;

public sealed class DataDiffTests
{
    private static AcdEntry File(string name, string text) => new(name, Encoding.UTF8.GetBytes(text));

    private static IReadOnlyList<FieldChange> Compare(AcdEntry before, AcdEntry after) => DataDiff.Compare([before], [after]);

    [Fact]
    public void Ini_changes_are_listed_per_key_in_file_order()
    {
        var before = File("engine.ini", "[ENGINE_DATA]\r\nLIMITER=6500 ; rev limiter\r\nMINIMUM=1000\r\n[TURBO_0]\r\nMAX_BOOST=2.0\r\n");
        var after = File("engine.ini", "[ENGINE_DATA]\r\nLIMITER=5500 ; rev limiter\r\n[TURBO_0]\r\nMAX_BOOST=2.0\r\nWASTEGATE=1.5\r\n");

        Assert.Equal(
            [
                new FieldChange("engine.ini", "[ENGINE_DATA] LIMITER", "6500", "5500"),
                new FieldChange("engine.ini", "[ENGINE_DATA] MINIMUM", "1000", null),
                new FieldChange("engine.ini", "[TURBO_0] WASTEGATE", null, "1.5"),
            ],
            Compare(before, after));
    }

    [Fact]
    public void Lut_changes_are_listed_per_row_key()
    {
        var before = File("power.lut", "0|100\r\n4000|200\r\n8000|150\r\n");
        var after = File("power.lut", "0|100\r\n4000|210\r\n9000|120\r\n");

        Assert.Equal(
            [
                new FieldChange("power.lut", "4000", "200", "210"),
                new FieldChange("power.lut", "8000", "150", null),
                new FieldChange("power.lut", "9000", null, "120"),
            ],
            Compare(before, after));
    }

    [Fact]
    public void Repeated_keys_compare_by_position()
    {
        var before = File("car.ini", "[INFO]\r\nTAG=a\r\nTAG=b\r\n");
        var after = File("car.ini", "[INFO]\r\nTAG=a\r\nTAG=c\r\n");

        Assert.Equal([new FieldChange("car.ini", "[INFO] TAG #2", "b", "c")], Compare(before, after));
    }

    [Theory]
    [InlineData("engine.ini", "[ENGINE_DATA]\r\nLIMITER=6500 ; rev limiter\r\n", "[ENGINE_DATA]\r\nLIMITER=6500 ; the rev limiter\r\n")] // comment only
    [InlineData("power.lut", "0|100\r\n", "0|100\n")] // line ending only
    [InlineData("readme.txt", "a", "b")] // not an .ini, .lut or .rto
    public void A_text_change_without_a_field_change_is_still_listed(string name, string before, string after)
    {
        Assert.Equal([new FieldChange(name, "(other text)", null, null)], Compare(File(name, before), File(name, after)));
    }

    [Fact]
    public void Added_and_removed_files_are_listed_and_unchanged_files_are_not()
    {
        var changes = DataDiff.Compare(
            [File("car.ini", "[INFO]\r\n"), File("old.lut", "0|1\r\n")],
            [File("CAR.INI", "[INFO]\r\n"), File("new.lut", "0|1\r\n")]); // names ignore case, like Windows

        Assert.Equal(
            [new FieldChange("new.lut", "(whole file)", null, "present"), new FieldChange("old.lut", "(whole file)", "present", null)],
            changes);
    }
}
