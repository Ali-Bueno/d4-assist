using D4Assist.Game;

namespace D4Assist.Tests;

public class JumpLinkCollectorTests
{
    // Sightless_Skovos_SW_Traversal_01's six jump points at (64,64), in the scene's marker order (live 2026-10-18).
    private static readonly (float X, float Y)[] Points =
        [(91.4f, 87.0f), (87.4f, 89.8f), (76.8f, 89.8f), (74.7f, 85.4f), (79.9f, 74.3f), (82.7f, 70.3f)];

    [Fact]
    public void Each_jump_point_pairs_with_its_mutual_nearest()
    {
        var links = JumpLinkCollector.Pair(Points);

        Assert.Equal(3, links.Count);
        Assert.Contains(links, l => l.A == Points[0] && l.B == Points[1]);
        Assert.Contains(links, l => l.A == Points[2] && l.B == Points[3]);
        Assert.Contains(links, l => l.A == Points[4] && l.B == Points[5]);
    }

    [Fact]
    public void A_jump_point_with_no_partner_near_is_left_out()
    {
        Assert.Empty(JumpLinkCollector.Pair([(0f, 0f), (20f, 0f)]));
    }
}
