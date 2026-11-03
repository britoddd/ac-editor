using AcMod.Core.Validation;

namespace AcMod.Core.Learning;

/// <summary>
/// One checklist item (LE-2). <paramref name="Rules"/> are the validator rules that decide it automatically; an item with none is ticked by hand,
/// because version 1 can't check it (for example the object names inside a KN5).
/// </summary>
public sealed record ChecklistItem(string Id, string Title, string Steps, string Source, IReadOnlyList<string> Rules);

/// <summary>
/// An item's state for one car, the findings that block it, and the id the user ticks for it (the item itself for manual items, "own:&lt;item&gt;" for a
/// copied model or collider), or null when the validator alone decides.
/// </summary>
public sealed record ChecklistStatus(ChecklistItem Item, string State, IReadOnlyList<Finding> Blocking, string? TickId, bool Ticked);

/// <summary>
/// The first-car checklist (LE-2): folders, dummies, FBX export, ksEditor, collider, data, and sound. The steps come from this project's M0 work
/// (docs/VERIFIED_FACTS.md) and the community first-car guide; Kunos's car pipeline PDF in the AC SDK has the full lists but can only be linked.
/// </summary>
public static class FirstCarChecklist
{
    public static readonly IReadOnlyList<ChecklistItem> Items =
    [
        new("folders", "Car folder",
            "Name the folder with the car ID in lowercase letters, digits and _. Inside it: data\\ (or data.acd), ui\\ with ui_car.json and badge.png, sfx\\, and skins\\<name>\\.",
            "docs/REQUIREMENTS.md, M0 verdicts V-01, V-10, V-11", ["V-01", "V-10", "V-11"]),
        new("dummies", "Model objects (dummies)",
            "In Blender, name the wheels WHEEL_LF, WHEEL_RF, WHEEL_LR and WHEEL_RR, as the tutorial car does, and keep the body as its own object. " +
            "With the export settings below, the car's nose points to Blender's -Y and its left side to +X. Kunos's car pipeline PDF lists every dummy a car can use.",
            "docs/VERIFIED_FACTS.md: \"KN5 written by ksEditor\" and \"AC model axes\"; Kunos car pipeline PDF (SDK)", []),
        new("fbx", "FBX export from Blender",
            "File > Export > FBX with Apply Scalings = FBX All and Apply Transform on. Blender writes FBX 7.4, the version ksEditor reads; without these settings ksEditor reports FOUND SCALING ON NODE.",
            "docs/VERIFIED_FACTS.md: \"FBX export format\" and \"Blender FBX export settings for ksEditor\"", []),
        new("kseditor", "ksEditor model",
            "Import the FBX, check the node tree and the materials (ksPerPixel with a txDiffuse texture), then File > Save KN5 > Car into the car folder with the name lods.ini gives.",
            "docs/VERIFIED_FACTS.md: \"KN5 written by ksEditor\"; docs/REQUIREMENTS.md, M0 verdict V-02", ["V-02", "V-14", "V-05:lods.ini"]), // no lods.ini: no model yet
        new("collider", "Collider",
            "Export a simple low-poly shape of the body on its own and save it from ksEditor as collider.kn5 in the car folder.",
            "docs/REQUIREMENTS.md, M0 verdict V-04; the tutorial car; community first-car guide", ["V-04"]),
        new("data", "Physics data",
            "Start from a similar car's data (Save as new car copies a whole car), then tune it on the Forms, Power curve and Gears tabs. Every file the validator lists as missing must exist.",
            "docs/REQUIREMENTS.md, M0 verdicts V-05, V-06, V-07, V-12, V-13", ["V-05", "V-06", "V-07", "V-12", "V-13"]),
        new("sound", "Sound",
            "Copy a bank with a similar engine into sfx\\ named <car ID>.bank, and a GUIDs.txt naming the same ID. Save as new car renames both for you.",
            "docs/VERIFIED_FACTS.md: \"Car sound files\"; docs/REQUIREMENTS.md, M0 verdict V-09", ["V-09"]),
    ];

    /// <summary>
    /// Steps whose files exist in a copied car but aren't the user's work yet: the validator can't tell a copied model from a new one,
    /// so after Save as new car these wait for the user's own tick (owner report, 2026-11-03).
    /// </summary>
    public static readonly string[] CopySensitive = ["kseditor", "collider"];

    /// <summary>A rule such as "V-04", or "V-05:lods.ini" for that rule's finding about one file only.</summary>
    private static bool Matches(string rule, Finding finding) =>
        rule.Split(':') is [var id, var file] ? finding.Rule == id && string.Equals(finding.File, file, StringComparison.OrdinalIgnoreCase) : finding.Rule == rule;

    /// <summary>Each item's state for a car, from its validator findings, its manual ticks, and the car it was copied from, if any.</summary>
    public static IReadOnlyList<ChecklistStatus> For(IReadOnlyList<Finding> findings, IReadOnlySet<string> ticked, string? copiedFrom = null) =>
    [
        .. Items.Select(item =>
        {
            if (item.Rules.Count == 0)
            {
                var done = ticked.Contains(item.Id);
                return new ChecklistStatus(item, done ? "Done (ticked)" : "To do (tick when done)", [], item.Id, done);
            }

            var blocking = findings.Where(f => f.Severity != Severity.Info && item.Rules.Any(r => Matches(r, f))).ToList();
            if (copiedFrom != null && CopySensitive.Contains(item.Id))
            {
                var own = ticked.Contains("own:" + item.Id);
                var state = blocking.Count > 0 ? "To do" : own ? "Done (your own)" : $"Copied from {copiedFrom}: replace it with your own, then tick";
                return new ChecklistStatus(item, state, blocking, "own:" + item.Id, own);
            }

            return new ChecklistStatus(item, blocking.Count == 0 ? "Done (checked)" : "To do", blocking, null, blocking.Count == 0);
        }),
    ];
}
