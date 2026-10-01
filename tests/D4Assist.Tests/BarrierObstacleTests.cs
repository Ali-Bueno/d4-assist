using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

public class BarrierObstacleTests
{
    private const int Prop = 4, Effect = 3, NotGizmo = -1, WallSno = 1, EffectSno = 2, CrateSno = 3;

    // DRLGKit_fxKit_harmless_barrierWall_bone_01: aabbBounds centre (0, 0, 2.5), half-extents (0.5, 3, 3.5).
    private static readonly Vector3 WallCenter = new(0, 0, 2.5f), WallHalf = new(0.5f, 3, 3.5f);

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [WallSno] = ("DRLGKit_fxKit_harmless_barrierWall_bone_01", new(Prop, NotGizmo, WallCenter, WallHalf)),
        [EffectSno] = ("fxKit_bone_barrierWall_bonewall_frags", new(Effect, NotGizmo, Vector3.Zero, Vector3.Zero)), // no box
        [CrateSno] = ("Generic_Crate", new(Prop, NotGizmo, WallCenter, WallHalf)),
    };

    private static BarrierObstacles Barriers() =>
        new(sno => Actors.TryGetValue(sno, out var a) ? a.Name : null, sno => Actors.TryGetValue(sno, out var a) ? a.Info : null);

    private static readonly ActorState Wall = new(7, WallSno, 48.6f, -101f, 0);
    private static PlayerState At(float x, float y) => new(x, y, 0, 0);

    [Fact]
    public void AStandingWallBlocksItsFootprint()
    {
        var cells = Barriers().Update([Wall, new ActorState(8, EffectSno, 48.6f, -101f, 0), new ActorState(9, CrateSno, 40, -90, 0)], At(48, -95));

        Assert.Equal(DoorObstacles.Footprint(Wall, WallCenter, WallHalf).Cells.ToHashSet(), cells); // the wall only
        Assert.Contains((48, -101), cells);
    }

    [Fact]
    public void AWallOutOfTheClientsRange_StaysBlocked()
    {
        var barriers = Barriers();
        barriers.Update([Wall], At(48, -95));

        var cells = barriers.Update([], At(48, -40)); // 60 units away: the client dropped it

        Assert.Contains((48, -101), cells);
    }

    [Fact]
    public void AWallMissingNearItsSpot_IsOpen()
    {
        var barriers = Barriers();
        barriers.Update([Wall], At(48, -95));

        Assert.Empty(barriers.Update([], At(48.5f, -95.3f))); // live: 6 units from the spot after the sentinel died
    }

    [Fact]
    public void AWallListedAgainUnderANewId_IsTheSameWall()
    {
        var barriers = Barriers();
        var first = barriers.Update([Wall], At(48, -95));

        var again = barriers.Update([Wall with { Id = 99 }], At(48, -95));

        Assert.Equal(first.Count, again.Count);
    }

    [Fact]
    public void AWallOnAnotherFloor_DoesNotBlock() =>
        Assert.Empty(Barriers().Update([Wall with { Z = 30 }], At(48, -95)));

    [Fact]
    public void ANewDungeon_ForgetsTheWalls()
    {
        var barriers = Barriers();
        barriers.Update([Wall], At(48, -95));

        barriers.Reset();

        Assert.Empty(barriers.Update([], At(48, -40)));
    }
}
