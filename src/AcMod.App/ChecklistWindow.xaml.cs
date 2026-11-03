using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AcMod.Core.Learning;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>A button on a checklist row that takes the user where the step's work happens (LE-2).</summary>
public sealed record ChecklistAction(string Label, Action Run);

/// <summary>One checklist row. Ticking it hands the change back to the window, which saves it.</summary>
public sealed class ChecklistRow(ChecklistStatus status, IReadOnlyList<ChecklistAction> actions, Action<string, bool> setTick)
{
    public string Title => status.Item.Title;

    public string State => status.State;

    public string Steps => status.Item.Steps;

    public string Source => status.Item.Source;

    public string Blocking => string.Join("\n", status.Blocking.Select(f => $"{f.Rule}: {f.Message}"));

    public IReadOnlyList<ChecklistAction> Actions => actions;

    /// <summary>True for steps the user ticks: manual steps, and a copied model or collider once it's the user's own.</summary>
    public bool IsManual => status.TickId != null;

    public bool Ticked
    {
        get => status.Ticked;
        set
        {
            if (status.TickId != null) setTick(status.TickId, value);
        }
    }
}

/// <summary>
/// The first-car checklist for one car (LE-2): live state from the validator where it can check, ticks where it can't, a note on steps copied from
/// another car, and buttons that open the place each step's work happens.
/// </summary>
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

    private string CarFolder => Path.Combine(_workspace.Root, "cars", _carId);

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var findings = _workspace.Validate(_carId).Findings ?? [];
        var copiedFrom = _workspace.CopiedFrom(_carId);
        var statuses = FirstCarChecklist.For(findings, _workspace.ChecklistTicks(_carId), copiedFrom);
        ItemGrid.ItemsSource = statuses.Select(s => new ChecklistRow(s, ActionsFor(s), SetTick)).ToList();
        Summary.Text = $"{statuses.Count(s => s.State.StartsWith("Done"))} of {statuses.Count} steps done." +
                       (copiedFrom == null ? "" : $"  This car was copied from {copiedFrom}: replace its model and collider with your own.");
    }

    private IReadOnlyList<ChecklistAction> ActionsFor(ChecklistStatus status)
    {
        var folder = new ChecklistAction("Open car folder", () => Launch.Folder(CarFolder));
        var ksEditor = new ChecklistAction("Open ksEditor", () => Report(Launch.KsEditor(_workspace.AcFolder)));
        var actions = status.Item.Id switch
        {
            "folders" or "dummies" or "fbx" => new List<ChecklistAction> { folder },
            "kseditor" or "collider" => [ksEditor, folder],
            "data" => [new("Copy data from...", CopyData), new("Open Forms", () => OpenCar(null))],
            "sound" => [new("Copy sound from...", CopySound)],
            _ => [],
        };
        if (status.Blocking.Count > 0) actions.Add(new ChecklistAction("Open Validate", () => OpenCar("Validate")));
        return actions;
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChecklistAction action }) action.Run();
    }

    /// <summary>Data: the other workspace cars, and Kunos's formula_k sample from the SDK (owner decision, 2026-11-03).</summary>
    private void CopyData()
    {
        var choices = OtherWorkspaceCars();
        if (_workspace.AcFolder != null && Directory.Exists(FieldHelp.SampleCarFolder(_workspace.AcFolder)))
        {
            choices.Insert(0, new PickChoice("formula_k (Kunos's sample car in the SDK)", FieldHelp.SampleCarFolder(_workspace.AcFolder)));
        }

        if (Pick("Copy data from...", $"Copy which car's data files into {_carId}? A snapshot is taken first, so this can be undone.", choices) is not { } source) return;
        var (copied, error) = _workspace.CopyDataFrom(_carId, source.Folder);
        Report(error ?? $"Copied {copied} data files from {source.Label}. Tune them on the Forms tab.");
    }

    /// <summary>Sound: the other workspace cars and the cars installed in AC; the bank and its GUIDs lines move to this car's ID.</summary>
    private void CopySound()
    {
        var choices = OtherWorkspaceCars();
        if (_workspace.AcFolder != null)
        {
            choices.AddRange(Directory.GetDirectories(Path.Combine(_workspace.AcFolder, "content", "cars"))
                .Where(d => Directory.Exists(Path.Combine(d, "sfx")))
                .Select(d => new PickChoice($"{Path.GetFileName(d)} (installed in AC)", d)));
        }

        if (Pick("Copy sound from...", $"Copy which car's engine sound into {_carId}? Its bank is copied and renamed, and GUIDs.txt is pointed at {_carId}.", choices) is not { } source) return;
        Report(_workspace.CopySoundFrom(_carId, source.Folder) ?? $"Copied the sound from {source.Label}.");
    }

    private List<PickChoice> OtherWorkspaceCars() =>
        [.. _workspace.Cars().Where(c => c.Id != _carId).Select(c => new PickChoice($"{c.Id} (workspace)", Path.Combine(_workspace.Root, "cars", c.Id)))];

    private PickChoice? Pick(string title, string prompt, IReadOnlyList<PickChoice> choices) =>
        new PickWindow(title, prompt, choices) { Owner = this } is var dialog && dialog.ShowDialog() == true ? dialog.Picked : null;

    private void OpenCar(string? tab)
    {
        var (edit, error) = _workspace.OpenCar(_carId);
        if (edit == null)
        {
            Report(error);
            return;
        }

        new CarWindow(edit, tab) { Owner = this }.ShowDialog();
        Refresh();
    }

    private void Report(string? message)
    {
        if (message != null) MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        Refresh();
    }

    private void SetTick(string tickId, bool ticked)
    {
        if (_workspace.SetChecklistTick(_carId, tickId, ticked) is { } error) MessageBox.Show(this, error, "Checklist", MessageBoxButton.OK, MessageBoxImage.Warning);
        Dispatcher.BeginInvoke(Refresh); // after the check box finishes its click
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { NavigateUri: { } uri }) Launch.Open(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri);
    }
}
