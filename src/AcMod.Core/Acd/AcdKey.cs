using System.Globalization;
using System.Text;

namespace AcMod.Core.Acd;

/// <summary>
/// The data.acd key, written only from the public notes in docs/ACD.md ("Key"), never from acd.py or AcManager.Internal.
/// The key is an ASCII string of eight numbers such as "101-99-107-127-42-28-74-100".
/// </summary>
public static class AcdKey
{
    /// <summary>
    /// The name a file's key comes from: the parent folder's name if the file name starts with "data" (any case),
    /// otherwise the file's own name. So "acm_tutorial_basic\data.acd" uses "acm_tutorial_basic".
    /// </summary>
    public static string SourceName(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("data", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(Path.GetDirectoryName(path)) ?? ""
            : fileName;
    }

    public static string ForPath(string path) => FromName(SourceName(path));

    /// <summary>The key for a car ID or other key source name. Throws on an empty or non-ASCII name.</summary>
    public static string FromName(string name)
    {
        // ponytail: non-ASCII names are rejected, because tools disagree on their key and AC's behaviour is unknown (V-01).
        if (name.Length == 0 || !Ascii.IsValid(name))
            throw new ArgumentException($"An ACD key needs a non-empty ASCII name, got \"{name}\".", nameof(name));

        int[] b = name.ToLowerInvariant().Select(c => (int)c).ToArray();
        int a = b.Length;

        // Values 2 and 5 overflow int on long names. C# wraps silently by default, and wrapping
        // doesn't change the result mod 256. "/" truncates toward zero, as the notes require.
        int t1 = b.Sum();
        int t2 = 0;
        for (int i = 0; i < a - 1; i += 2) t2 = t2 * b[i] - b[i + 1];
        int t3 = 0;
        for (int i = 1; i < a - 3; i += 3) t3 = t3 * b[i] / (b[i + 1] + 27) - 27 - b[i - 1];
        int t4 = 5763;
        for (int i = 1; i < a; i++) t4 -= b[i];
        int t5 = 66;
        for (int i = 1; i < a - 4; i += 4) t5 = (b[i] + 15) * t5 * (b[i - 1] + 15) + 22;
        int t6 = 101;
        for (int i = 0; i < a - 2; i += 2) t6 -= b[i];
        int t7 = 171;
        for (int i = 0; i < a - 2; i += 2) t7 %= b[i];
        int t8 = 171;
        for (int i = 0; i < a - 1; i++) t8 = t8 / b[i] + b[i + 1];

        return string.Join('-', new[] { t1, t2, t3, t4, t5, t6, t7, t8 }
            .Select(t => ((t % 256 + 256) % 256).ToString(CultureInfo.InvariantCulture)));
    }
}
