using System.Text.Json;
using AcMod.Core.Workspaces;
using AcMod.Mcp;

namespace AcMod.Core.Tests.Mcp;

/// <summary>The MCP read tools (CC-3) on a temporary workspace, and CC-9: unreadable data returns only its status.</summary>
[Collection("CarTools.Open")] // every class that swaps the static CarTools.Open runs in this collection, so never in parallel
public sealed class CarToolsTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_mcp_").FullName;
    private readonly Workspace _workspace;

    public CarToolsTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        Validation.ValidatorTests.MakeGoodCar(Path.Combine(_workspace.Root, "cars"), "acm_good");
        CarTools.Open = () => (_workspace, null);
    }

    public void Dispose()
    {
        CarTools.Open = () => (null, "test finished");
        Directory.Delete(_temp, recursive: true);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Read_car_lists_the_data_files_and_returns_one_file_s_text()
    {
        var list = Parse(CarTools.ReadCar("acm_good"));
        Assert.Equal("Folder", list.GetProperty("source").GetString());
        Assert.Contains(list.GetProperty("files").EnumerateArray(), f => f.GetProperty("Name").GetString() == "engine.ini");

        var file = Parse(CarTools.ReadCar("acm_good", "LODS.INI")); // the name is matched ignoring case
        Assert.Equal("lods.ini", file.GetProperty("file").GetString());
        Assert.Contains("FILE=Car_Main.kn5", file.GetProperty("text").GetString());
    }

    [Fact]
    public void A_car_with_unreadable_data_returns_only_its_status()
    {
        var car = Path.Combine(_workspace.Root, "cars", "acm_good");
        File.WriteAllText(Path.Combine(car, "data.acd"), "not an acd file");
        Directory.Delete(Path.Combine(car, "data"), recursive: true);

        foreach (var json in new[] { CarTools.ReadCar("acm_good"), CarTools.ReadCar("acm_good", "engine.ini"), CarTools.Gears("acm_good") })
        {
            var result = Parse(json);
            Assert.Equal("unreadable", result.GetProperty("status").GetString());
            Assert.False(result.TryGetProperty("text", out _));
            Assert.False(result.TryGetProperty("files", out _));
        }
    }

    [Fact]
    public void Compare_snapshots_lists_the_fields_changed_since_a_snapshot()
    {
        var (snapshotId, error) = _workspace.WriteCarFiles("acm_good",
            new Dictionary<string, byte[]> { [@"data\car.ini"] = "[HEADER]\r\nVERSION=2\r\n"u8.ToArray() }); // the good car has VERSION=1
        Assert.Null(error);

        var change = Assert.Single(Parse(CarTools.CompareSnapshots("acm_good", snapshotId!)).EnumerateArray());
        Assert.Equal(("car.ini", "1", "2"), (change.GetProperty("File").GetString(), change.GetProperty("Before").GetString(), change.GetProperty("After").GetString()));
    }

    [Fact]
    public void An_unknown_car_or_snapshot_is_an_error_not_an_exception()
    {
        Assert.Contains("no car", Parse(CarTools.ReadCar("acm_missing")).GetProperty("error").GetString());
        Assert.Contains("no snapshot", Parse(CarTools.CompareSnapshots("acm_good", "20990101-000000-000")).GetProperty("error").GetString());
    }
}
