using D4Assist.Game;

namespace D4Assist.Tests;

public class WorldLogWatcherTests
{
    private const string Marker = "[Game] Client entered world | world: ";

    [Fact]
    public void ReadLastWorldAtStartup_ReturnsTheMostRecentEntry()
    {
        string dir = MakeTempInstallDir();
        File.WriteAllText(Path.Combine(dir, WorldLogWatcher.LogFileName),
            $"{Marker}PIT_Step_City some tail\n{Marker}DGN_Hawe_HeathensKeep\n");

        using var watcher = new WorldLogWatcher(dir);

        Assert.Equal(new WorldEntry("DGN_Hawe_HeathensKeep", null), watcher.ReadLastWorldAtStartup());
    }

    [Fact]
    public void ReadLastWorldAtStartup_ReturnsNullWhenTheLogDoesNotExistYet()
    {
        string dir = MakeTempInstallDir();

        using var watcher = new WorldLogWatcher(dir);

        Assert.Null(watcher.ReadLastWorldAtStartup());
    }

    [Fact]
    public void PollOnce_FiresWorldChangedForANewCompleteLine()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "startup content\n");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += world => seen.Add(world.Name);

        File.AppendAllText(path, $"{Marker}DGN_Naha_SleeplessHollow\n");
        watcher.PollOnce();

        Assert.Equal(["DGN_Naha_SleeplessHollow"], seen);
    }

    [Fact]
    public void PollOnce_AFailingListener_NeitherEscapesNorReplaysTheLinesNorStarvesTheOthers()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += _ => throw new IOException("Diablo IV.exe is locked by the update");
        watcher.WorldChanged += world => seen.Add(world.Name);

        File.AppendAllText(path, $"{Marker}DGN_Naha_SleeplessHollow\n");
        watcher.PollOnce();
        watcher.PollOnce();

        Assert.Equal(["DGN_Naha_SleeplessHollow"], seen);
    }

    [Fact]
    public void PollOnce_FiresCutsceneStateChangedWithTheState()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.CutsceneStateChanged += seen.Add;

        File.AppendAllText(path,
            "I 2026.09.29 03:53:07.864062 1\t[Game] ptCutscene->m_eCutsceneState: PRELOADING\r\n" +
            "I 2026.09.29 03:53:08.011002 1\t[Game] ptCutscene->m_eCutsceneState: PLAYBACK\r\n" +
            "I 2026.09.29 03:53:05.119000 1\t[Game] CutscenesGfxInit\r\n");
        watcher.PollOnce();

        Assert.Equal(["PRELOADING", "PLAYBACK"], seen);
    }

    [Fact]
    public void PollOnce_DoesNotFireForAPartialLineStillBeingWritten()
    {
        string dir = MakeTempInstallDir();
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, "");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.WorldChanged += world => seen.Add(world.Name);

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
        watcher.WorldChanged += world => seen.Add(world.Name);
        watcher.PollOnce(); // establishes position at end of the padded content (no new lines since construction)
        Assert.Empty(seen);

        // Simulates log rotation: the game replaces the file with a fresh, shorter one.
        File.WriteAllText(path, $"{Marker}DGN_Hawe_HeathensKeep\n");
        watcher.PollOnce();

        Assert.Equal(["DGN_Hawe_HeathensKeep"], seen);
    }

    [Fact]
    public void WorldChanged_CarriesTheInstanceId()
    {
        string dir = MakeTempInstallDir();
        File.WriteAllText(Path.Combine(dir, WorldLogWatcher.LogFileName),
            $"I 2026.09.29 17:09:32.600574 59490 {Marker}S15_Triad_B_Worldstone | CWorldID: 1048578\n");

        using var watcher = new WorldLogWatcher(dir);

        Assert.Equal(new WorldEntry("S15_Triad_B_Worldstone", "1048578"), watcher.ReadLastWorldAtStartup());
    }

    private static string MakeTempInstallDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d4assist-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
