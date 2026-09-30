using System.Numerics;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Speech;
using D4Data.Format;
using D4Data.Model;
using D4Data.Readers;

namespace D4Assist.Tests;

public class RadarOnlyWorldTests
{
    private const string Den = "Mercenary_Hideout";
    private static readonly string[] DenList = [Den];

    [Fact]
    public void A_drlg_world_is_a_dungeon_even_when_listed()
    {
        Assert.Equal(WorldKind.Dungeon, WorldClassifier.KindOf(Conventions.LayoutDrlg, Den, DenList));
    }

    [Fact]
    public void A_listed_non_drlg_world_is_radar_only()
    {
        Assert.Equal(WorldKind.RadarOnly, WorldClassifier.KindOf(1, Den, DenList));
    }

    [Theory]
    [InlineData("mercenary_hideout")]
    [InlineData("Mercenary_Hideout_2")]
    [InlineData("Sanctuary_Eastern_Continent")]
    public void An_unlisted_or_differently_cased_non_drlg_world_is_other_when_reads_outside_dungeons_are_off(string name)
    {
        Assert.Equal(WorldKind.Other, WorldClassifier.KindOf(1, name, DenList));
    }

    [Fact]
    public void The_open_world_is_radar_only_only_when_listed()
    {
        Assert.Equal(WorldKind.RadarOnly, WorldClassifier.KindOf(1, WorldClassifier.OpenWorldName, [WorldClassifier.OpenWorldName], outsideDungeons: true));
    }

    [Fact]
    public void No_list_means_no_radar_only_worlds()
    {
        Assert.Equal(WorldKind.Other, WorldClassifier.KindOf(1, Den, null));
    }

    [Fact]
    public void Config_defaults_to_no_radar_only_world()
    {
        Assert.Empty(new AppConfig().RadarOnlyWorlds); // story areas are radar-only without being listed
    }

    [Fact]
    public void Static_tiles_use_the_chunk_origin_the_rotation_yaw_and_one_room()
    {
        float half = MathF.Sqrt(0.5f); // quaternion of a 90 degree turn about Z
        var chunks = new[]
        {
            new SceneChunkInfo(10, new Vector3(-96, -96, 0), Quaternion.Identity, 0),
            new SceneChunkInfo(11, new Vector3(0, 96, 0), new Quaternion(0, 0, half, half), 0),
        };
        var builder = new StaticWorldTiles(_ => (96f, chunks), _ => null, sno => $"scene{sno}");

        var tiles = builder.Build(7);

        Assert.Equal(2, tiles.Count);
        var first = tiles[0];
        Assert.Equal((10, "scene10", -96f, -96f, 96f, 96f, 0, 0f), (first.SceneSno, first.Name, first.OriginX, first.OriginY, first.SizeX, first.SizeY, first.ExitMask, first.YawDegrees));
        Assert.Equal(90f, tiles[1].YawDegrees, 0.01f);
        Assert.Equal((tiles[0].NodeId, tiles[0].GroupId), (tiles[1].NodeId, tiles[1].GroupId)); // no seams as exits
        Assert.Same(tiles, builder.Build(7)); // cached per world
    }

    [Fact]
    public void A_static_tile_spans_one_grid_cell_or_its_whole_walk_map()
    {
        var chunks = new[] { new SceneChunkInfo(10, Vector3.Zero, Quaternion.Identity, 0) };
        var longMap = new WalkableMap(1f, [0f, 0f], [.. Enumerable.Repeat(new string('.', 40), 288)]); // Rivergate: 288 x 40

        var tile = new StaticWorldTiles(_ => (96f, chunks), _ => longMap, _ => null).Build(7)[0];

        Assert.Equal((288f, 96f), (tile.SizeX, tile.SizeY));
    }

    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; }
        public bool CurrentWorldRadarOnly { get; set; }

        public void Run()
        {
            WorldChanged?.Invoke(1, Den, false);
            var tiles = new[]
            {
                new TileState(10, "a", 0, 0, 96, 96, 0, 1, 1),
                new TileState(11, "b", 96, 0, 96, 96, 0, 2, 2),
            };
            TilesUpdated?.Invoke(tiles);
            for (int i = 0; i < 5; i++) PlayerMoved?.Invoke(new PlayerState(10 + i * 10, 10, 0, 10));
        }
    }

    private sealed class Speech : ISpeechSink
    {
        public List<string> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add(text);
        public void Dispose() { }
    }

    [Fact]
    public void Navigation_stays_silent_in_a_radar_only_world()
    {
        var speech = new Speech();
        var feed = new Feed { CurrentWorldRadarOnly = true };
        _ = new NavigationBridge(feed, speech, announcementsEnabled: true, diagnostics: false);

        feed.Run();

        Assert.Empty(speech.Spoken);
    }

    [Fact]
    public void Navigation_still_speaks_in_an_ordinary_dungeon()
    {
        var speech = new Speech();
        var feed = new Feed();
        _ = new NavigationBridge(feed, speech, announcementsEnabled: true, diagnostics: false);

        feed.Run();

        Assert.NotEmpty(speech.Spoken);
    }
}
