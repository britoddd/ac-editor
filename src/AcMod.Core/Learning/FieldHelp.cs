using System.Text.RegularExpressions;
using AcMod.Core.Acd;
using AcMod.Core.Tuning;

namespace AcMod.Core.Learning;

/// <summary>Where an explanation came from (LE-1).</summary>
public enum HelpSource
{
    /// <summary>A comment in the car's own file (on this line, or on the same key in another section).</summary>
    ThisFile,

    /// <summary>A comment on the same key in Kunos's SDK sample car formula_k.</summary>
    KunosSample,

    /// <summary>A note from this project's own tests, recorded in docs/VERIFIED_FACTS.md.</summary>
    VerifiedNotes,
}

/// <summary>One explanation of a field, and its source as the user sees it.</summary>
public sealed record Explanation(string Text, HelpSource Source, string SourceLabel);

/// <summary>
/// Field explanations for the guided forms (LE-1), each labelled with its source, in this order: the car file's own comment; the same key's comment in
/// Kunos's SDK sample car formula_k (read from the user's AC install at run time, never copied into this repo); and short notes on fields this project
/// tested, citing docs/VERIFIED_FACTS.md. No explanation is written from memory. The M7 step 1 survey of 230 installed cars found that formula_k adds
/// explanations for many fields (for example 6,497 suspensions.ini fields), and that many tyres.ini and brakes.ini fields still have none.
/// </summary>
public sealed class FieldHelp
{
    private static readonly (string File, string? Section, Regex Key, string Text, string Entry)[] Notes =
    [
        ("engine.ini", "ENGINE_DATA", Exact("LIMITER"), "The engine cuts at this rpm: lowered from 6500 to 5500, AC cut at 5500 in game, and the Gears tab's speeds follow it.", "docs/VERIFIED_FACTS.md, \"Tuned limiter in game\""),
        ("engine.ini", "HEADER", Exact("POWER_CURVE"), "Names the torque curve. Peak torque × rpm × 2π/60 × (1 + MAX_BOOST) matched the power AC logs within 0.1% on the tutorial car. 4 of 230 installed cars use a name other than power.lut.", "docs/VERIFIED_FACTS.md, \"Validator rule checks by the owner\" and \"Gear, limiter, turbo and tyre fields\""),
        ("engine.ini", null, new Regex("^MAX_BOOST$"), "With one turbo, AC's logged power matches the curve's torque × (1 + MAX_BOOST). Adding several turbos' boosts is not verified.", "docs/VERIFIED_FACTS.md, \"Validator rule checks by the owner\" and \"Gear, limiter, turbo and tyre fields\""),
        ("engine.ini", "COAST_CURVE", Exact("FILENAME"), "AC loads cars whose coast.lut named here is missing, so this obsolete setting isn't checked (V-07).", "docs/REQUIREMENTS.md, M0 verdict V-07"),
        ("drivetrain.ini", "GEARS", new Regex(@"^GEAR_\d+$"), "A gear ratio. If setup.ini lists ratios for this gear ([GEAR_n] RATIOS=...), AC's garage replaces a ratio that isn't in the list with the list's first option.", "docs/VERIFIED_FACTS.md, \"Added gear and off-list ratios in game\""),
        ("drivetrain.ini", "GEARS", Exact("FINAL"), "The final drive ratio, applied to every gear. If setup.ini lists final drives ([FINAL_GEAR_RATIO] RATIOS=...), a value not in that list is likely replaced the same way as a gear ratio (seen for gears, not tested for FINAL).", "docs/VERIFIED_FACTS.md, \"Added gear and off-list ratios in game\""),
        ("drivetrain.ini", "GEARS", Exact("COUNT"), "The number of forward gears, with one GEAR_n line each. AC loaded a 7th gear added this way (log: ADDING GEAR:7).", "docs/VERIFIED_FACTS.md, \"Added gear and off-list ratios in game\""),
        ("drivetrain.ini", "TRACTION", Exact("TYPE"), "Installed cars use RWD (193), AWD2 (16), FWD (11) or AWD (10). What AWD2 changes compared with AWD is not verified.", "docs/VERIFIED_FACTS.md, \"Gear, limiter, turbo and tyre fields\""),
        ("tyres.ini", "COMPOUND_DEFAULT", Exact("INDEX"), "The default tyre compound: 0 means [FRONT]/[REAR], n means [FRONT_n]/[REAR_n]. That reading fits 229 of 230 installed cars but is not verified in game.", "docs/VERIFIED_FACTS.md, \"Gear, limiter, turbo and tyre fields\""),
        ("tyres.ini", null, Exact("RADIUS"), "Tyre radius in metres. On the AWD tutorial car (front 0.275, rear 0.285), the garage's top speeds matched the rear radius.", "docs/VERIFIED_FACTS.md, \"Added gear and off-list ratios in game\""),
        ("car.ini", "INFO", Exact("SCREEN_NAME"), "Where this name shows in game is not verified: a change to it wasn't seen in game.", "docs/VERIFIED_FACTS.md, \"Core-packed data.acd in game\""),
    ];

    private readonly Dictionary<string, Dictionary<string, string>> _sample;

    private FieldHelp(Dictionary<string, Dictionary<string, string>> sample) => _sample = sample;

    /// <summary>formula_k's folder in an AC install: sdk\dev\content\cars\formula_k.</summary>
    public static string SampleCarFolder(string acFolder) => Path.Combine(acFolder, "sdk", "dev", "content", "cars", "formula_k");

    /// <summary>Reads formula_k's comments from its folder; null or a missing folder gives explanations without that source.</summary>
    public static FieldHelp Load(string? sampleCarFolder)
    {
        var sample = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (sampleCarFolder != null && Directory.Exists(sampleCarFolder) && CarData.Load(sampleCarFolder) is { Error: null } data)
        {
            foreach (var (_, file) in TuningForms.Groups)
            {
                var entry = data.Files.FirstOrDefault(f => f.Name.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (entry == null) continue;
                sample[file] = TuningForms.Fields(entry.Data).Where(f => f.Hint != null).GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First().Hint!);
            }
        }

        return new FieldHelp(sample);
    }

    /// <summary>Every explanation for a field, in source order. Empty when no source explains it.</summary>
    public IReadOnlyList<Explanation> For(string file, FormField field)
    {
        var explanations = new List<Explanation>();
        if (field.Hint != null) explanations.Add(new Explanation(field.Hint, HelpSource.ThisFile, field.HintFrom == null ? "this file" : $"this file, {field.HintFrom}"));

        if (_sample.TryGetValue(file, out var sample) && sample.TryGetValue(field.Key, out var kunos) && kunos != field.Hint)
        {
            explanations.Add(new Explanation(kunos, HelpSource.KunosSample, "Kunos SDK sample formula_k"));
        }

        explanations.AddRange(Notes
            .Where(n => n.File.Equals(file, StringComparison.OrdinalIgnoreCase) && (n.Section == null || n.Section == field.Section) && n.Key.IsMatch(field.Key))
            .Select(n => new Explanation(n.Text, HelpSource.VerifiedNotes, $"tested here: {n.Entry}")));
        return explanations;
    }

    private static Regex Exact(string key) => new($"^{Regex.Escape(key)}$");
}
