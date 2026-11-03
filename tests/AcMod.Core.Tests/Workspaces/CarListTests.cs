using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class CarListTests : IDisposable
{
    private static readonly DateTime Old = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_carlist_").FullName;
    private readonly Workspace _workspace;

    public CarListTests() => _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Put(string carId, string path, string text)
    {
        var full = Path.Combine(_workspace.Root, "cars", carId, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    /// <summary>Sets the time of every file and folder in a folder, the folder included.</summary>
    private static void Age(string folder, DateTime time)
    {
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, time);
        foreach (var dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Append(folder)) Directory.SetLastWriteTimeUtc(dir, time);
    }

    private string Car(string carId) => Path.Combine(_workspace.Root, "cars", carId);

    [Fact]
    public void Cars_lists_ID_display_name_size_and_last_change()
    {
        Put("acm_b", @"ui\ui_car.json", "{\"name\": \"B Car\"}"); // 17 bytes
        Put("acm_b", @"data\car.ini", "0123456789"); // 10 bytes
        Put("acm_a", @"data\car.ini", "12345"); // no ui_car.json
        File.SetAttributes(Put("acm_a", "hidden.txt", "123"), FileAttributes.Hidden); // hidden files still take space
        Age(Car("acm_a"), Old);
        Age(Car("acm_b"), Old);
        File.SetLastWriteTimeUtc(Path.Combine(Car("acm_b"), "data", "car.ini"), Old.AddDays(3)); // the newest change wins

        Assert.Equal(
            [new CarInfo("acm_a", null, 8, Old), new CarInfo("acm_b", "B Car", 27, Old.AddDays(3))],
            _workspace.Cars());
    }

    [Fact]
    public void A_broken_ui_car_json_gives_no_display_name()
    {
        Put("acm_broken", @"ui\ui_car.json", "{\"name\": ");

        Assert.Null(Assert.Single(_workspace.Cars()).DisplayName);
    }

    [Fact]
    public void UnusedCars_returns_only_cars_unchanged_for_the_period()
    {
        Put("acm_old", @"data\car.ini", "x");
        Put("acm_new", @"data\car.ini", "x");
        Age(Car("acm_old"), Old);

        Assert.Equal(["acm_old"], _workspace.UnusedCars(TimeSpan.FromDays(30)).Select(c => c.Id));
    }

    [Fact]
    public void A_car_imported_with_old_files_counts_as_recent()
    {
        Put("acm_imported", @"data\car.ini", "x");
        File.SetLastWriteTimeUtc(Path.Combine(Car("acm_imported"), "data", "car.ini"), Old); // copied files keep their old times

        Assert.Empty(_workspace.UnusedCars(TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Subfolders_shows_each_cars_snapshots_with_their_size()
    {
        Put("acm_x", @"data\car.ini", "12345");
        Put("acm_x", "car.kn5", "not in snapshots");
        _workspace.Snapshot("acm_x");
        _workspace.Snapshot("acm_x");

        var snapshots = Assert.Single(_workspace.Subfolders("snapshots"));

        Assert.Equal("acm_x", snapshots.Name);
        Assert.Equal(10, snapshots.Size); // two copies of car.ini
        Assert.Empty(_workspace.Subfolders("trash"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(@"cars\acm_x")]
    public void Subfolders_refuses_anything_but_a_workspace_folder(string folder)
    {
        Assert.Throws<ArgumentException>(() => _workspace.Subfolders(folder));
    }
}
