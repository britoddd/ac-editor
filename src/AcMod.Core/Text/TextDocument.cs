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

    public byte[] ToBytes()
    {
        var text = string.Concat(Lines.Select(line => line.Content + line.Ending));
        var body = EncodingKind == TextEncodingKind.Utf8 ? StrictUtf8.GetBytes(text) : Encoding.Latin1.GetBytes(text);
        return HasBom ? [.. Utf8Bom, .. body] : body;
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
