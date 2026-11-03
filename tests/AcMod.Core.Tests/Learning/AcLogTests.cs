using AcMod.Core.Learning;

namespace AcMod.Core.Tests.Learning;

public sealed class AcLogTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_aclog_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    // Lines as they appear in real logs read in M2 to M6.
    private const string Clean =
        "Installing CrashGuard Fault Handler\r\n" +
        "[ASSETTO_CORSA] WARNINGS_AS_ERRORS: 0\r\n" + // a setting echo, not an error
        "ERROR: Variable type cannot be found\r\n" +
        "Creating car: acm_tutorial_basic []\r\n" +
        "Data path: content/cars/acm_tutorial_basic/data/\r\n" +
        "POWER CLASS INDEX: 280.362976 (acm_tutorial_basic) PW:188460.000000 (252.728622 HP) W:672.200012\r\n" +
        "LOADING COLLIDER MESH FILE: content/cars/acm_tutorial_basic/collider.kn5\r\n" +
        "POWER CLASS INDEX\r\n" +
        "ERROR: InputDevice::initFF(), lpDirectInputDevice->CreateEffect failed GUID_ConstantForce\r\n" +
        "ERROR: Python [ERROR] File apps/python/acnotify/acnotify.py not found\r\n" +
        "ERROR: NO STAT FOR acm_tutorial_basic\r\n" +
        "Creating car: some_ai_car []\r\n" +
        "CLEAN EXIT\r\n";

    private string Log(string text)
    {
        var path = Path.Combine(_temp, "log.txt");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_clean_session_names_the_car_and_its_power_and_has_no_problems()
    {
        var report = AcLog.Read(Log(Clean));

        Assert.Equal(("acm_tutorial_basic", 188460.0, true), (report.CarId, report.PowerW, report.CleanExit));
        Assert.DoesNotContain(report.Lines, l => l.Kind == LogKind.Problem);
        Assert.Equal(4, report.Lines.Count(l => l.Kind == LogKind.KnownHarmless));
        Assert.Equal([4, 6, 7, 12], report.Lines.Where(l => l.Kind == LogKind.Car).Select(l => l.Number));
    }

    [Fact]
    public void Unknown_errors_are_problems_and_a_missing_clean_exit_is_reported()
    {
        var crashed = Clean.Replace("CLEAN EXIT\r\n", "") + "ERROR: cannot load content/cars/acm_tutorial_basic/data/tyres.ini\r\nUnhandled exception at 0x0000\r\n";

        var report = AcLog.Read(Log(crashed));

        Assert.False(report.CleanExit);
        Assert.Equal(["ERROR: cannot load content/cars/acm_tutorial_basic/data/tyres.ini", "Unhandled exception at 0x0000"],
            report.Lines.Where(l => l.Kind == LogKind.Problem).Select(l => l.Text));
    }

    [Fact]
    public void Another_cars_power_line_is_not_taken_for_the_players()
    {
        var report = AcLog.Read(Log("Creating car: mine []\r\nPOWER CLASS INDEX: 1 (other) PW:5.0 (1 HP) W:1\r\n"));

        Assert.Equal("mine", report.CarId);
        Assert.Null(report.PowerW);
    }
}
