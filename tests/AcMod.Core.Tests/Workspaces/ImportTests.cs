using System.IO.Compression;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class ImportTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_import_").FullName;
    private readonly Workspace _workspace;

    public ImportTests() => _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Source(string path, string text)
    {
        var full = Path.Combine(_temp, "src", path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    private string Zip(string name, params string[] entries)
    {
        var path = Path.Combine(_temp, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write("content of " + entry);
        }

        return path;
    }

    private string Imported(string carId, string path) => File.ReadAllText(Path.Combine(_workspace.Root, "cars", carId, path));

    private void AssertNothingImported()
    {
        Assert.Empty(_workspace.Cars());
        Assert.False(Directory.Exists(Path.Combine(_workspace.Root, "import.tmp")));
    }

    [Fact]
    public void A_car_folder_is_copied_whole_and_the_source_is_untouched()
    {
        Source(@"acm_x\data\car.ini", "car");
        Source(@"acm_x\car.kn5", "model");
        File.SetAttributes(Source(@"acm_x\ui\ui_car.json", "{}"), FileAttributes.ReadOnly | FileAttributes.Hidden);

        Assert.Equal(("acm_x", null), _workspace.ImportCar(Path.Combine(_temp, "src", "acm_x")));

        Assert.Equal("car", Imported("acm_x", @"data\car.ini"));
        Assert.Equal("model", Imported("acm_x", "car.kn5"));
        var json = File.GetAttributes(Path.Combine(_workspace.Root, "cars", "acm_x", "ui", "ui_car.json"));
        Assert.False(json.HasFlag(FileAttributes.ReadOnly)); // editable in the workspace
        Assert.True(json.HasFlag(FileAttributes.Hidden)); // other attributes are kept
        Assert.True(File.Exists(Path.Combine(_temp, "src", "acm_x", "car.kn5")));
        Assert.False(Directory.Exists(Path.Combine(_workspace.Root, "import.tmp")));
        File.SetAttributes(Path.Combine(_temp, "src", "acm_x", "ui", "ui_car.json"), FileAttributes.Normal); // so Dispose can clean up
    }

    [Fact]
    public void A_parent_folder_with_one_car_inside_finds_the_car()
    {
        Source(@"My Mod v1.2\content\cars\acm_x\data.acd", "acd");
        Source(@"My Mod v1.2\readme.txt", "readme");

        Assert.Equal(("acm_x", null), _workspace.ImportCar(Path.Combine(_temp, "src", "My Mod v1.2")));

        Assert.Equal("acd", Imported("acm_x", "data.acd"));
        Assert.Single(Directory.GetFiles(Path.Combine(_workspace.Root, "cars", "acm_x"), "*", SearchOption.AllDirectories)); // no readme
    }

    [Theory]
    [InlineData("acm_x/data/car.ini", "acm_x/car.kn5")] // the usual layout
    [InlineData("content/cars/acm_x/data/car.ini", "content/cars/acm_x/car.kn5", "readme.txt")] // Content Manager layout
    [InlineData("content\\cars\\acm_x\\data\\car.ini", "content\\cars\\acm_x\\car.kn5")] // backslashes from some Windows tools
    [InlineData("acm_x/data.acd", "acm_x/car.kn5")] // packed data only
    public void A_ZIP_with_one_car_folder_imports_it(params string[] entries)
    {
        var (carId, error) = _workspace.ImportCar(Zip("mod.zip", entries));

        Assert.Null(error);
        Assert.Equal("acm_x", carId);
        Assert.StartsWith("content of ", Imported("acm_x", "car.kn5"));
        Assert.False(File.Exists(Path.Combine(_workspace.Root, "cars", "acm_x", "readme.txt")));
    }

    [Fact]
    public void A_ZIP_with_car_files_at_the_top_takes_its_ID_from_the_ZIP_name()
    {
        Assert.Equal(("acm_top", null), _workspace.ImportCar(Zip("acm_top.zip", "data/car.ini", "car.kn5")));

        Assert.Equal("content of data/car.ini", Imported("acm_top", @"data\car.ini"));
    }

    [Theory]
    [InlineData("acm_x/../../evil.txt")] // lands in the workspace root
    [InlineData("acm_x/../../../evil.txt")] // lands outside the workspace
    [InlineData("acm_x/data/../../acm_y/evil.txt")]
    [InlineData("acm_x/C:/evil.txt")]
    [InlineData("acm_x/data/car.ini:evil")] // NTFS alternate stream
    public void A_ZIP_entry_that_escapes_the_car_folder_stops_the_whole_import(string evil)
    {
        var (carId, error) = _workspace.ImportCar(Zip("evil.zip", "acm_x/data/car.ini", evil));

        Assert.Null(carId);
        Assert.Contains("nothing was imported", error);
        AssertNothingImported();
        Assert.Empty(Directory.GetFiles(_temp, "evil.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void Import_refuses_a_car_ID_that_is_taken()
    {
        _workspace.ImportCar(Zip("first.zip", "acm_x/data/car.ini"));

        var (carId, error) = _workspace.ImportCar(Zip("second.zip", "acm_x/data/power.lut"));

        Assert.Null(carId);
        Assert.Contains("already in the workspace", error);
        Assert.False(File.Exists(Path.Combine(_workspace.Root, "cars", "acm_x", "data", "power.lut")));
    }

    [Theory]
    [InlineData("two_cars.zip", "holds 2 cars (acm_a, acm_b)", "acm_a/data/car.ini", "acm_b/data.acd")]
    [InlineData("no_car.zip", "No car found", "acm_x/car.kn5", "readme.txt")]
    [InlineData("data_file_only.zip", "No car found", "acm_x/data")] // a file named data, not a folder
    public void Import_refuses_a_ZIP_without_exactly_one_car(string name, string message, params string[] entries)
    {
        var (carId, error) = _workspace.ImportCar(Zip(name, entries));

        Assert.Null(carId);
        Assert.Contains(message, error);
        AssertNothingImported();
    }

    [Fact]
    public void Import_refuses_unreadable_and_unsupported_sources()
    {
        var notZip = Path.Combine(_temp, "broken.zip");
        File.WriteAllText(notZip, "not a zip");
        var rar = Path.Combine(_temp, "mod.rar");
        File.WriteAllText(rar, "rar");

        Assert.Contains("Can't read broken.zip", _workspace.ImportCar(notZip).Error);
        Assert.Contains("Only folders and .zip files", _workspace.ImportCar(rar).Error);
        Assert.Contains("doesn't exist", _workspace.ImportCar(Path.Combine(_temp, "missing")).Error);
        AssertNothingImported();
    }

    [Fact]
    public void Import_refuses_a_folder_inside_the_workspace_or_holding_it()
    {
        Assert.Contains("outside the workspace", _workspace.ImportCar(Path.Combine(_workspace.Root, "trash")).Error);
        Assert.Contains("outside the workspace", _workspace.ImportCar(_temp).Error);
        AssertNothingImported();
    }

    [Fact]
    public void A_crashed_imports_leftovers_are_cleared()
    {
        var leftover = Path.Combine(_workspace.Root, "import.tmp", "acm_x", "data");
        Directory.CreateDirectory(leftover);
        File.WriteAllText(Path.Combine(leftover, "stale.ini"), "from a crash");

        Assert.Null(_workspace.ImportCar(Zip("mod.zip", "acm_x/data/car.ini")).Error);

        Assert.False(File.Exists(Path.Combine(_workspace.Root, "cars", "acm_x", "data", "stale.ini")));
        Assert.False(Directory.Exists(Path.Combine(_workspace.Root, "import.tmp")));
    }
}
