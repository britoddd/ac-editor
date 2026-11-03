using AcMod.Core.Learning;
using AcMod.Core.Validation;
using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Learning;

public sealed class FirstCarChecklistTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_checklist_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void It_covers_the_seven_areas_LE2_names_and_every_item_has_steps_and_a_source()
    {
        Assert.Equal(["folders", "dummies", "fbx", "kseditor", "collider", "data", "sound"], FirstCarChecklist.Items.Select(i => i.Id));
        Assert.All(FirstCarChecklist.Items, i => Assert.True(i.Steps.Length > 0 && i.Source.Length > 0));
    }

    [Fact]
    public void Checked_items_follow_the_validator_and_ignore_info_findings()
    {
        Finding F(string rule, Severity s) => new(rule, s, null, "m", "f", Evidence.Verified);

        var statuses = FirstCarChecklist.For([F("V-04", Severity.Error), F("V-15", Severity.Info), F("V-08", Severity.Info)], new HashSet<string> { "fbx" })
            .ToDictionary(s => s.Item.Id);

        Assert.Equal("To do", statuses["collider"].State);
        Assert.Equal("V-04", Assert.Single(statuses["collider"].Blocking).Rule);
        Assert.Equal("Done (checked)", statuses["data"].State);
        Assert.Equal("Done (ticked)", statuses["fbx"].State);
        Assert.Equal("To do (tick when done)", statuses["dummies"].State);
    }

    [Fact]
    public void Ticks_are_saved_per_car_in_the_workspace_root()
    {
        var workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;

        Assert.Null(workspace.SetChecklistTick("acm_a", "dummies", true));
        Assert.Null(workspace.SetChecklistTick("acm_a", "fbx", true));
        Assert.Null(workspace.SetChecklistTick("acm_a", "fbx", false));
        Assert.Null(workspace.SetChecklistTick("acm_b", "fbx", true));

        var reopened = Workspace.Open(workspace.Root, null).Workspace!;
        Assert.Equal(["dummies"], reopened.ChecklistTicks("acm_a"));
        Assert.Equal(["fbx"], reopened.ChecklistTicks("acm_b"));
        Assert.True(File.Exists(Path.Combine(workspace.Root, "checklists.json")));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    [Fact]
    public void Only_manual_items_can_be_ticked()
    {
        var workspace = Workspace.Open(Path.Combine(_temp, "ws"), null).Workspace!;

        Assert.Contains("isn't a checklist item", workspace.SetChecklistTick("acm_a", "collider", true)); // the validator decides that one
        Assert.NotNull(workspace.SetChecklistTick("..", "fbx", true));
    }
}
