using System.Windows;
using AcMod.Core.Validation;

namespace AcMod.App;

/// <summary>The validate tab (VA-1 to VA-3): every finding for the car's saved files, with its fix and how well the rule is supported.</summary>
public partial class CarWindow
{
    private void RunValidation_Click(object sender, RoutedEventArgs e) => ShowValidation();

    private void ShowValidation()
    {
        var findings = _edit.Validate();
        FindingGrid.ItemsSource = findings
            .OrderByDescending(f => f.Severity)
            .Select(f => new { Severity = f.Severity.ToString(), f.Rule, File = f.File ?? "(car)", f.Message, f.Fix, Evidence = f.Evidence.ToString().ToLowerInvariant() })
            .ToList();
        ValidationSummary.Text = Summary(findings) +
                                 (_edit.ChangedFiles.Count > 0 ? "   This checks the saved files; save first to include your unsaved changes." : "");
    }

    /// <summary>"No findings." or "2 error(s), 1 warning(s), 5 info", for the tab and the car list.</summary>
    public static string Summary(IReadOnlyList<Finding> findings) =>
        findings.Count == 0 ? "No findings."
        : $"{findings.Count(f => f.Severity == Severity.Error)} error(s), {findings.Count(f => f.Severity == Severity.Warning)} warning(s), {findings.Count(f => f.Severity == Severity.Info)} info";

    /// <summary>
    /// M0's rule for V-14: saving physics changes to a car whose model may be encrypted needs an extra confirmation, because CSP's data-protection
    /// mode can stop such a model working once data.acd changes (CSP wiki, unverified here).
    /// </summary>
    private bool ConfirmSaveOnProtectedModel()
    {
        var flagged = _edit.Validate().Where(f => f.Rule == "V-14").Select(f => f.File).ToList();
        if (flagged.Count == 0) return true;
        var question = $"This car's model may be encrypted ({string.Join(", ", flagged)}). The CSP wiki says its protection mode can make such a model stop " +
                       "working when data.acd changes. This is unverified here.\n\nSave anyway? A snapshot is taken first.";
        return MessageBox.Show(this, question, "Model may be encrypted", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
