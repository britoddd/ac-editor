using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private const double ChartPad = 30;
    private const double ChartLeft = 55; // room for the grid labels
    private double _chartTop = 1; // the value at the top grid line; torque and power share this scale
    private List<(int Row, double Rpm, double Torque)> _chartPoints = [];
    private double _boostFactor = 1;
    private double? _limiter; // engine.ini [ENGINE_DATA] LIMITER, drawn as a vertical line

    // While a point is dragged: where the drag started, the torque scale at that moment (kept fixed so the curve doesn't jump), and the change so far.
    private (double StartY, double Scale)? _drag;
    private double _dragDelta;

    private HashSet<int> SelectedPowerRows => PowerGrid.SelectedItems.OfType<PowerRow>().Select(r => r.RowIndex).ToHashSet();

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
        _boostFactor = estimate.BoostFactor;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].KeyAsNumber is not { } rpm) continue;
            var hp = PowerEstimate.PowerW(rpm, rows[i].Value, estimate.BoostFactor) / 745.699872;
            grid.Add(new PowerRow(i, rows[i].Key, Number(rows[i].Value), hp.ToString("0.0", CultureInfo.InvariantCulture), SetTorque));
            _chartPoints.Add((i, rpm, rows[i].Value));
        }

        // Rebuilding the table drops its selection, so select the same rows again.
        var keep = SelectedPowerRows;
        PowerGrid.ItemsSource = grid;
        foreach (var row in grid.Where(r => keep.Contains(r.RowIndex))) PowerGrid.SelectedItems.Add(row);
        PowerGrid.IsReadOnly = _edit.ReadOnly;

        var limiterField = LimiterField();
        _limiter = limiterField != null && TryNumber(limiterField.Value, out var limiter) ? limiter : null;
        LimiterBox.Text = limiterField?.Value ?? "";
        LimiterBox.IsEnabled = limiterField != null && !_edit.ReadOnly;
        var topRpm = _chartPoints.Count > 0 ? _chartPoints[^1].Rpm : 0;
        var limiterNote = _limiter > topRpm
            ? Invariant($"\nThe limiter ({_limiter:0} rpm) is above the curve's last row ({topRpm:0} rpm). How AC treats the gap isn't verified; add rows up to the limiter.")
            : "";
        var turbos = estimate.TurboCount == 0 ? "no turbo" : $"{estimate.TurboCount} turbo(s), so torque × {Number(estimate.BoostFactor)}";
        // Invariant(...) formats numbers with '.', whatever the Windows number settings are.
        PowerSummary.Text =
            Invariant($"{curveName}, {turbos}. Estimated peak power {estimate.PeakPowerHp:0.0} hp ({estimate.PeakPowerW:0} W) at {estimate.PeakPowerRpm:0} rpm; ") +
            Invariant($"peak torque {estimate.PeakTorqueNm:0.0} Nm at {estimate.PeakTorqueRpm:0} rpm.\n") +
            "The power formula matched the PW in AC's log within 0.1% for the tutorial car (one turbo)." +
            (estimate.TurboCount > 1 ? " With several turbos, adding their boosts is not verified." : "") +
            (estimate.RatedTorqueEstimateNm is { } rated ? Invariant($"\nRated engine torque ≈ {rated:0} Nm (community estimate: power.lut torque ÷ 0.85, no turbo).") : "") +
            limiterNote;
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

    /// <summary>engine.ini's [ENGINE_DATA] LIMITER field, or null when the car has none.</summary>
    private FormField? LimiterField() =>
        _edit.FileName("engine.ini") is { } engine
            ? TuningForms.Fields(_edit.Current(engine)).FirstOrDefault(f => f.Section == "ENGINE_DATA" && f.Key == "LIMITER")
            : null;

    /// <summary>TU-9: sets the rev limiter. It changes the Gears tab's speeds too, which follow LIMITER.</summary>
    private void SetLimiter_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || LimiterField() is not { } field || _edit.FileName("engine.ini") is not { } engine) return;
        if (!TryNumber(LimiterBox.Text, out var rpm) || rpm <= 0)
        {
            Status.Text = $"\"{LimiterBox.Text}\" isn't an rpm above 0.";
            return;
        }

        Status.Text = _edit.SetIniValue(engine, field.LineIndex, Invariant($"{Math.Round(rpm)}")) ?? Invariant($"Limiter set to {Math.Round(rpm)} rpm.");
        MarkChanged();
        ShowPower();
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

    /// <summary>
    /// TU-9: adds a row one rpm step above the last (the step between the last two rows), with the last row's torque, so the engine can rev higher
    /// with power. Unverified: how AC treats LIMITER above the last row, so extend the curve before raising LIMITER.
    /// </summary>
    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || PowerEstimate.CurveFile(_edit.CurrentFiles) is not { } curveName || _chartPoints.Count < 2) return;
        var (last, previous) = (_chartPoints[^1], _chartPoints[^2]);
        var rpm = last.Rpm + Math.Max(1, last.Rpm - previous.Rpm);
        Status.Text = _edit.Apply(curveName, bytes => GearEdits.AddCurveRow(bytes, rpm, last.Torque)) ?? Invariant($"Added a row at {rpm:0} rpm. Drag or type its torque.");
        MarkChanged();
        ShowPower();
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || PowerEstimate.CurveFile(_edit.CurrentFiles) is not { } curveName) return;
        Status.Text = _edit.Apply(curveName, GearEdits.RemoveLastCurveRow) ?? "Removed the last row.";
        MarkChanged();
        ShowPower();
    }

    /// <summary>A typed number. AC files always use '.', but Indonesian Windows types ',' for decimals, so accept either (QS-11).</summary>
    private static bool TryNumber(string text, out double number) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    private void PowerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_drag == null) DrawChart(); // the dots show the table's selection
    }

    /// <summary>
    /// Torque (blue, with a dot per row; filled dots are selected) and power (orange), each scaled to its own peak, against rpm from 0 to the last row.
    /// During a drag the selected points are drawn moved by the drag so far.
    /// </summary>
    private void DrawChart()
    {
        Chart.Children.Clear();
        if (_chartPoints.Count < 2 || Chart.ActualWidth < 50 || Chart.ActualHeight < 50) return;

        const double pad = ChartPad;
        var (left, width, height) = (ChartLeft, Chart.ActualWidth - ChartLeft - pad, Chart.ActualHeight - 2 * pad);
        var selected = SelectedPowerRows;
        var points = _chartPoints
            .Select(p => (p.Row, p.Rpm, Torque: _drag != null && selected.Contains(p.Row) ? p.Torque + _dragDelta : p.Torque))
            .Select(p => (p.Row, p.Rpm, p.Torque, Hp: PowerEstimate.PowerW(p.Rpm, p.Torque, _boostFactor) / 745.699872))
            .ToList();
        var maxRpm = Math.Max(1, Math.Max(points.Max(p => p.Rpm), _limiter ?? 0));
        var maxTorque = points.Max(p => p.Torque);
        var maxHp = points.Max(p => p.Hp);

        // One scale for both curves, in round steps, with the top line just above the higher curve. It stays fixed during a drag.
        var (top, step) = NiceScale(Math.Max(1e-9, Math.Max(maxTorque, maxHp)));
        if (_drag is { } drag) (top, step) = (drag.Scale, NiceScale(drag.Scale / 1.02).Step);
        _chartTop = top;
        Point At(double rpm, double value) => new(left + rpm / maxRpm * width, pad + height - value / top * height);

        for (var v = 0.0; v <= top + step / 2; v += step)
        {
            var y = At(0, v).Y;
            Chart.Children.Add(new Line { X1 = left, Y1 = y, X2 = left + width, Y2 = y, Stroke = v == 0 ? Brushes.Gray : Brushes.Gainsboro });
            AddText(Number(Math.Round(v, 6)), 4, y - 8, Brushes.Gray);
        }

        if (_limiter is { } limiter)
        {
            var x = At(limiter, 0).X;
            Chart.Children.Add(new Line { X1 = x, Y1 = pad, X2 = x, Y2 = pad + height, Stroke = Brushes.Red, StrokeThickness = 1.5, StrokeDashArray = [4, 3] });
            AddText(Invariant($"limiter {limiter:0}"), x - 70, pad + height - 18, Brushes.Red);
        }

        Chart.Children.Add(new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2, Points = [.. points.Select(p => At(p.Rpm, p.Torque))] });
        Chart.Children.Add(new Polyline { Stroke = Brushes.DarkOrange, StrokeThickness = 2, Points = [.. points.Select(p => At(p.Rpm, p.Hp))] });

        foreach (var p in points)
        {
            var at = At(p.Rpm, p.Torque);
            var dot = new Ellipse
            {
                Width = 10, Height = 10, Tag = p.Row, Cursor = _edit.ReadOnly ? null : Cursors.SizeNS,
                Stroke = Brushes.SteelBlue, StrokeThickness = 2, Fill = selected.Contains(p.Row) ? Brushes.SteelBlue : Brushes.White,
            };
            dot.MouseLeftButtonDown += Point_MouseLeftButtonDown;
            Canvas.SetLeft(dot, at.X - 5);
            Canvas.SetTop(dot, at.Y - 5);
            Chart.Children.Add(dot);
        }

        AddText(Invariant($"torque (Nm, as in the file), peak {maxTorque:0.0}"), left + 4, 2, Brushes.SteelBlue);
        AddText(Invariant($"power (hp), peak {maxHp:0.0}    same scale for both; drag a dot, Ctrl-click to pick several"), left + 4, 16, Brushes.DarkOrange);
        AddText("0 rpm", left, pad + height + 4, Brushes.Gray);
        AddText(Invariant($"{maxRpm:0} rpm"), left + width - 60, pad + height + 4, Brushes.Gray);
        if (_drag != null) AddText(Invariant($"{_dragDelta:+0.0;-0.0;0.0} Nm on {selected.Count} point(s)"), left + width / 2 - 60, pad + 4, Brushes.Black);
    }

    /// <summary>A grid step of 1, 2 or 5 × 10ⁿ giving about five lines, and the first line at or above the value with 2% headroom.</summary>
    private static (double Top, double Step) NiceScale(double max)
    {
        var raw = max / 5;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        return (Math.Ceiling(max * 1.02 / step) * step, step);
    }

    /// <summary>A click on empty chart space (not on a dot) without Ctrl clears the selection.</summary>
    private void Chart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) PowerGrid.UnselectAll();
    }

    /// <summary>A click picks the point (Ctrl-click adds or removes it), then starts a drag of every selected point.</summary>
    private void Point_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_edit.ReadOnly || sender is not Ellipse { Tag: int row }) return;
        var item = PowerGrid.Items.OfType<PowerRow>().First(r => r.RowIndex == row);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (PowerGrid.SelectedItems.Contains(item)) PowerGrid.SelectedItems.Remove(item);
            else PowerGrid.SelectedItems.Add(item);
        }
        else if (!PowerGrid.SelectedItems.Contains(item))
        {
            PowerGrid.SelectedItems.Clear();
            PowerGrid.SelectedItems.Add(item);
        }

        e.Handled = true;
        if (!PowerGrid.SelectedItems.Contains(item)) return; // Ctrl-click just removed it, so there's nothing to drag

        _drag = (e.GetPosition(Chart).Y, _chartTop);
        _dragDelta = 0;
        Chart.CaptureMouse(); // keep getting mouse moves even outside the chart
    }

    private void Chart_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag is not { } drag) return;
        var plotHeight = Chart.ActualHeight - 2 * ChartPad;
        _dragDelta = (drag.StartY - e.GetPosition(Chart).Y) / plotHeight * drag.Scale;
        DrawChart();
    }

    /// <summary>Releasing the mouse makes the drag one unsaved change, with values rounded to 0.1 Nm (owner decision).</summary>
    private void Chart_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag == null) return;
        var delta = _dragDelta;
        _drag = null;
        Chart.ReleaseMouseCapture();
        if (Math.Abs(delta) >= 0.05 && PowerEstimate.CurveFile(_edit.CurrentFiles) is { } curveName)
        {
            var selected = SelectedPowerRows;
            _edit.SetLutValues(curveName, CurveEdits.Drag(LutFile.Parse(_edit.Current(curveName)).Rows, selected, delta));
            Status.Text = Invariant($"{selected.Count} point(s) moved by {delta:+0.0;-0.0} Nm.");
            MarkChanged();
            ShowPower();
        }
        else
        {
            DrawChart();
        }
    }

    private void Chart_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_drag == null) return; // released normally
        _drag = null; // capture taken away, for example by another window: cancel the drag
        DrawChart();
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
