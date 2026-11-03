using System.Text;

namespace AcMod.Core.Text;

/// <summary>How the bytes of a text file were decoded, so they can be encoded back unchanged.</summary>
public enum TextEncodingKind
{
    /// <summary>Valid UTF-8, which includes plain ASCII. Every file in the M1 survey was this.</summary>
    Utf8,

    /// <summary>
    /// Not valid UTF-8. Each byte becomes exactly one char (Latin-1), so the bytes still round-trip.
    /// Only ASCII edits are safe in such a file.
    /// </summary>
    Latin1,
}

/// <summary>One line of text and the exact line break that ended it.</summary>
/// <param name="Content">The line without its line break.</param>
/// <param name="Ending">"\r\n", "\n", "\r", or "" for a last line that has no line break.</param>
public sealed record TextLine(string Content, string Ending);

/// <summary>
/// A text file split into lines, keeping the BOM, the encoding, and every line's own line break,
/// so that an unchanged file is written back byte-for-byte (QS-1).
/// </summary>
public sealed class TextDocument
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    // throwOnInvalidBytes makes invalid UTF-8 fail loudly instead of being replaced with '?' (which would lose bytes).
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public TextDocument(IReadOnlyList<TextLine> lines, TextEncodingKind encodingKind, bool hasBom)
    {
        Lines = lines;
        EncodingKind = encodingKind;
        HasBom = hasBom;
    }

    public IReadOnlyList<TextLine> Lines { get; }

    public TextEncodingKind EncodingKind { get; }

    /// <summary>True when the file started with the UTF-8 byte order mark EF BB BF.</summary>
    public bool HasBom { get; }

    /// <summary>Decodes any bytes. It never fails: bytes that aren't valid UTF-8 fall back to Latin-1.</summary>
    public static TextDocument Parse(ReadOnlySpan<byte> bytes)
    {
        var hasBom = bytes.StartsWith(Utf8Bom);
        var body = hasBom ? bytes[Utf8Bom.Length..] : bytes;

        try
        {
            return new TextDocument(SplitLines(StrictUtf8.GetString(body)), TextEncodingKind.Utf8, hasBom);
        }
        catch (DecoderFallbackException)
        {
            // Decode the whole file, BOM bytes included, so nothing is lost.
            return new TextDocument(SplitLines(Encoding.Latin1.GetString(bytes)), TextEncodingKind.Latin1, hasBom: false);
        }
    }

    /// <summary>The whole file as one string, line breaks included (without the BOM).</summary>
    public string Text => string.Concat(Lines.Select(line => line.Content + line.Ending));

    /// <summary>Returns a copy with one line's content replaced. Its line break and every other line stay as they were.</summary>
    public TextDocument WithLineContent(int index, string content)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Lines.Count);
        EnsureEncodable(content);

        var lines = Lines.ToArray();
        lines[index] = lines[index] with { Content = content };
        return new TextDocument(lines, EncodingKind, HasBom);
    }

    /// <summary>
    /// Returns a copy with new lines inserted before line <paramref name="index"/> (Lines.Count appends). Each new line ends with the line break
    /// of the nearest line above that has one (or "\r\n" in a file without any), so the file keeps its style. Every other line stays as it was,
    /// except that a last line without a line break gets one when lines are appended after it, and the appended block then ends without one.
    /// </summary>
    public TextDocument WithLinesInserted(int index, IReadOnlyList<string> contents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Lines.Count);
        foreach (var content in contents) EnsureEncodable(content);
        if (contents.Count == 0) return this;

        var lines = Lines.ToList();
        var ending = lines.Take(index).LastOrDefault(l => l.Ending != "")?.Ending ?? lines.FirstOrDefault(l => l.Ending != "")?.Ending ?? "\r\n";
        var added = contents.Select(c => new TextLine(c, ending)).ToList();
        if (index == lines.Count && index > 0 && lines[index - 1].Ending == "")
        {
            lines[index - 1] = lines[index - 1] with { Ending = ending };
            added[^1] = added[^1] with { Ending = "" };
        }

        lines.InsertRange(index, added);
        return new TextDocument(lines, EncodingKind, HasBom);
    }

    /// <summary>Returns a copy without lines <paramref name="index"/> to <paramref name="index"/> + <paramref name="count"/> - 1. Every other line stays as it was.</summary>
    public TextDocument WithLinesRemoved(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, Lines.Count);

        var lines = Lines.ToList();
        lines.RemoveRange(index, count);
        return new TextDocument(lines, EncodingKind, HasBom);
    }

    /// <summary>Returns a copy holding new text, keeping the encoding and BOM. Used by formats whose values can span lines.</summary>
    public TextDocument WithText(string text)
    {
        EnsureEncodable(text);
        return new TextDocument(SplitLines(text), EncodingKind, HasBom);
    }

    public byte[] ToBytes()
    {
        var body = EncodingKind == TextEncodingKind.Utf8 ? StrictUtf8.GetBytes(Text) : Encoding.Latin1.GetBytes(Text);
        return HasBom ? [.. Utf8Bom, .. body] : body;
    }

    // Latin-1 can only hold chars up to U+00FF. Anything above would be written as '?', silently losing data.
    private void EnsureEncodable(string text)
    {
        if (EncodingKind == TextEncodingKind.Latin1 && text.Any(c => c > 'ÿ'))
        {
            throw new ArgumentException("This file is not UTF-8, so it can only hold characters up to U+00FF.", nameof(text));
        }
    }

    private static List<TextLine> SplitLines(string text)
    {
        var lines = new List<TextLine>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c != '\r' && c != '\n')
            {
                i++;
                continue;
            }

            var endingLength = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            lines.Add(new TextLine(text[start..i], text.Substring(i, endingLength)));
            i += endingLength;
            start = i;
        }

        if (start < text.Length)
        {
            lines.Add(new TextLine(text[start..], ""));
        }

        return lines;
    }
}
