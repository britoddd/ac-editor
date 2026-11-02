using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace AcMod.Core.Acd;

/// <summary>One file inside a data.acd, already decrypted. <see cref="Name"/> is a plain file name such as "car.ini".</summary>
public sealed record AcdEntry(string Name, byte[] Data);

/// <summary>
/// A decrypted data.acd (layout and cipher: docs/ACD.md). A file that isn't a readable, standard data.acd
/// gets an <see cref="Error"/> and no entries, so the app can show it read-only instead of crashing (V-13).
/// </summary>
public sealed class AcdFile
{
    private const int HeaderMarker = -1111;
    private const string PathChars = "/\\:*?\"<>|";

    private AcdFile(IReadOnlyList<AcdEntry> entries, string? error)
    {
        Entries = entries;
        Error = error;
    }

    public IReadOnlyList<AcdEntry> Entries { get; }

    /// <summary>Why the file can't be read, or null when it was read.</summary>
    public string? Error { get; }

    /// <summary>
    /// Never throws. <paramref name="carId"/> is the key source name: the name of the car folder the file sits in.
    /// Every entry must decrypt to valid UTF-8 with no control characters except tab, CR and LF,
    /// as in all 1,839 real data files checked (docs/VERIFIED_FACTS.md). A wrong key fails that check.
    /// </summary>
    public static AcdFile Parse(ReadOnlySpan<byte> bytes, string carId)
    {
        byte[] key;
        try
        {
            key = Encoding.ASCII.GetBytes(AcdKey.FromName(carId));
        }
        catch (ArgumentException e)
        {
            return Fail(e.Message);
        }

        var entries = new List<AcdEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pos = bytes.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(bytes) == HeaderMarker ? 8 : 0;

        while (pos < bytes.Length)
        {
            var where = $"Entry {entries.Count + 1}";
            if (!TryReadInt32(bytes, ref pos, out var nameLength) || nameLength is < 1 or > 255 || nameLength > bytes.Length - pos)
                return Fail($"{where} has a bad name length.");

            // Latin-1 maps each byte to one char, so a non-ASCII byte stays visible to IsPlainFileName.
            var name = Encoding.Latin1.GetString(bytes.Slice(pos, nameLength));
            pos += nameLength;
            if (!IsPlainFileName(name))
                return Fail($"{where} has a name that isn't a plain ASCII file name.");
            if (!names.Add(name))
                return Fail($"\"{name}\" appears twice.");

            if (!TryReadInt32(bytes, ref pos, out var length) || length < 0 || length > (bytes.Length - pos) / 4)
                return Fail($"\"{name}\" has a bad data length.");

            var data = new byte[length];
            for (var i = 0; i < length; i++, pos += 4)
            {
                // Non-zero padding was never seen in a real file. Reject it rather than drop bytes we don't understand.
                if (bytes[pos + 1] != 0 || bytes[pos + 2] != 0 || bytes[pos + 3] != 0)
                    return Fail($"\"{name}\" has non-zero padding, so it isn't a standard data.acd.");
                data[i] = (byte)(bytes[pos] - key[i % key.Length]);
            }

            // ponytail: a wrong key shifts each byte by -12..+12, so it is caught mainly through line breaks and spaces turning
            // into control bytes. A tiny one-line entry can pass, but a real data.acd fails on its first file
            // (4,000 wrong IDs tried in AcdPyComparisonTests). Add a content check if a real false pass ever shows up.
            if (!Utf8.IsValid(data) || data.Any(b => (b < 0x20 && b is not (9 or 10 or 13)) || b == 0x7f))
                return Fail($"\"{name}\" doesn't decrypt to text. The car folder name may not match the one it was packed for, or it isn't a standard data.acd.");

            entries.Add(new AcdEntry(name, data));
        }

        return entries.Count == 0 ? Fail("It contains no files.") : new AcdFile(entries, null);
    }

    /// <summary>
    /// Packs entries into data.acd bytes for <paramref name="carId"/>, the name of the car folder it will sit in.
    /// No header, zero padding, and entries in ordinal (case-sensitive) name order, as acd.py writes (docs/ACD.md).
    /// Throws on a non-ASCII car ID or entry name.
    /// </summary>
    public static byte[] Pack(IEnumerable<AcdEntry> entries, string carId)
    {
        var key = Encoding.ASCII.GetBytes(AcdKey.FromName(carId));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream); // always little-endian
        foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            if (!IsPlainFileName(entry.Name))
                throw new ArgumentException($"\"{entry.Name}\" isn't a plain ASCII file name.", nameof(entries));

            writer.Write(entry.Name.Length);
            writer.Write(Encoding.ASCII.GetBytes(entry.Name));
            writer.Write(entry.Data.Length);
            for (var i = 0; i < entry.Data.Length; i++)
            {
                // An int holding 0-255 is written as that byte followed by three zero padding bytes.
                writer.Write((int)(byte)(entry.Data[i] + key[i % key.Length]));
            }
        }

        return stream.ToArray();
    }

    /// <summary>Printable ASCII (spaces allowed, as in real mods), no path characters, and not "." or "..".</summary>
    private static bool IsPlainFileName(string name) =>
        name.Length is >= 1 and <= 255
        && name.All(c => c is >= ' ' and < '\x7f' && !PathChars.Contains(c))
        && name is not ("." or "..");

    private static AcdFile Fail(string error) => new([], error);

    private static bool TryReadInt32(ReadOnlySpan<byte> bytes, ref int pos, out int value)
    {
        var ok = BinaryPrimitives.TryReadInt32LittleEndian(bytes[pos..], out value);
        pos += 4;
        return ok;
    }
}
