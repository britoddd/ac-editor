using System.Globalization;
using System.IO;
using System.Windows;
using AcMod.Core.Learning;
using AcMod.Core.Tuning;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>The latest AC log (LE-4): the driven car, its logged power against the workspace car's estimate, a clean exit or not, and highlighted lines.</summary>
public partial class LogWindow : Window
{
    private readonly Workspace _workspace;

    public LogWindow(Workspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var path = AcLog.DefaultPath;
        if (!File.Exists(path))
        {
            Summary.Text = $"No AC log at {path}. Drive once, quit AC, then read again.";
            return;
        }

        var report = AcLog.Read(path);
        var summary = $"{path}, written {report.WrittenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.  " +
                      (report.CleanExit ? "AC exited cleanly." : "No CLEAN EXIT line: AC may have crashed, or is still running.") +
                      $"\nCar: {report.CarId ?? "(none)"}" +
                      (report.PowerW is { } pw ? string.Create(CultureInfo.InvariantCulture, $", logged power {pw:0} W") : "");
        if (report.CarId != null && report.PowerW is { } logged && _workspace.OpenCar(report.CarId).Edit is { ReadOnly: false } edit &&
            PowerEstimate.FromData(edit.Data.Files).Estimate is { } estimate)
        {
            summary += string.Create(CultureInfo.InvariantCulture,
                $"; the workspace car's estimate is {estimate.PeakPowerW:0} W ({(estimate.PeakPowerW - logged) / logged * 100:+0.00;-0.00}%). A big gap means AC loaded different data.");
        }

        var problems = report.Lines.Count(l => l.Kind == LogKind.Problem);
        Summary.Text = summary + $"\n{problems} likely problem(s), {report.Lines.Count(l => l.Kind == LogKind.KnownHarmless)} known harmless line(s).";
        LineGrid.ItemsSource = report.Lines.OrderBy(l => l.Kind).ThenBy(l => l.Number).ToList();
    }
}
