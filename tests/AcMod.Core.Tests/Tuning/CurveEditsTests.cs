using System.Text;
using AcMod.Core.Formats;
using AcMod.Core.Tuning;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Tuning;

public sealed class CurveEditsTests : IDisposable
{
    private const string Lut = "; rpm|torque\r\n0|100\r\n4000|200   ; peak\r\n8000|150\r\n";

    private readonly string _temp = Directory.CreateTempSubdirectory("acm_curveedits_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static IReadOnlyList<LutRow> Rows => LutFile.Parse(Encoding.UTF8.GetBytes(Lut)).Rows;

    [Fact]
    public void Scale_multiplies_the_selected_rows_without_floating_point_noise()
    {
        Assert.Equal(new Dictionary<int, double> { [1] = 210, [2] = 157.5 }, CurveEdits.Scale(Rows, [1, 2, 2], 5)); // a repeated row counts once
    }

    [Fact]
    public void Add_shifts_the_selected_rows()
    {
        Assert.Equal(new Dictionary<int, double> { [0] = 90, [1] = 190 }, CurveEdits.Add(Rows, [0, 1], -10));
    }

    [Fact]
    public void Drag_rounds_to_a_tenth()
    {
        Assert.Equal(new Dictionary<int, double> { [1] = 212.4, [2] = 162.4 }, CurveEdits.Drag(Rows, [1, 2], 12.3871));
    }

    [Fact]
    public void SetLutValues_changes_only_those_numbers_as_one_change()
    {
        var workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;
        var data = Directory.CreateDirectory(Path.Combine(workspace.Root, "cars", "acm_x", "data")).FullName;
        File.WriteAllText(Path.Combine(data, "power.lut"), Lut);
        var edit = workspace.OpenCar("acm_x").Edit!;

        edit.SetLutValues("power.lut", CurveEdits.Scale(Rows, [1, 2], 5));

        Assert.Equal("; rpm|torque\r\n0|100\r\n4000|210   ; peak\r\n8000|157.5\r\n", Encoding.UTF8.GetString(edit.Current("power.lut")));
        Assert.Equal(["power.lut"], edit.ChangedFiles);
        Assert.Equal(2, edit.Changes.Count);
    }
}
