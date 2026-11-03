using System.Windows.Controls;
using AcMod.Core.Tuning;

namespace AcMod.App;

/// <summary>One field of a form. Setting Value (by editing the cell) hands the typed text back to the window.</summary>
public sealed class FormRow(int lineIndex, string key, string text, string hint, string mark, Action<int, string> setValue)
{
    public string Mark { get; } = mark;

    public string Key { get; } = key;

    public string Hint { get; } = hint;

    public string Value
    {
        get => text;
        set => setValue(lineIndex, value);
    }
}

/// <summary>
/// The forms tab (TU-2): car, engine, drivetrain, brakes, suspension, tyres and aero. Each form lists every field of its file, one section
/// at a time, with hints from the file's own comments. Fuller, sourced explanations are LE-1 (M7).
/// </summary>
public partial class CarWindow
{
    private bool _formLoading; // true while the code fills the section list, so that isn't taken as a user choice

    private void InitForms()
    {
        GroupList.ItemsSource = TuningForms.Groups.Select(g => g.Group).ToList();
        GroupList.SelectedIndex = 0;
    }

    /// <summary>The car's own name for the selected group's file (data file names can differ in case), or null when the car has none.</summary>
    private string? FormFile =>
        GroupList.SelectedIndex < 0 ? null
        : _edit.Data.Files.FirstOrDefault(f => f.Name.Equals(TuningForms.Groups[GroupList.SelectedIndex].File, StringComparison.OrdinalIgnoreCase))?.Name;

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowForms();

    private void SectionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_formLoading) ShowFormRows();
    }

    /// <summary>Refills the section list for the selected group, keeping the chosen section when the file still has it.</summary>
    private void ShowForms()
    {
        if (FormFile is not { } file)
        {
            FormNote.Text = GroupList.SelectedIndex < 0 ? "" : $"This car has no {TuningForms.Groups[GroupList.SelectedIndex].File}.";
            SectionBox.ItemsSource = null;
            FormGrid.ItemsSource = null;
            return;
        }

        var sections = TuningForms.Fields(_edit.Current(file)).Select(f => f.Section).Distinct().ToList();
        var previous = SectionBox.SelectedItem as string;
        _formLoading = true;
        SectionBox.ItemsSource = sections;
        SectionBox.SelectedItem = previous != null && sections.Contains(previous) ? previous : sections.FirstOrDefault();
        _formLoading = false;
        ShowFormRows();
    }

    private void ShowFormRows()
    {
        if (FormFile is not { } file || SectionBox.SelectedItem is not string section)
        {
            FormGrid.ItemsSource = null;
            return;
        }

        var saved = TuningForms.Fields(_edit.Data.Files.First(f => f.Name == file).Data).ToDictionary(f => f.Id, f => f.Value);
        var rows = TuningForms.Fields(_edit.Current(file))
            .Where(f => f.Section == section)
            .Select(f => new FormRow(
                f.LineIndex,
                f.Key,
                f.Value,
                f.Hint == null ? "" : f.HintFrom == null ? f.Hint : $"{f.Hint}   (from {f.HintFrom})",
                saved.GetValueOrDefault(f.Id) == f.Value ? "" : "*",
                SetField))
            .ToList();
        FormGrid.ItemsSource = rows;
        FormGrid.IsReadOnly = _edit.ReadOnly;
        FormNote.Text = $"{file}: {rows.Count} field(s) in this section";
    }

    private void SetField(int lineIndex, string text)
    {
        if (_edit.ReadOnly || FormFile is not { } file) return;
        Status.Text = _edit.SetIniValue(file, lineIndex, text) ?? "";
        MarkChanged();

        // Rebuild the table after the grid finishes its edit; rebuilding during the edit isn't allowed.
        Dispatcher.BeginInvoke(ShowFormRows);
    }
}
