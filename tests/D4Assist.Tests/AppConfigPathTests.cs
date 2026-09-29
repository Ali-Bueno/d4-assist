using D4Assist.Config;

namespace D4Assist.Tests;

public class AppConfigPathTests
{
    [Fact]
    public void Install_path_uses_the_configured_folder_when_it_exists()
    {
        var config = new AppConfig { InstallPath = @"E:\Games\Diablo IV" };

        Assert.Equal(@"E:\Games\Diablo IV", config.ResolveInstallPath(_ => true, () => @"C:\located"));
    }

    [Fact]
    public void Install_path_falls_back_to_detection_when_empty_or_missing()
    {
        Assert.Equal(@"C:\located", new AppConfig().ResolveInstallPath(_ => true, () => @"C:\located"));
        Assert.Equal(@"C:\located", new AppConfig { InstallPath = @"Z:\gone" }.ResolveInstallPath(_ => false, () => @"C:\located"));
        Assert.Null(new AppConfig().ResolveInstallPath(_ => false, () => null));
    }

    [Fact]
    public void Definitions_path_defaults_to_the_embedded_copy_resolves_relative_paths_and_keeps_absolute_ones()
    {
        Assert.Null(new AppConfig().ResolveDefinitionsPath(@"C:\app"));
        Assert.Equal(Path.Combine(@"C:\app", "defs.json"), new AppConfig { DefinitionsPath = "defs.json" }.ResolveDefinitionsPath(@"C:\app"));
        Assert.Equal(@"D:\data\defs.json", new AppConfig { DefinitionsPath = @"D:\data\defs.json" }.ResolveDefinitionsPath(@"C:\app"));
    }
}
