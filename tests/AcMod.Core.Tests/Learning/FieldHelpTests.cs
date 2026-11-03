using System.Text;
using AcMod.Core.Learning;
using AcMod.Core.Tuning;

namespace AcMod.Core.Tests.Learning;

public sealed class FieldHelpTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_fieldhelp_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    /// <summary>A stand-in for formula_k with one commented engine.ini.</summary>
    private string Sample()
    {
        var data = Directory.CreateDirectory(Path.Combine(_temp, "formula_k", "data")).FullName;
        File.WriteAllText(Path.Combine(data, "engine.ini"), "[ENGINE_DATA]\r\nINERTIA=0.1 ; engine inertia\r\nLIMITER=9000 ; engine rev limiter. 0 no limiter\r\n");
        return Path.Combine(_temp, "formula_k");
    }

    private static FormField Field(string ini, string id) => TuningForms.Fields(Encoding.UTF8.GetBytes(ini)).Single(f => f.Id == id);

    [Fact]
    public void Explanations_come_in_source_order_with_their_labels()
    {
        var help = FieldHelp.Load(Sample());
        var field = Field("[ENGINE_DATA]\r\nLIMITER=6500 ; my own note\r\n", "[ENGINE_DATA] LIMITER");

        var explanations = help.For("engine.ini", field);

        Assert.Equal([HelpSource.ThisFile, HelpSource.KunosSample, HelpSource.VerifiedNotes], explanations.Select(e => e.Source));
        Assert.Equal("my own note", explanations[0].Text);
        Assert.Equal("Kunos SDK sample formula_k", explanations[1].SourceLabel);
        Assert.Contains("docs/VERIFIED_FACTS.md, \"Tuned limiter in game\"", explanations[2].SourceLabel);
    }

    [Fact]
    public void An_uncommented_field_gets_the_Kunos_sample_comment()
    {
        var explanations = FieldHelp.Load(Sample()).For("engine.ini", Field("[ENGINE_DATA]\r\nINERTIA=0.2\r\n", "[ENGINE_DATA] INERTIA"));

        var only = Assert.Single(explanations);
        Assert.Equal(("engine inertia", HelpSource.KunosSample), (only.Text, only.Source));
    }

    [Fact]
    public void Without_the_SDK_only_the_file_and_the_project_notes_explain()
    {
        var help = FieldHelp.Load(Path.Combine(_temp, "no_sdk_here"));

        Assert.Empty(help.For("engine.ini", Field("[ENGINE_DATA]\r\nINERTIA=0.2\r\n", "[ENGINE_DATA] INERTIA")));
        Assert.Equal([HelpSource.VerifiedNotes], help.For("drivetrain.ini", Field("[GEARS]\r\nGEAR_3=1.4\r\n", "[GEARS] GEAR_3")).Select(e => e.Source));
        Assert.Empty(FieldHelp.Load(null).For("brakes.ini", Field("[DATA]\r\nMAX_TORQUE=2000\r\n", "[DATA] MAX_TORQUE")));
    }

    [Fact]
    public void A_note_applies_only_to_its_own_file_and_section()
    {
        var help = FieldHelp.Load(null);

        Assert.Empty(help.For("setup.ini", Field("[GEARS]\r\nFINAL=3\r\n", "[GEARS] FINAL"))); // the FINAL note is for drivetrain.ini
        Assert.Empty(help.For("car.ini", Field("[OTHER]\r\nSCREEN_NAME=x\r\n", "[OTHER] SCREEN_NAME"))); // and for [INFO] only
    }

    [Fact]
    public void The_real_formula_k_explains_the_limiter()
    {
        var formulaK = Path.Combine(Fixtures.LocalRoot, "formula_k");
        if (!Directory.Exists(formulaK)) return; // gitignored Kunos sample, missing on a fresh clone

        var kunos = FieldHelp.Load(formulaK).For("engine.ini", Field("[ENGINE_DATA]\r\nLIMITER=6500\r\n", "[ENGINE_DATA] LIMITER"))
            .Single(e => e.Source == HelpSource.KunosSample);

        Assert.Contains("limiter", kunos.Text, StringComparison.OrdinalIgnoreCase);
    }
}
