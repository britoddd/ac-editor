using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AcMod.Core.Acd;
using AcMod.Core.Formats;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>
/// One data file in the list. It tells WPF when its label changes (INotifyPropertyChanged), so the * appears
/// without rebuilding the list, which would lose the selection and the cursor.
/// </summary>
public sealed class FileRow(string name) : INotifyPropertyChanged
{
    private bool _changed;

    public string Name { get; } = name;

    public string Label => (_changed ? "* " : "  ") + Name;

    public bool Changed
    {
        get => _changed;
        set
        {
            if (_changed == value) return;
            _changed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>The raw editor for a car's data files (TU-3), with unsaved marks and a preview of every change before saving (TU-7).</summary>
public partial class CarWindow : Window
{
    private readonly CarEdit _edit;
    private readonly List<FileRow> _rows;
    private bool _loading; // true while the code itself fills the editor, so that isn't taken as a user edit

    public CarWindow(CarEdit edit)
    {
        InitializeComponent();
        _edit = edit;
        Title = $"{edit.CarId} - raw data editor";
        _rows = [.. edit.Data.Files.Select(f => new FileRow(f.Name))];
        FileList.ItemsSource = _rows;

        var source = edit.Data.Source == DataSource.Acd ? "data.acd" : "the data folder";
        Header.Text = $"{edit.CarId}: editing {source}, which is what AC uses." +
            (edit.Data.FolderIgnored ? "\nWarning: a data folder also exists, but AC ignores it while data.acd exists (V-12). Edits go to data.acd." : "") +
            (edit.ReadOnly ? $"\nRead-only: {edit.Data.Error}" : "");
        Editor.IsReadOnly = edit.ReadOnly;
        SaveButton.IsEnabled = DiscardButton.IsEnabled = !edit.ReadOnly;
        if (_rows.Count > 0) FileList.SelectedIndex = 0;
    }

    private FileRow? Selected => FileList.SelectedItem as FileRow;

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSelected();

    private void ShowSelected()
    {
        _loading = true;
        Editor.Text = Selected == null ? "" : _edit.Text(Selected.Name);
        _loading = false;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected == null || _edit.ReadOnly) return;
        Status.Text = _edit.SetText(Selected.Name, Editor.Text) ?? "";
        Selected.Changed = _edit.ChangedFiles.Contains(Selected.Name);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var changes = _edit.Changes;
        if (changes.Count == 0)
        {
            Status.Text = "Nothing to save.";
            return;
        }

        var preview = $"Files that will be written:\n  {string.Join("\n  ", _edit.WriteTargets)}\n\nChanges:\n{Describe(changes)}\n\n" +
                      "A snapshot is taken first, so this can be undone. Save?";
        if (MessageBox.Show(this, preview, "Save", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            var (snapshotId, error) = _edit.Save();
            Status.Text = error ?? $"Saved. The previous version is in snapshot {snapshotId}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = $"Save failed, and the car was left as it was: {ex.Message}";
        }

        MarkChanged();
        ShowSelected();
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        _edit.Discard();
        MarkChanged();
        ShowSelected();
        Status.Text = "Changes discarded.";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_edit.ChangedFiles.Count == 0) return;
        var question = $"Close without saving? Unsaved changes in:\n  {string.Join("\n  ", _edit.ChangedFiles)}";
        e.Cancel = MessageBox.Show(this, question, "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes;
    }

    private void MarkChanged()
    {
        foreach (var row in _rows) row.Changed = _edit.ChangedFiles.Contains(row.Name);
    }

    /// <summary>One line per change, at most 30, for the save preview.</summary>
    private static string Describe(IReadOnlyList<FieldChange> changes)
    {
        var lines = changes.Take(30).Select(c => c switch
        {
            { Before: null, After: null } => $"  {c.File}: text changed (comments, spacing, or line breaks)",
            { Field: "(whole file)" } => $"  {c.File}: file {(c.Before == null ? "added" : "removed")}",
            _ => $"  {c.File}  {c.Field}: {c.Before ?? "(new)"} -> {c.After ?? "(removed)"}",
        });
        return string.Join("\n", lines) + (changes.Count > 30 ? $"\n  ...and {changes.Count - 30} more" : "");
    }
}
