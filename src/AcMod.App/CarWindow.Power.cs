using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AcMod.Core.Formats;
using AcMod.Core.Tuning;
using static System.FormattableString;

namespace AcMod.App;

/// <summary>One row of the power table. Setting Torque (by editing the cell) hands the typed text back to the window.</summary>
public sealed class PowerRow(int rowIndex, string rpm, string torque, string power, Action<int, string> setTorque)
{
    /// <summary>The row's index in the file's rows, which <see cref="CarEdit.SetLutValues"/> expects.</summary>
    public int RowIndex { get; } = rowIndex;

    public string Rpm { get; } = rpm;

    public string Power { get; } = power;

    public string Torque
    {
        get => torque;
        set => setTorque(RowIndex, value);
    }
}

/// <summary>The power curve tab (TU-4): edit power.lut's torque values as a table and see the curve and the estimates change.</summary>
public partial class CarWindow
{
    private List<(double Rpm, double Torque, double Hp)> _chartPoints = [];

    private void ShowPower()
    {
        var files = _edit.CurrentFiles;
        var curveName = PowerEstimate.CurveFile(files);
        var (estimate, error) = PowerEstimate.FromData(files);
        if (estimate == null || curveName == null)
        {
            PowerSummary.Text = error ?? "engine.ini has no [HEADER] POWER_CURVE.";
            PowerGrid.ItemsSource = null;
            _chartPoints = [];
            DrawChart();
            return;
        }

        var rows = LutFile.Parse(_edit.Current(curveName)).Rows;
        var grid = new List<PowerRow>();
        _chartPoints = [];
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].KeyAsNumber is not { } rpm) continue;
            var hp = PowerEstimate.PowerW(rpm, rows[i].Value, estimate.BoostFactor) / 745.699872;
            grid.Add(new PowerRow(i, rows[i].Key, Number(rows[i].Value), hp.ToString("0.0", CultureInfo.InvariantCulture), SetTorque));
            _chartPoints.Add((rpm, rows[i].Value, hp));
        }

        // Rebuilding the table drops its selection, so select the same rows again.
        var keep = PowerGrid.SelectedItems.OfType<PowerRow>().Select(r => r.RowIndex).ToHashSet();
        PowerGrid.ItemsSource = grid;
        foreach (var row in grid.Where(r => keep.Contains(r.RowIndex))) PowerGrid.SelectedItems.Add(row);
        PowerGrid.IsReadOnly = _edit.ReadOnly;
        var turbos = estimate.TurboCount == 0 ? "no turbo" : $"{estimate.TurboCount} turbo(s), so torque × {Number(estimate.BoostFactor)}";
        // Invariant(...) formats numbers with '.', whatever the Windows number settings are.
        PowerSummary.Text =
            Invariant($"{curveName}, {turbos}. Estimated peak power {estimate.PeakPowerHp:0.0} hp ({estimate.PeakPowerW:0} W) at {estimate.PeakPowerRpm:0} rpm; ") +
            Invariant($"peak torque {estimate.PeakTorqueNm:0.0} Nm at {estimate.PeakTorqueRpm:0} rpm.\n") +
            "The power formula matched the PW in AC's log within 0.1% for the tutorial car (one turbo)." +
            (estimate.TurboCount > 1 ? " With several turbos, adding their boosts is not verified." : "") +
            (estimate.RatedTorqueEstimateNm is { } rated ? Invariant($"\nRated engine torque ≈ {rated:0} Nm (community estimate: power.lut torque ÷ 0.85, no turbo).") : "");
        DrawChart();
    }

    private void SetTorque(int rowIndex, string text)
    {
        if (_edit.ReadOnly || PowerEstimate.CurveFile(_edit.CurrentFiles) is not { } curveName) return;

        if (!TryNumber(text, out var torque))
        {
            Status.Text = $"\"{text}\" isn't a number, so it wasn't changed.";
        }
        else
        {
            _edit.SetLutValue(curveName, rowIndex, torque);
            Status.Text = "";
            MarkChanged();
        }

        // Rebuild the table after the grid finishes its edit; rebuilding during the edit isn't allowed.
        Dispatcher.BeginInvoke(ShowPower);
    }

    private void Scale_Click(object sender, RoutedEventArgs e) => ApplyToSelected(CurveEdits.Scale, "scaled by {0}%");

    private void Add_Click(object sender, RoutedEventArgs e) => ApplyToSelected(CurveEdits.Add, "changed by {0} Nm");

    /// <summary>TU-8: applies Scale % or Add Nm, with the amount in the box, to every selected row as one change.</summary>
    private void ApplyToSelected(Func<IReadOnlyList<LutRow>, IEnumerable<int>, double, Dictionary<int, double>> change, string done)
    {
        if (_edit.ReadOnly || PowerEstimate.CurveFile(_edit.CurrentFiles) is not { } curveName) return;
        var selected = PowerGrid.SelectedItems.OfType<PowerRow>().Select(r => r.RowIndex).ToList();
        if (selected.Count == 0)
        {
            Status.Text = "Select rows in the table first (Ctrl or Shift click to pick several).";
            return;
        }

        if (!TryNumber(AmountBox.Text, out var amount))
        {
            Status.Text = $"\"{AmountBox.Text}\" isn't a number.";
            return;
        }

        _edit.SetLutValues(curveName, change(LutFile.Parse(_edit.Current(curveName)).Rows, selected, amount));
        Status.Text = $"{selected.Count} row(s) " + string.Format(CultureInfo.InvariantCulture, done, amount) + ".";
        MarkChanged();
        ShowPower();
    }

    /// <summary>A typed number. AC files always use '.', but Indonesian Windows types ',' for decimals, so accept either (QS-11).</summary>
    private static bool TryNumber(string text, out double number) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    /// <summary>Torque (blue) and power (orange), each scaled to its own peak, against rpm from 0 to the last row.</summary>
    private void DrawChart()
    {
        Chart.Children.Clear();
        if (_chartPoints.Count < 2 || Chart.ActualWidth < 50 || Chart.ActualHeight < 50) return;

        const double pad = 30;
        var (width, height) = (Chart.ActualWidth - 2 * pad, Chart.ActualHeight - 2 * pad);
        var maxRpm = Math.Max(1, _chartPoints.Max(p => p.Rpm));
        var maxTorque = Math.Max(1e-9, _chartPoints.Max(p => p.Torque));
        var maxHp = Math.Max(1e-9, _chartPoints.Max(p => p.Hp));

        Polyline Curve(Func<(double Rpm, double Torque, double Hp), double> value, double max, Brush brush) => new()
        {
            Stroke = brush,
            StrokeThickness = 2,
            Points = [.. _chartPoints.Select(p => new Point(pad + p.Rpm / maxRpm * width, pad + height - value(p) / max * height))],
        };

        Chart.Children.Add(new Line { X1 = pad, Y1 = pad + height, X2 = pad + width, Y2 = pad + height, Stroke = Brushes.Gray });
        Chart.Children.Add(Curve(p => p.Torque, maxTorque, Brushes.SteelBlue));
        Chart.Children.Add(Curve(p => p.Hp, maxHp, Brushes.DarkOrange));
        AddText($"torque, peak {Number(maxTorque)}", 4, 4, Brushes.SteelBlue);
        AddText(Invariant($"power, peak {maxHp:0.0} hp"), 4, 20, Brushes.DarkOrange);
        AddText("0 rpm", pad, pad + height + 4, Brushes.Gray);
        AddText(Invariant($"{maxRpm:0} rpm"), pad + width - 60, pad + height + 4, Brushes.Gray);
    }

    private void AddText(string text, double x, double y, Brush brush)
    {
        var label = new TextBlock { Text = text, Foreground = brush };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        Chart.Children.Add(label);
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
