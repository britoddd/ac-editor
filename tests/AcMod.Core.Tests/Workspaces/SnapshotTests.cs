using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class SnapshotTests : IDisposable
{
    private const string CarId = "acm_snapshot_test";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_snapshot_").FullName;
    private readonly Workspace _workspace;
    private readonly string _car;

    public SnapshotTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        _car = Path.Combine(_workspace.Root, "cars", CarId);
        Put(@"data\car.ini", "[INFO]\r\nSCREEN_NAME=original\r\n");
        Put(@"data\power.lut", "0|100\r\n8000|200\r\n");
        Put(@"ui\ui_car.json", "{\"name\": \"Original\"}");
        Put(@"sfx\GUIDs.txt", "{guid} event:/cars/acm_snapshot_test/engine\r\n");
        Put("car.kn5", "not really a model");
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_car, path))!);
        File.WriteAllText(Path.Combine(_car, path), text);
    }

    private string Read(string path) => File.ReadAllText(Path.Combine(_car, path));

    private static Dictionary<string, byte[]> Files(params (string Path, string Text)[] files) =>
        files.ToDictionary(f => f.Path, f => Encoding.UTF8.GetBytes(f.Text));

    private string Write(params (string Path, string Text)[] files)
    {
        var (snapshotId, error) = _workspace.WriteCarFiles(CarId, Files(files));
        Assert.Null(error);
        return snapshotId!;
    }

    private string Restore(string snapshotId)
    {
        var (beforeId, error) = _workspace.RestoreSnapshot(CarId, snapshotId);
        Assert.Null(error);
        return beforeId!;
    }

    [Fact]
    public void A_write_first_snapshots_the_data_and_UI_files_but_not_the_model()
    {
        var snapshotId = Write((@"data\car.ini", "[INFO]\r\nSCREEN_NAME=edited\r\n"));

        Assert.Contains("edited", Read(@"data\car.ini"));
        var snapshot = Path.Combine(_workspace.Root, "snapshots", CarId, snapshotId);
        Assert.Equal(
            [@"data\car.ini", @"data\power.lut", @"sfx\GUIDs.txt", @"ui\ui_car.json"],
            Directory.GetFiles(snapshot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(snapshot, f)).Order());
        Assert.Contains("original", File.ReadAllText(Path.Combine(snapshot, "data", "car.ini")));
        Assert.Equal([snapshotId], _workspace.Snapshots(CarId));
    }

    [Theory]
    [InlineData("car.kn5")] // a model is never snapshotted (WS-10)
    [InlineData(@"data\..\car.kn5")] // normalized before the scope check
    [InlineData(@"..\acm_other\data\car.ini")] // another car
    [InlineData(@"..\..\snapshots\x.txt")] // inside the workspace, outside the car
    [InlineData("data")] // the folder itself
    public void A_write_outside_the_snapshot_scope_is_refused_and_changes_nothing(string path)
    {
        var (snapshotId, error) = _workspace.WriteCarFiles(CarId, Files((@"data\car.ini", "changed"), (path, "x")));

        Assert.Null(snapshotId);
        Assert.NotNull(error);
        Assert.Contains("original", Read(@"data\car.ini")); // the valid path wasn't written either
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData(@"cars\acm_snapshot_test")]
    [InlineData("acm_snapshot_test.")] // Windows would drop the dot and reach the real car
    [InlineData("acm_missing")]
    public void A_write_to_a_bad_or_missing_car_ID_is_refused(string carId)
    {
        var (snapshotId, error) = _workspace.WriteCarFiles(carId, Files((@"data\car.ini", "x")));

        Assert.Null(snapshotId);
        Assert.NotNull(error);
        Assert.Contains("original", Read(@"data\car.ini"));
    }

    [Fact]
    public void A_failed_write_leaves_the_file_intact_and_no_temp_files()
    {
        File.SetAttributes(Path.Combine(_car, "data", "car.ini"), FileAttributes.ReadOnly); // makes the final move fail

        Assert.Throws<UnauthorizedAccessException>(() => _workspace.WriteCarFiles(CarId, Files((@"data\car.ini", "changed"))));

        File.SetAttributes(Path.Combine(_car, "data", "car.ini"), FileAttributes.Normal);
        Assert.Contains("original", Read(@"data\car.ini"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_workspace.Root, "snapshots", CarId))); // the write-N.tmp files are gone
    }

    [Fact]
    public void A_write_that_fails_partway_puts_back_the_files_already_written()
    {
        File.SetAttributes(Path.Combine(_car, "data", "car.ini"), FileAttributes.ReadOnly);

        // power.lut is moved into place first, then car.ini fails.
        Assert.Throws<UnauthorizedAccessException>(() =>
            _workspace.WriteCarFiles(CarId, Files((@"data\power.lut", "0|1\r\n"), (@"data\car.ini", "changed"))));

        File.SetAttributes(Path.Combine(_car, "data", "car.ini"), FileAttributes.Normal);
        Assert.Equal("0|100\r\n8000|200\r\n", Read(@"data\power.lut"));
        Assert.Contains("original", Read(@"data\car.ini"));
    }

    [Fact]
    public void A_failed_write_to_an_acd_only_car_leaves_no_empty_data_folder()
    {
        var packed = CarData.PackFolder(_car);
        Assert.Null(_workspace.WriteCarFiles(CarId, new Dictionary<string, byte[]> { ["data.acd"] = packed.Bytes! }).Error);
        Directory.Delete(Path.Combine(_car, "data"), recursive: true);
        File.SetAttributes(Path.Combine(_car, "ui", "ui_car.json"), FileAttributes.ReadOnly);

        Assert.Throws<UnauthorizedAccessException>(() =>
            _workspace.WriteCarFiles(CarId, Files((@"data\car.ini", "unpacked"), (@"ui\ui_car.json", "{}"))));

        File.SetAttributes(Path.Combine(_car, "ui", "ui_car.json"), FileAttributes.Normal);
        Assert.False(Directory.Exists(Path.Combine(_car, "data"))); // an empty data\ would raise a false V-12 warning
        Assert.False(CarData.Load(_car).FolderIgnored);
    }

    [Fact]
    public void A_restore_that_fails_partway_puts_back_the_files_it_changed()
    {
        var original = Write((@"data\power.lut", "edited"), (@"ui\ui_car.json", "edited"));
        // data\ is restored before ui\, so power.lut is put back first, then ui_car.json fails.
        File.SetAttributes(Path.Combine(_car, "ui", "ui_car.json"), FileAttributes.ReadOnly);

        Assert.Throws<UnauthorizedAccessException>(() => _workspace.RestoreSnapshot(CarId, original));

        File.SetAttributes(Path.Combine(_car, "ui", "ui_car.json"), FileAttributes.Normal);
        Assert.Equal("edited", Read(@"data\power.lut"));
        Assert.Equal("edited", Read(@"ui\ui_car.json"));
    }

    [Fact]
    public void Restore_removes_a_data_acd_packed_after_the_snapshot_and_can_be_undone()
    {
        var beforeEdit = Write((@"data\car.ini", "[INFO]\r\nSCREEN_NAME=edited\r\n"));
        var packed = CarData.PackFolder(_car);
        Assert.Null(packed.Error);
        Assert.Null(_workspace.WriteCarFiles(CarId, new Dictionary<string, byte[]> { ["data.acd"] = packed.Bytes! }).Error);
        Assert.Equal(DataSource.Acd, CarData.Load(_car).Source);

        var beforeRestore = Restore(beforeEdit);

        Assert.False(File.Exists(Path.Combine(_car, "data.acd")));
        Assert.Contains("original", Read(@"data\car.ini"));
        Assert.Equal(DataSource.Folder, CarData.Load(_car).Source); // AC would use data\ again
        Assert.Equal("not really a model", Read("car.kn5")); // out of scope, untouched

        Restore(beforeRestore); // undo
        Assert.True(File.Exists(Path.Combine(_car, "data.acd")));
        Assert.Contains("edited", Read(@"data\car.ini"));
    }

    [Fact]
    public void Restore_removes_a_data_folder_the_snapshot_did_not_have()
    {
        var packed = CarData.PackFolder(_car);
        Assert.Null(_workspace.WriteCarFiles(CarId, new Dictionary<string, byte[]> { ["data.acd"] = packed.Bytes! }).Error);
        Directory.Delete(Path.Combine(_car, "data"), recursive: true); // a car shipped as data.acd only
        var acdOnly = _workspace.Snapshot(CarId).SnapshotId!;
        Write((@"data\car.ini", "unpacked")); // the user unpacks a file
        Assert.True(CarData.Load(_car).FolderIgnored);

        Restore(acdOnly);

        Assert.False(Directory.Exists(Path.Combine(_car, "data")));
        Assert.False(CarData.Load(_car).FolderIgnored); // no V-12 warning left behind
    }

    [Theory]
    [InlineData("20261103-044419-603", "2026-11-03T04:44:19.603Z")]
    [InlineData("20261103-044419-603-2", "2026-11-03T04:44:19.603Z")] // a clash suffix
    [InlineData("notatime", null)]
    [InlineData("20261303-044419-603", null)] // month 13
    public void A_snapshot_ID_gives_its_UTC_time(string snapshotId, string? expected)
    {
        var time = Workspace.SnapshotTimeUtc(snapshotId);

        Assert.Equal(expected, time?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(time == null || time.Value.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public void Snapshot_IDs_carry_the_time_they_were_taken()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var time = Workspace.SnapshotTimeUtc(_workspace.Snapshot(CarId).SnapshotId!);

        Assert.InRange(time!.Value, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void A_snapshots_data_acd_is_read_with_the_car_IDs_key()
    {
        Assert.Null(_workspace.WriteCarFiles(CarId, new Dictionary<string, byte[]> { ["data.acd"] = CarData.PackFolder(_car).Bytes! }).Error);
        var snapshotId = _workspace.Snapshot(CarId).SnapshotId!;

        var (data, error) = _workspace.SnapshotData(CarId, snapshotId);

        Assert.Null(error);
        Assert.Equal(DataSource.Acd, data!.Source);
        Assert.Null(data.Error);
        Assert.Contains(data.Files, f => f.Name == "car.ini");
        // The folder name is the snapshot ID, so a key built from it can't read the file.
        Assert.NotNull(CarData.Load(Path.Combine(_workspace.Root, "snapshots", CarId, snapshotId)).Error);
        Assert.NotNull(_workspace.SnapshotData(CarId, "20990101-000000-000").Error);
    }

    [Theory]
    [InlineData("20990101-000000-000")]
    [InlineData(@"..\..\cars")]
    [InlineData("")]
    public void Restore_refuses_an_unknown_snapshot(string snapshotId)
    {
        var (beforeId, error) = _workspace.RestoreSnapshot(CarId, snapshotId);

        Assert.Null(beforeId);
        Assert.Contains("has no snapshot", error);
        Assert.Empty(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void Snapshots_taken_in_the_same_millisecond_get_distinct_IDs_in_order()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => _workspace.Snapshot(CarId).SnapshotId!).ToArray();

        Assert.Equal(ids, _workspace.Snapshots(CarId));
        Assert.Equal(5, ids.Distinct().Count());
    }
}
