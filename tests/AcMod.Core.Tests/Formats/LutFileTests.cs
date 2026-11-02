using System.Globalization;
using AcMod.Core.Formats;

namespace AcMod.Core.Tests.Formats;

public class LutFileTests
{
    private static LutFile Load(string name) => LutFile.Parse(File.ReadAllBytes(Fixtures.Synthetic("lut", name)));

    [Fact]
    public void Reads_plain_rows()
    {
        var lut = Load("plain_crlf.lut");

        Assert.Equal(["0", "1000", "2000"], lut.Rows.Select(r => r.Key));
        Assert.Equal([100, 150.5, 120], lut.Rows.Select(r => r.Value));
        Assert.Equal(1000, lut.Rows[1].KeyAsNumber);
    }

    [Fact]
    public void Reads_rto_labels_as_text_keys()
    {
        var rto = Load("labels.rto");

        Assert.Equal(["10//31", "1ST", "REVERSE"], rto.Rows.Select(r => r.Key));
        Assert.Equal([3.10, 1.00, -5.00], rto.Rows.Select(r => r.Value));
        Assert.Null(rto.Rows[0].KeyAsNumber);
    }

    [Fact]
    public void Only_key_pipe_number_lines_are_rows()
    {
        var lut = Load("comments_and_junk.lut");

        // ";a|5" is a comment, and "0/1.00", "[VALUES]", and "C=1.27" are not rows. All of them stay in the file.
        Assert.Equal(["0", "4000"], lut.Rows.Select(r => r.Key));
        Assert.Equal(-900, lut.Rows[1].Value);
        Assert.Equal(8, lut.Document.Lines.Count);
    }

    // QS-2: editing one value changes only that number's characters. FixtureRunnerTests repeats this for every fixture row.
    [Theory]
    [InlineData("spaces_around_pipe.rto", 1, 4.5, "3.900 | 3.9\r\n4.312\t|\t4.5\r\n")]
    [InlineData("labels.rto", 0, 2.85, "10//31|2.85\r\n1ST|1.00\nREVERSE|-5.00")]
    [InlineData("comments_and_junk.lut", 1, -850, ";made-up header\r\n\r\n0|100\r\n4000|-850     (engagement point)\r\n0/1.00\r\n[VALUES]\r\nC=1.27\r\n;a|5\r\n")]
    [InlineData("lf_no_final_eol.lut", 2, 0.125, "0|0.5\n4000|1.25\n8000|0.125")]
    public void Editing_a_value_changes_only_that_number(string name, int row, double value, string expected)
    {
        var edited = Load(name).WithValue(row, value);

        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(edited.ToBytes()));
        Assert.Equal(value, edited.Rows[row].Value);
    }

    // QS-11: Indonesian Windows uses ',' as the decimal mark. AC files must still read and write '.'.
    [Fact]
    public void Uses_invariant_numbers_under_indonesian_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("id-ID");
        try
        {
            var lut = Load("lf_no_final_eol.lut");
            var edited = lut.WithValue(0, 2.75);

            Assert.Equal(0.5, lut.Rows[0].Value);
            Assert.Equal("0|2.75", edited.Document.Lines[0].Content);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Rejects_values_ac_cannot_read()
    {
        var lut = Load("plain_crlf.lut");

        Assert.Throws<ArgumentException>(() => lut.WithValue(0, double.NaN));
        Assert.Throws<ArgumentException>(() => lut.WithValue(0, double.PositiveInfinity));
    }

    [Fact]
    public void Rejects_a_row_that_does_not_exist()
    {
        var lut = Load("plain_crlf.lut");

        Assert.Throws<ArgumentOutOfRangeException>(() => lut.WithValue(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => lut.WithValue(-1, 1));
    }
}
