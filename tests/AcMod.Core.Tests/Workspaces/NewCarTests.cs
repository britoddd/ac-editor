using AcMod.Core.Formats;
using AcMod.Core.Learning;
using AcMod.Core.Tests.Validation;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class NewCarTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_newcar_").FullName;
    private readonly Workspace _workspace;

    public NewCarTests() => _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car(string id) => Path.Combine(_workspace.Root, "cars", id);

    private Dictionary<string, ChecklistStatus> Checklist(string id) =>
        FirstCarChecklist.For(_workspace.Validate(id).Findings!, _workspace.ChecklistTicks(id), _workspace.CopiedFrom(id)).ToDictionary(s => s.Item.Id);

    [Fact]
    public void An_empty_car_has_only_the_folders_and_its_name_so_the_checklist_starts_at_To_do()
    {
        Assert.Null(_workspace.NewEmptyCar("acm_second", "My \"second\" car"));

        Assert.Equal(["data", "sfx", "skins", "ui"], Directory.GetDirectories(Car("acm_second")).Select(Path.GetFileName).Order());
        Assert.Equal("My \"second\" car", UiCarJson.Parse(File.ReadAllBytes(Path.Combine(Car("acm_second"), "ui", "ui_car.json"))).GetString("/name"));
        var checklist = Checklist("acm_second");
        Assert.All(["kseditor", "collider", "data", "sound"], id => Assert.Equal("To do", checklist[id].State));
        Assert.False(Directory.Exists(Path.Combine(_workspace.Root, "import.tmp")));
    }

    [Theory]
    [InlineData("acm_Second", "lowercase")]
    [InlineData("acm_taken", "already in the workspace")]
    public void A_bad_or_taken_ID_is_refused(string id, string message)
    {
        Directory.CreateDirectory(Car("acm_taken"));

        Assert.Contains(message, _workspace.NewEmptyCar(id, "x"));
    }

    [Fact]
    public void A_copied_car_says_its_model_and_collider_are_copied_until_ticked_as_your_own()
    {
        ValidatorTests.MakeGoodCar(Path.Combine(_workspace.Root, "cars"), "acm_good");
        Assert.Null(_workspace.SaveAsNewCar("acm_good", "acm_copy", null).Error);

        Assert.Equal("acm_good", _workspace.CopiedFrom("acm_copy"));
        Assert.StartsWith("Copied from acm_good", Checklist("acm_copy")["kseditor"].State);
        Assert.Equal("own:collider", Checklist("acm_copy")["collider"].TickId);
        Assert.Equal("Done (checked)", Checklist("acm_copy")["data"].State); // copied data is meant to be reused

        Assert.Null(_workspace.SetChecklistTick("acm_copy", "own:kseditor", true));
        Assert.Equal("Done (your own)", Checklist("acm_copy")["kseditor"].State);
        Assert.Null(_workspace.CopiedFrom("acm_good"));
    }

    [Fact]
    public void Data_is_copied_into_the_car_after_a_snapshot()
    {
        ValidatorTests.MakeGoodCar(Path.Combine(_workspace.Root, "cars"), "acm_good");
        _workspace.NewEmptyCar("acm_second", "x");

        var (copied, error) = _workspace.CopyDataFrom("acm_second", Car("acm_good"));

        Assert.Null(error);
        Assert.Equal(13, copied);
        Assert.Equal(File.ReadAllText(Path.Combine(Car("acm_good"), "data", "engine.ini")), File.ReadAllText(Path.Combine(Car("acm_second"), "data", "engine.ini")));
        Assert.Single(_workspace.Snapshots("acm_second"));
        Assert.Equal("Done (checked)", Checklist("acm_second")["data"].State);
    }

    [Fact]
    public void Sound_is_copied_with_the_bank_and_GUIDs_moved_to_the_new_ID()
    {
        ValidatorTests.MakeGoodCar(Path.Combine(_workspace.Root, "cars"), "acm_good");
        _workspace.NewEmptyCar("acm_second", "x");

        Assert.Null(_workspace.CopySoundFrom("acm_second", Car("acm_good")));

        Assert.True(File.Exists(Path.Combine(Car("acm_second"), "sfx", "acm_second.bank")));
        Assert.Equal(["bank:acm_second", "event:cars/acm_second/engine_ext"],
            GuidsFile.Parse(File.ReadAllBytes(Path.Combine(Car("acm_second"), "sfx", "GUIDs.txt"))).Entries.Select(e => $"{e.Kind}:{e.Path}"));
        Assert.Equal("Done (checked)", Checklist("acm_second")["sound"].State);
        Assert.Contains("already has", _workspace.CopySoundFrom("acm_second", Car("acm_good"))); // a bank isn't overwritten
    }

    [Fact]
    public void A_Kunos_style_car_lends_its_lines_from_ACs_shared_GUIDs()
    {
        var ac = Path.Combine(_temp, "ac");
        Directory.CreateDirectory(Path.Combine(ac, "content", "sfx"));
        File.WriteAllText(Path.Combine(ac, "content", "sfx", "GUIDs.txt"),
            "{00000000-0000-0000-0000-000000000000} bank:/ks_kunos\r\n{00000000-0000-0000-0000-000000000001} event:/cars/ks_kunos/engine_ext\r\n" +
            "{00000000-0000-0000-0000-000000000002} bank:/someone_else\r\n");
        var workspace = Workspace.Open(Path.Combine(_temp, "ws2"), ac).Workspace!;
        var kunos = Directory.CreateDirectory(Path.Combine(_temp, "ks_kunos", "sfx")).FullName;
        File.WriteAllText(Path.Combine(kunos, "ks_kunos.bank"), "bank"); // a Kunos car has no GUIDs.txt of its own
        workspace.NewEmptyCar("acm_second", "x");

        Assert.Null(workspace.CopySoundFrom("acm_second", Path.Combine(_temp, "ks_kunos")));

        Assert.Equal(["bank:acm_second", "event:cars/acm_second/engine_ext"],
            GuidsFile.Parse(File.ReadAllBytes(Path.Combine(workspace.Root, "cars", "acm_second", "sfx", "GUIDs.txt"))).Entries.Select(e => $"{e.Kind}:{e.Path}"));
    }

    [Fact]
    public void Ticks_saved_in_the_first_checklist_format_are_still_read()
    {
        File.WriteAllText(Path.Combine(_workspace.Root, "checklists.json"), "{ \"acm_a\": [\"dummies\"] }");

        Assert.Equal(["dummies"], _workspace.ChecklistTicks("acm_a"));
        Assert.Null(_workspace.SetChecklistTick("acm_a", "fbx", true));
        Assert.Equal(["dummies", "fbx"], _workspace.ChecklistTicks("acm_a").Order());
    }
}
