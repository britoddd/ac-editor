using System.Globalization;
using System.IO;
using System.Windows;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>A car's snapshots (WS-9, WS-10): take one, compare, or restore one.</summary>
public partial class SnapshotsWindow : Window
{
    private readonly Workspace _workspace;
    private readonly string _carId;

    public SnapshotsWindow(Workspace workspace, string carId)
    {
        InitializeComponent();
        (_workspace, _carId) = (workspace, carId);
        Title = $"{carId} - snapshots";
        Refresh();
    }

    private void Refresh() =>
        SnapshotList.ItemsSource = _workspace.Snapshots(_carId).Reverse()
            .Select(id => new VersionItem(id, $"{Workspace.SnapshotTimeUtc(id)?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? id}   ({id})"))
            .ToList();

    private void Take_Click(object sender, RoutedEventArgs e)
    {
        Status.Text = _workspace.Snapshot(_carId) is ({ } id, _) ? $"Took snapshot {id}." : "The snapshot failed.";
        Refresh();
    }

    private void Compare_Click(object sender, RoutedEventArgs e) => new CompareWindow(_workspace, _carId) { Owner = this }.ShowDialog();

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotList.SelectedItem is not VersionItem { SnapshotId: { } id } item)
        {
            Status.Text = "Choose a snapshot first.";
            return;
        }

        var question = $"Put {_carId}'s data and UI files back to {item.Label}?\n\nThe current files are saved in a new snapshot first, so you can undo this.";
        if (MessageBox.Show(this, question, "Restore snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var (before, error) = _workspace.RestoreSnapshot(_carId, id);
            Status.Text = error ?? $"Restored. To undo, restore {before}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = ex.Message;
        }

        Refresh();
    }
}
