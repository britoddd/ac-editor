using AcMod.Core.Acd;

namespace AcMod.Core.Tests.Acd;

/// <summary>
/// Part of the M2 exit gate: Core against acd.py's output, used only as a black-box oracle (docs/ACD.md).
/// Each pair is a data folder and acd.py's pack of it, sitting in a folder named after the car ID so the key comes from the path.
/// </summary>
public class AcdPyComparisonTests
{
    public static TheoryData<string, string> Pairs()
    {
        // Committed: synthetic files written for this project, packed once by the owner with acd.py.
        var pairs = new TheoryData<string, string>
        {
            { "fixtures/synthetic/acd/acm_acd_synthetic/data", "fixtures/synthetic/acd/acm_acd_synthetic/data.acd" },
        };

        // Local only: the tutorial car's 40 files, packed by acd.py on 2026-11-02.
        const string tutorialAcd = "fixtures/local/acdpy_reference/acm_tutorial_basic/data.acd";
        if (File.Exists(Path.Combine(Fixtures.RepoRoot, tutorialAcd)))
        {
            pairs.Add("fixtures/local/acm_tutorial_basic/data", tutorialAcd);
        }

        return pairs;
    }

    private static AcdEntry[] ReadFolder(string dataFolder) => CarData.ReadFolder(Path.Combine(Fixtures.RepoRoot, dataFolder));

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Core_unpacks_acd_py_output_to_the_original_files(string dataFolder, string acdPath)
    {
        var acd = AcdFile.Parse(File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot, acdPath)), AcdKey.SourceName(acdPath));
        var original = ReadFolder(dataFolder);

        Assert.Null(acd.Error);
        Assert.Equal(original.Select(e => e.Name), acd.Entries.Select(e => e.Name));
        Assert.Equal(original.Select(e => e.Data), acd.Entries.Select(e => e.Data));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Every_wrong_car_id_is_reported(string dataFolder, string acdPath)
    {
        _ = dataFolder;
        var bytes = File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot, acdPath));
        var carId = AcdKey.SourceName(acdPath);

        var wrongIds = Enumerable.Range(0, 1000).SelectMany(i => new[] { carId + i, "car_" + i });

        Assert.All(wrongIds, id => Assert.NotNull(AcdFile.Parse(bytes, id).Error));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Core_packs_the_folder_byte_identical_to_acd_py(string dataFolder, string acdPath)
    {
        var expected = File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot, acdPath));

        // PK-1 end to end: the car folder holding data\ has the same name as the folder holding acd.py's data.acd.
        var (bytes, error) = CarData.PackFolder(Path.GetDirectoryName(Path.Combine(Fixtures.RepoRoot, dataFolder))!);

        Assert.Null(error);
        Assert.True(expected.AsSpan().SequenceEqual(bytes));
    }
}
