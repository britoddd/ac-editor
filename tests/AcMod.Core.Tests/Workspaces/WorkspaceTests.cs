using System.Diagnostics;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class WorkspaceTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_workspace_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private Workspace Open()
    {
        var (workspace, error) = Workspace.Open(Path.Combine(_temp, "ws"), Path.Combine(_temp, "ac"));
        Assert.Null(error);
        return workspace!;
    }

    [Fact]
    public void Open_creates_the_workspace_folders()
    {
        var workspace = Open();

        Assert.Equal(Path.Combine(_temp, "ws"), workspace.Root);
        Assert.All(Workspace.Folders, f => Assert.True(Directory.Exists(Path.Combine(workspace.Root, f)), f));
    }

    [Theory]
    [InlineData("ac", "ac")] // same folder
    [InlineData(@"ac\content\cars\ws", "ac")] // workspace inside AC
    [InlineData("ws", @"ws\ac")] // AC inside the workspace
    [InlineData("ws", @"WS\AC\")] // case and a trailing separator don't hide it
    public void Open_refuses_a_workspace_that_overlaps_AC(string root, string ac)
    {
        var (workspace, error) = Workspace.Open(Path.Combine(_temp, root), Path.Combine(_temp, ac));

        Assert.Null(workspace);
        Assert.Contains("overlaps the AC folder", error);
        Assert.Empty(Directory.GetFileSystemEntries(_temp)); // nothing was created
    }

    [Fact]
    public void Open_allows_a_sibling_whose_name_starts_like_AC()
    {
        // "assettocorsa_mods" starts with "assettocorsa" but isn't inside it.
        var (_, error) = Workspace.Open(Path.Combine(_temp, "assettocorsa_mods"), Path.Combine(_temp, "assettocorsa"));

        Assert.Null(error);
    }

    [Theory]
    [InlineData(@"cars\acm_x\data\car.ini")]
    [InlineData("cars/acm_x/data/car.ini")]
    [InlineData(@"cars\acm_x\..\acm_y\data.acd")] // ".." that stays inside is fine
    public void Resolve_returns_a_path_inside_the_workspace(string relative)
    {
        var workspace = Open();

        var (path, error) = workspace.Resolve(relative);

        Assert.Null(error);
        Assert.StartsWith(workspace.Root + Path.DirectorySeparatorChar, path);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")] // the root itself
    [InlineData(@"..\outside.txt")]
    [InlineData(@"cars\..\..\outside.txt")]
    [InlineData(@"..\ws_other\x.txt")] // a sibling whose name starts like the root
    [InlineData(@"C:\Windows\x.txt")]
    [InlineData(@"\Windows\x.txt")]
    [InlineData(@"C:x.txt")] // drive-relative
    [InlineData(@"cars\acm_x\car.ini:hidden")] // NTFS alternate stream
    [InlineData("cars\\a|b")]
    public void Resolve_refuses_paths_that_could_leave_the_workspace(string relative)
    {
        var (path, error) = Open().Resolve(relative);

        Assert.Null(path);
        Assert.NotNull(error);
    }

    [Fact]
    public void Resolve_refuses_a_path_through_a_junction()
    {
        var workspace = Open();
        var outside = Directory.CreateDirectory(Path.Combine(_temp, "outside")).FullName;
        var link = Path.Combine(workspace.Root, "cars", "acm_link");
        MakeJunction(link, outside);

        var (path, error) = workspace.Resolve(@"cars\acm_link\data\car.ini");

        Directory.Delete(link); // removes only the junction, not the folder it points to
        Assert.Null(path);
        Assert.Contains("junction or symlink", error);
    }

    /// <summary>A junction needs no admin rights, unlike Directory.CreateSymbolicLink. .NET has no API to make one.</summary>
    internal static void MakeJunction(string link, string target)
    {
        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { RedirectStandardOutput = true })!;
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);
    }
}
