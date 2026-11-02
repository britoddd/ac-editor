namespace AcMod.Core.Tests;

/// <summary>Finds the repo's fixtures folders from the test output folder.</summary>
internal static class Fixtures
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static string Synthetic(params string[] parts) => Path.Combine([RepoRoot, "fixtures", "synthetic", .. parts]);

    /// <summary>The gitignored folder of real cars. It may not exist, for example on a fresh clone.</summary>
    public static string LocalRoot => Path.Combine(RepoRoot, "fixtures", "local");

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AcMod.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find AcMod.slnx above " + AppContext.BaseDirectory);
    }
}
