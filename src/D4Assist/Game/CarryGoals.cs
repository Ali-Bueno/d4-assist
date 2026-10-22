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

    /// <summary>A chain is taken this close to its station: live 4 units (2026-10-21, Inarius).</summary>
    public const float TakenFromUnits = 8f;

    /// <summary>Forgets what the player held; call on every world change.</summary>
    public void Reset()
    {
        _held = Nothing;
        _spent.Clear();
    }

    // The chargeables a carried item was taken from: emptied, they lead no more while still set off.
    private readonly HashSet<(int X, int Y)> _spent = [];

    /// <summary>True for the carryable in the player's hands: the copy that follows the player has no game record.</summary>
    public bool IsHeld(ActorState actor) => actor.ClientOnly && objectives.IsCarryable(actor.ActorSno);

    /// <summary>For a carryable, whether it waits for the player's hands (they hold another one now); null for any other actor.</summary>
    public bool? WaitsForHands(int actorSno) => objectives.IsCarryable(actorSno) ? _held.Count > 0 : null;

    /// <summary>For a receptacle, whether it takes the carryable the player holds now; null for any other actor.</summary>
    public bool? TakesHeld(int actorSno) =>
        objectives.IsReceptacle(actorSno) ? _held.Any(carryable => objectives.Accepts(actorSno, carryable)) : null;

    /// <summary>While the player holds a carryable, a receptacle that takes it is the quest step even when quest-locked and every
    /// other quest step waits; a closed door beside a receptacle still empty waits for it.</summary>
    /// <param name="dpoActors">The world's DPO objective actors: a door the dungeon's objective names is the player's to open.</param>
    public IReadOnlyList<ActorState> Apply(IReadOnlyList<ActorState> actors, IReadOnlySet<int> dpoActors)
    {
        var holding = actors.Where(IsHeld).ToList();
        var held = holding.Select(a => a.ActorSno).ToHashSet();
        if (_held.Count == 0) // just picked up: the chargeable it came from is spent
            foreach (var item in holding)
                foreach (var source in actors.Where(a => objectives.IsChargeable(a.ActorSno) && SetOff(a) && Distance(a, item) <= TakenFromUnits))
                    _spent.Add(Spot(source));
        _spent.RemoveWhere(spot => actors.Any(a => objectives.IsChargeable(a.ActorSno) && Spot(a) == spot && !SetOff(a)));
        _held = held;
        var empty = actors.Where(a => objectives.IsReceptacle(a.ActorSno) && !a.Operated).ToList();
        if (held.Count == 0 && empty.Count == 0 && _spent.Count == 0) return actors;
        return [.. actors.Select(a =>
            objectives.IsChargeable(a.ActorSno) && (held.Count > 0 || _spent.Contains(Spot(a))) ? Waiting(a)
            : WaitsForReceptacle(a, empty, dpoActors) ? Waiting(a)
            : held.Count == 0 ? a
            : held.Any(c => objectives.Accepts(a.ActorSno, c)) ? a.QuestLocked ? a with { QuestLocked = false, QuestStep = true } : a
            : IsBossToBind(a) ? a with { Invulnerable = false, QuestStep = true }
            : a.QuestStep ? Waiting(a) : a)];
    }

    /// <summary>True for a boss the quest names, invulnerable while the player carries something: what is carried goes to him.
    /// Inarius's chains, pulled down from their stations, bind him, and he stood silent from the moment they came free (live
    /// 2026-10-21, "Die Erde, die Saat, die Frucht": the player held a chain and could not find him).</summary>
    public bool IsBossToBind(ActorState actor) =>
        actor.Invulnerable && actor.NamedByQuest && !actor.ClientOnly && objectives.IsBossOrUnique(actor.ActorSno);

    private static ActorState Waiting(ActorState actor) => actor with { QuestStep = false, QuestLocked = true };

    /// <summary>True for a gizmo the game has set off, read live: Inarius's chain stations, released by his script, come back up
    /// when a wipe starts the fight over, so a used spot remembered (GizmoUsedSignal) would lead to a station still up.</summary>
    public static bool SetOff(ActorState actor) => actor.Operated || actor.StateChanged || actor.GizmoStateSet;

    private static (int X, int Y) Spot(ActorState actor) => ((int)MathF.Round(actor.X), (int)MathF.Round(actor.Y));

    private static float Distance(ActorState a, ActorState b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // Filling the receptacles opens their door by script (Hakan's Refuge, Ancients' Lament, the Shifting City).
    private bool WaitsForReceptacle(ActorState actor, List<ActorState> empty, IReadOnlySet<int> dpoActors) =>
        objectives.IsDoor(actor.ActorSno) && !actor.GizmoStateSet && !dpoActors.Contains(actor.ActorSno)
        && empty.Any(r => MathF.Sqrt((r.X - actor.X) * (r.X - actor.X) + (r.Y - actor.Y) * (r.Y - actor.Y)) <= ReceptacleDoorUnits);
}
