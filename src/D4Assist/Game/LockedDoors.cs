using System.Numerics;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>A locked door's 1-unit world cells and the height of its base; a tall blocker also says how far up it reaches.</summary>
public sealed record DoorWall(IReadOnlySet<(int X, int Y)> Cells, float Z, float Height = 0);

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
        /// <summary>Listed once on the player's floor: only those wall the one-floor walk map (a door below a ledge isn't on it).</summary>
        public bool SeenOnFloor { get; set; }
        /// <summary>On the player's floor when last listed.</summary>
        public bool OnFloor { get; set; }
        /// <summary>The door's quest state when last read, and when the player tried it.</summary>
        public (bool, bool, bool) State { get; set; }
        public (bool, bool, bool) TriedState { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<(int X, int Y), Door> _doors = [];
    private HashSet<(int X, int Y)> _listed = [];

    /// <summary>Changes whenever a door starts or stops being a wall: a path found before it may lead through a door now shut.</summary>
    public int Version { get { lock (_gate) return _version; } }
    private int _version;

    /// <summary>The cells every locked door on the player's floor covers, after this actor poll.</summary>
    public IReadOnlySet<(int X, int Y)> Update(IReadOnlyList<ActorState> actors, PlayerState player)
    {
        lock (_gate)
        {
            var listed = new HashSet<(int X, int Y)>();
            // Every floor's doors: paths over several heights need the Darkened Way's invisible doors below the petal while the
            // player is up at the gate (tester 2026-11-01, research/dungeon-data.md §4.1c). Two copies on one spot, one usable (a
            // cinematic's, live 2026-10-22: the Sightless Eye's mural): the usable one counts.
            foreach (var copies in doors.ClosedOnAnyFloor(actors, player.Z).GroupBy(f => Key(f.Footprint.Door)))
            {
                var (footprint, floor) = copies.FirstOrDefault(f => !f.Footprint.Door.Unusable, copies.First());
                var key = copies.Key;
                listed.Add(key);
                if (!_doors.TryGetValue(key, out var door)) _doors[key] = door = new Door(footprint.Door.ActorSno, footprint);
                door.Footprint = footprint;
                door.OnFloor = floor;
                door.SeenOnFloor |= floor;
                Observe(door, footprint.Door);
            }
            _listed = listed;
            var here = new Vector2(player.X, player.Y);
            foreach (var (key, door) in _doors.ToList())
            {
                // Listed open, or missing where the client lists doors, is open.
                var actor = actors.FirstOrDefault(a => a.ActorSno == door.Sno && Vector2.Distance(new(a.X, a.Y), door.Spot) <= SameSpotUnits);
                bool open = actor is not null ? actor.StateChanged || actor.GizmoStateSet : !listed.Contains(key) && Vector2.Distance(here, door.Spot) <= ListedWithinUnits;
                if (open)
                {
                    _doors.Remove(key);
                    if (door.GameLocked || door.Tried) Changed("open", door);
                    continue;
                }
                if (door.OnFloor) Track(door, here); // walking over a door on the floor below doesn't try it
            }
            return _doors.Values.Where(d => (d.GameLocked || d.Tried) && d.SeenOnFloor).SelectMany(d => d.Footprint.Cells).ToHashSet();
        }
    }

    /// <summary>Every locked door's 1-unit cells and the height it stands at, on every floor, for paths over floors at several
    /// heights (OverworldPaths).</summary>
    public IReadOnlyList<DoorWall> Walls()
    {
        lock (_gate)
            return [.. _doors.Values.Where(d => d.GameLocked || d.Tried).Select(d => new DoorWall(d.Footprint.Cells.ToHashSet(), d.Footprint.Door.Z))];
    }

    /// <summary>The doors the game itself keeps shut now (not merely tried), for the guide: an objective past one leads to it.</summary>
    public IReadOnlyList<GuideGate> Gates()
    {
        lock (_gate)
            return [.. _doors.Where(d => d.Value.GameLocked && d.Value.SeenOnFloor).OrderBy(d => d.Key).Select(d =>
                new GuideGate($"{d.Key.X},{d.Key.Y}", (d.Value.Spot.X, d.Value.Spot.Y), d.Value.Footprint.Cells.ToHashSet()))];
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
                Changed("re-armed", door);
            }
    }

    /// <summary>A quest phase moved on: a door unlocked or opened by script while the player was out of its listing range would
    /// stay a wall for good, so every door leads again; one still locked locks again once the client lists it. A door listed now
    /// keeps the game's own lock, read every poll.</summary>
    public void QuestMovedOn()
    {
        lock (_gate)
            foreach (var (key, door) in _doors.Where(d => d.Value.Tried || (d.Value.GameLocked && !_listed.Contains(d.Key))))
            {
                door.Tried = false;
                door.GameLocked &= _listed.Contains(key);
                Changed("re-armed (quest moved on)", door);
            }
    }

    /// <summary>A new world: no door known.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _doors.Clear();
            _listed = [];
            _namedSnos = new HashSet<int>();
            _version++;
        }
    }

    private IReadOnlySet<int> _namedSnos = new HashSet<int>();

    /// <summary>Takes the actor SNOs the active quests' current phases name (GameSession.QuestActorsNamed): a tried door whose SNO a
    /// phase starts naming may open now, though the client dropped it far away (live 2026-10-17, the Light's Watch gate after the
    /// key: re-armed only once the player came back within earshot).</summary>
    public void ObserveNamed(IReadOnlySet<int> snos)
    {
        lock (_gate)
        {
            foreach (var door in _doors.Values.Where(d => d.Tried && snos.Contains(d.Sno) && !_namedSnos.Contains(d.Sno)))
            {
                door.Tried = false;
                Changed("re-armed (named by the quest)", door);
            }
            _namedSnos = snos;
        }
    }

    // The game's own lock follows its flags; a tried door waits for its quest to change (the key found: the phase now names it).
    private void Observe(Door door, ActorState actor)
    {
        if (door.GameLocked != actor.Unusable)
        {
            door.GameLocked = actor.Unusable;
            Changed(actor.Unusable ? "locked" : "unlocked", door);
        }
        door.State = (actor.Unusable, actor.QuestStep, actor.NamedByQuest);
        if (!door.Tried || door.State == door.TriedState) return;
        door.Tried = false;
        Changed("re-armed (quest changed)", door);
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
            Changed("tried", door);
        }
    }

    private void Changed(string what, Door door)
    {
        _version++;
        log?.Invoke($"DOOR {what}: {nameOf(door.Sno) ?? door.Sno.ToString()} at ({door.Spot.X:F1},{door.Spot.Y:F1})");
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1) : 0;
        return Vector2.Distance(p, a + ab * t);
    }

    private static (int X, int Y) Key(ActorState door) => ((int)MathF.Round(door.X), (int)MathF.Round(door.Y));
}
