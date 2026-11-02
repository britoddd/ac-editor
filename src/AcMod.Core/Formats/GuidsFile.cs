using System.Text.RegularExpressions;
using AcMod.Core.Text;

namespace AcMod.Core.Formats;

/// <summary>One "{guid} kind:/path" line, for example "{2738...} bank:/my_car" or "{111d...} event:/cars/my_car/engine_ext".</summary>
/// <param name="LineIndex">Index of the line in <see cref="GuidsFile.Document"/>.</param>
/// <param name="Kind">"bank", "event", "bus", "vca" or "snapshot".</param>
public sealed record GuidEntry(int LineIndex, string Guid, string Kind, string Path);

/// <summary>
/// A lossless sfx/GUIDs.txt, which maps FMOD sound GUIDs to names (QS-1). Its main edit is changing the car ID,
/// the groundwork for WS-7 "Save as new car". It rewrites only the ID part of that car's own bank and event lines (QS-2).
/// </summary>
public sealed partial class GuidsFile
{
    private GuidsFile(TextDocument document)
    {
        Document = document;
        Entries = document.Lines
            .Select((line, i) => (Match: LinePattern().Match(line.Content), Index: i))
            .Where(x => x.Match.Success)
            .Select(x => new GuidEntry(x.Index, x.Match.Groups["guid"].Value, x.Match.Groups["kind"].Value, x.Match.Groups["path"].Value))
            .ToList();
    }

    public TextDocument Document { get; }

    public IReadOnlyList<GuidEntry> Entries { get; }

    /// <summary>Never fails. A line that isn't "{guid} kind:/path" is kept and ignored.</summary>
    public static GuidsFile Parse(ReadOnlySpan<byte> bytes) => new(TextDocument.Parse(bytes));

    public byte[] ToBytes() => Document.ToBytes();

    /// <summary>
    /// Renames the car in "bank:/{oldId}" and "event:/cars/{oldId}/..." lines. The old ID is matched ignoring case,
    /// as AC matches folder names. Lines for other cars stay untouched, because some mods list other cars' sounds too.
    /// </summary>
    public GuidsFile WithCarId(string oldId, string newId)
    {
        if (newId.Length == 0 || newId.Any(c => c == '/' || char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new ArgumentException("A car ID cannot be empty or contain '/', spaces, or control characters.", nameof(newId));
        }

        var document = Document;
        var changed = 0;
        foreach (var entry in Entries)
        {
            var idStart = entry.Kind switch
            {
                "bank" when entry.Path.Equals(oldId, StringComparison.OrdinalIgnoreCase) => 0,
                "event" when entry.Path.StartsWith("cars/" + oldId + "/", StringComparison.OrdinalIgnoreCase) => "cars/".Length,
                _ => -1,
            };
            if (idStart < 0)
            {
                continue;
            }

            var line = document.Lines[entry.LineIndex].Content;
            var pathStart = line.Length - entry.Path.Length; // the path runs to the end of the line
            var at = pathStart + idStart;
            document = document.WithLineContent(entry.LineIndex, line[..at] + newId + line[(at + oldId.Length)..]);
            changed++;
        }

        if (changed == 0)
        {
            throw new ArgumentException($"No bank or event lines for car '{oldId}'.", nameof(oldId));
        }

        return new GuidsFile(document);
    }

    [GeneratedRegex(@"^\{(?<guid>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\} (?<kind>[a-z]+):/(?<path>.*)$")]
    private static partial Regex LinePattern();
}
