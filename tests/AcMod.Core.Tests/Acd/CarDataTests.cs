using System.Text;
using AcMod.Core.Acd;

namespace AcMod.Core.Tests.Acd;

public sealed class CarDataTests : IDisposable
{
    private const string CarId = "acm_cardata_test";

    // Multi-line like real files, so a wrong key is caught (see the ponytail note in AcdFile.Parse).
    private static readonly AcdEntry[] FolderFiles =
        [new("car.ini", "[INFO]\r\nSCREEN_NAME=folder\r\n"u8.ToArray()), new("B.lut", "0|1\r\n1|2\r\n"u8.ToArray())];
    private static readonly AcdEntry[] AcdFiles = [new("car.ini", "[INFO]\r\nSCREEN_NAME=acd\r\n"u8.ToArray())];

    private readonly string _root = Directory.CreateTempSubdirectory("acm_cardata_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Makes a car folder named <see cref="CarId"/> with an optional data\ folder and data.acd.</summary>
    private string Car(bool folder, byte[]? acd)
    {
        var car = Path.Combine(_root, CarId);
        Directory.CreateDirectory(car);
        if (folder)
        {
            Directory.CreateDirectory(Path.Combine(car, "data"));
            foreach (var file in FolderFiles) File.WriteAllBytes(Path.Combine(car, "data", file.Name), file.Data);
        }

        if (acd != null) File.WriteAllBytes(Path.Combine(car, "data.acd"), acd);
        return car;
    }

    /// <summary>The file names, then car.ini's SCREEN_NAME, which tells the two sources apart: "folder" or "acd".</summary>
    private static string[] Texts(CarData data) =>
        [.. data.Files.Select(f => f.Name), Encoding.UTF8.GetString(data.Files.Single(f => f.Name == "car.ini").Data).Split('=')[1].TrimEnd()];

    [Fact]
    public void A_data_folder_alone_is_the_source()
    {
        var data = CarData.Load(Car(folder: true, acd: null));

        Assert.Equal(DataSource.Folder, data.Source);
        Assert.Equal(["B.lut", "car.ini", "folder"], Texts(data)); // ordinal order
        Assert.Null(data.Error);
        Assert.False(data.FolderIgnored);
    }

    [Fact]
    public void A_data_acd_alone_is_the_source()
    {
        var data = CarData.Load(Car(folder: false, acd: AcdFile.Pack(AcdFiles, CarId)));

        Assert.Equal(DataSource.Acd, data.Source);
        Assert.Equal(["car.ini", "acd"], Texts(data));
        Assert.Null(data.Error);
        Assert.False(data.FolderIgnored);
    }

    [Fact]
    public void With_both_the_acd_wins_and_the_folder_is_flagged_as_ignored()
    {
        var data = CarData.Load(Car(folder: true, acd: AcdFile.Pack(AcdFiles, CarId)));

        Assert.Equal(DataSource.Acd, data.Source);
        Assert.Equal(["car.ini", "acd"], Texts(data));
        Assert.True(data.FolderIgnored);
    }

    [Fact]
    public void An_unreadable_acd_stays_the_source_and_does_not_fall_back_to_the_folder()
    {
        var data = CarData.Load(Car(folder: true, acd: AcdFile.Pack(AcdFiles, "some_other_car")));

        Assert.Equal(DataSource.Acd, data.Source);
        Assert.NotNull(data.Error);
        Assert.Empty(data.Files);
        Assert.True(data.FolderIgnored);
    }

    [Fact]
    public void A_car_with_neither_is_reported()
    {
        var data = CarData.Load(Car(folder: false, acd: null));

        Assert.Equal(DataSource.None, data.Source);
        Assert.NotNull(data.Error);
    }

    [Fact]
    public void The_committed_synthetic_car_has_both_and_reads_its_acd()
    {
        var data = CarData.Load(Fixtures.Synthetic("acd", "acm_acd_synthetic"));

        Assert.Equal(DataSource.Acd, data.Source);
        Assert.Null(data.Error);
        Assert.True(data.FolderIgnored);
        Assert.Equal(7, data.Files.Count);
    }
}
