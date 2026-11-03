using AcMod.Core.Formats;

namespace AcMod.Core.Tuning;

/// <summary>
/// New values for several rows of a .lut file at once (TU-8). Each returns row index → new value, for <see cref="Workspaces.CarEdit.SetLutValues"/>.
/// Scale and Add round to 6 decimals, only to drop floating-point noise (200 × 1.05 is 210.00000000000003 in a double).
/// Drag rounds to 0.1, as the owner chose (2026-11-03), because a mouse can't place a value more precisely than that.
/// </summary>
public static class CurveEdits
{
    /// <summary>Each selected row's value × (1 + percent / 100), so +5 means 5% more.</summary>
    public static Dictionary<int, double> Scale(IReadOnlyList<LutRow> rows, IEnumerable<int> selected, double percent) =>
        selected.Distinct().ToDictionary(i => i, i => Math.Round(rows[i].Value * (1 + percent / 100), 6));

    /// <summary>Each selected row's value + amount.</summary>
    public static Dictionary<int, double> Add(IReadOnlyList<LutRow> rows, IEnumerable<int> selected, double amount) =>
        selected.Distinct().ToDictionary(i => i, i => Math.Round(rows[i].Value + amount, 6));

    /// <summary>Each selected row's value moved by the same amount, rounded to 0.1, as a drag on the chart does.</summary>
    public static Dictionary<int, double> Drag(IReadOnlyList<LutRow> rows, IEnumerable<int> selected, double delta) =>
        selected.Distinct().ToDictionary(i => i, i => Math.Round(rows[i].Value + delta, 1, MidpointRounding.AwayFromZero));
}
