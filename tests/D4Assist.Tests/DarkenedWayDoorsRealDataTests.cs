using System.Numerics;
using D4Assist.Game;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4Data.Tests;

namespace D4Assist.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class DarkenedWayDoorsRealDataTests
{
    // Tester 2026-11-01 (0.1.27, quest 486210 phase 260 "In Her Wake"): past the gate beyond Lilith's petal the step's trigger lies
    // down the ramp at x -129..-125, which four invisible doors shut (research/dungeon-data.md §4.1c). The doors as read live, a
    // hair off the half units, lay 4 cells each and leave column -129 between them; the paths pressed for ran down it.
    private const string World = "Frac_Underworld";
    private static readonly Vector2 Trigger = new(-127.6f, 47.9f);
    private static readonly (float X, float Y, float Z)[] InvisibleDoors =
        [(-127.53f, 59.47f, -6.8f), (-132.53f, 59.47f, -6.8f), (-127.53f, 54.2f, -9.1f), (-132.53f, 53.3f, -9.1f)];

    // Where the tester pressed D-pad left: past the gate, east of the petal, at the petal.
    public static TheoryData<float, float, float> Starts => new() { { -149.2f, 67.5f, 1.2f }, { -116.7f, 74.2f, 0f }, { -129.6f, 70f, -3f } };

    [Theory]
    [MemberData(nameof(Starts))]
    public void The_invisible_doors_send_the_path_round_by_the_free_climb(float x, float y, float z)
    {
        if (Load() is not { } loaded) return;
        var (paths, walls, store) = loaded;
        using var _ = store;
        var from = new Vector3(x, y, z);
        Assert.DoesNotContain(walls, w => w.Cells.Any(c => c.X == -129)); // the gap the dilation closes

        var open = paths.Find(World, from, Trigger);
        var shut = paths.Find(World, from, Trigger, walls[..2]); // the upper row only, as listed at the petal

        Assert.True(Length(from, open!) < 55f);
        Assert.NotNull(shut);
        Assert.True(Length(from, shut) > 80f);
        Assert.DoesNotContain(shut, p => p.X is > -131 and < -123 && p.Y is > 52 and < 66); // not down the ramp
    }

    private static float Length(Vector3 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        var at = new Vector2(from.X, from.Y);
        foreach (var p in points) (length, at) = (length + Vector2.Distance(at, p), p);
        return length;
    }

    private static (OverworldPaths Paths, DoorWall[] Walls, SnoStore Store)? Load()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return null;
        var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        int door = store.Resolve(schema.SnoGroup("ActorDefinition"), "DesignKit_InvisibleDoor");
        var info = new ActorDefinitionReader(store, schema).Read(door)!.Value;
        var walls = InvisibleDoors.Select(d =>
            new DoorWall(DoorObstacles.Footprint(new ActorState(1, door, d.X, d.Y, d.Z), info.BoundsCenter, info.BoundsHalfExtents).Cells.ToHashSet(), d.Z));
        return (OverworldPaths.FromGameData(store, schema, new object()), [.. walls], store);
    }
}
