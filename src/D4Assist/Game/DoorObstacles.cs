using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>A closed door laid on the radar grid: its cells, and the two ends of its width (where the walls should be).</summary>
public sealed record DoorFootprint(ActorState Door, IReadOnlyList<(int X, int Y)> Cells, Vector2 EndA, Vector2 EndB);

/// <summary>
/// Closed doors as radar obstacles. Doorways are baked walkable, so without this a closed door reads as floor
/// (research/dungeon-objectives.md §6). Each door's aabbBounds box, turned by its yaw, is laid on the 1-unit radar grid.
/// </summary>
public sealed class DoorObstacles(InteractableClassifier doors, Func<int, ActorDefinitionInfo?> definition)
{
    private const float CellSize = NavGridReader.MapResolution;
    // At least half a cell diagonal thick, so a thin slab (0.2-0.7 units) covers every cell centre it crosses, diagonal doors included.
    private static readonly float MinHalfThickness = CellSize * MathF.Sqrt(2) / 2;
    // One cell more on each side of the width: the 1-unit walk map ORs 0.5-unit voxels, so baked floor reaches up to a
    // cell past the door's edge (doorways of 3-5 cells around a 2.4-wide ProtoDun_Door, dungeon-objectives.md §6).
    private const float WidthPad = CellSize;

    /// <summary>The doors that are closed (never used) and on the player's floor.</summary>
    public IReadOnlyList<DoorFootprint> Closed(IEnumerable<ActorState> actors, float playerZ)
    {
        var result = new List<DoorFootprint>();
        foreach (var actor in actors)
        {
            if (actor.StateChanged || doors.Classify(actor.ActorSno)?.Kind != InteractableKind.Door) continue;
            if (definition(actor.ActorSno) is not { } info || info.BoundsHalfExtents.X <= 0 || info.BoundsHalfExtents.Y <= 0) continue;
            if (!OnPlayerFloor(actor, info, playerZ)) continue;
            result.Add(Footprint(actor, info.BoundsCenter, info.BoundsHalfExtents));
        }
        return result;
    }

    // A door whose bottom is more than its own height from the player's feet stands on another floor.
    private static bool OnPlayerFloor(ActorState door, ActorDefinitionInfo info, float playerZ)
    {
        float bottom = door.Z + info.BoundsCenter.Z - info.BoundsHalfExtents.Z;
        return MathF.Abs(playerZ - bottom) <= 2 * info.BoundsHalfExtents.Z;
    }

    public static DoorFootprint Footprint(ActorState door, Vector3 center, Vector3 half)
    {
        float cos = MathF.Cos(door.Yaw), sin = MathF.Sin(door.Yaw);
        var middle = new Vector2(door.X + center.X * cos - center.Y * sin, door.Y + center.X * sin + center.Y * cos);
        var across = new Vector2(-sin, cos); // the door's local +Y: its width
        float hx = MathF.Max(half.X, MinHalfThickness), hy = half.Y + WidthPad, reach = MathF.Sqrt(hx * hx + hy * hy);

        var cells = new List<(int X, int Y)>();
        for (int x = Cell(middle.X - reach); x <= Cell(middle.X + reach); x++)
            for (int y = Cell(middle.Y - reach); y <= Cell(middle.Y + reach); y++)
            {
                var offset = new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize) - middle;
                float along = offset.X * cos + offset.Y * sin, width = Vector2.Dot(offset, across);
                if (MathF.Abs(along) <= hx && MathF.Abs(width) <= hy) cells.Add((x, y));
            }
        return new DoorFootprint(door, cells, middle - across * (hy + CellSize), middle + across * (hy + CellSize));
    }

    private static int Cell(float coordinate) => (int)MathF.Floor(coordinate / CellSize);
}
