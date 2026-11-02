using AcMod.Core.Formats;

namespace AcMod.Core.Tests.Formats;

public class IniFileTests
{
    private static IniFile Load(string name) => IniFile.Parse(File.ReadAllBytes(Fixtures.Synthetic("ini", name)));

    private static string[] Describe(IniFile ini) => ini.Entries.Select(e => $"{e.Section}/{e.Key}={e.Value}").ToArray();

    [Fact]
    public void Reads_sections_and_values_without_comments()
    {
        var ini = Load("kunos_style.ini");

        Assert.Equal(["HEADER", "WING_0"], ini.Sections.Select(s => s.Name));
        Assert.Equal(["HEADER/VERSION=1", "WING_0/NAME=BODY", "WING_0/LUT_GH_CL=", "WING_0/CHORD=1.25"], Describe(ini));
    }

    [Fact]
    public void Treats_double_slash_as_a_comment_start_even_with_a_semicolon_inside()
    {
        var ini = Load("slashes.ini");

        Assert.Equal(["ITEM_0/POSITION=-0.012, -0.01, -0.001", "ITEM_0/YAW_CL_GAIN=-0.10"], Describe(ini));
    }

    [Fact]
    public void Keeps_duplicate_sections_and_keys_in_file_order()
    {
        var ini = Load("duplicates.ini");

        Assert.Equal(["DATA", "DATA"], ini.Sections.Select(s => s.Name));
        Assert.Equal(["DATA/X=1", "DATA/X=2", "DATA/X=3"], Describe(ini));
    }

    [Fact]
    public void Ignores_lines_that_are_neither_sections_nor_entries()
    {
        var ini = Load("junk_and_globals.ini");

        // Prose, a lone "3", "=5" and "[unclosed" are not entries. They stay in the file (see the round-trip test).
        Assert.Equal(["INFO", "_EXTENSION"], ini.Sections.Select(s => s.Name));
        Assert.Equal(
            ["/TOP=before any section", "INFO/Plane.003=0", "INFO/Plate_A:Plate_LODA=0", "_EXTENSION/AFTER=spaced value"],
            Describe(ini));
    }

    // QS-2: editing one value changes only that value's characters.
    [Theory]
    [InlineData("kunos_style.ini", "WING_0", "NAME", "WING", "NAME=WING\t\t\t\t; name of the wing")]
    [InlineData("kunos_style.ini", "WING_0", "CHORD", "1.5", "CHORD=1.5")]
    [InlineData("kunos_style.ini", "WING_0", "LUT_GH_CL", "h.lut", "LUT_GH_CL=h.lut\t\t\t\t; empty value with a comment")]
    [InlineData("slashes.ini", "ITEM_0", "YAW_CL_GAIN", "-0.2", "YAW_CL_GAIN=-0.2   //// -0.10")]
    [InlineData("junk_and_globals.ini", "_EXTENSION", "AFTER", "new", "AFTER = new ; note")]
    public void Editing_a_value_changes_only_that_value(string file, string section, string key, string value, string expectedLine)
    {
        var ini = Load(file);
        var entry = ini.Find(section, key).Single();

        var edited = ini.WithValue(entry, value);

        Assert.Equal(expectedLine, edited.Document.Lines[entry.LineIndex].Content);
        Assert.Equal(value, edited.Find(section, key).Single().Value);
        Assert.Single(edited.Document.Lines.Where((line, i) => line != ini.Document.Lines[i]));
    }

    [Fact]
    public void Edits_only_the_chosen_duplicate()
    {
        var ini = Load("duplicates.ini");
        var second = ini.Find("DATA", "X")[1];

        var edited = ini.WithValue(second, "20");

        Assert.Equal(["DATA/X=1", "DATA/X=20", "DATA/X=3"], Describe(edited));
    }

    // QS-11: Indonesian Windows uses ',' as the decimal mark. AC files must still get '.'.
    [Fact]
    public void Writes_numbers_with_invariant_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("id-ID");
        try
        {
            var ini = Load("kunos_style.ini");

            var edited = ini.WithValue(ini.Find("WING_0", "CHORD").Single(), 2.5);

            Assert.Equal("2.5", edited.Find("WING_0", "CHORD").Single().Value);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("1\r\nX=2")]
    [InlineData("1 ; sneaky comment")]
    [InlineData("1 // sneaky comment")]
    [InlineData(" 1")]
    public void Rejects_values_that_would_read_back_differently(string value)
    {
        var ini = Load("kunos_style.ini");

        Assert.Throws<ArgumentException>(() => ini.WithValue(ini.Find("WING_0", "CHORD").Single(), value));
    }

    [Fact]
    public void Rejects_an_entry_from_another_file_and_non_finite_numbers()
    {
        var ini = Load("kunos_style.ini");
        var foreign = Load("duplicates.ini").Entries[0];

        Assert.Throws<ArgumentException>(() => ini.WithValue(foreign, "1"));
        Assert.Throws<ArgumentException>(() => ini.WithValue(ini.Entries[0], double.NaN));
    }

}
