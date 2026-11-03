using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using AcMod.Core.Learning;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>One checklist row. Ticking a manual item hands the change back to the window, which saves it.</summary>
public sealed class ChecklistRow(ChecklistStatus status, bool ticked, Action<string, bool> setTick)
{
    public string Title => status.Item.Title;

    public string State => status.State;

    public string Steps => status.Item.Steps;

    public string Source => status.Item.Source;

    public string Blocking => string.Join("\n", status.Blocking.Select(f => $"{f.Rule}: {f.Message}"));

    public bool IsManual => status.Item.Rules.Count == 0;

    public bool Ticked
    {
        get => IsManual ? ticked : status.Blocking.Count == 0;
        set
        {
            if (IsManual) setTick(status.Item.Id, value);
        }
    }
}

/// <summary>The first-car checklist for one car (LE-2): live state from the validator where it can check, ticks by hand where it can't.</summary>
public partial class ChecklistWindow : Window
{
    private readonly Workspace _workspace;
    private readonly string _carId;

    public ChecklistWindow(Workspace workspace, string carId)
    {
        InitializeComponent();
        (_workspace, _carId) = (workspace, carId);
        Title = $"{carId} - first-car checklist";
        var pdf = workspace.AcFolder == null ? null : Path.Combine(workspace.AcFolder, "sdk", "dev", "car_pipeline_2.0rev", "AC_Pipeline_PUB_Rev2.0.pdf");
        if (pdf != null && File.Exists(pdf)) PipelineLink.NavigateUri = new Uri(pdf);
        else PipelineLink.IsEnabled = false;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var findings = _workspace.Validate(_carId).Findings ?? [];
        var ticks = _workspace.ChecklistTicks(_carId);
        var statuses = FirstCarChecklist.For(findings, ticks);
        ItemGrid.ItemsSource = statuses.Select(s => new ChecklistRow(s, ticks.Contains(s.Item.Id), SetTick)).ToList();
        Summary.Text = $"{statuses.Count(s => s.State.StartsWith("Done"))} of {statuses.Count} steps done.";
    }

    private void SetTick(string itemId, bool ticked)
    {
        if (_workspace.SetChecklistTick(_carId, itemId, ticked) is { } error) MessageBox.Show(this, error, "Checklist", MessageBoxButton.OK, MessageBoxImage.Warning);
        Dispatcher.BeginInvoke(Refresh); // after the check box finishes its click
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { NavigateUri: { } uri }) Process.Start(new ProcessStartInfo(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri) { UseShellExecute = true });
    }
}
