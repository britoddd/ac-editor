using System.Text.RegularExpressions;
using AcMod.Core.Text;

namespace AcMod.Core.Formats;

/// <summary>A "[NAME]" line.</summary>
/// <param name="LineIndex">Index of the line in <see cref="IniFile.Document"/>.</param>
public sealed record IniSection(int LineIndex, string Name);

/// <summary>A "KEY=VALUE" line.</summary>
/// <param name="LineIndex">Index of the line in <see cref="IniFile.Document"/>.</param>
/// <param name="Section">Name of the section the entry is in, or "" for entries before the first section.</param>
/// <param name="Value">The text after '=', without surrounding spaces or a trailing ';' or '//' comment.</param>
public sealed record IniEntry(int LineIndex, string Section, string Key, string Value);

/// <summary>
/// A lossless AC .ini file. Every line is kept exactly as written (QS-1). Sections and entries are found
/// for reading, and comments, blank lines, junk text, duplicate sections and duplicate keys all stay in
/// the file. Based on the M1 step 5 survey of 855 real files.
/// </summary>
public sealed partial class IniFile
{
    private IniFile(TextDocument document)
    {
        Document = document;
        (Sections, Entries) = Read(document);
    }

    public TextDocument Document { get; }

    /// <summary>All sections in file order. A name can appear more than once.</summary>
    public IReadOnlyList<IniSection> Sections { get; }

    /// <summary>All entries in file order. A key can appear more than once in one section.</summary>
    public IReadOnlyList<IniEntry> Entries { get; }

    /// <summary>Never fails. A line that is neither a section nor an entry is kept and ignored.</summary>
    public static IniFile Parse(ReadOnlySpan<byte> bytes) => new(TextDocument.Parse(bytes));

    public byte[] ToBytes() => Document.ToBytes();

    /// <summary>
    /// All entries with this section and key, in file order. Names match exactly (ordinal).
    /// Several matches mean the file has duplicates. Which one AC uses is not verified, so the caller chooses.
    /// </summary>
    public IReadOnlyList<IniEntry> Find(string section, string key) =>
        Entries.Where(e => e.Section == section && e.Key == key).ToList();

    /// <summary>Returns a copy where only this entry's value text is replaced (QS-2).</summary>
    public IniFile WithValue(IniEntry entry, string value)
    {
        if (!Entries.Contains(entry))
        {
            throw new ArgumentException("The entry is not from this file.", nameof(entry));
        }

        if (value.Contains('\r') || value.Contains('\n') || value.Contains(';') || value.Contains("//") || value != value.Trim())
        {
            throw new ArgumentException(
                "A value cannot contain line breaks, ';', '//', or leading or trailing spaces, because it would read back differently.",
                nameof(value));
        }

        var line = Document.Lines[entry.LineIndex].Content;
        var match = EntryPattern().Match(line);
        var old = match.Groups["value"];

        // An empty value goes right after '=', so "LUT=\t\t; note" becomes "LUT=x\t\t; note", not "LUT=\t\tx; note".
        var start = old.Length > 0 ? old.Index : line.IndexOf('=', match.Groups["key"].Index + match.Groups["key"].Length) + 1;
        var edited = line[..start] + value + line[(start + old.Length)..];
        return new IniFile(Document.WithLineContent(entry.LineIndex, edited));
    }

    /// <summary>Writes a number the way AC expects: invariant culture, so always '.' as the decimal mark (QS-11).</summary>
    public IniFile WithValue(IniEntry entry, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("AC files cannot hold NaN or infinity.", nameof(value));
        }

        // ponytail: shortest round-trip format ("2.5", not "2.50"); keep the original decimals if diffs get noisy.
        return WithValue(entry, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static (List<IniSection>, List<IniEntry>) Read(TextDocument document)
    {
        var sections = new List<IniSection>();
        var entries = new List<IniEntry>();
        var current = "";
        for (var i = 0; i < document.Lines.Count; i++)
        {
            var line = document.Lines[i].Content;
            var section = SectionPattern().Match(line);
            if (section.Success)
            {
                current = section.Groups["name"].Value;
                sections.Add(new IniSection(i, current));
                continue;
            }

            var entry = EntryPattern().Match(line);
            if (entry.Success)
            {
                entries.Add(new IniEntry(i, current, entry.Groups["key"].Value, entry.Groups["value"].Value));
            }
        }

        return (sections, entries);
    }

    // "[NAME]" with optional leading spaces. Anything after ']' (spaces, a comment, junk) is allowed and kept.
    [GeneratedRegex(@"^[ \t]*\[(?<name>[^\]]*)\]")]
    private static partial Regex SectionPattern();

    // "KEY=VALUE". The key may not start with ';', '[' or '//', so comment lines are never entries.
    // The value stops before the spaces leading into a ';' or '//' comment. Real files write
    // "POSITION=0, 0, 0  /// (x,y,z) x-/right;  z+/forward", and AC's treatment of '//' is not verified.
    // Group "value" also marks where step 6 edits the text.
    [GeneratedRegex(@"^[ \t]*(?<key>(?![;\[]|//)[^=\s][^=]*?)[ \t]*=[ \t]*(?<value>.*?)[ \t]*(?:;.*|//.*)?$")]
    internal static partial Regex EntryPattern();
}
