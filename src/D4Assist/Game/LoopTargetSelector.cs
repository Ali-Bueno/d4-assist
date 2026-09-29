using D4Assist.Audio;

namespace D4Assist.Game;

/// <summary>What a looped actor is, in D2R's voice priority order (quest, exits, doors, containers): the exit beacon
/// leads the player and mutes the exploration guide, the others do not.</summary>
public enum LoopKind { ExitBeacon, Objective, Door, Container }

/// <summary>The loop an actor gets: sound, distance profile (already scaled by the user volume) and the reason logged.</summary>
/// <param name="StopsWhenUsed">A used gizmo falls silent; beacons (the Undercity pad activates on approach) and NPCs never do.</param>
public sealed record LoopTarget(LoopKind Kind, string Sound, LoopProfile Profile, string Reason, bool StopsWhenUsed = false);

/// <summary>Maps each placed actor to its loop (or none): exit beacons by SNO, then dungeon objectives, then doors, chests and readables.</summary>
public sealed class LoopTargetSelector
{
    /// <summary>Sound id of the beacon and objective loops (a WAV under Sounds/): the D2R quest-item loop.</summary>
    public const string QuestItemSound = "quest-item";

    /// <summary>Reason logged for the exit beacons.</summary>
    public const string BeaconReason = "beacon";

    private readonly IReadOnlySet<int> _beaconSnos;
    private readonly LoopTarget _beacon;
    private readonly ObjectiveClassifier? _objectives;
    private readonly Func<int, IReadOnlySet<int>> _dpoActors;
    private readonly Dictionary<ObjectiveReason, LoopTarget> _objectiveTargets;
    private readonly Dictionary<ObjectiveReason, LoopTarget> _npcObjectiveTargets;
    private readonly InteractableClassifier? _interactables;
    private readonly IReadOnlySet<InteractableKind> _interactableKinds;
    private readonly LoopProfile _interactableProfile;
    private volatile IReadOnlySet<int> _worldDpoActors = new HashSet<int>();

    /// <param name="objectives">Null disables the objective loops.</param>
    /// <param name="dpoActors">The DPO objective actors of a world SNO (DungeonObjectiveReader.ActorSnos).</param>
    /// <param name="interactables">Null disables the door, chest and readable loops.</param>
    /// <param name="interactableKinds">The interactable kinds that loop; null means all.</param>
    public LoopTargetSelector(
        IReadOnlySet<int> beaconSnos, double beaconVolume, ObjectiveClassifier? objectives, double objectiveVolume,
        Func<int, IReadOnlySet<int>> dpoActors, InteractableClassifier? interactables = null,
        IReadOnlySet<InteractableKind>? interactableKinds = null, double interactableVolume = 1.0)
    {
        _beaconSnos = beaconSnos;
        _objectives = objectives;
        _dpoActors = dpoActors;
        _interactables = interactables;
        _interactableKinds = interactableKinds ?? Enum.GetValues<InteractableKind>().ToHashSet();
        _interactableProfile = Scaled(LoopProfiles.Container, interactableVolume);
        _beacon = new LoopTarget(LoopKind.ExitBeacon, QuestItemSound, Scaled(LoopProfiles.Beacon, beaconVolume), BeaconReason);
        _objectiveTargets = Enum.GetValues<ObjectiveReason>().ToDictionary(
            reason => reason,
            reason => new LoopTarget(
                LoopKind.Objective, reason == ObjectiveReason.Prisoner ? InteractableClassifier.CorpseSound : QuestItemSound,
                Scaled(LoopProfiles.Quest, objectiveVolume), reason.ToString().ToLowerInvariant(), StopsWhenUsed: true));
        _npcObjectiveTargets = _objectiveTargets.ToDictionary(kv => kv.Key, kv => kv.Value with { StopsWhenUsed = false });
    }

    /// <summary>Rebuilds the world's objective allow-list; called on every world change.</summary>
    public void SetWorld(int worldSno) => _worldDpoActors = _objectives is null ? new HashSet<int>() : _dpoActors(worldSno);

    /// <summary>Null when the actor gets no loop.</summary>
    public LoopTarget? Select(ActorState actor)
    {
        if (_beaconSnos.Contains(actor.ActorSno)) return _beacon;
        if (_objectives?.Classify(actor.ActorSno, _worldDpoActors) is { } reason)
            return (_objectives.IsMonster(actor.ActorSno) ? _npcObjectiveTargets : _objectiveTargets)[reason];
        return _interactables?.Classify(actor.ActorSno) is { } thing && _interactableKinds.Contains(thing.Kind)
            ? new LoopTarget(
                thing.Kind == InteractableKind.Door ? LoopKind.Door : LoopKind.Container, thing.Sound, _interactableProfile,
                thing.Kind.ToString().ToLowerInvariant(), StopsWhenUsed: true)
            : null;
    }

    private static LoopProfile Scaled(LoopProfile profile, double volume) => profile with { NearGain = profile.NearGain * volume };
}
