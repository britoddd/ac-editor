using AcMod.Core.Formats;

namespace AcMod.Core.Tests.Formats;

public class GuidsFileTests
{
    private static GuidsFile Load() => GuidsFile.Parse(File.ReadAllBytes(Fixtures.Synthetic("guids", "GUIDs.txt")));

    [Fact]
    public void Reads_every_guid_line_and_skips_blank_lines()
    {
        var guids = Load();

        Assert.Equal(8, guids.Entries.Count);
        Assert.Equal(["bank", "event", "event", "event", "event", "bus", "vca", "snapshot"], guids.Entries.Select(e => e.Kind));
        Assert.Equal("cars/old_car/engine_ext", guids.Entries[1].Path);
        Assert.Equal("00000000-0000-0000-0000-000000000001", guids.Entries[0].Guid);
    }

    [Fact]
    public void Changing_the_car_id_rewrites_only_that_cars_bank_and_event_lines()
    {
        var guids = Load();

        var renamed = guids.WithCarId("old_car", "new_car");

        Assert.Equal(
            ["new_car", "cars/new_car/engine_ext", "cars/new_car/horn", "cars/other_car/engine_ext",
             "cars/old_car_extra/engine_ext", "old_car", "master", "pause"],
            renamed.Entries.Select(e => e.Path));

        // Only the three lines for old_car changed, and their line breaks stayed as they were.
        var changed = renamed.Document.Lines.Where((line, i) => line != guids.Document.Lines[i]).ToList();
        Assert.Equal(3, changed.Count);
        Assert.Equal(guids.Document.Lines.Select(l => l.Ending), renamed.Document.Lines.Select(l => l.Ending));
    }

    [Theory]
    [InlineData("")]
    [InlineData("new/car")]
    [InlineData("new car")]
    [InlineData("new\ncar")]
    public void Rejects_invalid_new_ids(string newId)
    {
        Assert.Throws<ArgumentException>(() => Load().WithCarId("old_car", newId));
    }

    [Fact]
    public void Rejects_an_old_id_with_no_lines_instead_of_silently_doing_nothing()
    {
        Assert.Throws<ArgumentException>(() => Load().WithCarId("missing_car", "new_car"));
    }
}
