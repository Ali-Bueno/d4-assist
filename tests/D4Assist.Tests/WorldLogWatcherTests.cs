using D4Assist.Game;

namespace D4Assist.Tests;

public class WorldLogWatcherTests
{
    private const string Marker = "[Game] Client entered world | world: ";

    [Fact]
    public void ReadLastWorldNameAtStartup_ReturnsTheMostRecentEntry()
    {
        string dir = MakeTempInstallDir();
        File.WriteAllText(Path.Combine(dir, WorldLogWatcher.LogFileName),
            $"{Marker}PIT_Step_City some tail\n{Marker}DGN_Hawe_HeathensKeep\n");

        using var watcher = new WorldLogWatcher(dir);

        Assert.Equal("DGN_Hawe_HeathensKeep", watcher.ReadLastWorldNameAtStartup());
    }

    [Fact]
    public void ReadLastWorldNameAtStartup_ReturnsNullWhenTheLogDoesNotExistYet()
    {
        string dir = MakeTempInstallDir();

        using var watcher = new WorldLogWatcher(dir);

        Assert.Null(watcher.ReadLastWorldNameAtStartup());
    }

    [Fact]
    public void PollOnce_FiresWorldChangedForANewCompleteLine()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "startup content\n");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += seen.Add;

        File.AppendAllText(path, $"{Marker}DGN_Naha_SleeplessHollow\n");
        watcher.PollOnce();

        Assert.Equal(["DGN_Naha_SleeplessHollow"], seen);
    }

    [Fact]
    public void PollOnce_DoesNotFireForAPartialLineStillBeingWritten()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += seen.Add;

        File.AppendAllText(path, $"{Marker}DGN_Partial"); // no trailing newline yet
        watcher.PollOnce();
        Assert.Empty(seen);

        File.AppendAllText(path, "\n"); // now complete
        watcher.PollOnce();
        Assert.Equal(["DGN_Partial"], seen);
    }

    [Fact]
    public void PollOnce_RestartsFromTheTopWhenTheFileShrinks()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, $"{Marker}PIT_Step_City\n a very long line padding the file out \n");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += seen.Add;
        watcher.PollOnce(); // establishes position at end of the padded content (no new lines since construction)
        Assert.Empty(seen);

        // Simulates log rotation: the game replaces the file with a fresh, shorter one.
        File.WriteAllText(path, $"{Marker}DGN_Hawe_HeathensKeep\n");
        watcher.PollOnce();

        Assert.Equal(["DGN_Hawe_HeathensKeep"], seen);
    }

    private static string MakeTempInstallDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d4assist-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
