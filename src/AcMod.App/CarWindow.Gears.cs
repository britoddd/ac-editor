using System.Globalization;
using AcMod.Core.Tuning;
using static System.FormattableString;

namespace AcMod.App;

/// <summary>One row of the gear table, formatted for display.</summary>
public sealed record GearRow(string Gear, string Ratio, string Front, string Rear);

/// <summary>The gears tab (TU-5): speed in each gear at the rev limiter, from the current (possibly unsaved) data.</summary>
public partial class CarWindow
{
    private void ShowGears()
    {
        var (table, error) = GearTable.FromData(_edit.CurrentFiles);
        if (table == null)
        {
            GearSummary.Text = error;
            GearGrid.ItemsSource = null;
            return;
        }

        var driven = table.DrivenAxle;
        FrontColumn.Header = "Front tyre km/h" + (driven is "front" or "both" ? " (driven)" : "");
        RearColumn.Header = "Rear tyre km/h" + (driven is "rear" or "both" ? " (driven)" : "");
        GearGrid.ItemsSource = table.Gears
            .Select(g => new GearRow(g.Gear.ToString(CultureInfo.InvariantCulture), Invariant($"{g.Ratio}"), Invariant($"{g.FrontKmh:0.0}"), Invariant($"{g.RearKmh:0.0}")))
            .ToList();

        GearSummary.Text =
            Invariant($"Speed at the rev limiter, {table.LimiterRpm:0} rpm, with final drive {table.Final}. ") +
            Invariant($"Traction {table.Traction}; default tyre compound {table.CompoundIndex}: front radius {table.FrontRadius} m, rear {table.RearRadius} m.\n") +
            "Not verified in game: that the compound index picks these tyre sections" +
            (driven == "both" && table.FrontRadius != table.RearRadius ? ", and which radius sets the speed on this all-wheel-drive car" : "") +
            ". Tyres flex and slip, so the speedometer may read a little differently.";
    }
}
