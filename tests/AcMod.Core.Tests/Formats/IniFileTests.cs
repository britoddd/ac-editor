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

    // QS-1: an unchanged file is written back byte-for-byte, including real cars in fixtures/local.
    [Fact]
    public void Every_fixture_ini_round_trips_byte_for_byte()
    {
        var roots = new[] { Fixtures.Synthetic(), Fixtures.LocalRoot }.Where(Directory.Exists);
        var files = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.ini", SearchOption.AllDirectories))
            .ToList();

        var failures = files.Where(f =>
        {
            var bytes = File.ReadAllBytes(f);
            return !bytes.AsSpan().SequenceEqual(IniFile.Parse(bytes).ToBytes());
        }).ToList();

        Assert.NotEmpty(files);
        Assert.Empty(failures);
    }
}
