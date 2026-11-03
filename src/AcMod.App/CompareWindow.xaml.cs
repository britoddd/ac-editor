using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AcMod.Core.Acd;
using AcMod.Core.Formats;
using AcMod.Core.Workspaces;

namespace AcMod.App;

/// <summary>One choice in a version list: the car's current files (no snapshot ID) or one of its snapshots. The list shows Label.</summary>
public sealed record VersionItem(string? SnapshotId, string Label)
{
    public override string ToString() => Label;
}

/// <summary>One difference, formatted for the table.</summary>
public sealed record DiffRow(string File, string Field, string Left, string Right);

/// <summary>Compares the data of two snapshots, two cars, or a car with one of its snapshots, field by field (TU-6).</summary>
public partial class CompareWindow : Window
{
    private readonly Workspace _workspace;

    public CompareWindow(Workspace workspace, string? carId)
    {
        InitializeComponent();
        _workspace = workspace;
        var cars = workspace.Cars().Select(c => c.Id).ToList();
        LeftCar.ItemsSource = cars;
        RightCar.ItemsSource = cars;
        LeftCar.SelectedItem = RightCar.SelectedItem = carId ?? cars.FirstOrDefault();
        if (LeftVersion.Items.Count > 1) LeftVersion.SelectedIndex = 1; // by default: the newest snapshot against the current files
    }

    private void LeftCar_SelectionChanged(object sender, SelectionChangedEventArgs e) => FillVersions(LeftCar, LeftVersion);

    private void RightCar_SelectionChanged(object sender, SelectionChangedEventArgs e) => FillVersions(RightCar, RightVersion);

    /// <summary>"current files" first, then the car's snapshots, newest first, labelled with their local time.</summary>
    private void FillVersions(ComboBox car, ComboBox version)
    {
        if (car.SelectedItem is not string carId)
        {
            version.ItemsSource = null;
            return;
        }

        var snapshots = _workspace.Snapshots(carId).Reverse().Select(id => new VersionItem(id, $"snapshot {Local(id)}   ({id})"));
        version.ItemsSource = new[] { new VersionItem(null, "current files") }.Concat(snapshots).ToList();
        version.SelectedIndex = 0;
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        var (left, leftError) = Load(LeftCar, LeftVersion);
        var (right, rightError) = Load(RightCar, RightVersion);
        DiffGrid.ItemsSource = null;
        if (left == null || right == null)
        {
            Summary.Text = leftError ?? rightError;
            return;
        }

        if (left.Error != null || right.Error != null)
        {
            Summary.Text = $"Can't compare, because {(left.Error != null ? "the left" : "the right")} side's data can't be read: {left.Error ?? right.Error}";
            return;
        }

        var changes = DataDiff.Compare(left.Files, right.Files);
        DiffGrid.ItemsSource = changes.Select(c => c switch
        {
            { Before: null, After: null } => new DiffRow(c.File, "(comments, spacing, or line breaks)", "differs", "differs"),
            _ => new DiffRow(c.File, c.Field, c.Before ?? "(none)", c.After ?? "(none)"),
        }).ToList();
        Summary.Text = (changes.Count == 0 ? "No differences in the data files." : $"{changes.Count} difference(s) in {changes.Select(c => c.File).Distinct().Count()} file(s).") +
                       $"  Left reads {Source(left)}, right reads {Source(right)}.";
    }

    /// <summary>The data a side points at: a car's current files or one of its snapshots.</summary>
    private (CarData? Data, string? Error) Load(ComboBox car, ComboBox version)
    {
        if (car.SelectedItem is not string carId || version.SelectedItem is not VersionItem item) return (null, "Choose a car and a version on both sides.");
        if (item.SnapshotId != null) return _workspace.SnapshotData(carId, item.SnapshotId);
        var (edit, error) = _workspace.OpenCar(carId);
        return (edit?.Data, error);
    }

    private static string Source(CarData data) => data.Source == DataSource.Acd ? "data.acd" : "the data folder";

    private static string Local(string snapshotId) =>
        Workspace.SnapshotTimeUtc(snapshotId)?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? snapshotId;
}
