using System.Globalization;
using System.Text.RegularExpressions;

namespace AcMod.Core.Learning;

public enum LogKind
{
    /// <summary>An error line not known to be harmless: look at it first.</summary>
    Problem,

    /// <summary>An error line seen in sessions that ended cleanly with the car working (BRITOD-DESK logs, 2026-11-03).</summary>
    KnownHarmless,

    /// <summary>A line about the driven car: creating it, its power, its collider.</summary>
    Car,
}

/// <summary>One highlighted log line (1-based line number) and, for known lines, why it matters or doesn't.</summary>
public sealed record LogLine(int Number, string Text, LogKind Kind, string? Note);

/// <summary>What the latest AC log says (LE-4): the driven car, its logged power, whether AC exited cleanly, and the highlighted lines.</summary>
public sealed record LogReport(DateTime WrittenUtc, string? CarId, double? PowerW, bool CleanExit, IReadOnlyList<LogLine> Lines);

/// <summary>
/// Reads AC's log.txt (LE-4). The patterns come from the logs read during M2 to M6 (docs/VERIFIED_FACTS.md); no others are guessed.
/// AC overwrites the log each time it starts, so read it right after quitting.
/// </summary>
public static partial class AcLog
{
    private static readonly (Regex Pattern, string Note)[] Harmless =
    [
        (new Regex(@"^ERROR: Variable type cannot be found"), "Seen 4 times in every clean session of the tutorial car; not about car data."),
        (new Regex(@"^ERROR: Python \[ERROR\] File apps/python/.+ not found"), "A Python app is switched on in AC's settings but isn't installed; not about the car."),
        (new Regex(@"^ERROR: NO STAT FOR "), "AC has no lap statistics yet for this car or track; seen in clean sessions."),
        (new Regex(@"^ERROR: InputDevice::initFF\(\)|^ERROR: DAMPER CREATION FAILED"), "Force feedback on the input device; not about the car."),
        (new Regex(@"^Audio error .*event:/collisions/track/"), "A missing track collision sound event; seen in a clean session."),
    ];

    /// <summary>The usual log location: Documents\Assetto Corsa\logs\log.txt.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assetto Corsa", "logs", "log.txt");

    public static LogReport Read(string path)
    {
        var lines = File.ReadAllLines(path);
        string? carId = null;
        double? power = null;
        var cleanExit = false;
        var highlighted = new List<LogLine>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (line == "CLEAN EXIT") cleanExit = true;

            if (CreatingCar().Match(line) is { Success: true } created)
            {
                carId ??= created.Groups[1].Value; // the first car created is the player's
                highlighted.Add(new LogLine(i + 1, line, LogKind.Car, null));
            }
            else if (PowerLine().Match(line) is { Success: true } pw && pw.Groups[1].Value == carId)
            {
                power ??= double.Parse(pw.Groups[2].Value, CultureInfo.InvariantCulture);
                highlighted.Add(new LogLine(i + 1, line, LogKind.Car, "The power AC computed from the car's data (docs/VERIFIED_FACTS.md: the app's estimate matched it within 0.1%)."));
            }
            else if (Harmless.FirstOrDefault(h => h.Pattern.IsMatch(line)) is { Note: not null } known)
            {
                highlighted.Add(new LogLine(i + 1, line, LogKind.KnownHarmless, known.Note));
            }
            else if (!SettingEcho().IsMatch(line) && // such as "[ASSETTO_CORSA] WARNINGS_AS_ERRORS: 0", a setting, not an error
                     (line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("exception", StringComparison.OrdinalIgnoreCase)))
            {
                highlighted.Add(new LogLine(i + 1, line, LogKind.Problem, null));
            }
            else if (carId != null && line.Contains($"content/cars/{carId}/", StringComparison.OrdinalIgnoreCase) && line.Contains("COLLIDER", StringComparison.Ordinal))
            {
                highlighted.Add(new LogLine(i + 1, line, LogKind.Car, null));
            }
        }

        return new LogReport(File.GetLastWriteTimeUtc(path), carId, power, cleanExit, highlighted);
    }

    [GeneratedRegex(@"^\[[A-Z_]+\] [A-Z_]+: ")]
    private static partial Regex SettingEcho();

    [GeneratedRegex(@"^Creating car: (\S+)")]
    private static partial Regex CreatingCar();

    // "POWER CLASS INDEX: 280.362976 (acm_tutorial_basic) PW:188460.000000 (252.728622 HP) W:672.200012"
    [GeneratedRegex(@"^POWER CLASS INDEX: [\d.]+ \((\S+)\) PW:([\d.]+)")]
    private static partial Regex PowerLine();
}
