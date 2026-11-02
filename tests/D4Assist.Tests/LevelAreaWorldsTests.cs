using D4Assist.Game;

namespace D4Assist.Tests;

public class LevelAreaWorldsTests
{
    private static readonly Dictionary<int, string> Areas = new()
    {
        [1] = "CSD_Frac_Underworld_AncientCity_Holdout", [2] = "Frac_Underworld_Depth_1", [3] = "Frac_Glacier_East",
    };

    private static readonly HashSet<string> Worlds = ["CSD_Frac_Underworld_AncientCity", "Frac_Underworld", "Sanctuary_Eastern_Continent"];

    private static LevelAreaWorlds Read() => new(sno => Areas.GetValueOrDefault(sno), Worlds.Contains);

    // Tester 2026-11-01: back in the Darkened Way from the Ancient City, the map still listed the dungeon's quest marker.
    [Fact]
    public void A_level_area_lies_in_the_world_its_name_begins_with_and_the_open_worlds_in_none()
    {
        var worlds = Read();

        Assert.Equal("CSD_Frac_Underworld_AncientCity", worlds.WorldOf(1));
        Assert.Equal("Frac_Underworld", worlds.WorldOf(2));
        Assert.Null(worlds.WorldOf(3));
        Assert.Null(worlds.WorldOf(99));
        Assert.False(worlds.InWorld(1, "Frac_Underworld"));
        Assert.True(worlds.InWorld(2, "Frac_Underworld"));
        Assert.True(worlds.InWorld(3, "Sanctuary_Eastern_Continent"));
    }
}
