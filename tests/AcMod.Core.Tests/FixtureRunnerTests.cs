using AcMod.Core.Formats;
using AcMod.Core.Text;

namespace AcMod.Core.Tests;

/// <summary>
/// The M1 exit gate. For every fixture file (fixtures/synthetic, plus fixtures/local when present):
/// QS-1, an unchanged file is written back byte-for-byte; and QS-2, every possible single edit changes only that value.
/// </summary>
public class FixtureRunnerTests
{
    public static TheoryData<string> Files()
    {
        var roots = new[] { Fixtures.Synthetic(), Fixtures.LocalRoot }.Where(Directory.Exists);
        var data = new TheoryData<string>();
        foreach (var file in roots.SelectMany(r => Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories)).Where(f => FormatOf(f) != null))
        {
            data.Add(Path.GetRelativePath(Fixtures.RepoRoot, file).Replace('\\', '/'));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Fixture_round_trips_and_every_single_edit_changes_only_that_value(string relativePath)
    {
        var path = Path.Combine(Fixtures.RepoRoot, relativePath);
        var bytes = File.ReadAllBytes(path);

        switch (FormatOf(path))
        {
            case "ini": CheckIni(bytes); break;
            case "lut": CheckLut(bytes); break;
            case "json": CheckJson(bytes); break;
            case "guids": CheckGuids(bytes, CarIdOf(path)); break;
        }
    }

    // .rto files use the same "key|value" format as .lut files. All .json files are read as ui_car.json style JSON.
    private static string? FormatOf(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "guids.txt" => "guids",
        var name when name.EndsWith(".ini") => "ini",
        var name when name.EndsWith(".lut") || name.EndsWith(".rto") => "lut",
        var name when name.EndsWith(".json") => "json",
        _ => null,
    };

    private static void CheckIni(byte[] bytes)
    {
        var ini = IniFile.Parse(bytes);
        Assert.Equal(bytes, ini.ToBytes());

        foreach (var entry in ini.Entries)
        {
            var edited = IniFile.Parse(ini.WithValue(entry, "123").ToBytes());

            Assert.Equal(ini.Entries.Select(e => e == entry ? e with { Value = "123" } : e), edited.Entries);
            Assert.True(ChangedLines(ini.Document, edited.Document) <= 1);
        }
    }

    private static void CheckLut(byte[] bytes)
    {
        var lut = LutFile.Parse(bytes);
        Assert.Equal(bytes, lut.ToBytes());

        for (var i = 0; i < lut.Rows.Count; i++)
        {
            var newValue = lut.Rows[i].Value + 1;
            var edited = LutFile.Parse(lut.WithValue(i, newValue).ToBytes());

            Assert.Equal(lut.Rows.Select((r, j) => j == i ? r with { Value = newValue } : r), edited.Rows);
            Assert.Equal(1, ChangedLines(lut.Document, edited.Document));
        }
    }

    private static void CheckJson(byte[] bytes)
    {
        var json = UiCarJson.Parse(bytes);
        Assert.Equal(bytes, json.ToBytes());
        if (json.Error != null)
        {
            return; // invalid JSON is only round-tripped; edits are refused (tested in UiCarJsonTests)
        }

        foreach (var pointer in json.Pointers)
        {
            UiCarJson edited;
            if (json.GetString(pointer) != null)
            {
                edited = UiCarJson.Parse(json.WithString(pointer, "x").ToBytes());
            }
            else if (json.GetNumber(pointer) is { } number)
            {
                edited = UiCarJson.Parse(json.WithNumber(pointer, number + 1).ToBytes());
            }
            else
            {
                continue; // objects, arrays, true/false/null are not edited directly
            }

            Assert.Null(edited.Error);
            Assert.Equal(json.Pointers, edited.Pointers);
            foreach (var other in json.Pointers.Where(p => p != pointer))
            {
                Assert.Equal(json.GetString(other), edited.GetString(other));
                Assert.Equal(json.GetNumber(other), edited.GetNumber(other));
            }
        }
    }

    private static void CheckGuids(byte[] bytes, string carId)
    {
        var guids = GuidsFile.Parse(bytes);
        Assert.Equal(bytes, guids.ToBytes());

        var renamed = GuidsFile.Parse(guids.WithCarId(carId, carId + "_renamed").ToBytes());

        Assert.Equal(guids.Entries.Select(e => e.Guid), renamed.Entries.Select(e => e.Guid));
        Assert.Equal(guids.Entries.Select(e => e.Kind), renamed.Entries.Select(e => e.Kind));
        var changedPaths = guids.Entries.Zip(renamed.Entries).Count(pair => pair.First.Path != pair.Second.Path);
        Assert.Equal(changedPaths, ChangedLines(guids.Document, renamed.Document));
        Assert.True(changedPaths > 0);
    }

    // A car's sfx/GUIDs.txt belongs to the car folder above sfx. The synthetic one is written for "old_car".
    private static string CarIdOf(string path)
    {
        var sfx = Directory.GetParent(path)!;
        return sfx.Name.Equals("sfx", StringComparison.OrdinalIgnoreCase) ? sfx.Parent!.Name : "old_car";
    }

    private static int ChangedLines(TextDocument before, TextDocument after)
    {
        Assert.Equal(before.Lines.Count, after.Lines.Count);
        return before.Lines.Where((line, i) => line != after.Lines[i]).Count();
    }
}
