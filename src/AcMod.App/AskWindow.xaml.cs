using System.Windows;

namespace AcMod.App;

/// <summary>Asks for one line of text, such as a new car ID or the car ID typed to confirm a permanent delete.</summary>
public partial class AskWindow : Window
{
    public AskWindow(string title, string prompt, string answer, string ok)
    {
        InitializeComponent();
        (Title, Prompt.Text, Answer.Text, OkButton.Content) = (title, prompt, answer, ok);
        Answer.SelectAll();
        Answer.Focus();
    }

    public string Text => Answer.Text.Trim();

    /// <summary>Shows the dialog and returns the typed text, or null when cancelled.</summary>
    public static string? Ask(Window owner, string title, string prompt, string answer = "", string ok = "OK") =>
        new AskWindow(title, prompt, answer, ok) { Owner = owner } is var dialog && dialog.ShowDialog() == true ? dialog.Text : null;

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
