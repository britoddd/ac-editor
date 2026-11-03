using AcMod.Core.Workspaces;
using Xunit.Abstractions;

namespace AcMod.Core.Tests.Workspaces;

public sealed class AcInstallTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_acinstall_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Steam => Path.Combine(_temp, "Program Files (x86)", "Steam");

    /// <summary>Puts AC in a Steam library the way Steam does: an appmanifest naming the folder under steamapps\common.</summary>
    private static string InstallAc(string library, string installDir = "assettocorsa", bool withExe = true)
    {
        Directory.CreateDirectory(Path.Combine(library, "steamapps"));
        File.WriteAllText(
            Path.Combine(library, "steamapps", "appmanifest_244210.acf"),
            $"\"AppState\"\n{{\n\t\"appid\"\t\t\"244210\"\n\t\"name\"\t\t\"Assetto Corsa\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n");
        var ac = Path.Combine(library, "steamapps", "common", installDir);
        Directory.CreateDirectory(Path.Combine(ac, "content", "cars"));
        if (withExe) File.WriteAllText(Path.Combine(ac, "acs.exe"), "");
        return ac;
    }

    /// <summary>Writes libraryfolders.vdf the way Steam does, with each backslash written as \\.</summary>
    private void ListLibraries(params string[] libraries)
    {
        Directory.CreateDirectory(Path.Combine(Steam, "steamapps"));
        var blocks = libraries.Select((library, i) =>
            $"\t\"{i}\"\n\t{{\n\t\t\"path\"\t\t\"{library.Replace(@"\", @"\\")}\"\n\t\t\"label\"\t\t\"\"\n\t\t\"apps\"\n\t\t{{\n\t\t\t\"228980\"\t\t\"29212173\"\n\t\t}}\n\t}}\n");
        File.WriteAllText(Path.Combine(Steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\"\n{\n" + string.Concat(blocks) + "}\n");
    }

    [Fact]
    public void Finds_AC_in_the_Steam_folder_itself()
    {
        var ac = InstallAc(Steam);
        ListLibraries(Steam);

        Assert.Equal(ac, AcInstall.FindInSteam(Steam));
    }

    [Fact]
    public void Finds_AC_in_a_secondary_library()
    {
        var library = Path.Combine(_temp, "Games (E)", "SteamLibrary");
        var ac = InstallAc(library);
        ListLibraries(Steam, library);

        Assert.Equal(ac, AcInstall.FindInSteam(Steam));
    }

    [Fact]
    public void Finds_AC_in_the_Steam_folder_without_a_libraryfolders_vdf()
    {
        var ac = InstallAc(Steam);

        Assert.Equal(ac, AcInstall.FindInSteam(Steam));
    }

    [Fact]
    public void Finds_nothing_when_no_library_has_AC()
    {
        var library = Path.Combine(_temp, "SteamLibrary");
        Directory.CreateDirectory(library);
        ListLibraries(Steam, library);

        Assert.Null(AcInstall.FindInSteam(Steam));
        Assert.Null(AcInstall.FindInSteam(Path.Combine(_temp, "no_steam_here")));
    }

    [Fact]
    public void Finds_nothing_when_the_manifests_folder_is_not_an_AC_install()
    {
        InstallAc(Steam, withExe: false); // left behind after an uninstall, say
        ListLibraries(Steam);

        Assert.Null(AcInstall.FindInSteam(Steam));
    }

    [Fact]
    public void IsAcFolder_needs_acs_exe_and_content_cars()
    {
        var ac = InstallAc(Steam);

        Assert.True(AcInstall.IsAcFolder(ac));
        Assert.False(AcInstall.IsAcFolder(Path.Combine(ac, "content")));
        Directory.Delete(Path.Combine(ac, "content", "cars"));
        Assert.False(AcInstall.IsAcFolder(ac));
    }

    [Fact]
    public void Find_on_this_PC_returns_an_AC_folder_or_null()
    {
        var ac = AcInstall.Find(); // reads the real registry; Steam may not be installed

        output.WriteLine($"AcInstall.Find() = {ac ?? "null"}");
        Assert.True(ac == null || AcInstall.IsAcFolder(ac));
    }
}
