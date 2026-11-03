using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class TrashTests : IDisposable
{
    private const string CarId = "acm_my_car"; // underscores, like most car IDs

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_trash_").FullName;
    private readonly Workspace _workspace;

    public TrashTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        Put(@"data\car.ini", "original");
        Put("car.kn5", "model");
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car => Path.Combine(_workspace.Root, "cars", CarId);

    private void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Car, path))!);
        File.WriteAllText(Path.Combine(Car, path), text);
    }

    private string Delete()
    {
        var (trashId, error) = _workspace.DeleteCar(CarId);
        Assert.Null(error);
        return trashId!;
    }

    [Fact]
    public void Delete_moves_the_car_to_the_trash_and_restore_brings_it_back()
    {
        var trashId = Delete();

        Assert.StartsWith(CarId + "_", trashId);
        Assert.Empty(_workspace.Cars());
        Assert.Equal([trashId], _workspace.Subfolders("trash").Select(f => f.Name));

        Assert.Equal((CarId, null), _workspace.RestoreFromTrash(trashId));

        Assert.Equal("original", File.ReadAllText(Path.Combine(Car, "data", "car.ini")));
        Assert.Equal("model", File.ReadAllText(Path.Combine(Car, "car.kn5"))); // the whole car, not just the snapshot scope
        Assert.Empty(_workspace.Subfolders("trash"));
    }

    [Fact]
    public void Deleting_the_same_car_twice_keeps_both_trash_entries()
    {
        var first = Delete();
        Put(@"data\car.ini", "second");
        var second = Delete();

        Assert.NotEqual(first, second);
        Assert.Equal(2, _workspace.Subfolders("trash").Length);
    }

    [Theory]
    [InlineData("acm_missing")]
    [InlineData("..")]
    [InlineData("")]
    public void Delete_refuses_a_missing_or_bad_car_ID(string carId)
    {
        var (trashId, error) = _workspace.DeleteCar(carId);

        Assert.Null(trashId);
        Assert.NotNull(error);
        Assert.Single(_workspace.Cars());
    }

    [Fact]
    public void Restore_refuses_when_the_car_ID_is_taken_again()
    {
        var trashId = Delete();
        Put(@"data\car.ini", "imported again");

        var (carId, error) = _workspace.RestoreFromTrash(trashId);

        Assert.Null(carId);
        Assert.Contains("already in the workspace", error);
        Assert.Equal("imported again", File.ReadAllText(Path.Combine(Car, "data", "car.ini")));
        Assert.Single(_workspace.Subfolders("trash"));
    }

    [Theory]
    [InlineData("acm_my")]
    [InlineData("ACM_MY_CAR")] // case must match too
    [InlineData("acm_my_car ")]
    [InlineData("")]
    public void Permanent_delete_refuses_unless_the_typed_ID_matches_exactly(string typed)
    {
        var trashId = Delete();

        var error = _workspace.DeletePermanently(trashId, typed);

        Assert.Contains("Type the car ID acm_my_car exactly", error);
        Assert.Single(_workspace.Subfolders("trash"));
    }

    [Fact]
    public void Permanent_delete_removes_the_entry_and_its_snapshots_even_with_read_only_files()
    {
        _workspace.Snapshot(CarId);
        File.SetAttributes(Path.Combine(Car, "car.kn5"), FileAttributes.ReadOnly);
        var trashId = Delete();

        Assert.Null(_workspace.DeletePermanently(trashId, CarId));

        Assert.Empty(_workspace.Subfolders("trash"));
        Assert.Empty(_workspace.Subfolders("snapshots"));
    }

    [Fact]
    public void Permanent_delete_keeps_snapshots_while_the_car_ID_is_back_in_cars()
    {
        var trashId = Delete();
        Put(@"data\car.ini", "imported again");
        _workspace.Snapshot(CarId); // the new car's history

        Assert.Contains("are kept", _workspace.PermanentDeleteWarning(trashId).Warning);
        Assert.Null(_workspace.DeletePermanently(trashId, CarId));

        Assert.Single(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void Permanent_delete_keeps_snapshots_while_another_trash_entry_has_the_car_ID()
    {
        _workspace.Snapshot(CarId);
        var first = Delete();
        Put(@"data\car.ini", "second");
        Delete();

        Assert.Null(_workspace.DeletePermanently(first, CarId));

        Assert.Single(_workspace.Snapshots(CarId));
    }

    [Fact]
    public void The_warning_says_the_delete_is_permanent_and_names_the_snapshots()
    {
        _workspace.Snapshot(CarId);
        _workspace.Snapshot(CarId);
        var trashId = Delete();

        var (warning, error) = _workspace.PermanentDeleteWarning(trashId);

        Assert.Null(error);
        Assert.StartsWith($"WARNING: this permanently deletes {trashId} (0.0 MB) and its 2 snapshot(s) (0.0 MB).", warning);
        Assert.Contains("can't be undone", warning);
        Assert.EndsWith("Type acm_my_car to confirm.", warning);
        Assert.NotNull(_workspace.PermanentDeleteWarning("acm_my_car_20990101-000000-000").Error);
    }

    [Fact]
    public void Permanent_delete_removes_a_junction_but_not_what_it_points_to()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_temp, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "keep.txt"), "keep");
        File.SetAttributes(Path.Combine(outside, "keep.txt"), FileAttributes.ReadOnly);
        WorkspaceTests.MakeJunction(Path.Combine(Car, "linked"), outside);
        var trashId = Delete();

        Assert.Null(_workspace.DeletePermanently(trashId, CarId));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "keep.txt")));
        Assert.True(File.GetAttributes(Path.Combine(outside, "keep.txt")).HasFlag(FileAttributes.ReadOnly)); // not even its attributes
        File.SetAttributes(Path.Combine(outside, "keep.txt"), FileAttributes.Normal); // so Dispose can clean up
    }

    [Theory]
    [InlineData("acm_my_car_20990101-000000-000")] // not in the trash
    [InlineData(@"..\cars\acm_my_car")] // a car outside the trash
    [InlineData("..")]
    [InlineData("nounderscore")]
    public void Restore_and_permanent_delete_refuse_anything_but_a_trash_entry(string trashId)
    {
        Directory.CreateDirectory(Path.Combine(_workspace.Root, "trash", "nounderscore"));

        Assert.NotNull(_workspace.RestoreFromTrash(trashId).Error);
        Assert.NotNull(_workspace.DeletePermanently(trashId, CarId));
        Assert.True(Directory.Exists(Car));
    }
}
