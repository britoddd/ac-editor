using System.Windows;

namespace AcMod.App;

/// <summary>Asks for a new car's ID and name, and whether to start empty (WS-13) or from a complete car (WS-7).</summary>
public partial class NewCarWindow : Window
{
    public NewCarWindow(IReadOnlyList<string> carIds)
    {
        InitializeComponent();
        SourceBox.ItemsSource = carIds;
        if (carIds.Count > 0) SourceBox.SelectedIndex = 0;
        else CompleteBox.IsEnabled = false;
        IdBox.Focus();
    }

    public string CarId => IdBox.Text.Trim();

    public string DisplayName => NameBox.Text.Trim().Length > 0 ? NameBox.Text.Trim() : CarId;

    /// <summary>The car to copy, or null to start empty.</summary>
    public string? Source => CompleteBox.IsChecked == true ? SourceBox.SelectedItem as string : null;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CarId.Length > 0) DialogResult = true;
    }
}
