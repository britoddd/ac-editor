// LutReader: read an Assetto Corsa style power.lut and report peak values.
// Completed version: all five steps are filled in.
//   dotnet run           runs the program
//   dotnet run -- test   runs the self-checks

using System.Globalization;

record Point(double Rpm, double Torque); // Torque is in Nm

static class Lut
{
    /// Step 1: turn one line such as "5000|320" into a Point.
    /// Throw FormatException if it is not two numbers split by '|'.
    /// Always parse with CultureInfo.InvariantCulture. An Indonesian Windows
    /// setup treats ',' as the decimal mark, so plain double.Parse can misread "0.5".
    /// Hints: line.Split('|'), Trim(), double.Parse(text, CultureInfo.InvariantCulture)
    public static Point ParseLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 2)
        {
            throw new FormatException($"expected rpm|torque but got '{line.Trim()}'");
        }

        var rpm = double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
        var torque = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
        return new Point(rpm, torque);
    }

    /// Step 2: parse the whole file. Skip empty lines.
    /// On a bad line, throw FormatException with "line N" in the message, N starting at 1.
    /// Hints: text.Split('\n'), a for loop, List<Point>, try/catch, string.IsNullOrWhiteSpace
    public static List<Point> ReadCurve(string text)
    {
        var curve = new List<Point>();
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            try
            {
                curve.Add(ParseLine(lines[i]));
            }
            catch (FormatException error)
            {
                throw new FormatException($"line {i + 1}: {error.Message}");
            }
        }

        return curve;
    }

    /// Already done for you. 9549 converts rpm x Nm into kW.
    public static double PowerKw(Point point) => point.Rpm * point.Torque / 9549.0;

    /// Step 3: return the point with the highest torque, or null for an empty curve.
    /// Write this with a foreach loop first.
    public static Point? PeakTorque(List<Point> curve)
    {
        Point? best = null;

        foreach (var point in curve)
        {
            if (best is null || point.Torque > best.Torque)
            {
                best = point;
            }
        }

        return best;
    }

    /// Step 4: return the point with the highest PowerKw, or null for an empty curve.
    public static Point? PeakPower(List<Point> curve)
    {
        Point? best = null;

        foreach (var point in curve)
        {
            if (best is null || PowerKw(point) > PowerKw(best))
            {
                best = point;
            }
        }

        return best;
    }
}

static class Program
{
    static int Main(string[] args)
    {
        // Keeps '.' as the decimal mark in the printed output.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        if (args.Length > 0 && args[0] == "test")
        {
            return SelfTest.Run();
        }

        List<Point> curve;
        try
        {
            curve = Lut.ReadCurve(File.ReadAllText("sample.lut"));
        }
        catch (FormatException error)
        {
            Console.Error.WriteLine($"Bad file: {error.Message}");
            return 1;
        }

        // Step 5: use PeakTorque and PeakPower to print these two lines:
        // Peak torque: 320 Nm at 5000 rpm
        // Peak power: 205.3 kW (275.3 hp) at 7000 rpm
        // Hints: $"{value:F1}" gives one decimal. 1 kW = 1.34102 hp.
        var torquePeak = Lut.PeakTorque(curve);
        var powerPeak = Lut.PeakPower(curve);

        if (torquePeak is null || powerPeak is null)
        {
            Console.Error.WriteLine("Bad file: no data points");
            return 1;
        }

        var kw = Lut.PowerKw(powerPeak);
        var hp = kw * 1.34102;

        Console.WriteLine($"Peak torque: {torquePeak.Torque} Nm at {torquePeak.Rpm} rpm");
        Console.WriteLine($"Peak power: {kw:F1} kW ({hp:F1} hp) at {powerPeak.Rpm} rpm");
        return 0;
    }
}

static class SelfTest
{
    const string Sample = "0|100\n1000|150\n\n5000|320\n7000|280\n";

    public static int Run()
    {
        var checks = new (string Name, Func<bool> Check)[]
        {
            ("parses a good line", () => Lut.ParseLine(" 5000 | 320 ") == new Point(5000, 320)),
            ("parses decimals on Indonesian settings", () =>
            {
                var previous = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                try { return Lut.ParseLine("4500.5|0.25") == new Point(4500.5, 0.25); }
                finally { CultureInfo.CurrentCulture = previous; }
            }),
            ("rejects a line without |", () => Throws(() => Lut.ParseLine("5000"), "")),
            ("rejects text instead of a number", () => Throws(() => Lut.ParseLine("abc|320"), "")),
            ("skips empty lines", () => Lut.ReadCurve(Sample).Count == 4),
            ("handles Windows line endings", () => Lut.ReadCurve("0|100\r\n5000|320\r\n").Count == 2),
            ("reports the bad line number", () => Throws(() => Lut.ReadCurve("0|100\n\nbroken\n"), "line 3")),
            ("finds peak torque", () => Lut.PeakTorque(Lut.ReadCurve(Sample))?.Rpm == 5000),
            ("finds peak power", () => Lut.PeakPower(Lut.ReadCurve(Sample))?.Rpm == 7000),
            ("empty curve has no peak", () =>
                Lut.PeakTorque(new List<Point>()) is null && Lut.PeakPower(new List<Point>()) is null),
        };

        var passed = 0;
        foreach (var (name, check) in checks)
        {
            string result;
            try { result = check() ? "PASS" : "FAIL"; }
            catch (Exception error) { result = $"FAIL ({error.GetType().Name}: {error.Message})"; }
            if (result == "PASS") passed++;
            Console.WriteLine($"{result}  {name}");
        }

        Console.WriteLine($"{passed} of {checks.Length} passed");
        return passed == checks.Length ? 0 : 1;
    }

    static bool Throws(Action action, string expectedText)
    {
        try { action(); }
        catch (FormatException error) { return error.Message.Contains(expectedText); }
        return false;
    }
}
