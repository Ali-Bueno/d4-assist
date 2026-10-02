using D4Data.Casc;

namespace D4Data.Tests;

public sealed class InstallFolderSearchTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d4-install-search-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder(string relative) => Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;

    private static bool AnyFolder(string _) => true;

    [Fact]
    public void Finds_the_folder_up_to_three_levels_deep_shallowest_first()
    {
        var deep = Folder(@"Games\Blizzard\Diablo IV");
        var shallow = Folder(@"Diablo IV");

        Assert.Equal([shallow, deep], InstallFolderSearch.Candidates([_root]));
    }

    [Fact]
    public void Ignores_a_folder_four_levels_deep()
    {
        Folder(@"SteamLibrary\steamapps\common\Diablo IV");

        Assert.Empty(InstallFolderSearch.Candidates([_root]));
    }

    [Fact]
    public void Matches_the_name_in_any_case_and_only_the_whole_name()
    {
        var found = Folder(@"Games\DIABLO iv");
        Folder(@"Games\Diablo IV Backup");

        Assert.Equal([found], InstallFolderSearch.Candidates([_root]));
    }

    [Fact]
    public void Skips_hidden_folders()
    {
        var hidden = Folder("Hidden");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
        Folder(@"Hidden\Diablo IV");

        Assert.Empty(InstallFolderSearch.Candidates([_root]));
    }

    [Fact]
    public void A_picked_install_is_kept_and_a_picked_parent_is_searched()
    {
        var install = Folder(@"Games\Diablo IV");

        Assert.Equal(install, InstallFolderSearch.AtOrUnder(install, f => f == install));
        Assert.Equal(install, InstallFolderSearch.AtOrUnder(_root, f => f == install));
        Assert.Null(InstallFolderSearch.AtOrUnder(Folder("Empty"), f => f == install));
        Assert.Equal(_root, InstallFolderSearch.AtOrUnder(_root, AnyFolder));
    }
}
