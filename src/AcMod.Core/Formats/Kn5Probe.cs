using System.Text;
using System.Text.RegularExpressions;

namespace AcMod.Core.Formats;

/// <summary>What <see cref="Kn5Probe.Check"/> found at the start of a .kn5 file.</summary>
public enum Kn5Shape
{
    /// <summary>The header, texture table and first material parse in the standard layout.</summary>
    Standard,

    /// <summary>No "sc6969" header, so not a KN5 the probe knows.</summary>
    NotKn5,

    /// <summary>A valid "sc6969" header whose texture or material table doesn't parse in the standard layout.</summary>
    UnknownTables,
}

/// <summary>
/// A read-only look at the start of a KN5 model, for V-14 only. It reads the "sc6969" magic and version, the texture table (each texture's name
/// and size, seeking past the image data), and the first material's name and shader. Nothing is decoded or decrypted (hard rule 1).
/// Layout as noted for the M0 survey (scratch/kn5_survey.py, from the AcTools Kn5Reader layout, Ms-PL); no AcTools code is copied.
/// On BRITOD-DESK all 1,109 installed KN5 files start with sc6969, and 1,073 parse in this layout, including every Kunos file.
/// </summary>
public static partial class Kn5Probe
{
    public static Kn5Shape Check(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 10 || !reader.ReadBytes(6).SequenceEqual("sc6969"u8.ToArray())) return Kn5Shape.NotKn5;

        try
        {
            var version = reader.ReadInt32();
            if (version > 5) reader.ReadInt32(); // an extra header field in version 6

            var textures = reader.ReadInt32();
            if (textures is < 0 or > 10000) return Kn5Shape.UnknownTables;
            for (var i = 0; i < textures; i++)
            {
                reader.ReadInt32(); // texture type
                Name(reader);
                var size = reader.ReadInt32();
                if (size < 0 || size > stream.Length - stream.Position) return Kn5Shape.UnknownTables;
                stream.Seek(size, SeekOrigin.Current); // skip the image data
            }

            var materials = reader.ReadInt32();
            if (materials is < 0 or > 10000) return Kn5Shape.UnknownTables;
            if (materials > 0)
            {
                Name(reader); // material name
                if (!ShaderName().IsMatch(Name(reader))) return Kn5Shape.UnknownTables;
            }

            return Kn5Shape.Standard;
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException or DecoderFallbackException)
        {
            return Kn5Shape.UnknownTables;
        }
    }

    /// <summary>A length-prefixed UTF-8 string, at most 4096 bytes.</summary>
    private static string Name(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is < 0 or > 4096) throw new InvalidDataException("bad string length");
        return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(reader.ReadBytes(length));
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex ShaderName();
}
