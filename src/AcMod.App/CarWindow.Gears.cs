using System.Globalization;
using System.Windows;
using AcMod.Core.Tuning;
using static System.FormattableString;

namespace AcMod.App;

/// <summary>One row of the gear table. Setting Ratio (by editing the cell) hands the typed text back to the window.</summary>
public sealed class GearRow(string gear, string key, string ratio, string front, string rear, string note, IReadOnlyList<string>? options, bool editable, Action<string, string> setRatio)
{
    public string Gear { get; } = gear;

    /// <summary>The garage setup's ratios for this gear, or null when the car offers none (then the ratio is typed).</summary>
    public IReadOnlyList<string>? Options { get; } = options;

    public bool HasOptions => Options != null;

    public bool FreeText => Options == null;

    public bool Editable { get; } = editable;

    public string Front { get; } = front;

    public string Rear { get; } = rear;

    public string Note { get; } = note;

    public string Ratio
    {
        get => ratio;
        set => setRatio(key, value);
    }
}

/// <summary>A garage ratio list (.rto file) to edit, labelled with what uses it. The drop-down shows Label.</summary>
public sealed record RatioFileChoice(string File, string Label)
{
    public override string ToString() => Label;
}

/// <summary>One row of a garage ratio list, such as "14//37   2.64".</summary>
public sealed record RatioListRow(int RowIndex, string Text)
{
    public override string ToString() => Text;
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
        // So a gear with a list gets a drop-down of exactly those ratios (owner decision), and the note flags a value that is already off-list.
        GearRow Row(string label, string key, double ratio, string front, string rear, string note)
        {
            var options = GearEdits.RatioOptions(files, key);
            var offList = options is { Count: > 0 } && !options.Any(o => Math.Abs(o - ratio) < 0.0005);
            return new GearRow(label, key, Invariant($"{ratio}"), front, rear,
                offList ? Invariant($"{ratio} isn't in the garage setup's list, so AC uses {options![0]}. Pick a ratio from the list.") : note,
                options?.Select(o => Invariant($"{o}")).ToList(), !_edit.ReadOnly, SetRatio);
        }

        var rows = table.Gears
            .Select(g => Row(g.Gear.ToString(CultureInfo.InvariantCulture), $"GEAR_{g.Gear}", g.Ratio, Invariant($"{g.FrontKmh:0.0}"), Invariant($"{g.RearKmh:0.0}"), ""))
            .Append(Row("Final", "FINAL", table.Final, "", "", "final drive, applies to every gear"))
            .ToList();
        GearGrid.ItemsSource = rows;
        ShowRatioFiles(files, table.Gears.Count);

        GearSummary.Text =
            Invariant($"Speed at the rev limiter, {table.LimiterRpm:0} rpm, with final drive {table.Final}. ") +
            Invariant($"Traction {table.Traction}; default tyre compound {table.CompoundIndex}: front radius {table.FrontRadius} m, rear {table.RearRadius} m.\n") +
            "Not verified in game: that the compound index picks these tyre sections" +
            (driven == "both" && table.FrontRadius != table.RearRadius ? ", and which radius sets the speed on this all-wheel-drive car" : "") +
            ". Tyres flex and slip, so the speedometer may read a little differently.";
    }

    private bool _ratioFilesLoading; // true while the code refills the list choice, so that isn't taken as a user choice

    /// <summary>TU-10: fills the list choice with each .rto file setup.ini names, labelled with what uses it, and keeps the chosen one.</summary>
    private void ShowRatioFiles(IReadOnlyList<AcMod.Core.Acd.AcdEntry> files, int gearCount)
    {
        var users = Enumerable.Range(1, gearCount).Select(g => $"GEAR_{g}").Append("FINAL")
            .Select(key => (Key: key, File: GearEdits.RatioFile(files, key)))
            .Where(u => u.File != null)
            .GroupBy(u => u.File!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RatioFileChoice(g.Key, $"{g.Key}: " + string.Join(", ", g.Select(u => u.Key == "FINAL" ? "final drive" : $"gear {u.Key[5..]}"))))
            .ToList();
        RatioListBox.Visibility = users.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        var previous = (RatioFileBox.SelectedItem as RatioFileChoice)?.File;
        _ratioFilesLoading = true;
        RatioFileBox.ItemsSource = users;
        RatioFileBox.SelectedItem = users.FirstOrDefault(u => u.File == previous) ?? users.FirstOrDefault();
        _ratioFilesLoading = false;
        ShowRatioList();
    }

    private void RatioFileBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ratioFilesLoading) ShowRatioList();
    }

    private void ShowRatioList()
    {
        RatioList.ItemsSource = RatioFileBox.SelectedItem is RatioFileChoice choice
            ? AcMod.Core.Formats.LutFile.Parse(_edit.Current(choice.File)).Rows.Select((r, i) => new RatioListRow(i, $"{r.Key,-8} {Invariant($"{r.Value}")}")).ToList()
            : null;
    }

    /// <summary>TU-10: adds a typed ratio to the chosen list, labelled with a tooth pair. The gear drop-downs offer it at once.</summary>
    private void AddRatio_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || RatioFileBox.SelectedItem is not RatioFileChoice choice) return;
        if (!TryNumber(NewRatioBox.Text, out var ratio))
        {
            Status.Text = $"\"{NewRatioBox.Text}\" isn't a number.";
            return;
        }

        Status.Text = _edit.Apply(choice.File, bytes => GearEdits.AddRatio(bytes, ratio))
                      ?? Invariant($"Added {ratio} as {GearEdits.TeethLabel(ratio)} to {choice.File}. Pick it in the Ratio column.");
        MarkChanged();
        ShowGears();
    }

    private void RemoveRatio_Click(object sender, RoutedEventArgs e)
    {
        if (_edit.ReadOnly || RatioFileBox.SelectedItem is not RatioFileChoice choice) return;
        if (RatioList.SelectedItem is not RatioListRow row)
        {
            Status.Text = "Select a ratio in the list first.";
            return;
        }

        Status.Text = _edit.Apply(choice.File, bytes => GearEdits.RemoveRatio(bytes, row.RowIndex)) ?? $"Removed {row.Text.Trim()} from {choice.File}.";
        MarkChanged();
        ShowGears();
    }

    /// <summary>Stores a typed ratio for GEAR_n or FINAL in drivetrain.ini [GEARS].</summary>
    private void SetRatio(string key, string? text)
    {
        if (text == null || _edit.ReadOnly || _edit.FileName("drivetrain.ini") is not { } drivetrain) return; // null: the drop-down was emptied by a rebuild
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
        // With a garage list, start on the list's lowest ratio, the tallest gear it offers; without one, last gear × 0.88.
        var options = GearEdits.RatioOptions(_edit.CurrentFiles, $"GEAR_{table.Gears.Count}");
        var ratio = options is { Count: > 0 } ? options.Min() : Math.Round(table.Gears[^1].Ratio * 0.88, 2);
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
