using AcMod.Core.Learning;
using AcMod.Core.Validation;

namespace AcMod.Core.Tests.Learning;

public sealed class OutsideStepsTests
{
    private static Finding Finding(string rule, string? file = null) => new(rule, Severity.Error, file, "message", "fix", Evidence.Verified);

    [Theory]
    [InlineData("V-02", null)]
    [InlineData("V-04", null)]
    [InlineData("V-08", null)]
    [InlineData("V-09", @"sfx\acm_x.bank")]
    [InlineData("V-14", null)]
    public void Findings_the_dashboard_cant_fix_get_steps_with_a_source(string rule, string? file)
    {
        var step = OutsideSteps.For(Finding(rule, file));

        Assert.NotNull(step);
        Assert.False(string.IsNullOrWhiteSpace(step.Steps));
        Assert.Contains("docs/", step.Source);
    }

    [Theory]
    [InlineData("V-05", null)] // the Raw files tab fixes data
    [InlineData("V-09", @"sfx\GUIDs.txt")] // Save as new car or the Raw files tab fixes GUIDs.txt
    [InlineData("V-12", null)]
    public void Findings_the_dashboard_can_fix_get_none(string rule, string? file)
    {
        Assert.Null(OutsideSteps.For(Finding(rule, file)));
    }

    [Fact]
    public void The_collider_steps_use_the_FBX_settings_tested_in_M0()
    {
        Assert.Contains("Apply Scalings = FBX All and Apply Transform on", OutsideSteps.For(Finding("V-04"))!.Steps);
    }
}
