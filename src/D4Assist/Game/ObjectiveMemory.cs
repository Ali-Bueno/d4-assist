using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the objective loops heard in the current dungeon (quest steps, dungeon objectives) so the exploration guide
/// can lead back to one out of earshot. One that falls silent near the player is done; one the player reached waits until
/// another objective is done (a key found may open the door visited earlier) or the guide is switched on again.
/// </summary>
public sealed class ObjectiveMemory
{
    /// <summary>Within this distance an objective's actor is loaded (props seen up to ~65 units, the Undercity pad from
    /// 22-34; research/dungeon-objectives.md §5), so no loop there means it was used, locked or removed.</summary>
    public const float ConfirmRadiusUnits = ExplorationGuide.SeenRadiusUnits;

    /// <summary>Guide tiers: the tracked quest's step leads before the dungeon's own objectives.</summary>
    public const int QuestTier = 0, DungeonTier = 1;

    // A placed gizmo never moves; a re-created actor (new id) lands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Objective(int sno, string name, (float X, float Y) position)
    {
        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public string Id { get; } = $"objective:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";
        public (float X, float Y) Position { get; set; } = position;
        public uint ActorId { get; set; }
        public bool Quest { get; set; }
        public bool Pending { get; set; } = true;
        public bool Reached { get; set; }
    }

    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<Objective> _objectives = [];
    private (float X, float Y)? _player;

    /// <param name="log">Debug sink for sightings, arrivals and completions; null when not debugging.</param>
    public ObjectiveMemory(IDungeonFeed feed, Func<int, string?> nameOf, Action<string>? log = null)
    {
        _nameOf = nameOf;
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _objectives.Clear(); _player = null; } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
    }

    /// <summary>The objectives to lead to: looping when last heard and not reached yet; quest steps in <see cref="QuestTier"/>.</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
            return [.. _objectives.Where(o => o.Pending && !o.Reached).Select(o => new GuideDestination(o.Id, o.Position, o.Quest ? QuestTier : DungeonTier))];
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
        foreach (var objective in _objectives.Where(o => o.Reached))
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
