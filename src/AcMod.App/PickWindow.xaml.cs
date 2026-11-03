using System.Windows;

namespace AcMod.App;

/// <summary>One choice: what the list shows, and the folder it stands for.</summary>
public sealed record PickChoice(string Label, string Folder);

/// <summary>Asks the user to pick one folder from a list, such as the car to copy data or sound from.</summary>
public partial class PickWindow : Window
{
    public PickWindow(string title, string prompt, IReadOnlyList<PickChoice> choices)
    {
        InitializeComponent();
        Title = title;
        Prompt.Text = prompt;
        Choices.ItemsSource = choices;
        if (choices.Count > 0) Choices.SelectedIndex = 0;
    }

    public PickChoice? Picked => Choices.SelectedItem as PickChoice;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Picked != null) DialogResult = true;
    }
}
