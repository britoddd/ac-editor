using System.Text;
using AcMod.Core.Acd;

namespace AcMod.Core.Tests.Acd;

public class AcdFileTests
{
    private const string CarId = "acm_tutorial_basic";

    // Real-looking text: CRLF and LF lines, a tab, a UTF-8 BOM, non-ASCII UTF-8, and an empty file (docs/VERIFIED_FACTS.md).
    private static readonly (string Name, string Text)[] Files =
    [
        ("car.ini", "[HEADER]\r\nVERSION=2\r\n\r\n[INFO]\r\nSCREEN_NAME=Tutorial\t; café\r\n"),
        ("power.lut", "0|100\n1000|150\n7000|90"),
        ("aero.ini", "﻿[HEADER]\nVERSION=1\n"),
        ("drs.ini", ""),
    ];

    // ponytail: test-only packer, written from the docs/ACD.md layout. Step 3's real writer replaces it.
    private static byte[] Pack(string carId, bool header, params (string Name, string Text)[] files)
    {
        var key = Encoding.ASCII.GetBytes(AcdKey.FromName(carId));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        if (header)
        {
            writer.Write(-1111);
            writer.Write(591346);
        }

        foreach (var (name, text) in files)
        {
            var data = Encoding.UTF8.GetBytes(text);
            writer.Write(name.Length);
            writer.Write(Encoding.ASCII.GetBytes(name));
            writer.Write(data.Length);
            for (var i = 0; i < data.Length; i++)
            {
                writer.Write((int)(byte)(data[i] + key[i % key.Length]));
            }
        }

        return stream.ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_every_entry_with_or_without_the_header(bool header)
    {
        var acd = AcdFile.Parse(Pack(CarId, header, Files), CarId);

        Assert.Null(acd.Error);
        Assert.Equal(Files.Select(f => f.Name), acd.Entries.Select(e => e.Name));
        Assert.Equal(Files.Select(f => Encoding.UTF8.GetBytes(f.Text)), acd.Entries.Select(e => e.Data));
    }

    [Fact]
    public void A_wrong_car_id_is_reported_not_returned()
    {
        var acd = AcdFile.Parse(Pack(CarId, false, Files), "acm_tutorial_basic2");

        Assert.Contains("doesn't decrypt to text", acd.Error);
        Assert.Empty(acd.Entries);
    }

    [Fact]
    public void A_cut_short_file_never_throws_and_is_never_half_read()
    {
        var bytes = Pack(CarId, true, Files);

        for (var length = 0; length < bytes.Length; length++)
        {
            var acd = AcdFile.Parse(bytes.AsSpan(0, length), CarId);
            Assert.True(acd.Error != null || acd.Entries.Count < Files.Length, $"length {length}");
            if (acd.Error != null) Assert.Empty(acd.Entries);
        }
    }

    [Fact]
    public void Non_zero_padding_is_reported()
    {
        var bytes = Pack(CarId, false, Files);
        bytes[4 + "car.ini".Length + 4 + 1] = 1; // first padding byte of car.ini's data

        Assert.Contains("non-zero padding", AcdFile.Parse(bytes, CarId).Error);
    }

    [Theory]
    [InlineData("../car.ini")]
    [InlineData("data\\car.ini")]
    [InlineData("..")]
    [InlineData("car\tini")]
    public void Names_that_are_not_plain_file_names_are_reported(string name)
    {
        Assert.Contains("isn't a plain ASCII file name", AcdFile.Parse(Pack(CarId, false, (name, "x")), CarId).Error);
    }

    [Fact]
    public void A_name_with_a_space_is_allowed_as_in_real_mods()
    {
        Assert.Null(AcdFile.Parse(Pack(CarId, false, ("tyres - Copy.ini", "x")), CarId).Error);
    }

    [Fact]
    public void A_duplicate_name_is_reported_ignoring_case()
    {
        Assert.Contains("appears twice", AcdFile.Parse(Pack(CarId, false, ("car.ini", "a"), ("CAR.ini", "b")), CarId).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_file_with_no_entries_is_reported(bool header)
    {
        Assert.Equal("It contains no files.", AcdFile.Parse(Pack(CarId, header), CarId).Error);
    }

    [Fact]
    public void A_non_ascii_car_id_is_reported_not_thrown()
    {
        Assert.NotNull(AcdFile.Parse(Pack(CarId, false, Files), "café").Error);
    }
}
