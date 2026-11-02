using System.Text;
using AcMod.Core.Text;

namespace AcMod.Core.Tests.Text;

public class TextDocumentTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("a", new[] { "a|" })]
    [InlineData("a\n", new[] { "a|\n" })]
    [InlineData("a\r\n", new[] { "a|\r\n" })]
    [InlineData("a\r", new[] { "a|\r" })]
    [InlineData("a\r\nb\nc\rd", new[] { "a|\r\n", "b|\n", "c|\r", "d|" })]
    [InlineData("\n\r\n\r", new[] { "|\n", "|\r\n", "|\r" })]
    [InlineData("x\r\n\r\n", new[] { "x|\r\n", "|\r\n" })]
    public void Keeps_each_line_and_its_own_line_break(string text, string[] expected)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        var doc = TextDocument.Parse(bytes);

        Assert.Equal(expected, doc.Lines.Select(l => l.Content + "|" + l.Ending));
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void Keeps_a_utf8_bom()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n'];

        var doc = TextDocument.Parse(bytes);

        Assert.True(doc.HasBom);
        Assert.Equal(TextEncodingKind.Utf8, doc.EncodingKind);
        Assert.Equal("a", doc.Lines.Single().Content);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void Decodes_non_ascii_utf8()
    {
        var bytes = Encoding.UTF8.GetBytes("NAME=Café");

        var doc = TextDocument.Parse(bytes);

        Assert.Equal(TextEncodingKind.Utf8, doc.EncodingKind);
        Assert.Equal("NAME=Café", doc.Lines.Single().Content);
    }

    [Fact]
    public void Falls_back_to_latin1_for_invalid_utf8_without_losing_bytes()
    {
        byte[] bytes = [(byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)'\r', (byte)'\n']; // é in Latin-1 is not valid UTF-8

        var doc = TextDocument.Parse(bytes);

        Assert.Equal(TextEncodingKind.Latin1, doc.EncodingKind);
        Assert.Equal("Café", doc.Lines.Single().Content);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void Bom_followed_by_invalid_utf8_still_round_trips()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, 0xE9];

        var doc = TextDocument.Parse(bytes);

        Assert.Equal(TextEncodingKind.Latin1, doc.EncodingKind);
        Assert.False(doc.HasBom);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void Refuses_text_a_latin1_file_cannot_hold_instead_of_writing_question_marks()
    {
        var latin1 = TextDocument.Parse([(byte)'C', 0xE9]);
        var utf8 = TextDocument.Parse("Ca"u8);

        Assert.Throws<ArgumentException>(() => latin1.WithLineContent(0, "€5"));
        Assert.Throws<ArgumentException>(() => latin1.WithText("€5"));
        Assert.Equal("€5", TextDocument.Parse(utf8.WithText("€5").ToBytes()).Text);
    }

    // QS-1 on real data: every synthetic text fixture, and every text file in fixtures/local when it exists.
    [Fact]
    public void Every_fixture_text_file_round_trips_byte_for_byte()
    {
        string[] extensions = [".ini", ".lut", ".rto", ".json", ".txt"];
        var roots = new[] { Fixtures.Synthetic("text"), Fixtures.LocalRoot }.Where(Directory.Exists);
        var files = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        var failures = files.Where(f =>
        {
            var bytes = File.ReadAllBytes(f);
            return !bytes.AsSpan().SequenceEqual(TextDocument.Parse(bytes).ToBytes());
        }).ToList();

        Assert.NotEmpty(files);
        Assert.Empty(failures);
    }
}
