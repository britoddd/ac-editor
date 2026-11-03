using System.Globalization;
using System.IO;
using System.Windows;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>One row of the car list, already formatted for display.</summary>
public sealed record CarRow(string Id, string Name, string Size, string LastChanged, string Validation);

/// <summary>The car list (WS-3) with an Install button (WS-11). Every action calls AcMod.Core; this window only shows the results.</summary>
public partial class MainWindow : Window
{
    private Workspace? _workspace;

    public MainWindow()
    {
        InitializeComponent(); // builds the controls described in MainWindow.xaml
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        _workspace = null;
        CarGrid.ItemsSource = null;
        try
        {
            var (settings, settingsError) = Settings.Load(Settings.DefaultPath);
            if (settings.WorkspaceFolder == null)
            {
                Status.Text = settingsError ?? "No workspace yet. Run: acmod setup <workspace folder>";
                return;
            }

            var (workspace, openError) = Workspace.Open(settings.WorkspaceFolder, settings.AcFolder);
            if (workspace == null)
            {
                Status.Text = openError;
                return;
            }

            _workspace = workspace;
            Header.Text = $"Workspace: {workspace.Root}    AC: {workspace.AcFolder ?? "not set"}";
            CarGrid.ItemsSource = workspace.Cars()
                .Select(c => new CarRow(c.Id, c.DisplayName ?? "(no name)", Megabytes(c.Size), Local(c.LastChangedUtc), "not run (M5)"))
                .ToList();
            Status.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = ex.Message;
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null || CarGrid.SelectedItem is not CarRow row)
        {
            Status.Text = "Choose a car in the list first.";
            return;
        }

        var (edit, error) = _workspace.OpenCar(row.Id);
        if (edit == null)
        {
            Status.Text = error;
            return;
        }

        new CarWindow(edit) { Owner = this }.ShowDialog(); // waits until the car window closes
        Refresh(); // size and last change may have moved
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null) return;
        new CompareWindow(_workspace, (CarGrid.SelectedItem as CarRow)?.Id) { Owner = this }.ShowDialog();
    }

    /// <summary>PK-2: writes a ZIP to the workspace's exports folder and shows it in Explorer, for installing through Content Manager.</summary>
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null || CarGrid.SelectedItem is not CarRow row)
        {
            Status.Text = "Choose a car in the list first.";
            return;
        }

        try
        {
            var (zipPath, error) = _workspace.ExportZip(row.Id);
            if (zipPath == null)
            {
                MessageBox.Show(this, error, "Export ZIP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{zipPath}\"");
            MessageBox.Show(this,
                $"Exported to\n{zipPath}\n\nTo install: open Content Manager and drag this ZIP onto its window, then confirm the install. " +
                "(Content Manager accepting a dropped ZIP is from a community tutorial; tell Claude if it behaves differently.)",
                "Export ZIP", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export ZIP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null || CarGrid.SelectedItem is not CarRow row)
        {
            Status.Text = "Choose a car in the list first.";
            return;
        }

        var question = $"Copy {row.Id} into AC's content\\cars?\n\n{_workspace.AcFolder}";
        if (MessageBox.Show(this, question, "Install to AC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            // ponytail: runs on the UI thread, so the window freezes while a large car copies; move to Task.Run if that bothers.
            var (installed, replaced, error) = _workspace.InstallToAc(row.Id);
            var message = error ?? $"Installed to {installed}" + (replaced == null ? "" : $"\n\nThe previous install is in the trash as {replaced}.");
            MessageBox.Show(this, message, "Install to AC", MessageBoxButton.OK, error == null ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Install to AC", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }

    private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

    private static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
