using System.Text.Json;
using AcMod.Core.Acd;
using AcMod.Core.Workspaces;
using AcMod.Mcp;

namespace AcMod.Core.Tests.Mcp;

/// <summary>preview_change and apply_change (CC-4, CC-5) on a temporary workspace.</summary>
[Collection("CarTools.Open")]
public sealed class ChangeToolsTests : IDisposable
{
    private const string CarId = "acm_good";
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_mcp_change_").FullName;
    private readonly Workspace _workspace;

    public ChangeToolsTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        Validation.ValidatorTests.MakeGoodCar(Path.Combine(_workspace.Root, "cars"), CarId);
        Put(@"data\engine.ini", "[HEADER]\r\nPOWER_CURVE=POWER.lut\r\n[ENGINE_DATA]\r\nLIMITER=6500\t; rpm\r\n");
        CarTools.Open = () => (_workspace, null);
    }

    public void Dispose()
    {
        CarTools.Open = () => (null, "test finished");
        Directory.Delete(_temp, recursive: true);
    }

    private string Car => Path.Combine(_workspace.Root, "cars", CarId);

    private void Put(string path, string text) => File.WriteAllText(Path.Combine(Car, path), text);

    private string Read(string path) => File.ReadAllText(Path.Combine(Car, path));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly DataEdit Limiter = new("engine.ini", "7000", Section: "ENGINE_DATA", Key: "LIMITER");

    [Fact]
    public void Preview_shows_the_change_and_writes_nothing()
    {
        var result = Parse(CarTools.PreviewChange(CarId, [Limiter, new("POWER.LUT", "210", Row: 1)]));

        var changes = result.GetProperty("changes").EnumerateArray().Select(c => $"{c.GetProperty("File").GetString()} {c.GetProperty("Before").GetString()}->{c.GetProperty("After").GetString()}");
        Assert.Contains("engine.ini 6500->7000", changes);
        Assert.Contains("power.lut 200->210", changes);
        Assert.Contains("6500", Read(@"data\engine.ini"));
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void Apply_takes_a_snapshot_and_changes_only_that_value()
    {
        var result = Parse(CarTools.ApplyChange(CarId, [Limiter]));

        var snapshotId = result.GetProperty("snapshotId").GetString()!;
        Assert.Equal([snapshotId], _workspace.Snapshots(CarId));
        Assert.Equal("[HEADER]\r\nPOWER_CURVE=POWER.lut\r\n[ENGINE_DATA]\r\nLIMITER=7000\t; rpm\r\n", Read(@"data\engine.ini")); // comment and tab kept (QS-2)
        Assert.Equal(0, result.GetProperty("errorsNow").GetInt32());

        Assert.Null(_workspace.RestoreSnapshot(CarId, snapshotId).Error); // the change can be undone
        Assert.Contains("LIMITER=6500", Read(@"data\engine.ini"));
    }

    [Fact]
    public void Apply_repacks_data_acd_when_the_car_uses_it()
    {
        File.WriteAllBytes(Path.Combine(Car, "data.acd"), CarData.PackFolder(Car).Bytes!);
        Directory.Delete(Path.Combine(Car, "data"), recursive: true); // a plain temp test folder, so no junctions

        Assert.True(Parse(CarTools.ApplyChange(CarId, [Limiter])).TryGetProperty("snapshotId", out _));

        var engine = CarData.Load(Car).Files.Single(f => f.Name == "engine.ini");
        Assert.Contains("LIMITER=7000", System.Text.Encoding.UTF8.GetString(engine.Data));
    }

    [Theory]
    [InlineData("engine.ini", "ENGINE_DATA", "NO_SUCH_KEY", null, "1", "has no [ENGINE_DATA] NO_SUCH_KEY")]
    [InlineData("missing.ini", "X", "Y", null, "1", "has no data file missing.ini")]
    [InlineData("engine.ini", "ENGINE_DATA", "LIMITER", null, "7000; oops", "LIMITER")]
    [InlineData("power.lut", null, null, 5, "1", "has rows 0 to 1, not 5")]
    [InlineData("power.lut", null, null, 0, "fast", "isn't a number")]
    public void A_bad_edit_is_refused_and_nothing_is_written(string file, string? section, string? key, int? row, string value, string message)
    {
        var result = Parse(CarTools.ApplyChange(CarId, [Limiter, new(file, value, section, key, row)])); // the good first edit isn't saved either

        Assert.Contains(message, result.GetProperty("error").GetString());
        Assert.Contains("LIMITER=6500", Read(@"data\engine.ini"));
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void A_key_that_appears_twice_is_refused_because_which_one_AC_uses_is_unverified()
    {
        Put(@"data\engine.ini", "[ENGINE_DATA]\r\nLIMITER=6500\r\nLIMITER=6600\r\n");

        Assert.Contains("2 times", Parse(CarTools.PreviewChange(CarId, [Limiter])).GetProperty("error").GetString());
    }

    [Fact]
    public void A_car_that_may_be_encrypted_or_has_unreadable_data_is_not_changed()
    {
        Put("car_main_encrypted.kn5", "kn5");
        Put(@"data\lods.ini", "[LOD_0]\r\nFILE=car_main_encrypted.kn5\r\n");
        Assert.Contains("V-14", Parse(CarTools.ApplyChange(CarId, [Limiter])).GetProperty("error").GetString());

        File.WriteAllText(Path.Combine(Car, "data.acd"), "not an acd file");
        Assert.Equal("unreadable", Parse(CarTools.ApplyChange(CarId, [Limiter])).GetProperty("status").GetString());
        Assert.Empty(_workspace.Snapshots(CarId));
    }
}
