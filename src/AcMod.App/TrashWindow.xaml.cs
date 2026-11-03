using System.Globalization;
using System.IO;
using System.Windows;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>One trash entry, formatted for the list.</summary>
public sealed record TrashRow(string Name, string Size, string When);

/// <summary>The trash (WS-6): restore a deleted car, or delete it permanently after the warning and the typed car ID.</summary>
public partial class TrashWindow : Window
{
    private readonly Workspace _workspace;

    public TrashWindow(Workspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        Refresh();
    }

    private void Refresh() =>
        TrashGrid.ItemsSource = _workspace.Subfolders("trash")
            .Select(f => new TrashRow(f.Name, (f.Size / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB",
                f.LastChangedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

    private TrashRow? Selected
    {
        get
        {
            if (TrashGrid.SelectedItem is TrashRow row) return row;
            Status.Text = "Choose an entry first.";
            return null;
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        try
        {
            var (carId, error) = _workspace.RestoreFromTrash(row.Name);
            Status.Text = error ?? $"Restored {carId}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = $"{ex.Message} Close any Explorer window on that folder and try again.";
        }

        Refresh();
    }

    /// <summary>Hard rule 4: shows what will be lost, then deletes only when the user types the car ID exactly.</summary>
    private void Purge_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        var (warning, error) = _workspace.PermanentDeleteWarning(row.Name);
        if (warning == null)
        {
            Status.Text = error;
            return;
        }

        if (AskWindow.Ask(this, "Delete permanently", warning, ok: "Delete permanently") is not { } typed) return;
        try
        {
            Status.Text = _workspace.DeletePermanently(row.Name, typed) ?? $"Deleted {row.Name} permanently.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = ex.Message;
        }

        Refresh();
    }
}
