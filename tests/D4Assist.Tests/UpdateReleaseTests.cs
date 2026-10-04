using System.IO.Compression;
using D4Assist.Navigation;
using D4Assist.Strings;
using D4Assist.Update;

namespace D4Assist.Tests;

public class UpdateReleaseTests
{
    // The shape of GET /repos/Ali-Bueno/d4-assist/releases/latest (trimmed to the fields the updater reads).
    // Versions are compared in the updater's own normal form (revision 0, never -1).
    private static readonly Version V016 = new(0, 1, 6, 0);

    private const string LatestJson = """
        {
          "tag_name": "v0.1.6",
          "name": "Diablo 4 Assist 0.1.6",
          "body": "**What's new**\n- Quests in progress nearby\n- `NpcVoices` setting",
          "assets": [
            { "name": "notes.txt", "browser_download_url": "https://example.test/notes.txt" },
            {
              "name": "D4Assist.zip",
              "browser_download_url": "https://github.com/Ali-Bueno/d4-assist/releases/download/v0.1.6/D4Assist.zip",
              "digest": "sha256:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef"
            }
          ]
        }
        """;

    [Fact]
    public void Release_json_gives_version_notes_zip_and_digest()
    {
        var release = ReleaseInfo.Parse(LatestJson)!;

        Assert.Equal(V016, release.Version);
        Assert.Contains("Quests in progress nearby", release.Notes);
        Assert.Equal("https://github.com/Ali-Bueno/d4-assist/releases/download/v0.1.6/D4Assist.zip", release.ZipUrl);
        Assert.Equal("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", release.Sha256);
    }

    [Fact]
    public void A_missing_digest_is_allowed_but_a_missing_zip_or_a_garbage_tag_is_not()
    {
        Assert.Null(ReleaseInfo.Parse(LatestJson.Replace("\"digest\"", "\"other\""))!.Sha256);
        Assert.Null(ReleaseInfo.Parse(LatestJson.Replace("\"D4Assist.zip\"", "\"other.zip\"")));
        Assert.Null(ReleaseInfo.Parse(LatestJson.Replace("v0.1.6", "latest")));
        Assert.Null(ReleaseInfo.Parse("""{ "message": "Not Found" }"""));
    }

    [Theory]
    [InlineData("v0.1.7", true)]
    [InlineData("0.1.6", false)]
    [InlineData("v0.1.5", false)]
    [InlineData("v0.2.0", true)]
    [InlineData("v0.1.10", true)]
    [InlineData("beta", false)]
    [InlineData("", false)]
    public void A_tag_is_an_update_only_when_it_is_newer_than_0_1_6(string tag, bool isUpdate)
    {
        bool parsed = UpdateVersion.TryParse(tag, out var version);

        Assert.Equal(isUpdate, parsed && version > V016);
    }

    [Fact]
    public void The_commit_suffix_of_the_informational_version_is_ignored()
    {
        Assert.True(UpdateVersion.TryParse("0.1.6+f0718f3", out var version));
        Assert.Equal(V016, version);
        Assert.True(UpdateVersion.Current > new Version(0, 0));
    }

    [Fact]
    public void Dialog_text_has_both_versions_and_trimmed_plain_notes()
    {
        var format = SettingsStrings.English.UpdateDialogFormat;
        var text = UpdatePrompt.Text(format, new Version(0, 1, 7, 0), V016, "**New**\n`code` " + new string('x', 3000));

        Assert.Contains("0.1.7", text);
        Assert.Contains("0.1.6", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("`", text);
        Assert.True(text.Length < format.Length + 1700);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void Sha256_is_checked_against_the_file_in_any_case()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d4assist-sha-{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, "abc");
        try
        {
            const string abc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
            Assert.True(UpdatePackage.Sha256Matches(path, abc));
            Assert.True(UpdatePackage.Sha256Matches(path, abc.ToUpperInvariant()));
            Assert.False(UpdatePackage.Sha256Matches(path, new string('0', 64)));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_the_update_texts(SpeechLanguage language)
    {
        var app = AppStrings.For(language);
        var settings = SettingsStrings.For(language);
        Assert.Contains("{0}", app.UpdateAvailableFormat);
        Assert.Contains("{0}", app.UpdateProgressFormat);
        Assert.Contains("{0}", app.UpdatedToFormat);
        Assert.Contains("{2}", settings.UpdateDialogFormat);
        Assert.All(new[]
        {
            app.UpdateDownloading, app.UpdateRestarting, app.UpdateFailed, app.UpdateFolderNotWritable, app.UpToDate,
            app.UpdateCheckFailed, app.UpdateOnlyDownloaded, settings.MenuCheckUpdates, settings.CheckForUpdates, settings.CompassDirections,
        }, text => Assert.False(string.IsNullOrWhiteSpace(text)));
    }

    [Fact]
    public void Download_progress_is_spoken_once_per_quarter()
    {
        var spoken = new List<int>();
        var sink = ProgressMilestones.Every(25, spoken.Add);

        foreach (var percent in new[] { 3, 24, 25, 26, 60, 61, 99, 100, 100 }) sink(percent);

        Assert.Equal([25, 50, 75], spoken);
    }

    public static TheoryData<SpeechLanguage> Languages => [.. SpeechLanguages.Supported];
}

public sealed class FileReplacementTests : IDisposable
{
    private const string Exe = "D4Assist.exe";
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"d4assist-update-{Guid.NewGuid():N}");
    private string App => Path.Combine(_root, "app");
    private string Files => Path.Combine(_root, "files");
    private string Backup => Path.Combine(_root, "backup");

    public FileReplacementTests()
    {
        Directory.CreateDirectory(App);
        Directory.CreateDirectory(Files);
        Put(App, Exe, "old exe");
        Put(App, "d4assist.json", "player config");
        Put(App, "README.txt", "old readme");
        Put(Files, Exe, "new exe");
        Put(Files, "d4assist.json", "starting config");
        Put(Files, "README.txt", "new readme");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Put(string folder, string name, string text) => File.WriteAllText(Path.Combine(folder, name), text);
    private string Read(string name) => File.ReadAllText(Path.Combine(App, name));

    [Fact]
    public void Replaces_the_exe_keeps_the_players_config_and_updates_the_rest()
    {
        var report = FileReplacement.Apply(App, Files, Exe, Backup);

        Assert.Equal("new exe", Read(Exe));
        Assert.Equal("old exe", Read(Exe + ".old"));
        Assert.Equal("player config", Read("d4assist.json"));
        Assert.Equal("new readme", Read("README.txt"));
        Assert.Equal(["d4assist.json"], report.Kept);
        Assert.Contains(Exe, report.Copied);
        Assert.Contains("README.txt", report.Copied);
        Assert.DoesNotContain("d4assist.json", report.Copied);
    }

    [Fact]
    public void Copies_the_config_when_the_app_folder_has_none()
    {
        File.Delete(Path.Combine(App, "d4assist.json"));

        var report = FileReplacement.Apply(App, Files, Exe, Backup);

        Assert.Equal("starting config", Read("d4assist.json"));
        Assert.Empty(report.Kept);
    }

    [Fact]
    public void Only_a_renamed_exe_marks_the_first_start_after_an_update()
    {
        Assert.False(FileReplacement.HasLeftover(App, Exe));
        FileReplacement.Apply(App, Files, Exe, Backup);
        Assert.True(FileReplacement.HasLeftover(App, Exe));
    }

    [Fact]
    public void A_stale_old_exe_is_replaced_and_leftovers_are_deleted_at_start()
    {
        Put(App, Exe + ".old", "stale");

        FileReplacement.Apply(App, Files, Exe, Backup);
        Assert.Equal("old exe", Read(Exe + ".old"));

        FileReplacement.DeleteLeftover(App, Exe);
        Assert.False(File.Exists(Path.Combine(App, Exe + ".old")));
    }

    [Fact]
    public void A_zip_without_the_exe_changes_nothing()
    {
        File.Delete(Path.Combine(Files, Exe));

        Assert.Throws<InvalidDataException>(() => FileReplacement.Apply(App, Files, Exe, Backup));

        Assert.Equal("old exe", Read(Exe));
        Assert.False(File.Exists(Path.Combine(App, Exe + ".old")));
    }

    [Fact]
    public void A_failed_copy_rolls_everything_back()
    {
        Put(Files, "zz-new.dll", "new dll");
        Put(App, "zz-new.dll", "locked dll");
        using var lockedFile = new FileStream(Path.Combine(App, "zz-new.dll"), FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Throws<IOException>(() => FileReplacement.Apply(App, Files, Exe, Backup));

        Assert.Equal("old exe", Read(Exe));
        Assert.False(File.Exists(Path.Combine(App, Exe + ".old")));
        Assert.Equal("old readme", Read("README.txt"));
        Assert.Equal("player config", Read("d4assist.json"));
    }

    [Fact]
    public void An_unzipped_release_replaces_files_in_subfolders_too()
    {
        var zip = Path.Combine(_root, "release.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(Path.Combine(Files, Exe), Exe);
            archive.CreateEntryFromFile(Path.Combine(Files, "README.txt"), Path.Combine("docs", "README.txt"));
        }
        var extracted = Path.Combine(_root, "extracted");
        ZipFile.ExtractToDirectory(zip, extracted);

        FileReplacement.Apply(App, extracted, Exe, Backup);

        Assert.Equal("new exe", Read(Exe));
        Assert.Equal("new readme", Read(Path.Combine("docs", "README.txt")));
    }
}
