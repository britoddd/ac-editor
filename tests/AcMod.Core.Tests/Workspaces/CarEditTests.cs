using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Formats;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class CarEditTests : IDisposable
{
    private const string CarId = "acm_edit_test";
    private const string CarIni = "[INFO]\r\nSCREEN_NAME=original\r\n";
    private const string EngineIni = "[ENGINE_DATA]\r\nLIMITER=6500\r\n";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_caredit_").FullName;
    private readonly Workspace _workspace;

    public CarEditTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        Directory.CreateDirectory(Path.Combine(Car, "data"));
        File.WriteAllText(Path.Combine(Car, "data", "car.ini"), CarIni);
        File.WriteAllText(Path.Combine(Car, "data", "engine.ini"), EngineIni);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car => Path.Combine(_workspace.Root, "cars", CarId);

    private CarEdit Open()
    {
        var (edit, error) = _workspace.OpenCar(CarId);
        Assert.Null(error);
        return edit!;
    }

    /// <summary>Packs data\ into data.acd, and removes data\ unless <paramref name="keepFolder"/>.</summary>
    private void UseAcd(bool keepFolder = false)
    {
        File.WriteAllBytes(Path.Combine(Car, "data.acd"), CarData.PackFolder(Car).Bytes!);
        if (!keepFolder) Directory.Delete(Path.Combine(Car, "data"), recursive: true);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void A_data_folder_car_saves_only_the_changed_file_after_a_snapshot()
    {
        var edit = Open();
        edit.Set("engine.ini", Bytes("[ENGINE_DATA]\r\nLIMITER=5500\r\n"));

        Assert.Equal(["engine.ini"], edit.ChangedFiles);
        Assert.Equal([@"data\engine.ini"], edit.WriteTargets);
        var (snapshotId, error) = edit.Save();

        Assert.Null(error);
        Assert.Equal([snapshotId!], _workspace.Snapshots(CarId));
        Assert.Contains("LIMITER=5500", File.ReadAllText(Path.Combine(Car, "data", "engine.ini")));
        Assert.Equal(CarIni, File.ReadAllText(Path.Combine(Car, "data", "car.ini")));
        Assert.Empty(edit.ChangedFiles);
        Assert.Contains("LIMITER=5500", Text(edit.Current("engine.ini"))); // reloaded after the save
    }

    [Fact]
    public void A_data_acd_car_saves_a_repacked_data_acd()
    {
        UseAcd();
        var edit = Open();
        Assert.Equal(DataSource.Acd, edit.Data.Source);
        edit.Set("engine.ini", Bytes("[ENGINE_DATA]\r\nLIMITER=5500\r\n"));

        Assert.Equal(["data.acd"], edit.WriteTargets);
        Assert.Null(edit.Save().Error);

        var saved = CarData.Load(Car);
        Assert.Null(saved.Error);
        Assert.Equal("[ENGINE_DATA]\r\nLIMITER=5500\r\n", Text(saved.Files.Single(f => f.Name == "engine.ini").Data));
        Assert.Equal(CarIni, Text(saved.Files.Single(f => f.Name == "car.ini").Data));
        Assert.False(Directory.Exists(Path.Combine(Car, "data"))); // no data\ appears (V-12)
    }

    [Fact]
    public void With_both_present_edits_go_to_data_acd_which_AC_uses()
    {
        UseAcd(keepFolder: true);
        var edit = Open();
        Assert.True(edit.Data.FolderIgnored);
        edit.Set("engine.ini", Bytes("[ENGINE_DATA]\r\nLIMITER=5500\r\n"));

        Assert.Null(edit.Save().Error);

        Assert.Equal(EngineIni, File.ReadAllText(Path.Combine(Car, "data", "engine.ini"))); // data\ untouched
        Assert.Contains("LIMITER=5500", Text(CarData.Load(Car).Files.Single(f => f.Name == "engine.ini").Data));
    }

    [Fact]
    public void Changes_lists_the_unsaved_fields()
    {
        var edit = Open();
        edit.Set("engine.ini", Bytes("[ENGINE_DATA]\r\nLIMITER=5500\r\n"));

        Assert.Equal([new FieldChange("engine.ini", "[ENGINE_DATA] LIMITER", "6500", "5500")], edit.Changes);
    }

    [Fact]
    public void Setting_a_file_back_to_its_saved_bytes_clears_the_change()
    {
        var edit = Open();
        edit.Set("car.ini", Bytes("changed"));
        edit.Set("car.ini", Bytes(CarIni));

        Assert.Empty(edit.ChangedFiles);
        Assert.Equal((null, null), edit.Save()); // nothing to save, so no snapshot
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void Text_edits_keep_the_files_line_breaks()
    {
        File.WriteAllText(Path.Combine(Car, "data", "lf.ini"), "[A]\nX=1\n");
        var edit = Open();

        Assert.Null(edit.SetText("lf.ini", "[A]\r\nX=2\r\nY=3\r\n")); // what a Windows text box returns
        Assert.Null(edit.SetText("car.ini", "[INFO]\r\nSCREEN_NAME=edited\r\n"));

        Assert.Equal("[A]\nX=2\nY=3\n", Text(edit.Current("lf.ini")));
        Assert.Equal("[INFO]\r\nSCREEN_NAME=edited\r\n", Text(edit.Current("car.ini")));
        Assert.Equal("[A]\nX=2\nY=3\n", edit.Text("lf.ini"));
    }

    [Fact]
    public void Text_edits_keep_the_BOM_and_unchanged_text_is_no_change()
    {
        File.WriteAllBytes(Path.Combine(Car, "data", "bom.ini"), [0xEF, 0xBB, 0xBF, .. Bytes("[A]\r\nX=1\r\n")]);
        var edit = Open();

        Assert.Null(edit.SetText("bom.ini", edit.Text("bom.ini")));
        Assert.Empty(edit.ChangedFiles);

        Assert.Null(edit.SetText("bom.ini", "[A]\r\nX=2\r\n"));
        Assert.Equal([0xEF, 0xBB, 0xBF, .. Bytes("[A]\r\nX=2\r\n")], edit.Current("bom.ini"));
    }

    [Fact]
    public void A_character_the_files_encoding_cannot_hold_is_refused()
    {
        File.WriteAllBytes(Path.Combine(Car, "data", "latin1.ini"), [.. Bytes("[A]\r\nNAME=Caf"), 0xE9, .. Bytes("\r\n")]); // not UTF-8
        var edit = Open();

        Assert.Contains("U+00FF", edit.SetText("latin1.ini", "[A]\r\nNAME=日本\r\n"));
        Assert.Empty(edit.ChangedFiles);
        Assert.Null(edit.SetText("latin1.ini", "[A]\r\nNAME=Cafés\r\n")); // up to U+00FF is fine
    }

    [Fact]
    public void SetLutValue_changes_only_that_rows_number()
    {
        File.WriteAllText(Path.Combine(Car, "data", "power.lut"), "; rpm|torque\r\n0|100\r\n4000|200   ; peak\r\n");
        var edit = Open();

        edit.SetLutValue("power.lut", 1, 212.5);

        Assert.Equal("; rpm|torque\r\n0|100\r\n4000|212.5   ; peak\r\n", Text(edit.Current("power.lut")));
        Assert.Equal([new FieldChange("power.lut", "4000", "200", "212.5")], edit.Changes);
    }

    [Fact]
    public void Discard_drops_unsaved_changes()
    {
        var edit = Open();
        edit.Set("car.ini", Bytes("changed"));

        edit.Discard();

        Assert.Empty(edit.ChangedFiles);
        Assert.Equal(CarIni, Text(edit.Current("car.ini")));
    }

    [Fact]
    public void Unreadable_data_opens_read_only()
    {
        File.WriteAllText(Path.Combine(Car, "data.acd"), "not an acd file"); // AC would use this and ignore data\ (V-12, V-13)
        var edit = Open();

        Assert.True(edit.ReadOnly);
        Assert.Throws<InvalidOperationException>(() => edit.Set("car.ini", Bytes("x")));
        Assert.Contains("read-only", edit.Save().Error);
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void Only_existing_data_files_can_be_changed()
    {
        Assert.Throws<ArgumentException>(() => Open().Set("new.ini", Bytes("x")));
    }

    [Theory]
    [InlineData("acm_missing")]
    [InlineData("..")]
    [InlineData("")]
    public void OpenCar_refuses_a_missing_or_bad_car_ID(string carId)
    {
        var (edit, error) = _workspace.OpenCar(carId);

        Assert.Null(edit);
        Assert.NotNull(error);
    }
}
