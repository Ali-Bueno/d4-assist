using System.Numerics;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Breakable props (gizmo type 48: desks, shelves, barrels) the static walk map calls floor, for the guide to walk around
/// (research/dungeon-objectives.md §30). One stands until the game breaks it: its game record gone after it had one, marked operated,
/// or missing from its spot while the player is near; a spot broken once stays broken in this world.
/// </summary>
public sealed class BreakableProps(Func<int, string?> nameOf, Func<int, ActorDefinitionInfo?> definition, Action<string>? log = null)
{
    /// <summary>Within this distance of a prop's spot the client lists it if it still stands, as for barriers.</summary>
    public const float ConfirmRadiusUnits = BarrierObstacles.ConfirmRadiusUnits;

    /// <summary>A prop whose base is this near the player's feet stands on the player's floor: a tile's stacked floors lie farther
    /// apart (a slide drops 5 units, research/dungeon-data.md §4.1c).</summary>
    public const float SameFloorUnits = 2f;

    private sealed class Prop(int sno, Vector2 spot, IReadOnlyList<(int X, int Y)> cells, bool hadRecord)
    {
        public int Sno { get; } = sno;
        public Vector2 Spot { get; } = spot;
        public IReadOnlyList<(int X, int Y)> Cells { get; } = cells;
        public bool HadRecord { get; set; } = hadRecord;
        public int MissingPolls { get; set; }
    }

    // Keyed by spot, not id: an actor dropped and listed again comes back under a new id.
    private readonly Dictionary<(int X, int Y), Prop> _standing = [];
    private readonly HashSet<(int X, int Y)> _broken = [];
    private IReadOnlySet<(int X, int Y)> _cells = new HashSet<(int X, int Y)>();
    private bool _changed;

    /// <summary>The cells every standing prop covers, after this actor poll.</summary>
    public IReadOnlySet<(int X, int Y)> Update(IReadOnlyList<ActorState> actors, PlayerState player)
    {
        var listed = new HashSet<(int X, int Y)>();
        foreach (var actor in actors)
        {
            if (definition(actor.ActorSno) is not { GizmoType: Conventions.GizmoBreakable } info) continue;
            var key = Key(actor.X, actor.Y);
            if (_broken.Contains(key)) continue;
            if (_standing.TryGetValue(key, out var prop) && prop.Sno == actor.ActorSno)
            {
                if (actor.Operated || (prop.HadRecord && actor.ClientOnly)) { Break(key, prop, actor.Operated ? "operated" : "its game record gone"); continue; }
                prop.HadRecord |= !actor.ClientOnly;
                listed.Add(key);
                continue;
            }
            if (actor.Operated || !OnPlayerFloor(actor, info, player.Z)) continue;
            var cells = DoorObstacles.Footprint(actor, info.PropCenter, info.PropHalfExtents).Cells;
            if (cells.Count == 0) continue;
            _standing[key] = new Prop(actor.ActorSno, new Vector2(actor.X, actor.Y), cells, !actor.ClientOnly);
            listed.Add(key);
            _changed = true;
            log?.Invoke($"PROP standing: {nameOf(actor.ActorSno)} at ({actor.X:F1},{actor.Y:F1}), {cells.Count} cells");
        }

        var here = new Vector2(player.X, player.Y);
        foreach (var (key, prop) in _standing.ToList())
        {
            prop.MissingPolls = listed.Contains(key) || Vector2.Distance(here, prop.Spot) > ConfirmRadiusUnits ? 0 : prop.MissingPolls + 1;
            if (prop.MissingPolls >= ObjectiveMemory.MissingPollsForDone) Break(key, prop, "gone");
        }
        if (_changed) _cells = _standing.Values.SelectMany(p => p.Cells).ToHashSet();
        _changed = false;
        return _cells;
    }

    /// <summary>A new dungeon: nothing stands or was broken.</summary>
    public void Reset()
    {
        _standing.Clear();
        _broken.Clear();
        _cells = new HashSet<(int X, int Y)>();
    }

    private void Break((int X, int Y) key, Prop prop, string cause)
    {
        _standing.Remove(key);
        _broken.Add(key);
        _changed = true;
        log?.Invoke($"PROP broken ({cause}): {nameOf(prop.Sno)} at ({prop.Spot.X:F1},{prop.Spot.Y:F1})");
    }

    private static bool OnPlayerFloor(ActorState prop, ActorDefinitionInfo info, float playerZ) =>
        MathF.Abs(playerZ - (prop.Z + info.PropCenter.Z - info.PropHalfExtents.Z)) <= SameFloorUnits;

    private static (int X, int Y) Key(float x, float y) => ((int)MathF.Round(x), (int)MathF.Round(y));
}
