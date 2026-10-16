using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the objective loops heard in the current dungeon (quest steps, dungeon objectives) so the exploration guide
/// can lead back to one out of earshot. One that falls silent near the player is done. One the player stood at stays the goal
/// until it is done; walked away from undone, it waits until another objective is done (a key found may open the door visited
/// earlier) or the guide is switched on again. A door the player can't open yet (LockedDoors) never leads.
/// The places the active quest's current phase names (a trigger to walk into: "chase Lilith") lead in the quest tier too,
/// until the phase moves on: reaching one is not the end of the step (live 2026-10-01: phase 154's trigger is one of the
/// step's hidden callbacks; the shown one waits for a group of enemies), so the guide holds there (ExplorationGuideBridge).
/// </summary>
public sealed class ObjectiveMemory
{
    /// <summary>Within this distance an objective's actor is loaded (props seen up to ~65 units, the Undercity pad from
    /// 22-34; research/dungeon-objectives.md §5), so no loop there means it was used, locked or removed.</summary>
    public const float ConfirmRadiusUnits = ExplorationGuide.SeenRadiusUnits;

    /// <summary>Walking this far from an objective the player stood at, still undone, sets it aside: it can't be used yet, or
    /// its use went unseen. The distance that confirms a silent one, so a player who used it learns that first.</summary>
    public const float LeaveUnits = ConfirmRadiusUnits;

    /// <summary>An objective left undone this often stays set aside when another one is done; switching the guide on re-arms it.</summary>
    public const int MaxTimesLeft = 2;

    /// <summary>Close enough to use an object or talk to an NPC, past the edge of its own footprint.</summary>
    public const float InteractUnits = 3f;

    /// <summary>Guide tiers: the tracked quest's step leads before the dungeon's own objectives.</summary>
    public const int QuestTier = 0, DungeonTier = 1;

    /// <summary>Id prefix of a quest place's guide destination.</summary>
    public const string QuestPlaceKind = "quest-place";

    // A placed gizmo never moves; a re-created actor (new id) lands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Objective(int sno, string name, (float X, float Y) position, float radius, string kind = "objective")
    {
        public float? Z { get; set; }
        public float Radius { get; } = radius;
        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public string Id { get; } = $"{kind}:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";
        public (float X, float Y) Position { get; set; } = position;
        public uint ActorId { get; set; }
        public bool Quest { get; set; }
        public bool Pending { get; set; } = true;
        /// <summary>The player stands within its radius.</summary>
        public bool AtIt { get; set; }
        /// <summary>Set aside: the player stood at it and walked away while it was still undone.</summary>
        public bool Reached { get; set; }
        /// <summary>How often the player walked away from it undone.</summary>
        public int TimesLeft { get; set; }
        /// <summary>Known only from the tile that places it or the map that marks it; cleared once heard.</summary>
        public bool PlacedOnly { get; set; }
        /// <summary>The game's map marks it now.</summary>
        public bool Marked { get; set; }
    }

    private readonly Func<int, string?> _nameOf;
    private readonly Func<int, float?> _triggerRadius;
    private readonly Func<int, float?> _footprint;
    private readonly Func<int, bool?> _takesHeld;
    private readonly Func<int, (float X, float Y), bool> _locked;
    private readonly Func<int, bool?> _carryableWaits;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<Objective> _objectives = [];
    private readonly List<Objective> _places = [];
    private (float X, float Y)? _player;

    /// <param name="log">Debug sink for sightings, arrivals and completions; null when not debugging.</param>
    /// <param name="triggerRadius">A trigger actor's ground radius (ActorDefinitionInfo.TriggerRadius): a quest place that is a
    /// trigger counts as there only inside it (Trigger_Sphere_8m: 4). Null for none.</param>
    /// <param name="takesHeld">For a receptacle, whether it takes what the player holds (LoopTargetSelector.ReceptacleTakesHeld): it
    /// leads only then, in the quest tier; empty-handed, the next carryable or unexplored ground comes first
    /// (research/dungeon-objectives.md §14). Null for none.</param>
    /// <param name="footprint">Half the ground size of an actor's box (ActorDefinitionInfo bounds), so a big altar is reached at its
    /// edge; null for none.</param>
    /// <param name="locked">True for a door of that SNO at that spot the player can't open yet (LockedDoors.IsLocked); null for none.</param>
    /// <param name="carryableWaits">For a carryable, whether the player's hands are full (LoopTargetSelector.CarryableWaits); null
    /// for any other actor. A carryable falls silent while another is carried, so it is done only once it left the ground (picked up,
    /// then delivered), and it leads only with empty hands (live 2026-10-16, the Lost Archives' second crate; the same in every
    /// statue, bloodstone and carving dungeon, research/dungeon-objectives.md §14). Null for none.</param>
    public ObjectiveMemory(
        IDungeonFeed feed, Func<int, string?> nameOf, Action<string>? log = null, Func<int, float?>? triggerRadius = null,
        Func<int, bool?>? takesHeld = null, Func<int, float?>? footprint = null, Func<int, (float X, float Y), bool>? locked = null,
        Func<int, bool?>? carryableWaits = null)
    {
        _carryableWaits = carryableWaits ?? (_ => null);
        _nameOf = nameOf;
        _triggerRadius = triggerRadius ?? (_ => null);
        _footprint = footprint ?? (_ => null);
        _takesHeld = takesHeld ?? (_ => null);
        _locked = locked ?? ((_, _) => false);
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _objectives.Clear(); _places.Clear(); _player = null; } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
    }

    /// <summary>The objectives to lead to: looping when last heard, not set aside and not a locked door; quest steps in <see cref="QuestTier"/>.</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
            return [.. _places.Concat(_objectives).Select(o => (Objective: o, Delivers: _takesHeld(o.Sno)))
                .Where(x => x.Objective.Pending && !x.Objective.Reached && x.Delivers != false && !_locked(x.Objective.Sno, x.Objective.Position)
                    && _carryableWaits(x.Objective.Sno) != true)
                .Select(x => new GuideDestination(x.Objective.Id, x.Objective.Position,
                    x.Objective.Quest || x.Delivers == true ? QuestTier : DungeonTier, x.Objective.Z, x.Objective.Radius, Located(x.Objective)))];
    }

    /// <summary>The objectives heard here and not done yet, to outlive a restart of the app (quest places are named again).</summary>
    public IReadOnlyList<SavedObjective> Export()
    {
        lock (_gate)
            return [.. _objectives.Where(o => o.Pending && !o.PlacedOnly).Select(o => new SavedObjective(o.Sno, o.Name, o.Position.X, o.Position.Y, o.Quest))];
    }

    /// <summary>Adds back the objectives an earlier run heard in this instance and not done: still waiting, as when out of earshot.</summary>
    public void Restore(IEnumerable<SavedObjective> saved)
    {
        lock (_gate)
            foreach (var s in saved)
                if (!_objectives.Any(o => o.Sno == s.Sno && Distance(o.Position, (s.X, s.Y)) <= SameSpotUnits))
                    _objectives.Add(new Objective(s.Sno, s.Name, (s.X, s.Y), Reach(s.Sno)) { Quest = s.Quest });
    }

    /// <summary>Takes the places the active quests' current phases name (GameSession.QuestPlacesRead, every actor poll).</summary>
    public void ObservePlaces(IReadOnlyList<QuestPlace> places)
    {
        lock (_gate)
        {
            ObserveAhead([.. places.Where(p => p.Ahead)]);
            var named = new HashSet<Objective>();
            foreach (var place in places.Where(p => !p.Ahead))
            {
                var objective = _places.FirstOrDefault(o => o.Sno == place.ActorSno && Distance(o.Position, (place.X, place.Y)) <= SameSpotUnits);
                if (objective is null)
                {
                    objective = new Objective(place.ActorSno, NameOf(place.ActorSno), (place.X, place.Y), Reach(place.ActorSno), QuestPlaceKind)
                        { Quest = true, Z = place.Z };
                    _places.Add(objective);
                    Log("seen (quest place)", objective);
                }
                else if (!objective.Pending)
                {
                    objective.Pending = true;
                    Log("named again", objective);
                }
                named.Add(objective);
            }
            foreach (var objective in _places.Where(o => o.Pending && !named.Contains(o)))
            {
                objective.Pending = false;
                Log("done (phase moved on)", objective);
            }
        }
    }

    // A gizmo the phase names, placed by a loaded tile or marked on the map, is an objective before it's heard. It's done once
    // silent near the player (already used), or when the phase stops naming it; a tile's copy with nothing there was never spawned.
    private void ObserveAhead(List<QuestPlace> placed)
    {
        var named = new HashSet<Objective>();
        foreach (var place in placed)
        {
            var objective = _objectives.FirstOrDefault(o => o.Sno == place.ActorSno && Distance(o.Position, (place.X, place.Y)) <= SameSpotUnits);
            if (objective is null)
            {
                objective = new Objective(place.ActorSno, NameOf(place.ActorSno), (place.X, place.Y), Reach(place.ActorSno))
                    { Quest = true, PlacedOnly = true, Z = place.Z };
                _objectives.Add(objective);
                Log(place.Marked ? "seen ahead (map)" : "seen ahead", objective);
            }
            objective.Marked = place.Marked || (named.Contains(objective) && objective.Marked);
            named.Add(objective);
        }
        foreach (var objective in _objectives.Where(o => o.PlacedOnly && !named.Contains(o)))
        {
            objective.Marked = false;
            if (!objective.Pending) continue;
            objective.Pending = false;
            Log("done (phase moved on)", objective);
        }
    }

    /// <summary>Takes one actor poll (ActorLoopBridge.ObjectivesSeen): the objective actors looping and silent, and every actor read.</summary>
    public void Observe(ObjectiveSighting seen)
    {
        lock (_gate)
        {
            var heard = new HashSet<Objective>();
            foreach (var actor in seen.Looping)
            {
                var objective = Find(actor) ?? Add(actor);
                objective.ActorId = actor.Id;
                objective.Position = (actor.X, actor.Y);
                objective.Z = actor.Z;
                objective.Quest = actor.QuestStep;
                objective.PlacedOnly = false;
                if (!objective.Pending) Log("looping again", objective);
                objective.Pending = true;
                heard.Add(objective);
            }
            if (_player is not { } here) return;

            var done = _objectives.Where(o => o.Pending && !heard.Contains(o) && Distance(o.Position, here) <= ConfirmRadiusUnits && Ended(o, seen)).ToList();
            foreach (var objective in done)
            {
                objective.Pending = false;
                objective.AtIt = false;
                Log("done", objective);
            }
            if (done.Count > 0) RearmReached(MaxTimesLeft);
        }
    }

    // Heard earlier: silent now. Not heard yet: its actor there is silent (used), or a tile's copy the map doesn't mark has no actor.
    // A door's or a trigger's copy loops as something else or not at all: it waits for the phase.
    // A carryable: once no copy lies on the ground there (the one in the player's hands has no game record).
    private bool Ended(Objective objective, ObjectiveSighting seen) =>
        _carryableWaits(objective.Sno) is not null ? !Stands(objective, [.. seen.Present.Where(a => !a.ClientOnly)])
        : !objective.PlacedOnly || Stands(objective, seen.Silent) || (!objective.Marked && !Stands(objective, seen.Present));

    // Heard here, or marked on this world's map; a quest place comes from script data, whatever world it belongs to.
    private bool Located(Objective objective) => _objectives.Contains(objective) && (!objective.PlacedOnly || objective.Marked);

    private static bool Stands(Objective objective, IReadOnlyList<ActorState> actors) =>
        actors.Any(a => a.ActorSno == objective.Sno && Distance(objective.Position, (a.X, a.Y)) <= SameSpotUnits);

    /// <summary>Lets the objectives the player walked away from lead again (the guide was just switched on by a lost player).</summary>
    public void Rearm()
    {
        lock (_gate) RearmReached(int.MaxValue);
    }

    // Another objective done may have unlocked one set aside; one left undone MaxTimesLeft times waits for the guide's toggle
    // (live 2026-10-16: the Light's Watch lectern drew the guide back four times, and the second guard was never found).
    private void RearmReached(int belowTimesLeft)
    {
        foreach (var objective in _places.Concat(_objectives).Where(o => o.Reached && o.TimesLeft < belowTimesLeft))
        {
            objective.Reached = false;
            Log("re-armed", objective);
        }
    }

    private void OnPlayer((float X, float Y) here)
    {
        _player = here;
        foreach (var objective in _objectives.Where(o => o.Pending && !o.Reached))
        {
            float distance = Distance(objective.Position, here);
            if (distance <= objective.Radius)
            {
                if (objective.AtIt) continue;
                objective.AtIt = true;
                Log("reached", objective);
            }
            else if (objective.AtIt && distance > LeaveUnits)
            {
                objective.AtIt = false;
                objective.Reached = true;
                objective.TimesLeft++;
                Log("left undone", objective);
            }
        }
    }

    // How close counts as there: inside a trigger, else within reach of the object's edge.
    private float Reach(int sno) => _triggerRadius(sno) ?? (_footprint(sno) ?? 0) + InteractUnits;

    private string NameOf(int sno) => _nameOf(sno) ?? sno.ToString();

    private Objective? Find(ActorState actor) =>
        _objectives.FirstOrDefault(o => o.ActorId == actor.Id && o.Sno == actor.ActorSno)
        ?? _objectives.FirstOrDefault(o => o.Sno == actor.ActorSno && Distance(o.Position, (actor.X, actor.Y)) <= SameSpotUnits);

    private Objective Add(ActorState actor)
    {
        var objective = new Objective(actor.ActorSno, NameOf(actor.ActorSno), (actor.X, actor.Y), Reach(actor.ActorSno));
        _objectives.Add(objective);
        Log(actor.QuestStep ? "seen (quest)" : "seen (dungeon)", objective);
        return objective;
    }

    private void Log(string what, Objective objective) =>
        _log?.Invoke($"OBJECTIVE {what}: {objective.Name} at ({objective.Position.X:F1},{objective.Position.Y:F1})");

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A remembered objective as ExplorationStore saves it.</summary>
public sealed record SavedObjective(int Sno, string Name, float X, float Y, bool Quest);
