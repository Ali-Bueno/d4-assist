using D4Assist.Game;
using D4Assist.Navigation;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class PortalTests
{
    private const int Gizmo = 2;
    private const int PortalSno = 1, PadSno = 2, TownPortalSno = 3, LeverSno = 4;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [PortalSno] = ("Prefab_Portal_Dungeon_Generic", new(Gizmo, Conventions.GizmoPortal)),
        [PadSno] = ("X1_Undercity_WarpPad", new(Gizmo, Conventions.GizmoSwitch)),
        [TownPortalSno] = ("Portal_Quest_TownPortal", new(Gizmo, Conventions.GizmoPortal)),
        [LeverSno] = ("Some_Lever", new(Gizmo, Conventions.GizmoSwitch)),
    };

    private static readonly PortalClassifier Classifier = new(
        sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, sno => Actors.GetValueOrDefault(sno).Name);

    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; } = ("DGN_Test", true);
        public void RaiseWorld(string name = "DGN_Test") => WorldChanged?.Invoke(0, name, true);
        public void RaiseTiles(params TileState[] tiles) => TilesUpdated?.Invoke(tiles);
        public void RaisePlayer(float x, float y) => PlayerMoved?.Invoke(new PlayerState(x, y, 1, 0));
        public void RaiseActors(params ActorState[] actors) => ActorsUpdated?.Invoke(actors);
    }

    // The Travincal hub's NE tile places the Spider Cavern portal at local (19.9, 5.7); live it stood at (-12.1, -26.3).
    private const int HubTileSno = 2589492;
    private static readonly TileState HubTile = new(HubTileSno, "S15_ZigOut_Naha_Trav_POI_NE_01", -32, -32, 32, 32, 5, 1, 1);
    private static readonly (float X, float Y) TilePortalAt = (-12.1f, -26.3f);

    private static IReadOnlyList<TileActor> TilePortals(int scene) =>
        scene == HubTileSno ? [new TileActor(PortalSno, new System.Numerics.Vector3(19.9f, 5.7f, 0))] : [];

    private static (Feed Feed, PortalMemory Memory) NewMemory(
        bool tilePortals = false, string world = "DGN_Test", IReadOnlySet<int>? objectiveActors = null)
    {
        var feed = new Feed { CurrentWorld = (world, true) };
        return (feed, new PortalMemory(
            feed, a => Classifier.IsPortal(a.ActorSno), sno => Actors.GetValueOrDefault(sno).Name, tilePortals: tilePortals ? TilePortals : null,
            objectiveActors: objectiveActors is null ? null : () => objectiveActors));
    }

    private static ActorState Portal(uint id, float x, float y, int sno = PortalSno) => new(id, sno, x, y, 1);

    // Polls the actors with the player standing at (x, y), as many times as it takes to find a placed portal missing.
    private static void StandAt(Feed feed, float x, float y, params ActorState[] actors)
    {
        feed.RaisePlayer(x, y);
        for (int i = 0; i < PortalMemory.AbsentPolls; i++) feed.RaiseActors(actors);
    }

    [Fact]
    public void Portals_are_portal_gizmos_and_warp_pads_but_not_town_portals_or_levers()
    {
        Assert.True(Classifier.IsPortal(PortalSno));
        Assert.True(Classifier.IsPortal(PadSno));
        Assert.False(Classifier.IsPortal(TownPortalSno));
        Assert.False(Classifier.IsPortal(LeverSno));
        Assert.False(Classifier.IsPortal(999)); // unreadable
    }

    [Fact]
    public void Portals_get_the_beacon_only_inside_dungeons()
    {
        var selector = new LoopTargetSelector(new HashSet<int>(), null, _ => new HashSet<int>(), isPortal: Classifier.IsPortal);

        selector.SetWorld(1, isDungeon: false);
        Assert.Null(selector.Select(Portal(1, 0, 0)));

        selector.SetWorld(2, isDungeon: true);
        Assert.Equal(LoopKind.ExitBeacon, selector.Select(Portal(1, 0, 0))!.Kind);
    }

    [Fact]
    public void The_portal_beside_the_arrival_point_is_used_and_a_far_one_is_fresh()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(0, 0);

        feed.RaiseActors(Portal(1, 3, 3));
        Assert.False(memory.FreshInSight);

        feed.RaiseActors(Portal(1, 3, 3), Portal(2, 60, 0));
        Assert.True(memory.FreshInSight);
        Assert.Equal([1, 0], memory.Destinations().Select(d => d.Tier));
    }

    [Fact]
    public void A_warp_marks_the_portal_left_through_and_the_one_arrived_by()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(0, 0);
        feed.RaiseActors(Portal(1, 60, 0));
        Assert.True(memory.FreshInSight);

        for (float x = 10; x <= 58; x += 8) feed.RaisePlayer(x, 0); // walked to it
        feed.RaisePlayer(500, 500); // took it
        feed.RaiseActors(Portal(7, 502, 500));

        Assert.False(memory.FreshInSight);
        Assert.All(memory.Destinations(), d => Assert.Equal(1, d.Tier));
    }

    [Fact]
    public void A_portal_seen_again_under_a_new_id_is_the_same_portal()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(0, 0);
        feed.RaiseActors(Portal(1, 60, 0));
        feed.RaiseActors(Portal(9, 60, 0));

        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void A_new_world_forgets_every_portal()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(0, 0);
        feed.RaiseActors(Portal(1, 60, 0));

        feed.RaiseWorld();

        Assert.Empty(memory.Destinations());
        Assert.False(memory.FreshInSight);
    }

    [Fact]
    public void A_portal_its_tile_places_leads_before_it_is_seen()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(60, 60);

        feed.RaiseTiles(HubTile);

        var placed = Assert.Single(memory.Destinations());
        Assert.Equal(0, placed.Tier);
        Assert.Equal(TilePortalAt.X, placed.Position.X, 0.01f);
        Assert.Equal(TilePortalAt.Y, placed.Position.Y, 0.01f);
        Assert.False(memory.FreshInSight); // its beacon only plays once the game shows it
    }

    [Fact]
    public void Seeing_a_placed_portal_keeps_it_as_the_same_goal()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);
        string id = Assert.Single(memory.Destinations()).Id;

        feed.RaiseActors(Portal(1, TilePortalAt.X + 0.1f, TilePortalAt.Y));

        Assert.Equal(id, Assert.Single(memory.Destinations()).Id);
        Assert.True(memory.FreshInSight);
    }

    [Fact]
    public void A_placed_portal_missing_beside_the_player_stops_leading_until_it_appears()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);
        string id = Assert.Single(memory.Destinations()).Id;

        for (float x = 50; x > TilePortalAt.X + 5; x -= 8) feed.RaisePlayer(x, TilePortalAt.Y); // walked up to it
        StandAt(feed, TilePortalAt.X + 5, TilePortalAt.Y);
        Assert.Empty(memory.Destinations());

        feed.RaiseActors(Portal(1, TilePortalAt.X, TilePortalAt.Y)); // opened later (the hub's last portal)
        Assert.NotEqual(id, Assert.Single(memory.Destinations()).Id); // a new goal: the guide had reached the old one
    }

    [Fact]
    public void A_placed_portal_still_streaming_in_after_a_warp_is_not_missed()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(500, 500);
        feed.RaiseTiles(HubTile);

        feed.RaisePlayer(TilePortalAt.X + 16, TilePortalAt.Y); // warped in beside it
        for (int i = 0; i < PortalMemory.AbsentPolls - 1; i++) feed.RaiseActors();

        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void The_portal_placed_beside_the_arrival_point_is_used()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(TilePortalAt.X + 3, TilePortalAt.Y);

        feed.RaiseTiles(HubTile);

        Assert.Equal(1, Assert.Single(memory.Destinations()).Tier);
    }

    [Fact]
    public void Only_seen_portals_are_saved()
    {
        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);
        feed.RaiseActors(Portal(2, 100, 0));

        Assert.Equal([(100f, 0f)], memory.Export().Select(p => (p.X, p.Y)));
    }

    [Fact]
    public void A_portal_the_worlds_objective_names_leads_as_an_objective_until_used()
    {
        var (feed, memory) = NewMemory(tilePortals: true, objectiveActors: new HashSet<int> { PortalSno });
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);

        var objective = Assert.Single(memory.ObjectiveDestinations());
        Assert.Equal(ObjectiveMemory.DungeonTier, objective.Tier);

        for (float x = 50; x > TilePortalAt.X + 8; x -= 8) feed.RaisePlayer(x, TilePortalAt.Y);
        feed.RaiseActors(Portal(1, TilePortalAt.X, TilePortalAt.Y));
        feed.RaisePlayer(TilePortalAt.X + 1, TilePortalAt.Y);
        feed.RaisePlayer(500, 500); // took it
        Assert.Empty(memory.ObjectiveDestinations());
    }

    [Fact]
    public void An_objective_portal_the_player_stood_at_leads_again_once_rearmed()
    {
        var (feed, memory) = NewMemory(tilePortals: true, objectiveActors: new HashSet<int> { PortalSno });
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);
        feed.RaiseActors(Portal(1, TilePortalAt.X, TilePortalAt.Y));

        for (float x = 50; x > TilePortalAt.X + 1; x -= 8) feed.RaisePlayer(x, TilePortalAt.Y);
        feed.RaisePlayer(TilePortalAt.X + 1, TilePortalAt.Y); // stood at it, didn't go in
        Assert.Empty(memory.ObjectiveDestinations());

        memory.Rearm();
        Assert.Single(memory.ObjectiveDestinations());
    }

    [Fact]
    public void Portals_the_worlds_objective_doesnt_name_are_no_objectives()
    {
        var (feed, memory) = NewMemory(tilePortals: true, objectiveActors: new HashSet<int> { PadSno });
        feed.RaisePlayer(60, 60);
        feed.RaiseTiles(HubTile);

        Assert.Empty(memory.ObjectiveDestinations());
        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void Endgame_worlds_and_rotated_tiles_place_no_portals()
    {
        var (endgame, inPit) = NewMemory(tilePortals: true, world: "PIT_Test");
        endgame.RaiseTiles(HubTile);
        Assert.Empty(inPit.Destinations());

        var (feed, memory) = NewMemory(tilePortals: true);
        feed.RaiseTiles(HubTile with { YawDegrees = 90 });
        Assert.Empty(memory.Destinations());

        feed.RaiseWorld("PIT_Test");
        feed.RaiseTiles(HubTile);
        Assert.Empty(memory.Destinations());
    }
}
