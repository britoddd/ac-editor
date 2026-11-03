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
                .Select(c => new CarRow(c.Id, c.DisplayName ?? "(no name)", Megabytes(c.Size), Local(c.LastChangedUtc),
                    workspace.Validate(c.Id).Findings is { } findings ? CarWindow.Summary(findings) : "can't run"))
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

        if (!ConfirmDespiteErrors(row.Id, "Export")) return;
        try
        {
            var (zipPath, error) = _workspace.ExportZip(row.Id, ignoreErrors: true, pack: PackItem.IsChecked); // the user has seen any errors above
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

    /// <summary>WS-7 and WS-8: copies the selected car to a new ID, then optionally trashes the old one if the copy validates.</summary>
    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null || CarGrid.SelectedItem is not CarRow row)
        {
            Status.Text = "Choose a car in the list first.";
            return;
        }

        var dialog = new SaveAsWindow(row.Id, row.Name == "(no name)" ? null : row.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var (result, error) = _workspace.SaveAsNewCar(row.Id, dialog.NewId, dialog.DisplayName.Length == 0 ? null : dialog.DisplayName);
            if (result == null)
            {
                MessageBox.Show(this, error, "Save as new car", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var message = $"Saved {row.Id} as {result.CarId}.";
            if (result.Notes.Count > 0) message += "\n\n" + string.Join("\n", result.Notes);
            if (result.MentionsOfOldId.Count > 0)
            {
                message += $"\n\nThese files still mention {row.Id}. What that does isn't verified, so they weren't changed; check them:\n  " +
                           string.Join("\n  ", result.MentionsOfOldId);
            }

            if (dialog.ReplacePrevious)
            {
                var (trashId, replaceError) = _workspace.ReplacePrevious(row.Id, result.CarId);
                message += trashId != null ? $"\n\n{row.Id} was moved to the trash as {trashId}." : $"\n\n{replaceError}";
            }

            MessageBox.Show(this, message, "Save as new car", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Save as new car", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }

    /// <summary>The selected car's ID, or null with a hint in the status line.</summary>
    private string? SelectedCar()
    {
        if (_workspace != null && CarGrid.SelectedItem is CarRow row) return row.Id;
        Status.Text = "Choose a car in the list first.";
        return null;
    }

    /// <summary>Runs a file operation and turns a Windows refusal into a message (an Explorer window on the folder is the usual cause).</summary>
    private void Try(string title, Func<string?> action)
    {
        try
        {
            if (action() is { } message) MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{ex.Message}\n\nClose any Explorer window or program using the car's folder and try again.", title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }

    /// <summary>WS-13: an empty template by default, or a copy of a complete car.</summary>
    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null) return;
        var dialog = new NewCarWindow([.. _workspace.Cars().Select(c => c.Id)]) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        Try("New car", () =>
        {
            if (dialog.Source == null) return _workspace.NewEmptyCar(dialog.CarId, dialog.DisplayName) ?? $"Created {dialog.CarId}. Open Car > First-car checklist to build it.";
            var (result, error) = _workspace.SaveAsNewCar(dialog.Source, dialog.CarId, dialog.DisplayName);
            return error ?? $"Created {result!.CarId} as a copy of {dialog.Source}. Its checklist marks the copied model and collider until you replace them.";
        });
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a car folder (or a folder holding one car)" };
        if (_workspace != null && dialog.ShowDialog(this) == true) Import(dialog.FolderName);
    }

    private void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose a car ZIP", Filter = "ZIP files (*.zip)|*.zip" };
        if (_workspace != null && dialog.ShowDialog(this) == true) Import(dialog.FileName);
    }

    /// <summary>WS-4, and VA-1: the validator runs after import.</summary>
    private void Import(string source) => Try("Import", () =>
    {
        var (carId, error) = _workspace!.ImportCar(source);
        if (carId == null) return error;
        var findings = _workspace.Validate(carId).Findings ?? [];
        return $"Imported {carId}. Validation: {CarWindow.Summary(findings)}";
    });

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCar() is not { } carId) return;
        if (AskWindow.Ask(this, "Rename", $"New car ID for {carId} (lowercase letters, digits and _). The old folder goes to the trash.", carId, "Rename") is not { } newId || newId == carId) return;
        Try("Rename", () =>
        {
            var (result, trashId, error) = _workspace!.RenameCar(carId, newId);
            if (result == null) return error;
            var notes = result.Notes.Concat(result.MentionsOfOldId.Count > 0 ? [$"These files still mention {carId}: {string.Join(", ", result.MentionsOfOldId)}"] : []);
            return string.Join("\n\n", new[] { $"Renamed {carId} to {result.CarId}; the old folder is in the trash as {trashId}." }.Concat(notes));
        });
    }

    /// <summary>WS-6: delete moves the car to the trash, where it can be restored.</summary>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCar() is not { } carId) return;
        if (MessageBox.Show(this, $"Move {carId} to the trash? You can restore it from Workspace > Trash.", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Try("Delete", () =>
        {
            var (trashId, error) = _workspace!.DeleteCar(carId);
            return error ?? $"Moved {carId} to the trash as {trashId}.";
        });
    }

    private void Trash_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null) return;
        new TrashWindow(_workspace) { Owner = this }.ShowDialog();
        Refresh();
    }

    private void Snapshots_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCar() is not { } carId) return;
        new SnapshotsWindow(_workspace!, carId) { Owner = this }.ShowDialog();
        Refresh();
    }

    private void WorkspaceFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace != null) Launch.Folder(_workspace.Root);
    }

    private void KsEditor_Click(object sender, RoutedEventArgs e) => Status.Text = Launch.KsEditor(_workspace?.AcFolder) ?? "Started ksEditor.";

    private void CarFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCar() is { } carId) Launch.Folder(Path.Combine(_workspace!.Root, "cars", carId));
    }

    private void SdkFolder_Click(object sender, RoutedEventArgs e)
    {
        var sdk = _workspace?.AcFolder == null ? null : Path.Combine(_workspace.AcFolder, "sdk");
        if (sdk != null && Directory.Exists(sdk)) Launch.Folder(sdk);
        else Status.Text = "AC's sdk folder wasn't found.";
    }

    /// <summary>WS-12: Steam starts AC by its app ID, 244210 (docs/VERIFIED_FACTS.md).</summary>
    private void StartAc_Click(object sender, RoutedEventArgs e) => Launch.Open("steam://rungameid/" + AcInstall.SteamAppId);

    /// <summary>WS-12, WS-14: Content Manager from the path the user chose once (owner decision, 2026-11-03).</summary>
    private void StartCm_Click(object sender, RoutedEventArgs e)
    {
        var path = Settings.Load(Settings.DefaultPath).Settings.ContentManagerPath;
        if (path == null || !File.Exists(path))
        {
            Status.Text = "Choose Content Manager first: Tools > Choose Content Manager...";
            return;
        }

        Launch.Open(path);
    }

    private void ChooseCm_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Find Content Manager.exe", Filter = "Programs (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) != true) return;
        var (settings, _) = Settings.Load(Settings.DefaultPath);
        (settings with { ContentManagerPath = dialog.FileName }).Save(Settings.DefaultPath);
        Status.Text = $"Content Manager: {dialog.FileName}";
    }

    private void Checklist_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null || CarGrid.SelectedItem is not CarRow row)
        {
            Status.Text = "Choose a car in the list first.";
            return;
        }

        new ChecklistWindow(_workspace, row.Id) { Owner = this }.Show(); // non-modal: keep it open while you work
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace != null) new LogWindow(_workspace) { Owner = this }.Show(); // non-modal: it can stay open beside the car list
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
        if (!ConfirmDespiteErrors(row.Id, "Install")) return;

        try
        {
            // ponytail: runs on the UI thread, so the window freezes while a large car copies; move to Task.Run if that bothers.
            var (installed, replaced, error) = _workspace.InstallToAc(row.Id, ignoreErrors: true); // the user has seen any errors above
            var message = error ?? $"Installed to {installed}" + (replaced == null ? "" : $"\n\nThe previous install is in the trash as {replaced}.");
            MessageBox.Show(this, message, "Install to AC", MessageBoxButton.OK, error == null ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Install to AC", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }

    /// <summary>
    /// PK-4 and the install gate (owner decision, 2026-11-03): validation errors block, unless the user explicitly continues. Warnings and infos never block.
    /// </summary>
    private bool ConfirmDespiteErrors(string carId, string action)
    {
        var errors = _workspace?.Validate(carId).Findings?.Where(f => f.Severity == AcMod.Core.Validation.Severity.Error).ToList() ?? [];
        if (errors.Count == 0) return true;
        var list = string.Join("\n", errors.Take(8).Select(f => $"  {f.Rule}  {f.Message}")) + (errors.Count > 8 ? $"\n  ...and {errors.Count - 8} more" : "");
        var question = $"Validation found {errors.Count} error(s) in {carId}:\n\n{list}\n\nThe car may not load or may misbehave in AC. {action} anyway?";
        return MessageBox.Show(this, question, $"{action}: validation errors", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

    private static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
