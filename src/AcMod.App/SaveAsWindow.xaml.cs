using System.Windows;

namespace AcMod.App;

/// <summary>Asks for the new car ID, its display name, and whether to replace the previous car (WS-7, WS-8). Core does the work.</summary>
public partial class SaveAsWindow : Window
{
    public SaveAsWindow(string carId, string? displayName)
    {
        InitializeComponent();
        Intro.Text = $"Copies {carId} to a new car. The sound bank, GUIDs.txt and data.acd follow the new ID; models keep their names.";
        NewIdBox.Text = carId + "_v2";
        NameBox.Text = (displayName ?? carId) + " v2"; // owner decision: suggest the old name + " v2"
        NewIdBox.SelectAll();
        NewIdBox.Focus();
    }

    public string NewId => NewIdBox.Text.Trim();

    public string DisplayName => NameBox.Text.Trim();

    public bool ReplacePrevious => ReplaceBox.IsChecked == true;

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true; // closes the dialog; ShowDialog returns true
}
