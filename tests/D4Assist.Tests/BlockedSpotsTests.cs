using System.Numerics;
using D4Assist.Game;

namespace D4Assist.Tests;

public class BlockedSpotsTests : IDisposable
{
    private const string World = "Sanctuary_Eastern_Continent";
    private readonly string _dir = Directory.CreateTempSubdirectory("d4assist-blocked-").FullName;
    private DateTime _now = new(2026, 11, 11, 19, 0, 0, DateTimeKind.Utc);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private BlockedSpots Load() => BlockedSpots.Load(_dir, () => _now);

    private static readonly HashSet<(int X, int Y)> Gap = [(-14, -699), (-13, -699), (-14, -700), (-13, -700)];

    [Fact]
    public void A_spot_learned_in_one_run_bars_the_way_in_the_next()
    {
        Load().Add(World, Gap, -10.5f, new Vector2(-11.5f, -699.7f));

        var wall = Assert.Single(Load().Walls(World));

        Assert.Equal(Gap, wall.Cells);
        Assert.Equal(-10.5f, wall.Z);
        Assert.Empty(Load().Walls("Hawe_Something_Else"));
    }

    [Fact]
    public void A_spot_older_than_the_keep_days_is_forgotten()
    {
        Load().Add(World, Gap, -10.5f, new Vector2(-11.5f, -699.7f));

        _now += TimeSpan.FromDays(BlockedSpots.KeepDays + 1);

        Assert.Empty(Load().Walls(World));
    }

    [Fact]
    public void Walking_through_a_spot_opens_it_in_the_file_too_but_swaying_where_stuck_does_not()
    {
        var spots = Load();
        spots.Add(World, Gap, -10.5f, new Vector2(-11f, -699.5f));

        Assert.Equal(0, spots.OpenWalkedThrough(World, new Vector3(-11.5f, -699.5f, -10.5f), 2f, 2f)); // not a cell of the spot
        Assert.Equal(0, spots.OpenWalkedThrough(World, new Vector3(-12.6f, -699.5f, -10.5f), 2f, 2f)); // 1.6 units past: swaying
        Assert.Equal(0, spots.OpenWalkedThrough(World, new Vector3(-13.5f, -699.5f, -4f), 2f, 2f));    // another floor
        Assert.Equal(1, spots.OpenWalkedThrough(World, new Vector3(-13.5f, -699.5f, -10.5f), 2f, 2f));

        Assert.Empty(spots.Walls(World));
        Assert.Empty(Load().Walls(World));
    }

    [Fact]
    public void An_unreadable_file_starts_empty()
    {
        File.WriteAllText(Path.Combine(_dir, BlockedSpots.FileName), "{ not json");

        Assert.Empty(Load().Walls(World));
    }
}
