namespace D4Assist.Game;

/// <summary>Quest steps that wait on an item carried to a receptacle (research/dungeon-objectives.md §3, §14).</summary>
public sealed class CarryGoals(ObjectiveClassifier objectives)
{
    /// <summary>The door a set of receptacles opens stands 2.2-6.0 units from them in every marker set that places both (offline
    /// census, research/dungeon-objectives.md §14).</summary>
    public const float ReceptacleDoorUnits = 8f;

    private static readonly IReadOnlySet<int> Nothing = new HashSet<int>();

    // Written on the actor poll, read by the guide.
    private volatile IReadOnlySet<int> _held = Nothing;

    /// <summary>Forgets what the player held; call on every world change.</summary>
    public void Reset() => _held = Nothing;

    /// <summary>True for the carryable in the player's hands: the copy that follows the player has no game record.</summary>
    public bool IsHeld(ActorState actor) => actor.ClientOnly && objectives.IsCarryable(actor.ActorSno);

    /// <summary>For a receptacle, whether it takes the carryable the player holds now; null for any other actor.</summary>
    public bool? TakesHeld(int actorSno) =>
        objectives.IsReceptacle(actorSno) ? _held.Any(carryable => objectives.Accepts(actorSno, carryable)) : null;

    /// <summary>While the player holds a carryable, a receptacle that takes it is the quest step even when quest-locked and every
    /// other quest step waits; a closed door beside a receptacle still empty waits for it.</summary>
    /// <param name="dpoActors">The world's DPO objective actors: a door the dungeon's objective names is the player's to open.</param>
    public IReadOnlyList<ActorState> Apply(IReadOnlyList<ActorState> actors, IReadOnlySet<int> dpoActors)
    {
        var held = actors.Where(IsHeld).Select(a => a.ActorSno).ToHashSet();
        _held = held;
        var empty = actors.Where(a => objectives.IsReceptacle(a.ActorSno) && !a.Operated).ToList();
        if (held.Count == 0 && empty.Count == 0) return actors;
        return [.. actors.Select(a =>
            WaitsForReceptacle(a, empty, dpoActors) ? Waiting(a)
            : held.Count == 0 ? a
            : held.Any(c => objectives.Accepts(a.ActorSno, c)) ? a.QuestLocked ? a with { QuestLocked = false, QuestStep = true } : a
            : a.QuestStep ? Waiting(a) : a)];
    }

    private static ActorState Waiting(ActorState actor) => actor with { QuestStep = false, QuestLocked = true };

    // Filling the receptacles opens their door by script (Hakan's Refuge, Ancients' Lament, the Shifting City).
    private bool WaitsForReceptacle(ActorState actor, List<ActorState> empty, IReadOnlySet<int> dpoActors) =>
        objectives.IsDoor(actor.ActorSno) && !actor.GizmoStateSet && !dpoActors.Contains(actor.ActorSno)
        && empty.Any(r => MathF.Sqrt((r.X - actor.X) * (r.X - actor.X) + (r.Y - actor.Y) * (r.Y - actor.Y)) <= ReceptacleDoorUnits);
}
