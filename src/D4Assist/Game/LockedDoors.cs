using System.Numerics;

namespace D4Assist.Game;

/// <summary>
/// Closed doors the player can't open yet, as walls for the guide's routes: doorways are baked walkable (research/dungeon-objectives.md
/// §6), so without them the guide leads through a key door before its key is found (§21). A door is locked while the game won't let
/// the player use it (quest-locked, script-disabled), or once the player stood at it and walked away with it still closed (tried),
/// until its quest state changes or the guide is switched on again. Remembered by spot while the client drops it far away; one seen
/// open, or missing where the client would list it, is open.
/// </summary>
public sealed class LockedDoors(DoorObstacles doors, Func<int, string?> nameOf, Action<string>? log = null)
{
    /// <summary>Within this distance of a door's spot the client lists it if it still stands: chests left the list at 16-18 units
    /// and came back at ~15 (research/dungeon-objectives.md §6).</summary>
    public const float ListedWithinUnits = 15f;

    // A placed door never moves; one listed again under a new id stands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Door(int sno, DoorFootprint footprint)
    {
        public int Sno { get; } = sno;
        public Vector2 Spot { get; } = new(footprint.Door.X, footprint.Door.Y);
        public DoorFootprint Footprint { get; set; } = footprint;
        public bool GameLocked { get; set; }
        public bool AtIt { get; set; }
        public bool Tried { get; set; }
        /// <summary>The door's quest state when last read, and when the player tried it.</summary>
        public (bool, bool, bool) State { get; set; }
        public (bool, bool, bool) TriedState { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<(int X, int Y), Door> _doors = [];

    /// <summary>The cells every locked door covers, after this actor poll.</summary>
    public IReadOnlySet<(int X, int Y)> Update(IReadOnlyList<ActorState> actors, PlayerState player)
    {
        lock (_gate)
        {
            var listed = new HashSet<(int X, int Y)>();
            foreach (var footprint in doors.Closed(actors, player.Z))
            {
                var key = Key(footprint.Door);
                listed.Add(key);
                if (!_doors.TryGetValue(key, out var door)) _doors[key] = door = new Door(footprint.Door.ActorSno, footprint);
                door.Footprint = footprint;
                Observe(door, footprint.Door);
            }
            var here = new Vector2(player.X, player.Y);
            foreach (var (key, door) in _doors.ToList())
            {
                // Listed closed on another floor still stands; listed open, or missing where the client lists doors, is open.
                var actor = actors.FirstOrDefault(a => a.ActorSno == door.Sno && Vector2.Distance(new(a.X, a.Y), door.Spot) <= SameSpotUnits);
                bool open = actor is not null ? actor.StateChanged || actor.GizmoStateSet : !listed.Contains(key) && Vector2.Distance(here, door.Spot) <= ListedWithinUnits;
                if (open)
                {
                    _doors.Remove(key);
                    if (door.GameLocked || door.Tried) Log("open", door);
                    continue;
                }
                Track(door, here);
            }
            return _doors.Values.Where(d => d.GameLocked || d.Tried).SelectMany(d => d.Footprint.Cells).ToHashSet();
        }
    }

    /// <summary>True while a locked door of <paramref name="actorSno"/> stands at <paramref name="at"/>: no objective to lead to.</summary>
    public bool IsLocked(int actorSno, (float X, float Y) at)
    {
        lock (_gate)
            return _doors.Values.Any(d => (d.GameLocked || d.Tried) && d.Sno == actorSno && Vector2.Distance(d.Spot, new(at.X, at.Y)) <= SameSpotUnits);
    }

    /// <summary>Lets the doors the player tried lead again (the guide was just switched on by a lost player).</summary>
    public void Rearm()
    {
        lock (_gate)
            foreach (var door in _doors.Values.Where(d => d.Tried))
            {
                door.Tried = false;
                Log("re-armed", door);
            }
    }

    /// <summary>A new world: no door known.</summary>
    public void Reset()
    {
        lock (_gate) _doors.Clear();
    }

    // The game's own lock follows its flags; a tried door waits for its quest to change (the key found: the phase now names it).
    private void Observe(Door door, ActorState actor)
    {
        if (door.GameLocked != actor.Unusable)
        {
            door.GameLocked = actor.Unusable;
            Log(actor.Unusable ? "locked" : "unlocked", door);
        }
        door.State = (actor.Unusable, actor.QuestStep, actor.NamedByQuest);
        if (!door.Tried || door.State == door.TriedState) return;
        door.Tried = false;
        Log("re-armed (quest changed)", door);
    }

    // Stood at it (within reach of its width) and walked away with it still closed: it doesn't open yet.
    private void Track(Door door, Vector2 player)
    {
        if (door.GameLocked || door.Tried) return;
        float distance = DistanceToSegment(player, door.Footprint.EndA, door.Footprint.EndB);
        if (distance <= ObjectiveMemory.InteractUnits) door.AtIt = true;
        else if (door.AtIt && distance > ObjectiveMemory.LeaveUnits)
        {
            door.AtIt = false;
            door.Tried = true;
            door.TriedState = door.State;
            Log("tried", door);
        }
    }

    private void Log(string what, Door door) =>
        log?.Invoke($"DOOR {what}: {nameOf(door.Sno) ?? door.Sno.ToString()} at ({door.Spot.X:F1},{door.Spot.Y:F1})");

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1) : 0;
        return Vector2.Distance(p, a + ab * t);
    }

    private static (int X, int Y) Key(ActorState door) => ((int)MathF.Round(door.X), (int)MathF.Round(door.Y));
}
