using System.Collections.Concurrent;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Why an actor counts as a dungeon objective (logged with the loop).</summary>
public enum ObjectiveReason { Dpo, Gizmo, Prisoner, Quest, Enticement, StolenSkill, AkaratLight, SoulBody, WardBreaker, Destructible, KillTarget, Monolith, CloneGate, Chargeable }

/// <summary>Decides whether an actor SNO is a dungeon objective to interact with (research/dungeon-objectives.md §3-4).</summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
/// <param name="namedByAnyQuest">True for an actor SNO some quest names (DungeonObjectiveReader.ActorsNamedByAnyQuest); null for none.</param>
public sealed class ObjectiveClassifier(
    Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf, Func<int, bool>? namedByAnyQuest = null)
{
    // Free-able prisoners are named DGN_NPC_<Region>_Prisoner_*; "Dead" excludes DGN_Standard_DeadPrisoner_*.
    private const string PrisonerToken = "Prisoner", DeadToken = "Dead";

    // Story switches and notes the game data can't tell from levers and lore (research/dungeon-objectives.md §8): named
    // for their quest, the prologue, or Lilith's blood petals.
    private static readonly string[] QuestTokens = ["QST_", "Quest", "Prologue", "BloodPetal"];
    private static readonly HashSet<int> QuestNamedGizmos = [Conventions.GizmoSwitch, Conventions.GizmoSign];

    // Undercity attunement switches, placed by the server (X1_Undercity_Enticement_SpiritHearth_Switch,
    // X1_Undercity_Enticements_SpiritBeaconSwitch; research/dungeon-objectives.md §5).
    private const string EnticementToken = "Enticement";

    // Mephisto's phase 2 scatters the player's stolen skills as these orbs, walked over to take back (research/dungeon-objectives.md §11).
    private const string StolenSkillToken = "Mephisto_Boss_P2_OrbActor";

    // Akarat's lights in the Harbinger of Hatred fight: orbs (effects) the player picks up for Akarat's power; their
    // Akarat_Orb_Pillar companion stands on the same spot (research/dungeon-objectives.md §11).
    private const string AkaratLightName = "Akarat_Orb";

    // Rathma's soul split: he tears the player's spirit out, and it must hurry back to the body it left, which makes no sound
    // (user request 2026-10-20, "Das Gewicht einer Klinge"). The resurrection actor appears as the split begins (SoulSplitBody
    // keeps it on the body); the boss's Location_Markers are five fixed spots round the arena, there from the fight's start.
    private const string SoulBodyName = "Rathma_SoulSplit_resurection_invisibleActor";

    // The rune light beam the player is pulled into as the split begins; it stands on the body until the split ends
    // (vfx_Rathmos_harmless_persistentCylindrical_SoulSplit_rune_lightbeams_02).
    private const string SoulSplitBeamToken = "SoulSplit_rune_lightbeams";
    private readonly ConcurrentDictionary<int, bool> _beams = new();

    // The Horadric Guardian's four vessels (QST_Skov_Skartara_WardBreakerShrine_Kulle/Rasha/Fahd/Cain), one per sigil: the
    // one matching the barrier's symbol breaks it, again at every barrier (research/dungeon-objectives.md §11).
    private const string WardBreakerToken = "WardBreakerShrine";

    // Structures broken by attacking them are hostile monster-type actors named *_Multistage (walls, barricades, totems; 184 in
    // build 3.2.2). The 10 a quest names must go for the story to go on: the hatred tumors of the Ancient Mausoleum
    // (Frac_Cursed_Tumor_01_Corner_2x2_01_Multistage), cracked walls, cocoons, a demon gate (research/dungeon-objectives.md §12).
    private const string DestructibleToken = "_Multistage";

    // Door gizmos used to summon a dungeon's boss (SilentMonolith_DYN, FrozenMonolith_DYN, the Grinning Labyrinth's), each beside
    // its boss spawner; not every DPO names them (research/dungeon-objectives.md §15).
    private const string MonolithToken = "Monolith";

    // Gates a boss summons its invulnerable clones from; while one stands the boss is immune (live 2026-10-04, the Echo of Diablo:
    // MorluGate_Triad_A_Boss_terrorCloneSource, 18 HP, not invulnerable).
    private const string CloneSourceToken = "CloneSource";

    // Gizmo types that are objectives in every dungeon.
    private static readonly HashSet<int> AlwaysGizmos =
    [
        Conventions.GizmoCarryable, Conventions.GizmoCarryableReceptacle, Conventions.GizmoQuestSwitch, Conventions.GizmoQuestChest,
    ];

    // The world-independent part of the answer, cached per actor SNO.
    private readonly record struct Traits(
        ObjectiveReason? Fixed, bool IsMonster, bool IsGizmo, bool IsDoor, bool IsCarryable, bool IsBossOrUnique, IReadOnlyList<int>? Accepts,
        bool IsTrigger);

    // Filled from several poll threads (timer ticks overlap); no lock is held while classifying, which takes the CASC gate.
    private readonly ConcurrentDictionary<int, Traits?> _traits = new();

    /// <param name="dpoActors">Actors the current dungeon's DPO quest names (DungeonObjectiveReader.ActorSnos).</param>
    public ObjectiveReason? Classify(int actorSno, IReadOnlySet<int> dpoActors)
    {
        if (TraitsOf(actorSno) is not { } traits) return null;
        if (traits.Fixed is { } reason) return reason;
        if (!dpoActors.Contains(actorSno)) return null;
        // An "enter the trigger" zone is invisible and never used: looped, it would sound at nothing for good, and its generic SNO
        // names every copy in the dungeon (research/dungeon-objectives.md §2). The followed phase leads into it instead.
        if (traits.IsTrigger) return null;
        // A monster the DPO names is a target the dungeon's objective asks the player to kill, which the game's objective
        // tracker shows: a unique elite or a structure (live 2026-10-01, Maulwood: the sentinel that holds a barrier).
        // Other monsters stay silent.
        return traits.IsMonster ? ObjectiveReason.KillTarget : ObjectiveReason.Dpo;
    }

    /// <summary>True for gizmos, the only actors a "used" signal applies to: monsters, NPCs and effects change state as they
    /// animate, and loop until they leave the actor list.</summary>
    public bool IsGizmo(int actorSno) => TraitsOf(actorSno)?.IsGizmo == true;

    /// <summary>True for an orb holding one of the skills Mephisto stole from the player.</summary>
    public bool IsStolenSkill(int actorSno) => TraitsOf(actorSno)?.Fixed == ObjectiveReason.StolenSkill;

    /// <summary>True for the actor marking the body Rathma's soul split leaves behind.</summary>
    public bool IsSoulBody(int actorSno) => TraitsOf(actorSno)?.Fixed == ObjectiveReason.SoulBody;

    /// <summary>True for the light beam standing on the body during Rathma's soul split.</summary>
    public bool IsSoulSplitBeam(int actorSno) =>
        _beams.GetOrAdd(actorSno, sno => nameOf(sno)?.Contains(SoulSplitBeamToken, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>True for an item the player carries to a receptacle (relics, idols, stone carvings).</summary>
    public bool IsCarryable(int actorSno) => TraitsOf(actorSno)?.IsCarryable == true;

    /// <summary>True for a receptacle a carryable goes into (altars, pedestals).</summary>
    public bool IsReceptacle(int actorSno) => TraitsOf(actorSno)?.Accepts is not null;

    /// <summary>True when the receptacle <paramref name="receptacleSno"/> takes the carryable <paramref name="carryableSno"/>.</summary>
    public bool Accepts(int receptacleSno, int carryableSno) => TraitsOf(receptacleSno)?.Accepts?.Contains(carryableSno) == true;

    /// <summary>True for door gizmos (doors, blockers, cages, monoliths).</summary>
    public bool IsDoor(int actorSno) => TraitsOf(actorSno)?.IsDoor == true;

    /// <summary>True for a hostile unique or boss: a quest phase that names one asks for that kill, while the ordinary monsters a
    /// few phases name come by the dozen (research/dungeon-objectives.md §16).</summary>
    public bool IsBossOrUnique(int actorSno) => TraitsOf(actorSno)?.IsBossOrUnique == true;

    private Traits? TraitsOf(int actorSno)
    {
        if (_traits.TryGetValue(actorSno, out var cached)) return cached;
        Traits? traits = definition(actorSno) is { } info
            ? new Traits(FixedReason(actorSno, info), info.EType == Conventions.ActorTypeMonster, info.GizmoType >= 0,
                info.GizmoType == Conventions.GizmoDoor, info.GizmoType == Conventions.GizmoCarryable, IsBossOrUnique(info),
                info.AcceptedCarryables, info.GizmoType == Conventions.GizmoTrigger)
            : null;
        return _traits[actorSno] = traits;
    }

    private ObjectiveReason? FixedReason(int actorSno, ActorDefinitionInfo info)
    {
        string? name = nameOf(actorSno);
        if (IsPrisoner(name, info)) return ObjectiveReason.Prisoner;
        if (name?.Contains(StolenSkillToken, StringComparison.OrdinalIgnoreCase) == true) return ObjectiveReason.StolenSkill;
        if (string.Equals(name, AkaratLightName, StringComparison.OrdinalIgnoreCase)) return ObjectiveReason.AkaratLight;
        if (string.Equals(name, SoulBodyName, StringComparison.OrdinalIgnoreCase)) return ObjectiveReason.SoulBody;
        if (info.GizmoType == Conventions.GizmoSwitch && name?.Contains(WardBreakerToken, StringComparison.OrdinalIgnoreCase) == true)
            return ObjectiveReason.WardBreaker;
        if (info.GizmoType == Conventions.GizmoSwitch && name?.Contains(EnticementToken, StringComparison.OrdinalIgnoreCase) == true)
            return ObjectiveReason.Enticement;
        if (info.GizmoType == Conventions.GizmoDoor && name?.Contains(MonolithToken, StringComparison.OrdinalIgnoreCase) == true)
            return ObjectiveReason.Monolith;
        if (info.EType == Conventions.ActorTypeMonster && info.DefaultTeam == Conventions.HostileTeam
            && name?.Contains(CloneSourceToken, StringComparison.OrdinalIgnoreCase) == true)
            return ObjectiveReason.CloneGate;
        if (IsQuestDestructible(actorSno, name, info)) return ObjectiveReason.Destructible;
        // A chargeable a quest names is the quest's to use once set off: Inarius's chain stations, each released by his script in
        // his fight, give the chain that binds him (live 2026-10-21). Capture points and pillars no quest names stay silent.
        if (IsQuestChargeable(actorSno, info)) return ObjectiveReason.Chargeable;
        bool questNamed = QuestNamedGizmos.Contains(info.GizmoType) && name is not null
            && QuestTokens.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
        return questNamed || AlwaysGizmos.Contains(info.GizmoType) ? ObjectiveReason.Gizmo : null;
    }

    /// <summary>True for a chargeable gizmo some quest names (Inarius's chain stations).</summary>
    public bool IsChargeable(int actorSno) => TraitsOf(actorSno)?.Fixed == ObjectiveReason.Chargeable;

    private bool IsQuestChargeable(int actorSno, ActorDefinitionInfo info) =>
        info.GizmoType == Conventions.GizmoChargeable && namedByAnyQuest?.Invoke(actorSno) == true;

    private bool IsQuestDestructible(int actorSno, string? name, ActorDefinitionInfo info) =>
        info.GizmoType < 0 && info.DefaultTeam == Conventions.HostileTeam
        && name?.Contains(DestructibleToken, StringComparison.OrdinalIgnoreCase) == true
        && namedByAnyQuest?.Invoke(actorSno) == true;

    private static bool IsBossOrUnique(ActorDefinitionInfo info) => EnemyClassifier.ClassOf(info) is not null;

    private static bool IsPrisoner(string? name, ActorDefinitionInfo info) =>
        name is not null
        && name.Contains(PrisonerToken, StringComparison.OrdinalIgnoreCase)
        && !name.Contains(DeadToken, StringComparison.OrdinalIgnoreCase)
        && (info.EType == Conventions.ActorTypeMonster || info.GizmoType == Conventions.GizmoSwitch);
}
