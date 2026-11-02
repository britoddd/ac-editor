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

            var nameBytes = bytes.Slice(pos, nameLength);
            pos += nameLength;
            var name = Encoding.ASCII.GetString(nameBytes);
            if (!Ascii.IsValid(nameBytes) || name.Any(c => c < ' ' || c == '\x7f' || PathChars.Contains(c)) || name is "." or "..")
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

            if (!Utf8.IsValid(data) || data.Any(b => (b < 0x20 && b is not (9 or 10 or 13)) || b == 0x7f))
                return Fail($"\"{name}\" doesn't decrypt to text. The car folder name may not match the one it was packed for, or it isn't a standard data.acd.");

            entries.Add(new AcdEntry(name, data));
        }

        return entries.Count == 0 ? Fail("It contains no files.") : new AcdFile(entries, null);
    }

    private static AcdFile Fail(string error) => new([], error);

    private static bool TryReadInt32(ReadOnlySpan<byte> bytes, ref int pos, out int value)
    {
        var ok = BinaryPrimitives.TryReadInt32LittleEndian(bytes[pos..], out value);
        pos += 4;
        return ok;
    }
}
