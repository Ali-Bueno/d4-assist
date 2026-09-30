using System.Numerics;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class DoorObstacleTests
{
    private const int Gizmo = 2, DoorSno = 1, StartOpenSno = 2, ChestSno = 3;

    // ProtoDun_Door: aabbBounds centre (0, 0, 1.4), half-extents (0.25, 1.2, 1.4); research/dungeon-objectives.md §6.
    private static readonly Vector3 DoorCenter = new(0, 0, 1.4f), DoorHalf = new(0.25f, 1.2f, 1.4f);

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [DoorSno] = ("ProtoDun_Door", new(Gizmo, Conventions.GizmoDoor, DoorCenter, DoorHalf)),
        [StartOpenSno] = ("DGN_Boss_Door_StartOpen", new(Gizmo, Conventions.GizmoDoor, DoorCenter, DoorHalf)),
        [ChestSno] = ("ProtoDunKeep_Chest_01", new(Gizmo, Conventions.GizmoChest, DoorCenter, DoorHalf)),
    };

    private static ActorDefinitionInfo? Info(int sno) => Actors.TryGetValue(sno, out var a) ? a.Info : null;

    private static DoorObstacles Obstacles() =>
        new(new InteractableClassifier(Info, sno => Actors.TryGetValue(sno, out var a) ? a.Name : null), Info);

    [Fact]
    public void AnUnrotatedDoorIsAThinWallAcrossY()
    {
        var footprint = DoorObstacles.Footprint(new ActorState(1, DoorSno, 10.5f, 20.5f, 0), DoorCenter, DoorHalf);

        Assert.All(footprint.Cells, c => Assert.Equal(10, c.X)); // one cell thick along X, the passage axis
        Assert.Equal([18, 19, 20, 21, 22], footprint.Cells.Select(c => c.Y).Order()); // 2.4 wide plus half a cell each side
        Assert.Equal(10.5f, footprint.EndA.X, 3);
        Assert.True(footprint.EndA.Y < 18 && footprint.EndB.Y > 22);
    }

    [Fact]
    public void AQuarterTurnLaysTheDoorAcrossX()
    {
        var footprint = DoorObstacles.Footprint(new ActorState(1, DoorSno, 10.5f, 20.5f, 0, Yaw: MathF.PI / 2), DoorCenter, DoorHalf);

        Assert.All(footprint.Cells, c => Assert.Equal(20, c.Y));
        Assert.Equal([8, 9, 10, 11, 12], footprint.Cells.Select(c => c.X).Order());
    }

    [Fact]
    public void ADiagonalDoorLeavesNoFourConnectedGap()
    {
        var cells = DoorObstacles.Footprint(new ActorState(1, DoorSno, 10.5f, 20.5f, 0, Yaw: MathF.PI / 4), DoorCenter, DoorHalf).Cells.ToHashSet();

        // Walking straight along X past the door line at every Y it spans must hit a door cell.
        foreach (var y in cells.Select(c => c.Y).Distinct().Where(y => y > cells.Min(c => c.Y) && y < cells.Max(c => c.Y)))
            Assert.Contains(cells, c => c.Y == y);
    }

    [Fact]
    public void OnlyClosedDoorsOnThePlayersFloorCount()
    {
        var actors = new[]
        {
            new ActorState(1, DoorSno, 0, 0, 0),
            new ActorState(2, DoorSno, 5, 0, 0, StateChanged: true), // opened
            new ActorState(3, StartOpenSno, 10, 0, 0),
            new ActorState(4, ChestSno, 15, 0, 0),
            new ActorState(5, DoorSno, 20, 0, 12), // a floor above
            new ActorState(6, DoorSno, 25, 0, 0, GizmoStateSet: true), // in the open state
        };

        var closed = Obstacles().Closed(actors, playerZ: 0);

        Assert.Equal([1u], closed.Select(d => d.Door.Id));
    }

    [Fact]
    public void RadarWorld_ADoorCoversOnlyWalkableFloor()
    {
        var world = new DungeonRadarWorld(new EmptyWalkability());
        world.SetClosedDoors(new HashSet<(int X, int Y)> { (0, 0) });

        Assert.Equal(D4Assist.Radar.RadarCell.Unknown, world.Cell(0, 0)); // no tile loaded: nothing to cover
    }

    [Fact]
    public void Yaw_ReadsTheRotationAboutZ()
    {
        Assert.Equal(MathF.PI / 2, ActorFilter.Yaw(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)), 4);
        Assert.Equal(0, ActorFilter.Yaw(Quaternion.Identity), 4);
    }

    private sealed class EmptyWalkability : D4Assist.Navigation.ITileWalkability
    {
        public bool TryGet(int sceneSno, out D4Assist.Navigation.WalkGrid grid) { grid = null!; return false; }
    }
}
