using System.Numerics;
using D4Assist.Game;

namespace D4Assist.Tests;

public class TraversalLinkTests
{
    // Live 2026-10-22, "Die Leere": the boss-room tile at (-64,-64), from its (Merged) layer, in world units.
    private static readonly (TraversalKind, Vector3)[] BossRoom =
    [
        (TraversalKind.Slide, new(8.7f, -3.0f, 2.3f)), (TraversalKind.Exit, new(10.5f, -9.7f, -2.7f)),
        (TraversalKind.ClimbDown, new(4.9f, -21.0f, -2.1f)), (TraversalKind.ClimbUp, new(4.9f, -21.0f, -7.1f)),
        (TraversalKind.Exit, new(5.3f, -21.9f, -2.1f)), (TraversalKind.Exit, new(4.0f, -19.2f, -7.1f)),
        (TraversalKind.Jump, new(-7.7f, -12.4f, -7.5f)), (TraversalKind.Jump, new(-3.0f, -13.7f, -7.5f)),
        (TraversalKind.Exit, new(-1.9f, -13.9f, -7.5f)), (TraversalKind.Exit, new(-8.7f, -12.1f, -7.5f)),
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
        var links = JumpLinkCollector.Links([(TraversalKind.ClimbUp, new(0f, 0f, 0f)), (TraversalKind.ClimbDown, new(0f, 5f, 4f))]);

        Assert.Equal((0f, 0f), Assert.Single(links).A);
    }

    [Theory]
    [InlineData("Traversal_Gizmo_Slide", TraversalKind.Slide)]
    [InlineData("Traversal_Gizmo_RungClimb_Down", TraversalKind.ClimbDown)]
    [InlineData("MarkerLocation_TraversalExit", TraversalKind.Exit)]
    [InlineData("Traversal_Gizmo_DuckUnder", TraversalKind.Jump)]
    [InlineData("Diablo_Statue", TraversalKind.None)]
    public void Traversal_markers_are_known_by_name(string name, TraversalKind kind) => Assert.Equal(kind, TraversalKinds.Of(name));
}
