using D4Assist.Audio;

namespace D4Assist.Game;

/// <summary>What a looped actor is: the exit beacon leads the player and mutes the exploration guide, objectives do not.</summary>
public enum LoopKind { ExitBeacon, Objective }

/// <summary>The loop an actor gets: sound, distance profile (already scaled by the user volume) and the reason logged.</summary>
public sealed record LoopTarget(LoopKind Kind, string Sound, LoopProfile Profile, string Reason);

/// <summary>Maps each placed actor to its loop (or none): exit beacons by SNO, then dungeon objectives by classification.</summary>
public sealed class LoopTargetSelector
{
    /// <summary>Sound id of every loop (a WAV under Sounds/): the D2R quest-item loop.</summary>
    public const string QuestItemSound = "quest-item";

    /// <summary>Reason logged for the exit beacons.</summary>
    public const string BeaconReason = "beacon";

    private readonly IReadOnlySet<int> _beaconSnos;
    private readonly LoopTarget _beacon;
    private readonly ObjectiveClassifier? _objectives;
    private readonly Func<int, IReadOnlySet<int>> _dpoActors;
    private readonly Dictionary<ObjectiveReason, LoopTarget> _objectiveTargets;
    private volatile IReadOnlySet<int> _worldDpoActors = new HashSet<int>();

    /// <param name="objectives">Null disables the objective loops.</param>
    /// <param name="dpoActors">The DPO objective actors of a world SNO (DungeonObjectiveReader.ActorSnos).</param>
    public LoopTargetSelector(
        IReadOnlySet<int> beaconSnos, double beaconVolume, ObjectiveClassifier? objectives, double objectiveVolume,
        Func<int, IReadOnlySet<int>> dpoActors)
    {
        _beaconSnos = beaconSnos;
        _objectives = objectives;
        _dpoActors = dpoActors;
        _beacon = new LoopTarget(LoopKind.ExitBeacon, QuestItemSound, Scaled(LoopProfiles.Beacon, beaconVolume), BeaconReason);
        _objectiveTargets = Enum.GetValues<ObjectiveReason>().ToDictionary(
            reason => reason,
            reason => new LoopTarget(LoopKind.Objective, QuestItemSound, Scaled(LoopProfiles.Quest, objectiveVolume), reason.ToString().ToLowerInvariant()));
    }

    /// <summary>Rebuilds the world's objective allow-list; called on every world change.</summary>
    public void SetWorld(int worldSno) => _worldDpoActors = _objectives is null ? new HashSet<int>() : _dpoActors(worldSno);

    /// <summary>Null when the actor gets no loop.</summary>
    public LoopTarget? Select(ActorState actor)
    {
        if (_beaconSnos.Contains(actor.ActorSno)) return _beacon;
        return _objectives?.Classify(actor.ActorSno, _worldDpoActors) is { } reason ? _objectiveTargets[reason] : null;
    }

    private static LoopProfile Scaled(LoopProfile profile, double volume) => profile with { NearGain = profile.NearGain * volume };
}
