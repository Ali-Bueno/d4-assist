using D4Assist.Audio;
using D4Assist.Config;

namespace D4Assist.Game;

/// <summary>What a looped actor is, in voice priority order: a boss fight's safe zone first, then D2R's (exits, quest, doors,
/// containers). The exit beacon leads the player and mutes the exploration guide, the others do not.</summary>
public enum LoopKind { SafeZone, ExitBeacon, Objective, Door, Container }

/// <summary>The loop an actor gets: sound, distance profile, the reason logged and the settings cue that levels it.</summary>
/// <param name="StopsWhenUsed">A used gizmo falls silent; beacons (the Undercity pad activates on approach) and NPCs never do.</param>
/// <param name="QuietWithin">The loop falls silent while the player is this close (sheltered in a safe zone); 0 = never.</param>
/// <param name="Reusable">A gizmo used again and again (the Horadric vessels): silent only while the game doesn't let the player use it.</param>
public sealed record LoopTarget(
    LoopKind Kind, string Sound, LoopProfile Profile, string Reason, SoundCue Cue, bool StopsWhenUsed = false, float QuietWithin = 0,
    bool Reusable = false)
{
    /// <summary>A gizmo the game can lock (quest-restricted, script-disabled): silent until it is allowed.</summary>
    public bool SilentWhileUnusable => StopsWhenUsed || Reusable;
}

/// <summary>Maps each placed actor to its loop (or none): safe zones, exit beacons (by SNO, or any portal in a dungeon), then the
/// quest's current gizmo and dungeon objectives, then doors, chests and readables. Every kind is selected: the sound
/// settings silence a switched-off cue (ActorLoopBridge), so switching it back on needs no restart.</summary>
public sealed class LoopTargetSelector
{
    /// <summary>Sound id of the beacon and objective loops (a WAV under Sounds/): the D2R quest-item loop.</summary>
    public const string QuestItemSound = "quest-item";

    /// <summary>Sound id of the Undercity attunement switches (the user's own sound, 2026-10-01).</summary>
    public const string EnticementSound = "attunement";

    /// <summary>Sound id of Mephisto's stolen-skill orbs: the quest loop until the user picks one of its own (2026-10-01).</summary>
    public const string StolenSkillSound = QuestItemSound;

    /// <summary>Sound id of Akarat's lights: the quest loop until the user picks one of its own (2026-10-01).</summary>
    public const string AkaratLightSound = QuestItemSound;

    /// <summary>Sound id of the Horadric Guardian's vessels: the quest loop until the user picks one of its own (2026-10-01).</summary>
    public const string WardBreakerSound = QuestItemSound;

    /// <summary>Sound id of the boss-fight safe zones (the user's own sound, 2026-10-01).</summary>
    public const string SafeZoneSound = "barrier";

    /// <summary>Reason logged for the exit beacons.</summary>
    public const string BeaconReason = "beacon";

    private readonly IReadOnlySet<int> _beaconSnos;
    private readonly LoopTarget _beacon;
    private readonly LoopTarget _safeZone;
    private readonly ObjectiveClassifier? _objectives;
    private readonly Func<int, IReadOnlySet<int>> _dpoActors;
    private readonly Dictionary<ObjectiveReason, LoopTarget> _objectiveTargets;
    private readonly Dictionary<ObjectiveReason, LoopTarget> _npcObjectiveTargets;
    private readonly InteractableClassifier? _interactables;
    private readonly Func<int, bool>? _isPortal;
    private readonly Func<int, float?>? _safeZoneRadius;
    private volatile IReadOnlySet<int> _worldDpoActors = new HashSet<int>();
    private volatile bool _inDungeon;

    /// <param name="objectives">Null disables the objective loops.</param>
    /// <param name="dpoActors">The DPO objective actors of a world SNO (DungeonObjectiveReader.ActorSnos).</param>
    /// <param name="interactables">Null disables the door, chest and readable loops.</param>
    /// <param name="isPortal">Portal actors that get the beacon inside dungeons (PortalClassifier.IsPortal); null for none.</param>
    /// <param name="safeZoneRadius">The sheltered distance of a safe-zone actor, null for others (SafeZoneClassifier.InsideRadius); null for none.</param>
    public LoopTargetSelector(
        IReadOnlySet<int> beaconSnos, ObjectiveClassifier? objectives, Func<int, IReadOnlySet<int>> dpoActors,
        InteractableClassifier? interactables = null, Func<int, bool>? isPortal = null, Func<int, float?>? safeZoneRadius = null)
    {
        _beaconSnos = beaconSnos;
        _isPortal = isPortal;
        _safeZoneRadius = safeZoneRadius;
        _objectives = objectives;
        _dpoActors = dpoActors;
        _interactables = interactables;
        _beacon = new LoopTarget(LoopKind.ExitBeacon, QuestItemSound, LoopProfiles.Beacon, BeaconReason, SoundCue.Portal);
        _safeZone = new LoopTarget(LoopKind.SafeZone, SafeZoneSound, LoopProfiles.Beacon, "safe-zone", SoundCue.SafeZone);
        _objectiveTargets = Enum.GetValues<ObjectiveReason>().ToDictionary(reason => reason, reason => ObjectiveTarget(reason, gizmo: true));
        _npcObjectiveTargets = Enum.GetValues<ObjectiveReason>().ToDictionary(reason => reason, reason => ObjectiveTarget(reason, gizmo: false));
    }

    /// <summary>Rebuilds the world's objective allow-list; called on every world change.</summary>
    public void SetWorld(int worldSno, bool isDungeon)
    {
        _worldDpoActors = _objectives is null ? new HashSet<int>() : _dpoActors(worldSno);
        _inDungeon = isDungeon;
    }

    /// <summary>Null when the actor gets no loop.</summary>
    public LoopTarget? Select(ActorState actor)
    {
        if (_safeZoneRadius?.Invoke(actor.ActorSno) is { } inside) return _safeZone with { QuietWithin = inside };
        if (_beaconSnos.Contains(actor.ActorSno) || (_inDungeon && _isPortal?.Invoke(actor.ActorSno) == true)) return _beacon;
        if (_objectives is not null && actor.QuestStep) return _objectiveTargets[ObjectiveReason.Quest];
        if (IsHeld(actor)) return null;
        if (_objectives?.Classify(actor.ActorSno, _worldDpoActors) is { } reason)
            return (_objectives.IsGizmo(actor.ActorSno) ? _objectiveTargets : _npcObjectiveTargets)[reason];
        if (_interactables?.Classify(actor.ActorSno) is not { } thing) return null;
        if (thing.Kind == InteractableKind.Door && actor.GizmoStateSet) return null; // already open: nothing to do
        return new LoopTarget(
            thing.Kind == InteractableKind.Door ? LoopKind.Door : LoopKind.Container, thing.Sound, LoopProfiles.Container,
            thing.Kind.ToString().ToLowerInvariant(), CueOf(thing), StopsWhenUsed: thing.Kind != InteractableKind.PointOfInterest);
    }

    // Only a gizmo has a "used" signal; the Horadric vessels are used once per barrier.
    private static LoopTarget ObjectiveTarget(ObjectiveReason reason, bool gizmo)
    {
        var (sound, cue) = SoundOf(reason);
        bool reusable = reason == ObjectiveReason.WardBreaker;
        return new LoopTarget(LoopKind.Objective, sound, LoopProfiles.Quest, reason.ToString().ToLowerInvariant(), cue,
            StopsWhenUsed: gizmo && !reusable, Reusable: gizmo && reusable);
    }

    private static (string Sound, SoundCue Cue) SoundOf(ObjectiveReason reason) => reason switch
    {
        ObjectiveReason.Prisoner => (InteractableClassifier.CorpseSound, SoundCue.Prisoner),
        ObjectiveReason.Enticement => (EnticementSound, SoundCue.Enticement),
        ObjectiveReason.StolenSkill => (StolenSkillSound, SoundCue.StolenSkill),
        ObjectiveReason.AkaratLight => (AkaratLightSound, SoundCue.AkaratLight),
        ObjectiveReason.WardBreaker => (WardBreakerSound, SoundCue.WardBreaker),
        _ => (QuestItemSound, SoundCue.QuestObjective),
    };

    /// <summary>The settings cue of an interactable: a body sounds, and so is levelled, as a corpse whether looted or read.</summary>
    public static SoundCue CueOf(Interactable thing) =>
        thing.Sound == InteractableClassifier.CorpseSound ? SoundCue.Corpse
        : thing.Kind switch
        {
            InteractableKind.Door => SoundCue.Door,
            InteractableKind.Chest => SoundCue.Chest,
            InteractableKind.Readable => SoundCue.Readable,
            _ => SoundCue.PointOfInterest,
        };

    /// <summary>While the player holds a carryable, delivering it is the quest step: a receptacle that takes it is the step
    /// even when quest-locked, and every other quest step waits (research/dungeon-objectives.md §3).</summary>
    public IReadOnlyList<ActorState> WithCarryGoals(IReadOnlyList<ActorState> actors)
    {
        if (_objectives is not { } objectives) return actors;
        var held = actors.Where(IsHeld).Select(a => a.ActorSno).ToHashSet();
        if (held.Count == 0) return actors;
        return [.. actors.Select(a => held.Any(c => objectives.Accepts(a.ActorSno, c))
            ? a.QuestLocked ? a with { QuestLocked = false, QuestStep = true } : a
            : a.QuestStep ? a with { QuestStep = false, QuestLocked = true } : a)];
    }

    // The carryable on the ground has a game record; the copy in the player's hands has none (research/dungeon-objectives.md §3).
    private bool IsHeld(ActorState actor) => actor.ClientOnly && _objectives?.IsCarryable(actor.ActorSno) == true;
}
