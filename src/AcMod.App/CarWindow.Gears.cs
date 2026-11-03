using System.Globalization;
using System.Windows;
using AcMod.Core.Tuning;
using static System.FormattableString;

namespace AcMod.App;

/// <summary>One row of the gear table. Setting Ratio (by editing the cell) hands the typed text back to the window.</summary>
public sealed class GearRow(string gear, string key, string ratio, string front, string rear, string note, Action<string, string> setRatio)
{
    public string Gear { get; } = gear;

    public string Front { get; } = front;

    public string Rear { get; } = rear;

    public string Note { get; } = note;

    public string Ratio
    {
        get => ratio;
        set => setRatio(key, value);
    }
}

/// <summary>
/// The gears tab: speed per gear at the rev limiter (TU-5), and editing ratios, FINAL, and adding or removing gears (TU-9),
/// all from the current (possibly unsaved) data.
/// </summary>
public partial class CarWindow
{
    private void ShowGears()
    {
        var files = _edit.CurrentFiles;
        var (table, error) = GearTable.FromData(files);
        if (table == null)
        {
            GearSummary.Text = error;
            GearGrid.ItemsSource = null;
            return;
        }

        var driven = table.DrivenAxle;
        FrontColumn.Header = "Front tyre km/h" + (driven is "front" or "both" ? " (driven)" : "");
        RearColumn.Header = "Rear tyre km/h" + (driven is "rear" or "both" ? " (driven)" : "");

        // Seen in game 2026-11-03: AC's garage puts an off-list ratio on the setup list's FIRST option, not the nearest one.
        string Note(int gear, double ratio) => GearEdits.RatioOptions(files, gear) is { Count: > 0 } options && !options.Any(o => Math.Abs(o - ratio) < 0.0005)
            ? Invariant($"not in the garage setup's list: AC uses its first option, {options[0]}, instead")
            : "";

        var rows = table.Gears
            .Select(g => new GearRow(g.Gear.ToString(CultureInfo.InvariantCulture), $"GEAR_{g.Gear}", Invariant($"{g.Ratio}"),
                Invariant($"{g.FrontKmh:0.0}"), Invariant($"{g.RearKmh:0.0}"), Note(g.Gear, g.Ratio), SetRatio))
            .Append(new GearRow("Final", "FINAL", Invariant($"{table.Final}"), "", "", "final drive, applies to every gear", SetRatio))
            .ToList();
        GearGrid.ItemsSource = rows;
        GearGrid.IsReadOnly = _edit.ReadOnly;

        GearSummary.Text =
            Invariant($"Speed at the rev limiter, {table.LimiterRpm:0} rpm, with final drive {table.Final}. ") +
            Invariant($"Traction {table.Traction}; default tyre compound {table.CompoundIndex}: front radius {table.FrontRadius} m, rear {table.RearRadius} m.\n") +
            "Not verified in game: that the compound index picks these tyre sections" +
            (driven == "both" && table.FrontRadius != table.RearRadius ? ", and which radius sets the speed on this all-wheel-drive car" : "") +
            ". Tyres flex and slip, so the speedometer may read a little differently.";
    }

    /// <summary>Stores a typed ratio for GEAR_n or FINAL in drivetrain.ini [GEARS].</summary>
    private void SetRatio(string key, string text)
    {
        if (_edit.ReadOnly || _edit.FileName("drivetrain.ini") is not { } drivetrain) return;
        var field = TuningForms.Fields(_edit.Current(drivetrain)).FirstOrDefault(f => f.Section == "GEARS" && f.Key == key);
        if (field == null) return;

        Status.Text = TryNumber(text, out var ratio) && ratio > 0
            ? _edit.SetIniValue(drivetrain, field.LineIndex, text) ?? ""
            : $"\"{text}\" isn't a ratio above 0, so it wasn't changed.";
        MarkChanged();

        // Rebuild the table after the grid finishes its edit; rebuilding during the edit isn't allowed.
        Dispatcher.BeginInvoke(ShowGears);
    }

    /// <summary>Adds a gear with the last gear's ratio × 0.88 (about the 5th-to-6th step of the tutorial car), rounded to 0.01. Edit it afterwards.</summary>
    private void AddGear_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || GearTable.FromData(_edit.CurrentFiles).Table is not { } table) return;
        var ratio = Math.Round(table.Gears[^1].Ratio * 0.88, 2);
        Status.Text = _edit.AddGear(ratio) ?? Invariant($"Added gear {table.Gears.Count + 1} with ratio {ratio}. Edit it in the table.");
        MarkChanged();
        ShowGears();
    }

    private void RemoveGear_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly) return;
        Status.Text = _edit.RemoveLastGear() ?? "Removed the last gear.";
        MarkChanged();
        ShowGears();
    }
}
