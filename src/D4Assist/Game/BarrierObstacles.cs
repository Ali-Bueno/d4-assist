using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Barrier walls a dungeon raises until an objective is done (research/dungeon-objectives.md §13): the floor under them is
/// baked walkable, so the guide and the radar need them laid on the 1-unit grid while they stand. A barrier is a non-gizmo
/// actor named <c>*barrierWall*</c> with a box (DRLGKit_fxKit_harmless_barrierWall_bone_01: 1 x 6 x 7 units); its effect
/// companions have no box. Live 2026-10-01 (Maulwood): the three barrier actors left the actor list when the sentinel died.
/// The client also drops actors far away, so a barrier seen once stays blocked until the player is near its spot without it.
/// </summary>
public sealed class BarrierObstacles(Func<int, string?> nameOf, Func<int, ActorDefinitionInfo?> definition, Action<string>? log = null)
{
    private const string NameToken = "barrierWall";

    /// <summary>Within this distance of a barrier's spot the client lists it if it still stands (actors 30+ units away were listed live).</summary>
    public const float ConfirmRadiusUnits = 20f;

    private sealed record Barrier(Vector2 Spot, IReadOnlyList<(int X, int Y)> Cells);

    // Keyed by spot, not id: an actor dropped and listed again comes back under a new id.
    private readonly Dictionary<(int X, int Y), Barrier> _standing = [];
    private readonly Dictionary<int, bool> _isBarrier = [];

    /// <summary>The cells every standing barrier covers, after this actor poll.</summary>
    public IReadOnlySet<(int X, int Y)> Update(IReadOnlyList<ActorState> actors, PlayerState player)
    {
        var listed = new HashSet<(int X, int Y)>();
        foreach (var actor in actors)
        {
            if (!IsBarrier(actor.ActorSno) || definition(actor.ActorSno) is not { } info) continue;
            if (!OnPlayerFloor(actor, info, player.Z)) continue;
            var key = Key(actor.X, actor.Y);
            listed.Add(key);
            if (_standing.ContainsKey(key)) continue;
            var cells = DoorObstacles.Footprint(actor, info.BoundsCenter, info.BoundsHalfExtents).Cells;
            _standing[key] = new Barrier(new Vector2(actor.X, actor.Y), cells);
            log?.Invoke($"BARRIER standing: {nameOf(actor.ActorSno)} at ({actor.X:F1},{actor.Y:F1}), {cells.Count} cells");
        }

        var here = new Vector2(player.X, player.Y);
        foreach (var (key, barrier) in _standing.ToList())
        {
            if (listed.Contains(key) || Vector2.Distance(here, barrier.Spot) > ConfirmRadiusUnits) continue;
            _standing.Remove(key);
            log?.Invoke($"BARRIER gone: at ({barrier.Spot.X:F1},{barrier.Spot.Y:F1})");
        }
        return _standing.Values.SelectMany(b => b.Cells).ToHashSet();
    }

    /// <summary>A new dungeon: nothing stands.</summary>
    public void Reset() => _standing.Clear();

    private bool IsBarrier(int actorSno)
    {
        if (_isBarrier.TryGetValue(actorSno, out bool cached)) return cached;
        bool barrier = nameOf(actorSno)?.Contains(NameToken, StringComparison.OrdinalIgnoreCase) == true
            && definition(actorSno) is { GizmoType: < 0 } info && info.BoundsHalfExtents.X > 0 && info.BoundsHalfExtents.Y > 0;
        return _isBarrier[actorSno] = barrier;
    }

    // A wall whose bottom is more than its own height from the player's feet stands on another floor (as for doors).
    private static bool OnPlayerFloor(ActorState wall, ActorDefinitionInfo info, float playerZ) =>
        MathF.Abs(playerZ - (wall.Z + info.BoundsCenter.Z - info.BoundsHalfExtents.Z)) <= 2 * info.BoundsHalfExtents.Z;

    private static (int X, int Y) Key(float x, float y) => ((int)MathF.Round(x), (int)MathF.Round(y));
}
