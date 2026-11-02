using System.Text;
using AcMod.Core.Acd;

namespace AcMod.Core.Tests.Acd;

public class AcdFileTests
{
    private const string CarId = "acm_tutorial_basic";

    // Real-looking text: CRLF and LF lines, a tab, a UTF-8 BOM, non-ASCII UTF-8, and an empty file (docs/VERIFIED_FACTS.md).
    private static readonly AcdEntry[] Files =
    [
        Entry("car.ini", "[HEADER]\r\nVERSION=2\r\n\r\n[INFO]\r\nSCREEN_NAME=Tutorial\t; café\r\n"),
        Entry("power.lut", "0|100\n1000|150\n7000|90"),
        Entry("aero.ini", "﻿[HEADER]\nVERSION=1\n"),
        Entry("drs.ini", ""),
    ];

    private static AcdEntry Entry(string name, string text) => new(name, Encoding.UTF8.GetBytes(text));

    private static byte[] WithHeader(byte[] acd) => [.. BitConverter.GetBytes(-1111), .. BitConverter.GetBytes(591346), .. acd];

    private static void AssertSameEntries(IReadOnlyList<AcdEntry> expected, AcdFile actual)
    {
        Assert.Null(actual.Error);
        Assert.Equal(expected.Select(e => e.Name), actual.Entries.Select(e => e.Name));
        Assert.Equal(expected.Select(e => e.Data), actual.Entries.Select(e => e.Data));
    }

    [Fact]
    public void Pack_matches_the_layout_and_cipher_worked_by_hand()
    {
        // Key for "t" is "116-0-0-131-66-101-171-171". 'A' (0x41) + '1' (0x31) = 0x72.
        var bytes = AcdFile.Pack([new AcdEntry("a", [0x41])], "t");

        Assert.Equal(new byte[] { 1, 0, 0, 0, (byte)'a', 1, 0, 0, 0, 0x72, 0, 0, 0 }, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_what_Pack_wrote_with_or_without_the_header(bool header)
    {
        var bytes = AcdFile.Pack(Files, CarId);

        AssertSameEntries(Files, AcdFile.Parse(header ? WithHeader(bytes) : bytes, CarId));
    }

    [Fact]
    public void Pack_writes_no_header_and_only_zero_padding()
    {
        var bytes = AcdFile.Pack(Files, CarId);

        Assert.Equal("car.ini".Length, BitConverter.ToInt32(bytes)); // the first entry starts at byte 0
        Assert.Equal(Files.Sum(f => 4 + f.Name.Length + 4 + 4 * f.Data.Length), bytes.Length);
        var data = bytes.AsSpan(4 + "car.ini".Length + 4, 4 * Files[0].Data.Length).ToArray();
        Assert.All(data.Where((_, i) => i % 4 != 0), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Repacking_for_a_new_car_id_reads_only_under_that_id()
    {
        var repacked = AcdFile.Pack(AcdFile.Parse(AcdFile.Pack(Files, CarId), CarId).Entries, "acm_tutorial_copy");

        AssertSameEntries(Files, AcdFile.Parse(repacked, "acm_tutorial_copy"));
        Assert.NotNull(AcdFile.Parse(repacked, CarId).Error);
    }

    [Theory]
    [InlineData("suspensions - 副本.ini")] // a real stray copy in a mod's data folder
    [InlineData("../car.ini")]
    [InlineData("")]
    public void Pack_rejects_names_that_are_not_plain_ascii_file_names(string name)
    {
        Assert.Throws<ArgumentException>(() => AcdFile.Pack([Entry(name, "x")], CarId));
    }

    [Fact]
    public void Pack_rejects_a_non_ascii_car_id()
    {
        Assert.Throws<ArgumentException>(() => AcdFile.Pack(Files, "café"));
    }

    [Fact]
    public void A_wrong_car_id_is_reported_not_returned()
    {
        var acd = AcdFile.Parse(AcdFile.Pack(Files, CarId), "acm_tutorial_basic2");

        Assert.Contains("doesn't decrypt to text", acd.Error);
        Assert.Empty(acd.Entries);
    }

    [Fact]
    public void A_cut_short_file_never_throws_and_is_never_half_read()
    {
        var bytes = WithHeader(AcdFile.Pack(Files, CarId));

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
        var bytes = AcdFile.Pack(Files, CarId);
        bytes[4 + "car.ini".Length + 4 + 1] = 1; // first padding byte of car.ini's data

        Assert.Contains("non-zero padding", AcdFile.Parse(bytes, CarId).Error);
    }

    [Theory]
    [InlineData("../car.ini")]
    [InlineData("data\\car.ini")]
    [InlineData("..")]
    [InlineData("car\tini")]
    [InlineData("café.ini")]
    public void Names_that_are_not_plain_file_names_are_reported(string name)
    {
        // Pack refuses such names, so pack a placeholder of the same length and patch the name bytes in.
        var bytes = AcdFile.Pack([Entry(new string('a', name.Length), "x")], CarId);
        Encoding.Latin1.GetBytes(name).CopyTo(bytes, 4);

        Assert.Contains("isn't a plain ASCII file name", AcdFile.Parse(bytes, CarId).Error);
    }

    [Fact]
    public void A_name_with_a_space_is_allowed_as_in_real_mods()
    {
        AssertSameEntries([Entry("tyres - Copy.ini", "x")], AcdFile.Parse(AcdFile.Pack([Entry("tyres - Copy.ini", "x")], CarId), CarId));
    }

    [Fact]
    public void A_duplicate_name_is_reported_ignoring_case()
    {
        var bytes = AcdFile.Pack([Entry("car.ini", "a"), Entry("CAR.ini", "b")], CarId);

        Assert.Contains("appears twice", AcdFile.Parse(bytes, CarId).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_file_with_no_entries_is_reported(bool header)
    {
        var bytes = AcdFile.Pack([], CarId);

        Assert.Equal("It contains no files.", AcdFile.Parse(header ? WithHeader(bytes) : bytes, CarId).Error);
    }

    [Fact]
    public void A_non_ascii_car_id_is_reported_not_thrown()
    {
        Assert.NotNull(AcdFile.Parse(AcdFile.Pack(Files, CarId), "café").Error);
    }
}
