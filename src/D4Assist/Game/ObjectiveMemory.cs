using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the objective loops heard in the current dungeon (quest steps, dungeon objectives) so the exploration guide
/// can lead back to one out of earshot. One that falls silent near the player is done; one the player reached waits until
/// another objective is done (a key found may open the door visited earlier) or the guide is switched on again.
/// The places the active quest's current phase names (a trigger to walk into: "chase Lilith") lead in the quest tier too,
/// until the phase moves on: reaching one is not the end of the step (live 2026-10-01: phase 154's trigger is one of the
/// step's hidden callbacks; the shown one waits for a group of enemies), so the guide holds there (ExplorationGuideBridge).
/// </summary>
public sealed class ObjectiveMemory
{
    /// <summary>Within this distance an objective's actor is loaded (props seen up to ~65 units, the Undercity pad from
    /// 22-34; research/dungeon-objectives.md §5), so no loop there means it was used, locked or removed.</summary>
    public const float ConfirmRadiusUnits = ExplorationGuide.SeenRadiusUnits;

    /// <summary>Guide tiers: the tracked quest's step leads before the dungeon's own objectives.</summary>
    public const int QuestTier = 0, DungeonTier = 1;

    /// <summary>Id prefix of a quest place's guide destination.</summary>
    public const string QuestPlaceKind = "quest-place";

    // A placed gizmo never moves; a re-created actor (new id) lands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Objective(int sno, string name, (float X, float Y) position, string kind = "objective")
    {
        public float? Z { get; init; }
        public float Radius { get; init; }
        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public string Id { get; } = $"{kind}:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";
        public (float X, float Y) Position { get; set; } = position;
        public uint ActorId { get; set; }
        public bool Quest { get; set; }
        public bool Pending { get; set; } = true;
        public bool Reached { get; set; }
        /// <summary>Known only from the tile that places it; cleared once heard.</summary>
        public bool PlacedOnly { get; set; }
    }

    // An item or NPC a quest phase names: close enough to interact.
    private const float PlaceRadiusUnits = 3f;

    private readonly Func<int, string?> _nameOf;
    private readonly Func<int, float?> _triggerRadius;
    private readonly Func<int, bool?> _takesHeld;
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
    public ObjectiveMemory(
        IDungeonFeed feed, Func<int, string?> nameOf, Action<string>? log = null, Func<int, float?>? triggerRadius = null,
        Func<int, bool?>? takesHeld = null)
    {
        _nameOf = nameOf;
        _triggerRadius = triggerRadius ?? (_ => null);
        _takesHeld = takesHeld ?? (_ => null);
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _objectives.Clear(); _places.Clear(); _player = null; } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
    }

    /// <summary>The objectives to lead to: looping when last heard and not reached yet; quest steps in <see cref="QuestTier"/>.</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
            return [.. _places.Concat(_objectives).Select(o => (Objective: o, Delivers: _takesHeld(o.Sno)))
                .Where(x => x.Objective.Pending && !x.Objective.Reached && x.Delivers != false)
                .Select(x => new GuideDestination(x.Objective.Id, x.Objective.Position,
                    x.Objective.Quest || x.Delivers == true ? QuestTier : DungeonTier, x.Objective.Z, x.Objective.Radius))];
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
                    _objectives.Add(new Objective(s.Sno, s.Name, (s.X, s.Y)) { Quest = s.Quest });
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
                    objective = new Objective(place.ActorSno, _nameOf(place.ActorSno) ?? place.ActorSno.ToString(), (place.X, place.Y), QuestPlaceKind)
                        { Quest = true, Z = place.Z, Radius = _triggerRadius(place.ActorSno) ?? PlaceRadiusUnits };
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
                objective.Reached = false;
                Log("done (phase moved on)", objective);
            }
        }
    }

    // A gizmo the phase names, placed by a loaded tile, is an objective before it's heard: like a heard one it's done once silent
    // near the player (already used), and dropped unheard when the phase stops naming it.
    private void ObserveAhead(List<QuestPlace> placed)
    {
        var named = new HashSet<Objective>();
        foreach (var place in placed)
        {
            var objective = _objectives.FirstOrDefault(o => o.Sno == place.ActorSno && Distance(o.Position, (place.X, place.Y)) <= SameSpotUnits);
            if (objective is null)
            {
                objective = new Objective(place.ActorSno, _nameOf(place.ActorSno) ?? place.ActorSno.ToString(), (place.X, place.Y))
                    { Quest = true, PlacedOnly = true };
                _objectives.Add(objective);
                Log("seen ahead", objective);
            }
            named.Add(objective);
        }
        foreach (var objective in _objectives.Where(o => o.PlacedOnly && o.Pending && !named.Contains(o)))
        {
            objective.Pending = false;
            Log("done (phase moved on)", objective);
        }
    }

    /// <summary>Takes the objective actors looping now (ActorLoopBridge.ObjectivesLooping, every actor poll).</summary>
    public void Observe(IReadOnlyList<ActorState> looping)
    {
        lock (_gate)
        {
            var heard = new HashSet<Objective>();
            foreach (var actor in looping)
            {
                var objective = Find(actor) ?? Add(actor);
                objective.ActorId = actor.Id;
                objective.Position = (actor.X, actor.Y);
                objective.Quest = actor.QuestStep;
                objective.PlacedOnly = false;
                if (!objective.Pending) Log("looping again", objective);
                objective.Pending = true;
                heard.Add(objective);
            }
            if (_player is not { } here) return;

            var done = _objectives.Where(o => o.Pending && !heard.Contains(o) && Distance(o.Position, here) <= ConfirmRadiusUnits).ToList();
            foreach (var objective in done)
            {
                objective.Pending = false;
                Log("done", objective);
            }
            if (done.Count > 0) RearmReached();
        }
    }

    /// <summary>Lets the objectives the player already reached lead again (the guide was just switched on by a lost player).</summary>
    public void Rearm()
    {
        lock (_gate) RearmReached();
    }

    private void RearmReached()
    {
        foreach (var objective in _places.Concat(_objectives).Where(o => o.Reached))
        {
            objective.Reached = false;
            Log("re-armed", objective);
        }
    }

    private void OnPlayer((float X, float Y) here)
    {
        _player = here;
        foreach (var objective in _objectives.Where(o => o.Pending && !o.Reached && Distance(o.Position, here) <= ExplorationGuide.DestinationArrivalUnits))
        {
            objective.Reached = true;
            Log("reached", objective);
        }
    }

    private Objective? Find(ActorState actor) =>
        _objectives.FirstOrDefault(o => o.ActorId == actor.Id && o.Sno == actor.ActorSno)
        ?? _objectives.FirstOrDefault(o => o.Sno == actor.ActorSno && Distance(o.Position, (actor.X, actor.Y)) <= SameSpotUnits);

    private Objective Add(ActorState actor)
    {
        var objective = new Objective(actor.ActorSno, _nameOf(actor.ActorSno) ?? actor.ActorSno.ToString(), (actor.X, actor.Y));
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
