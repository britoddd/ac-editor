using System.Reflection;

namespace AcMod.Core.Tests;

// QS-10: Core must stay free of WPF so it can be tested and reused without the UI.
public class CoreProjectTests
{
    private static readonly string[] WpfAssemblies =
        ["PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml"];

    [Fact]
    public void Core_does_not_reference_wpf()
    {
        var core = Assembly.Load("AcMod.Core");

        var references = core.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(references, name => WpfAssemblies.Contains(name));
    }

    [Fact]
    public void Core_is_not_windows_specific()
    {
        var core = Assembly.Load("AcMod.Core");

        // net10.0-windows builds carry a TargetPlatform attribute; plain net10.0 builds do not.
        var platform = core.GetCustomAttribute<System.Runtime.Versioning.TargetPlatformAttribute>();

        Assert.Null(platform);
    }
}
