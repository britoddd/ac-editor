using System.Text.Json;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class InstallTests : IDisposable
{
    private const string CarId = "acm_install_test";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_install_").FullName;
    private readonly string _ac;
    private readonly Workspace _workspace;

    public InstallTests()
    {
        _ac = Path.Combine(_temp, "assettocorsa");
        Directory.CreateDirectory(Path.Combine(_ac, "content", "cars"));
        File.WriteAllText(Path.Combine(_ac, "acs.exe"), "");
        _workspace = Workspace.Open(Path.Combine(_temp, "ws"), _ac).Workspace!;
        Put(Car, @"data\car.ini", "[INFO]\r\nSCREEN_NAME=v1\r\n");
        Put(Car, "car.kn5", "model");
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Car => Path.Combine(_workspace.Root, "cars", CarId);

    private string Installed => Path.Combine(_ac, "content", "cars", CarId);

    private static string Put(string folder, string path, string text)
    {
        var full = Path.Combine(folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    private string Install()
    {
        var (installed, _, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);
        Assert.Null(error);
        return installed!;
    }

    /// <summary>content\cars holds only the installed car, with no .acmod-installing or .acmod-old left behind.</summary>
    private void AssertAcCarsHoldsOnly(params string[] names) =>
        Assert.Equal(names, Directory.GetDirectories(Path.Combine(_ac, "content", "cars")).Select(Path.GetFileName));

    [Fact]
    public void A_fresh_install_copies_the_car_and_marks_it()
    {
        var (installed, replaced, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);

        Assert.Null(error);
        Assert.Null(replaced);
        Assert.Equal(Installed, installed);
        Assert.Equal("model", File.ReadAllText(Path.Combine(Installed, "car.kn5")));
        Assert.Contains("v1", File.ReadAllText(Path.Combine(Installed, "data", "car.ini")));
        using var marker = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Installed, Workspace.InstallMarker)));
        Assert.Equal(CarId, marker.RootElement.GetProperty("carId").GetString());
        Assert.False(File.Exists(Path.Combine(Car, Workspace.InstallMarker))); // the workspace car is untouched
        AssertAcCarsHoldsOnly(CarId);
    }

    [Fact]
    public void Reinstalling_replaces_this_apps_install_and_keeps_the_old_one_in_the_trash()
    {
        Install();
        Put(Installed, @"data\car.ini", "tuned in place"); // like the owner's in-game tuning in M0
        Put(Installed, @"data\proview_nodes.ini", "written by AC"); // AC creates this file (docs/VERIFIED_FACTS.md)
        Put(Car, @"data\car.ini", "[INFO]\r\nSCREEN_NAME=v2\r\n");

        var (_, replaced, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);

        Assert.Null(error);
        Assert.Contains("v2", File.ReadAllText(Path.Combine(Installed, "data", "car.ini")));
        Assert.False(File.Exists(Path.Combine(Installed, "data", "proview_nodes.ini")));
        var trash = Path.Combine(_workspace.Root, "trash", replaced!);
        Assert.Equal("tuned in place", File.ReadAllText(Path.Combine(trash, "data", "car.ini")));
        Assert.Equal("written by AC", File.ReadAllText(Path.Combine(trash, "data", "proview_nodes.ini")));
        AssertAcCarsHoldsOnly(CarId);
    }

    [Fact]
    public void A_folder_without_the_marker_is_never_replaced()
    {
        Put(Installed, @"data\car.ini", "someone else's mod"); // a hand copy, a Content Manager install, or a Kunos car

        var (installed, _, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);

        Assert.Null(installed);
        Assert.Contains("wasn't installed by this app", error);
        Assert.Equal("someone else's mod", File.ReadAllText(Path.Combine(Installed, "data", "car.ini")));
        Assert.Empty(_workspace.Subfolders("trash"));
        AssertAcCarsHoldsOnly(CarId);
    }

    [Fact]
    public void A_failed_replace_leaves_the_installed_car_as_it_was()
    {
        Install();
        Put(Installed, @"data\car.ini", "installed");
        Put(Car, @"data\car.ini", "[INFO]\r\nSCREEN_NAME=v2\r\n");

        string? error;
        using (File.Open(Path.Combine(Installed, "data", "car.ini"), FileMode.Open, FileAccess.Read, FileShare.Read)) // like AC holding it open
        {
            (_, _, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);
        }

        Assert.Contains("left as it was", error);
        Assert.Equal("installed", File.ReadAllText(Path.Combine(Installed, "data", "car.ini")));
        Assert.Empty(_workspace.Subfolders("trash")); // the copy made for the trash is removed again
        AssertAcCarsHoldsOnly(CarId);
    }

    [Fact]
    public void Leftovers_from_a_crashed_install_are_cleared()
    {
        Put(Installed + ".acmod-installing", "half.txt", "half copied");
        Put(Installed + ".acmod-old", "old.txt", "old");

        Install();

        AssertAcCarsHoldsOnly(CarId);
    }

    [Fact]
    public void Validation_errors_block_the_install_unless_the_user_overrides()
    {
        var (installed, _, error) = _workspace.InstallToAc(CarId); // this test car fails V-04, V-05 and V-09

        Assert.Null(installed);
        Assert.Contains("Validation found", error);
        AssertAcCarsHoldsOnly();
        Assert.NotNull(_workspace.InstallToAc(CarId, ignoreErrors: true).InstalledFolder);
    }

    [Fact]
    public void A_car_with_unreadable_data_is_refused()
    {
        File.WriteAllText(Path.Combine(Car, "data.acd"), "not an acd file"); // AC would use this and ignore data\ (V-12)

        var (installed, _, error) = _workspace.InstallToAc(CarId, ignoreErrors: true);

        Assert.Null(installed);
        Assert.Contains("data can't be read", error);
        AssertAcCarsHoldsOnly();
    }

    [Fact]
    public void A_car_with_no_data_is_refused()
    {
        Directory.Delete(Path.Combine(Car, "data"), recursive: true);

        Assert.Contains("data can't be read", _workspace.InstallToAc(CarId, ignoreErrors: true).Error);
        AssertAcCarsHoldsOnly();
    }

    [Theory]
    [InlineData("acm_missing")]
    [InlineData("..")]
    [InlineData(@"..\..\content")]
    [InlineData("")]
    public void A_missing_or_bad_car_ID_is_refused(string carId)
    {
        Assert.NotNull(_workspace.InstallToAc(carId, ignoreErrors: true).Error);
        AssertAcCarsHoldsOnly();
    }

    [Fact]
    public void Install_needs_a_real_AC_folder()
    {
        var noAc = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        File.Delete(Path.Combine(_ac, "acs.exe"));

        Assert.Contains("Choose the AC folder first", noAc.InstallToAc(CarId, ignoreErrors: true).Error);
        Assert.Contains("Choose the AC folder first", _workspace.InstallToAc(CarId, ignoreErrors: true).Error);
        AssertAcCarsHoldsOnly();
    }

    [Fact]
    public void A_junction_in_content_cars_is_never_replaced()
    {
        var elsewhere = Directory.CreateDirectory(Path.Combine(_temp, "elsewhere")).FullName;
        File.WriteAllText(Path.Combine(elsewhere, Workspace.InstallMarker), "{}"); // even with a marker inside
        WorkspaceTests.MakeJunction(Installed, elsewhere);

        var error = _workspace.InstallToAc(CarId, ignoreErrors: true).Error;

        Directory.Delete(Installed); // removes only the junction
        Assert.Contains("junction or symlink", error);
        Assert.Equal(["acmod_install.json"], Directory.GetFiles(elsewhere).Select(Path.GetFileName));
    }
}
