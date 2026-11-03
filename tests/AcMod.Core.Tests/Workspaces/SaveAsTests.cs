using AcMod.Core.Acd;
using AcMod.Core.Formats;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class SaveAsTests : IDisposable
{
    private const string OldId = "acm_old";
    private const string NewId = "acm_new";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_saveas_").FullName;
    private readonly Workspace _workspace;

    public SaveAsTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        Put(OldId, @"data\car.ini", "[INFO]\r\nSCREEN_NAME=Old\r\n");
        Put(OldId, @"data\lods.ini", "[LOD_0]\r\nFILE=acm_old.kn5\r\n"); // names a model, which keeps its name
        Put(OldId, "acm_old.kn5", "model");
        Put(OldId, @"sfx\ACM_OLD.bank", "bank"); // the case differs, as Windows allows
        Put(OldId, @"sfx\GUIDs.txt",
            "{00000000-0000-0000-0000-000000000000} bank:/acm_old\r\n{00000000-0000-0000-0000-000000000001} event:/cars/acm_old/engine_ext\r\n" +
            "{00000000-0000-0000-0000-000000000002} bank:/common\r\n");
        Put(OldId, @"ui\ui_car.json", "{\"name\": \"Old car\", \"brand\": \"ACM\"}");
        Put(OldId, @"skins\red\cm_skin_for.json", "{\"id\": \"acm_old\"}"); // a mention the app doesn't rewrite
        Put(OldId, Workspace.InstallMarker, "{}");
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car(string id) => Path.Combine(_workspace.Root, "cars", id);

    private void Put(string id, string path, string text)
    {
        var full = Path.Combine(Car(id), path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private string Read(string id, string path) => File.ReadAllText(Path.Combine(Car(id), path));

    [Fact]
    public void The_copy_gets_the_new_ID_in_its_folder_bank_GUIDs_and_name()
    {
        var (result, error) = _workspace.SaveAsNewCar(OldId, NewId, "New car");

        Assert.Null(error);
        Assert.True(File.Exists(Path.Combine(Car(NewId), "sfx", "acm_new.bank")));
        var guids = GuidsFile.Parse(File.ReadAllBytes(Path.Combine(Car(NewId), "sfx", "GUIDs.txt"))).Entries.Select(e => $"{e.Kind}:{e.Path}");
        Assert.Equal(["bank:acm_new", "event:cars/acm_new/engine_ext", "bank:common"], guids); // other banks stay
        Assert.Equal("New car", UiCarJson.Parse(File.ReadAllBytes(Path.Combine(Car(NewId), "ui", "ui_car.json"))).GetString("/name"));
        Assert.Equal("[LOD_0]\r\nFILE=acm_old.kn5\r\n", Read(NewId, @"data\lods.ini")); // models keep their names
        Assert.True(File.Exists(Path.Combine(Car(NewId), "acm_old.kn5")));
        Assert.False(File.Exists(Path.Combine(Car(NewId), Workspace.InstallMarker)));
        Assert.Equal([@"skins\red\cm_skin_for.json"], result!.MentionsOfOldId);
        Assert.Empty(result.Notes);
        Assert.False(Directory.Exists(Path.Combine(_workspace.Root, "import.tmp")));
    }

    [Fact]
    public void The_original_is_untouched()
    {
        _workspace.SaveAsNewCar(OldId, NewId, "New car");

        Assert.True(File.Exists(Path.Combine(Car(OldId), "sfx", "ACM_OLD.bank")));
        Assert.Contains("bank:/acm_old", Read(OldId, @"sfx\GUIDs.txt"));
        Assert.Contains("Old car", Read(OldId, @"ui\ui_car.json"));
    }

    [Fact]
    public void A_data_acd_is_repacked_with_the_new_IDs_key()
    {
        File.WriteAllBytes(Path.Combine(Car(OldId), "data.acd"), CarData.PackFolder(Car(OldId)).Bytes!);
        Directory.Delete(Path.Combine(Car(OldId), "data"), recursive: true);

        Assert.Null(_workspace.SaveAsNewCar(OldId, NewId, null).Error);

        var copy = CarData.Load(Car(NewId)); // the key comes from the folder name, acm_new
        Assert.Null(copy.Error);
        Assert.Equal(CarData.Load(Car(OldId)).Files.Select(f => f.Name), copy.Files.Select(f => f.Name));
        Assert.NotNull(CarData.Load(Car(NewId), OldId).Error); // the old key no longer reads it
    }

    [Theory]
    [InlineData("acm_New", "lowercase")]
    [InlineData("acm new", "lowercase")]
    [InlineData(OldId, "already in the workspace")]
    [InlineData("..", "")]
    public void A_bad_or_taken_new_ID_is_refused(string newId, string message)
    {
        var (result, error) = _workspace.SaveAsNewCar(OldId, newId, null);

        Assert.Null(result);
        Assert.Contains(message, error);
        Assert.Single(_workspace.Cars());
    }

    [Fact]
    public void Unreadable_data_and_a_model_that_may_be_encrypted_are_refused()
    {
        File.WriteAllText(Path.Combine(Car(OldId), "data.acd"), "not an acd file");
        Assert.Contains("can't be read", _workspace.SaveAsNewCar(OldId, NewId, null).Error);

        File.Delete(Path.Combine(Car(OldId), "data.acd"));
        Put(OldId, "acm_old_encrypted.kn5", "x");
        Assert.Contains("may be encrypted", _workspace.SaveAsNewCar(OldId, NewId, null).Error);
        Assert.Single(_workspace.Cars());
    }

    [Fact]
    public void The_tutorial_car_saved_as_a_new_ID_still_validates_cleanly()
    {
        var fixture = Path.Combine(Fixtures.LocalRoot, "acm_tutorial_basic");
        if (!Directory.Exists(fixture)) return; // gitignored real car, missing on a fresh clone
        Assert.Null(_workspace.ImportCar(fixture).Error);

        var (result, error) = _workspace.SaveAsNewCar("acm_tutorial_basic", "acm_tutorial_v2", "ACM Tutorial v2");

        Assert.Null(error);
        var findings = _workspace.Validate("acm_tutorial_v2").Findings!;
        Assert.DoesNotContain(findings, f => f.Severity != AcMod.Core.Validation.Severity.Info); // only the dev-file infos, as before
        Assert.Empty(result!.Notes);
    }

    [Fact]
    public void Missing_pieces_are_reported_as_notes()
    {
        File.Delete(Path.Combine(Car(OldId), "sfx", "ACM_OLD.bank"));
        Put(OldId, @"ui\ui_car.json", "{\"brand\": \"ACM\"}");

        var (result, _) = _workspace.SaveAsNewCar(OldId, NewId, "New car");

        Assert.Equal(2, result!.Notes.Count);
        Assert.Contains(result.Notes, n => n.Contains("no sfx\\acm_old.bank"));
        Assert.Contains(result.Notes, n => n.Contains("display name wasn't set"));
    }
}
