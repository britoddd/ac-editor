using AcMod.Core.Validation;

namespace AcMod.Core.Learning;

/// <summary>What to do in Blender or ksEditor (or another tool) for a finding the dashboard can't fix (LE-3), and where those steps come from.</summary>
public sealed record OutsideStep(string Tools, string Steps, string Source);

/// <summary>
/// Steps outside the dashboard for the validator findings that need them (LE-3). Each comes from this project's own M0 work recorded in
/// docs/VERIFIED_FACTS.md, plus the community first-car guide where noted. Findings the dashboard can fix itself have none.
/// </summary>
public static class OutsideSteps
{
    private const string FbxSettings = "Apply Scalings = FBX All and Apply Transform on";

    public static OutsideStep? For(Finding finding) => finding.Rule switch
    {
        "V-04" => new OutsideStep("Blender, then ksEditor",
            "In Blender, make a simple low-poly shape around the body as its own object and export only it as collider.fbx " +
            $"({FbxSettings}). In ksEditor, import collider.fbx and use File > Save KN5 > Car to save collider.kn5 in the car folder.",
            "docs/VERIFIED_FACTS.md: \"Blender FBX export settings for ksEditor\" and \"KN5 written by ksEditor\"; the tutorial car was built this way; community first-car guide"),
        "V-02" => new OutsideStep("ksEditor, or the Raw files tab",
            $"Export the missing model: in ksEditor, import its FBX ({FbxSettings} in Blender) and use File > Save KN5 > Car with the name lods.ini gives " +
            "(case doesn't matter), in the car folder. If the car has no such level of detail, remove that [LOD_n] section on the Raw files tab instead.",
            "docs/VERIFIED_FACTS.md: \"KN5 written by ksEditor\"; docs/REQUIREMENTS.md, M0 verdict V-02"),
        "V-08" => new OutsideStep("ksEditor (to look), Blender or the Raw files tab (to fix)",
            "Open the car's KN5 in ksEditor and look in its node tree for the OBJECT_NAME values digital_instruments.ini uses. Rename the objects in " +
            "Blender and export again, or change the names on the Raw files tab. AC still loads the car when they don't match; the instruments just don't show.",
            "docs/REQUIREMENTS.md, M0 verdict V-08 (tested in game); docs/VERIFIED_FACTS.md: ksEditor's node tree"),
        "V-09" when finding.File?.EndsWith(".bank", StringComparison.OrdinalIgnoreCase) == true => new OutsideStep("File Explorer, or FMOD Studio",
            "Copy the .bank of a car with a similar engine into sfx\\ and rename it to this car's ID; GUIDs.txt must name the same ID (Save as new car does both). " +
            "That's how the tutorial car got its sound. A sound of your own needs FMOD Studio and Kunos's audio pipeline (a PDF in your AC's sdk\\audio folder).",
            "docs/VERIFIED_FACTS.md: \"Car sound files\""),
        "V-14" => new OutsideStep("ksEditor, by the model's author",
            "Only the model's author can fix this, by saving the original model again from ksEditor without encryption. This app never decrypts or works around protection.",
            "docs/VERIFIED_FACTS.md: the CSP wiki on KN5 encryption"),
        _ => null,
    };
}
