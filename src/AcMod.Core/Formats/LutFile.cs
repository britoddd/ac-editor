using System.Globalization;
using System.Text.RegularExpressions;
using AcMod.Core.Text;

namespace AcMod.Core.Formats;

/// <summary>
/// One "key|value" row. In a .lut file the key is a number, such as rpm in power.lut.
/// In an .rto file it is a label, such as "10//31" or "REVERSE".
/// </summary>
/// <param name="LineIndex">Index of the row's line in <see cref="LutFile.Document"/>.</param>
public sealed record LutRow(int LineIndex, string Key, double Value)
{
    /// <summary>The key read as an invariant-culture number, or null when it is a label.</summary>
    public double? KeyAsNumber =>
        double.TryParse(Key, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}

/// <summary>
/// A lossless .lut or .rto file. Every line is kept exactly as written. Lines of the form
/// "key|number" are exposed as <see cref="Rows"/>. Anything else, such as comments, blank lines, or text
/// that isn't a row, stays in the file untouched (QS-1). Editing a row changes only that number (QS-2).
/// </summary>
public sealed partial class LutFile
{
    private LutFile(TextDocument document)
    {
        Document = document;
        Rows = FindRows(document);
    }

    public TextDocument Document { get; }

    public IReadOnlyList<LutRow> Rows { get; }

    /// <summary>Never fails. A line that doesn't look like a row is simply not in <see cref="Rows"/>.</summary>
    public static LutFile Parse(ReadOnlySpan<byte> bytes) => new(TextDocument.Parse(bytes));

    public byte[] ToBytes() => Document.ToBytes();

    /// <summary>Returns a copy where only the value of row <paramref name="rowIndex"/> is replaced.</summary>
    public LutFile WithValue(int rowIndex, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, Rows.Count);
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("AC files cannot hold NaN or infinity.", nameof(value));
        }

        var row = Rows[rowIndex];
        var line = Document.Lines[row.LineIndex].Content;
        var number = RowPattern().Match(line).Groups["value"];

        // ponytail: shortest round-trip format ("3.2", not "3.20"); keep the original decimals if diffs get noisy.
        var text = value.ToString(CultureInfo.InvariantCulture);
        var edited = line[..number.Index] + text + line[(number.Index + number.Length)..];
        return new LutFile(Document.WithLineContent(row.LineIndex, edited));
    }

    private static List<LutRow> FindRows(TextDocument document)
    {
        var rows = new List<LutRow>();
        for (var i = 0; i < document.Lines.Count; i++)
        {
            var match = RowPattern().Match(document.Lines[i].Content);
            if (match.Success)
            {
                var value = double.Parse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                rows.Add(new LutRow(i, match.Groups["key"].Value, value));
            }
        }

        return rows;
    }

    // Optional spaces or tabs, a key that isn't a ';' comment and has no '|', a '|', then a number.
    // Anything after the number (spaces, notes) is allowed and kept. Built from the M1 step 4 survey of 1,058 real files.
    [GeneratedRegex(@"^[ \t]*(?<key>(?!;)[^|\s][^|]*?)[ \t]*\|[ \t]*(?<value>[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)(?![\d.])")]
    private static partial Regex RowPattern();
}
