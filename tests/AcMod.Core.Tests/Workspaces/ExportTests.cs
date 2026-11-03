using System.IO.Compression;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class ExportTests : IDisposable
{
    private const string CarId = "acm_export_test";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_export_").FullName;
    private readonly Workspace _workspace;

    public ExportTests()
    {
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        foreach (var (path, text) in new[]
                 {
                     (@"data\car.ini", "[INFO]\r\nSCREEN_NAME=x\r\n"), ("car.kn5", "model"), (@"skins\red\preview.jpg", "jpg"),
                     (Workspace.InstallMarker, "{}"), ("car.blend", "blender"), (@"texture\work.PSD", "photoshop"),
                 })
        {
            var full = Path.Combine(Car, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car => Path.Combine(_workspace.Root, "cars", CarId);

    [Fact]
    public void The_ZIP_has_the_car_under_its_ID_without_the_marker_or_dev_files()
    {
        var (zipPath, error) = _workspace.ExportZip(CarId, ignoreErrors: true);

        Assert.Null(error);
        Assert.StartsWith(Path.Combine(_workspace.Root, "exports", CarId + "_"), zipPath);
        using var zip = ZipFile.OpenRead(zipPath!);
        Assert.Equal(
            [$"{CarId}/car.kn5", $"{CarId}/data/car.ini", $"{CarId}/skins/red/preview.jpg"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetFiles(Path.Combine(_workspace.Root, "exports"), "*.tmp"));
    }

    [Fact]
    public void An_exported_ZIP_imports_back_as_the_same_car()
    {
        var zipPath = _workspace.ExportZip(CarId, ignoreErrors: true).ZipPath!;
        var other = Workspace.Open(Path.Combine(_temp, "ws2"), null).Workspace!;

        Assert.Equal((CarId, null), other.ImportCar(zipPath));

        Assert.Equal("[INFO]\r\nSCREEN_NAME=x\r\n", File.ReadAllText(Path.Combine(other.Root, "cars", CarId, "data", "car.ini")));
    }

    [Fact]
    public void Validation_errors_block_the_export_unless_the_user_overrides()
    {
        var (zipPath, error) = _workspace.ExportZip(CarId); // this car has no collider.kn5, mandatory data files, or bank

        Assert.Null(zipPath);
        Assert.Contains("Validation found", error);
        Assert.Contains("V-04", error);
        Assert.Empty(Directory.GetFiles(Path.Combine(_workspace.Root, "exports")));
        Assert.NotNull(_workspace.ExportZip(CarId, ignoreErrors: true).ZipPath);
    }

    [Fact]
    public void A_car_with_unreadable_data_or_a_bad_ID_is_not_exported()
    {
        File.WriteAllText(Path.Combine(Car, "data.acd"), "not an acd file");

        Assert.Contains("can't be read", _workspace.ExportZip(CarId, ignoreErrors: true).Error);
        Assert.NotNull(_workspace.ExportZip("..", ignoreErrors: true).Error);
        Assert.Empty(Directory.GetFiles(Path.Combine(_workspace.Root, "exports")));
    }
}
