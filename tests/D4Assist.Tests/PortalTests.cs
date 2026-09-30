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
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
        public (string Name, bool IsDungeon)? CurrentWorld => ("DGN_Test", true);
        public void RaiseWorld() => WorldChanged?.Invoke(0, "DGN_Test", true);
        public void RaisePlayer(float x, float y) => PlayerMoved?.Invoke(new PlayerState(x, y, 1, 0));
        public void RaiseActors(params ActorState[] actors) => ActorsUpdated?.Invoke(actors);
    }

    private static (Feed Feed, PortalMemory Memory) NewMemory()
    {
        var feed = new Feed();
        return (feed, new PortalMemory(feed, a => Classifier.IsPortal(a.ActorSno), sno => Actors.GetValueOrDefault(sno).Name));
    }

    private static ActorState Portal(uint id, float x, float y, int sno = PortalSno) => new(id, sno, x, y, 1);

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
        var selector = new LoopTargetSelector(new HashSet<int>(), 1.0, null, 1.0, _ => new HashSet<int>(), isPortal: Classifier.IsPortal);

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
}
