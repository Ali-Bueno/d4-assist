using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Speech;

namespace D4Assist.Tests;

public class ExplorationPersistenceTests : IDisposable
{
    private const int PortalSno = 7, LeverSno = 8;
    private static readonly DateTime GameStart = new(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TileState TileA = new(10, "A", 0, 0, 32, 32, 4, 1, 1);
    private static readonly TileState TileB = new(11, "B", 0, 32, 32, 32, 8, 2, 2);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "d4assist-tests-" + Guid.NewGuid());

    public ExplorationPersistenceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; } = ("DGN_Test", true);
        public string? CurrentInstance { get; set; } = "524290";

        public void RaiseWorld(string name, bool isDungeon)
        {
            CurrentWorld = (name, isDungeon);
            WorldChanged?.Invoke(0, name, isDungeon);
        }
        public void RaiseTiles(params TileState[] tiles) => TilesUpdated?.Invoke(tiles);
        public void RaisePlayer(float x, float y) => PlayerMoved?.Invoke(new PlayerState(x, y, 0, 0));
        public void RaiseActors(params ActorState[] actors) => ActorsUpdated?.Invoke(actors);
    }

    private sealed class SilentSpeech : ISpeechSink
    {
        public string BackendName => "silent";
        public void Speak(string text, bool interrupt) { }
        public void Dispose() { }
    }

    private sealed record App(Feed Feed, NavigationBridge Navigation, PortalMemory Portals, ObjectiveMemory Objectives, List<string> Log);

    // One run of the app: created after the game already entered the world, like a restart mid-dungeon.
    private App Start(DateTime? gameStart = null, string instance = "524290")
    {
        var feed = new Feed { CurrentInstance = instance };
        var navigation = new NavigationBridge(feed, new SilentSpeech(), announcementsEnabled: false, diagnostics: false);
        var portals = new PortalMemory(feed, a => a.ActorSno == PortalSno, _ => "Portal");
        var objectives = new ObjectiveMemory(feed, _ => "Lever");
        var log = new List<string>();
        _ = new ExplorationPersistence(feed, new ExplorationStore(_dir), navigation, portals, () => gameStart ?? GameStart, log.Add, objectives);
        return new App(feed, navigation, portals, objectives, log);
    }

    // Arrives beside a portal in tile A, then the next tile poll saves.
    private App ExploreTileA()
    {
        var app = Start();
        app.Feed.RaiseTiles(TileA, TileB);
        app.Feed.RaisePlayer(16, 16);
        app.Feed.RaiseActors(new ActorState(1, PortalSno, 20, 20, 0));
        app.Feed.RaiseTiles(TileA, TileB);
        return app;
    }

    [Fact]
    public void A_restart_in_the_same_instance_keeps_the_map_the_explored_tiles_and_the_used_portal()
    {
        ExploreTileA();

        var restarted = Start();
        restarted.Feed.RaiseTiles(TileB); // the loaded window no longer reaches tile A

        var state = restarted.Navigation.ExportExploration();
        Assert.Contains(state.Tiles, t => t.Name == "A");
        Assert.Contains((0, 0), state.Visited);
        var portal = Assert.Single(restarted.Portals.Destinations());
        Assert.Equal(1, portal.Tier); // arrived by: leads back, not on
        Assert.Contains(restarted.Log, l => l.StartsWith("Exploration: restored"));
    }

    [Fact]
    public void A_restart_in_the_same_instance_keeps_the_objectives_heard_and_not_done()
    {
        // Live 2026-10-04: a restart forgot the Labyrinth's last lever, heard once, so the guide never led there.
        var app = Start();
        app.Feed.RaiseTiles(TileA, TileB);
        app.Feed.RaisePlayer(16, 16);
        app.Objectives.Heard(new ActorState(5, LeverSno, 60, 40, 0, QuestStep: true));
        app.Feed.RaiseTiles(TileA, TileB); // the next tile poll saves

        var restarted = Start();
        restarted.Feed.RaiseTiles(TileB);

        var lever = Assert.Single(restarted.Objectives.Destinations());
        Assert.Equal((60f, 40f), lever.Position);
        Assert.Equal(ObjectiveMemory.QuestTier, lever.Tier);
    }

    [Fact]
    public void Another_game_run_or_instance_starts_fresh()
    {
        ExploreTileA();

        var otherRun = Start(gameStart: GameStart.AddHours(1));
        otherRun.Feed.RaiseTiles(TileB);
        Assert.DoesNotContain(otherRun.Navigation.ExportExploration().Tiles, t => t.Name == "A");

        var otherInstance = Start(instance: "1048578");
        otherInstance.Feed.RaiseTiles(TileB);
        Assert.DoesNotContain(otherInstance.Navigation.ExportExploration().Tiles, t => t.Name == "A");
        Assert.Empty(otherInstance.Portals.Destinations());
    }

    [Fact]
    public void A_save_whose_tiles_disagree_with_the_loaded_ones_is_another_layout()
    {
        ExploreTileA();

        var restarted = Start();
        restarted.Feed.RaiseTiles(TileA with { SceneSno = 99 });

        var state = restarted.Navigation.ExportExploration();
        Assert.DoesNotContain(state.Tiles, t => t.Name == "B");
        Assert.Empty(state.Visited);
        Assert.Contains(restarted.Log, l => l.Contains("doesn't match"));
    }

    [Fact]
    public void Leaving_the_dungeon_discards_the_save()
    {
        var app = ExploreTileA();
        Assert.True(File.Exists(Path.Combine(_dir, ExplorationStore.FileName)));

        app.Feed.RaiseWorld("Kehj_Town", isDungeon: false);

        Assert.False(File.Exists(Path.Combine(_dir, ExplorationStore.FileName)));
    }

    [Fact]
    public void Nothing_is_kept_when_the_game_start_is_unknown()
    {
        var feed = new Feed();
        var navigation = new NavigationBridge(feed, new SilentSpeech(), announcementsEnabled: false, diagnostics: false);
        _ = new ExplorationPersistence(feed, new ExplorationStore(_dir), navigation, null, () => null);

        feed.RaiseTiles(TileA);

        Assert.False(File.Exists(Path.Combine(_dir, ExplorationStore.FileName)));
    }
}
