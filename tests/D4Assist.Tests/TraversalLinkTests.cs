using System.Numerics;
using D4Assist.Game;

namespace D4Assist.Tests;

public class TraversalLinkTests
{
    // Live 2026-10-22, "Die Leere": the boss-room tile at (-64,-64), from its (Merged) layer, in world units.
    private static readonly TraversalMarker[] BossRoom =
    [
        new(TraversalKind.Slide, new(8.7f, -3.0f, 2.3f)), new(TraversalKind.Exit, new(10.5f, -9.7f, -2.7f)),
        new(TraversalKind.ClimbDown, new(4.9f, -21.0f, -2.1f)), new(TraversalKind.ClimbUp, new(4.9f, -21.0f, -7.1f)),
        new(TraversalKind.Exit, new(5.3f, -21.9f, -2.1f)), new(TraversalKind.Exit, new(4.0f, -19.2f, -7.1f)),
        new(TraversalKind.Jump, new(-7.7f, -12.4f, -7.5f)), new(TraversalKind.Jump, new(-3.0f, -13.7f, -7.5f)),
        new(TraversalKind.Exit, new(-1.9f, -13.9f, -7.5f)), new(TraversalKind.Exit, new(-8.7f, -12.1f, -7.5f)),
    ];

    [Fact]
    public void A_slide_leads_one_way_to_its_foot_a_climb_joins_its_top_and_bottom_and_jumps_pair()
    {
        var links = JumpLinkCollector.Links(BossRoom);

        Assert.Equal(3, links.Count);
        Assert.Contains(links, l => l.A == (8.7f, -3.0f) && l.B == (10.5f, -9.7f) && l.OneWay);
        Assert.Contains(links, l => l.A == (5.3f, -21.9f) && l.B == (4.0f, -19.2f) && !l.OneWay);
        Assert.Contains(links, l => l.A == (-7.7f, -12.4f) && l.B == (-3.0f, -13.7f) && !l.OneWay);
    }

    [Fact]
    public void A_climb_without_exits_pairs_its_foot_and_top_as_jump_points()
    {
        var links = JumpLinkCollector.Links([new(TraversalKind.ClimbUp, new(0f, 0f, 0f)), new(TraversalKind.ClimbDown, new(0f, 5f, 4f))]);

        Assert.Equal((0f, 0f), Assert.Single(links).A);
    }

    // The Temple of the Deathspeaker's SW POI tile at (0,0): four Traversal_Jump_6m_Gizmo prefabs in a chain, their ends 4.9 apart
    // across each gap and 4.1 and 4.6 apart from the next prefab's on one ledge (prefab 1's second end and 2's first, 3's and 4's).
    private static readonly TraversalMarker[] TempleChain =
    [
        new(TraversalKind.Jump, new(2.1f, 10.4f, 5f), 1), new(TraversalKind.Jump, new(4.8f, 6.3f, 5f), 1),
        new(TraversalKind.Jump, new(8.6f, 4.8f, 5f), 2), new(TraversalKind.Jump, new(13.5f, 5.3f, 5f), 2),
        new(TraversalKind.Jump, new(5.6f, 16.8f, 5f), 3), new(TraversalKind.Jump, new(10.4f, 15.9f, 5f), 3),
        new(TraversalKind.Jump, new(14.6f, 17.7f, 5.4f), 4), new(TraversalKind.Jump, new(18.3f, 20.8f, 5.4f), 4),
    ];

    [Fact]
    public void Each_traversal_prefab_joins_its_own_ends_though_another_prefab_stands_nearer()
    {
        var links = JumpLinkCollector.Links(TempleChain);

        Assert.Equal(4, links.Count);
        for (int i = 0; i < TempleChain.Length; i += 2)
            Assert.Contains(links, l => l.A == Flat(TempleChain[i]) && l.B == Flat(TempleChain[i + 1]) && !l.OneWay);
    }

    [Fact]
    public void A_one_way_jump_prefab_leads_from_its_jump_point_to_the_exit_it_lands_at()
    {
        // Sightless_Mephisto_FLEX_POI_BossRoom_InariusPrison's Traversal_Jump_4m_OneWay_InariusBossArena: one end, its exit 5.9 on.
        var link = Assert.Single(JumpLinkCollector.Links(
            [new(TraversalKind.Jump, new(30.3f, 13.6f, 0f), 1), new(TraversalKind.Exit, new(33.3f, 18.7f, 0f), 1)]));

        Assert.Equal(((30.3f, 13.6f), (33.3f, 18.7f), true), (link.A, link.B, link.OneWay));
    }

    [Fact]
    public void A_climb_prefab_joins_its_top_and_bottom_exits_though_its_gizmos_stand_apart()
    {
        // Cave_Flooded_FLEX_POI_GrottoEntrance_01's first Traversal_RungClimb_8m_Gizmo: its down and up gizmos 1.2 apart.
        var link = Assert.Single(JumpLinkCollector.Links(
        [
            new(TraversalKind.Exit, new(5.1f, 14.2f, 0.1f), 1), new(TraversalKind.ClimbDown, new(6.1f, 14.2f, 0.1f), 1),
            new(TraversalKind.ClimbUp, new(7.3f, 14.2f, -7.9f), 1), new(TraversalKind.Exit, new(8.6f, 14.2f, -7.9f), 1),
        ]));

        Assert.Equal(((5.1f, 14.2f), (8.6f, 14.2f)), (link.A, link.B));
    }

    [Fact]
    public void A_rope_prefab_joins_ends_farther_apart_than_a_jump_and_a_prefab_of_another_shape_pairs_by_distance()
    {
        // A Traversal_HandOverHand_15m_Complete's two ends; then a prefab of three jump points, two of them mutual nearest.
        var links = JumpLinkCollector.Links(
        [
            new(TraversalKind.Jump, new(0f, 0f, 0f), 1), new(TraversalKind.Jump, new(15f, 0f, 0f), 1),
            new(TraversalKind.Jump, new(0f, 40f, 0f), 2), new(TraversalKind.Jump, new(5f, 40f, 0f), 2), new(TraversalKind.Jump, new(0f, 60f, 0f), 2),
        ]);

        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.A == (0f, 0f) && l.B == (15f, 0f));
        Assert.Contains(links, l => l.A == (0f, 40f) && l.B == (5f, 40f));
    }

    private static (float X, float Y) Flat(TraversalMarker marker) => (marker.At.X, marker.At.Y);

    [Theory]
    [InlineData("Traversal_Gizmo_Slide", TraversalKind.Slide)]
    [InlineData("Traversal_Gizmo_RungClimb_Down", TraversalKind.ClimbDown)]
    [InlineData("MarkerLocation_TraversalExit", TraversalKind.Exit)]
    [InlineData("Traversal_Gizmo_DuckUnder", TraversalKind.Jump)]
    [InlineData("Diablo_Statue", TraversalKind.None)]
    public void Traversal_markers_are_known_by_name(string name, TraversalKind kind) => Assert.Equal(kind, TraversalKinds.Of(name));
}
