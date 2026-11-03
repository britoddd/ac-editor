using AcMod.Core.Workspaces;

namespace AcMod.Core.Tests.Workspaces;

public sealed class SettingsTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_settings_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string SettingsPath => Path.Combine(_temp, "AcMod", "settings.json");

    [Fact]
    public void Saved_settings_load_back()
    {
        var settings = new Settings(@"D:\ACModding", @"C:\Program Files (x86)\Steam\steamapps\common\assettocorsa");

        settings.Save(SettingsPath); // creates the AcMod folder

        Assert.Equal((settings, null), Settings.Load(SettingsPath));
        Assert.Equal(["settings.json"], Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!).Select(Path.GetFileName)); // no .tmp left
    }

    [Fact]
    public void The_Content_Manager_path_is_remembered_and_old_files_without_it_still_load()
    {
        new Settings(@"D:\ACModding", null, @"C:\Tools\Content Manager.exe").Save(SettingsPath);
        Assert.Equal(@"C:\Tools\Content Manager.exe", Settings.Load(SettingsPath).Settings.ContentManagerPath);

        File.WriteAllText(SettingsPath, "{\"WorkspaceFolder\": \"D:\\\\ACModding\", \"AcFolder\": null}"); // written before WS-14
        Assert.Equal((new Settings(@"D:\ACModding", null), null), Settings.Load(SettingsPath));
    }

    [Fact]
    public void A_missing_file_gives_empty_settings()
    {
        Assert.Equal((new Settings(null, null), null), Settings.Load(SettingsPath));
    }

    [Fact]
    public void A_broken_file_gives_empty_settings_and_the_reason()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{\"WorkspaceFolder\": ");

        var (settings, error) = Settings.Load(SettingsPath);

        Assert.Equal(new Settings(null, null), settings);
        Assert.Contains("must be chosen again", error);
    }

    [Fact]
    public void The_default_path_is_in_local_app_data()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcMod", "settings.json"),
            Settings.DefaultPath);
        Assert.EndsWith(@"AppData\Local\AcMod\settings.json", Settings.DefaultPath);
    }
}
