using System.Numerics;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class BreakablePropsTests
{
    private const int Gizmo = 2, NotGizmo = -1, TableSno = 1, StatueSno = 3;

    // MageHalls_Prop_Furniture_Table_Medium_Corner_1x2_01_Arrangement: collision box centre (0, 0, 0.8), half-extents (0.7, 1.2, 0.8).
    private static readonly Vector3 TableCenter = new(0, 0, 0.8f), TableHalf = new(0.7f, 1.2f, 0.8f);

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [TableSno] = ("MageHalls_Prop_Furniture_Table_Medium_Corner_1x2_01_Arrangement",
            new(Gizmo, Conventions.GizmoBreakable, PropCenter: TableCenter, PropHalfExtents: TableHalf)),
        [StatueSno] = ("MageHalls_Statue", new(Gizmo, NotGizmo, PropCenter: TableCenter, PropHalfExtents: TableHalf)),
    };

    private static BreakableProps Props() =>
        new(sno => Actors.TryGetValue(sno, out var a) ? a.Name : null, sno => Actors.TryGetValue(sno, out var a) ? a.Info : null);

    private static readonly ActorState Table = new(7, TableSno, 11.5f, 28.5f, 0);
    private static PlayerState At(float x, float y, float z = 0) => new(x, y, z, 0);

    [Fact]
    public void A_standing_prop_covers_its_footprint()
    {
        var cells = Props().Update([Table, new ActorState(9, StatueSno, 20, 20, 0)], At(5, 28));

        Assert.Equal(DoorObstacles.Footprint(Table, TableCenter, TableHalf).Cells.ToHashSet(), cells);
        Assert.Contains((11, 28), cells);
    }

    [Fact]
    public void A_prop_on_another_floor_is_left_out_until_the_player_reaches_it()
    {
        var props = Props();

        Assert.Empty(props.Update([Table with { Z = 6 }], At(5, 28)));
        Assert.NotEmpty(props.Update([Table with { Z = 6 }], At(5, 28, z: 6)));
    }

    [Fact]
    public void A_prop_whose_game_record_goes_is_broken_and_stays_broken()
    {
        var props = Props();
        props.Update([Table], At(5, 28));

        Assert.Empty(props.Update([Table with { ClientOnly = true }], At(5, 28)));
        Assert.Empty(props.Update([Table with { Id = 12 }], At(5, 28))); // listed again under a new id: still broken
    }

    [Fact]
    public void A_prop_listed_without_a_game_record_from_the_start_stands()
    {
        var props = Props();
        props.Update([Table with { ClientOnly = true }], At(5, 28));

        Assert.NotEmpty(props.Update([Table with { ClientOnly = true }], At(5, 28)));
    }

    [Fact]
    public void An_operated_prop_is_broken()
    {
        var props = Props();
        props.Update([Table], At(5, 28));

        Assert.Empty(props.Update([Table with { Operated = true }], At(5, 28)));
    }

    [Fact]
    public void A_prop_missing_near_its_spot_for_a_while_is_broken_far_away_it_stands()
    {
        var props = Props();
        props.Update([Table], At(5, 28));

        Assert.NotEmpty(props.Update([], At(11, -40))); // 68 units away: the client dropped it
        for (var poll = 1; poll < ObjectiveMemory.MissingPollsForDone; poll++) Assert.NotEmpty(props.Update([], At(5, 28)));
        Assert.Empty(props.Update([], At(5, 28)));
    }

    [Fact]
    public void A_new_dungeon_forgets_standing_and_broken_props()
    {
        var props = Props();
        props.Update([Table], At(5, 28));
        props.Update([Table with { Operated = true }], At(5, 28));

        props.Reset();

        Assert.NotEmpty(props.Update([Table], At(5, 28)));
    }
}
