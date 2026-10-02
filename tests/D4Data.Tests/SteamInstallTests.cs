using D4Data.Casc;

namespace D4Data.Tests;

public class SteamInstallTests
{
    private const string LibraryFolders = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"C:\\Program Files (x86)\\Steam"
        		"label"		"path"
        		"apps"
        		{
        			"228980"		"1234"
        		}
        	}
        	// a second library on another drive
        	"1"
        	{
        		"path"		"E:\\SteamLibrary"
        		"apps"
        		{
        			"2344520"		"98765"
        		}
        	}
        }
        """;

    private const string Manifest = """
        "AppState"
        {
        	"appid"		"2344520"
        	"name"		"Diablo IV"
        	"installdir"		"Diablo IV"
        }
        """;

    [Fact]
    public void Values_follow_their_key_and_skip_blocks_values_and_comments()
    {
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"E:\SteamLibrary"], SteamInstall.ValuesOf(LibraryFolders, "path"));
        Assert.Equal(["Diablo IV"], SteamInstall.ValuesOf(Manifest, "INSTALLDIR"));
        Assert.Empty(SteamInstall.ValuesOf(LibraryFolders, "apps"));
    }

    [Fact]
    public void Quoted_strings_unescape_quotes_and_unquoted_tokens_are_read()
    {
        Assert.Equal(["say \"hi\""], SteamInstall.ValuesOf("\"note\" \"say \\\"hi\\\"\"", "note"));
        Assert.Equal(["Diablo"], SteamInstall.ValuesOf("installdir Diablo", "installdir"));
    }

    [Fact]
    public void The_game_is_found_in_the_library_whose_manifest_lists_it()
    {
        var steam = @"C:\Program Files (x86)\Steam";
        var files = new Dictionary<string, string>
        {
            [Path.Combine(steam, "steamapps", "libraryfolders.vdf")] = LibraryFolders,
            [Path.Combine(@"E:\SteamLibrary", "steamapps", $"appmanifest_{SteamInstall.AppId}.acf")] = Manifest,
        };

        var installs = SteamInstall.LibraryInstalls(steam, path => files.GetValueOrDefault(path)).ToList();

        Assert.Equal([Path.Combine(@"E:\SteamLibrary", "steamapps", "common", "Diablo IV")], installs);
    }

    [Fact]
    public void Without_a_library_list_only_the_steam_folder_itself_is_searched()
    {
        var steam = @"D:\Steam";
        var files = new Dictionary<string, string>
        {
            [Path.Combine(steam, "steamapps", $"appmanifest_{SteamInstall.AppId}.acf")] = Manifest,
        };

        Assert.Equal([Path.Combine(steam, "steamapps", "common", "Diablo IV")], SteamInstall.LibraryInstalls(steam, files.GetValueOrDefault));
        Assert.Empty(SteamInstall.LibraryInstalls(steam, _ => null));
    }
}
