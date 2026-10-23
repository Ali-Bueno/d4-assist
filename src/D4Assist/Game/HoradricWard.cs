using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// The Horadric guardian's barriers (QST_Skov_Skartara_06_VaultBoss, research/dungeon-objectives.md §11): each barrier names one of
/// the four vessels by the effect the guardian shows (Horadric_Guardian_harmless_Barrier_Zoltun/Tal/Iben/Jared); the player uses that
/// vessel, then stands in the barrier to break it. While a barrier stands, only its vessel sounds and the guide leads to it, then to
/// the guardian; through the rest of the fight the vessels are silent. The quest names the vessel of the first barrier only (phase
/// 271: Kulle); the later ones the guardian picks itself (live 2026-10-23: Zoltun, then Jared, the player used Tal Rasha's and died).
/// </summary>
/// <param name="nameOf">An SNO's name: the effects and the vessels are told by name.</param>
/// <param name="announce">Called with the vessel actor's name whenever a barrier names it (the app speaks the game's own name for
/// it, "Gefäß des Cain"); null for none.</param>
/// <param name="used">Called with the vessel actor's name once the player has used it (the app says it, and that the barrier is
/// next); null for none.</param>
public sealed class HoradricWard(Func<int, string?> nameOf, Action<string>? log = null, Action<string>? announce = null, Action<string>? used = null)
{
    // The guardian itself, hostile from its fight's start (its definition says team 1, ActorState.Hostile reads the live team),
    // and the chamber's other objects of it (QST_Horadric_Guardian_Boss_Base, _trigger, _spirit_trigger, _boss_SP).
    private const string GuardianName = "QST_Horadric_Guardian_boss", GuardianObjectPrefix = "QST_Horadric_Guardian_Boss";

    private const string BarrierPrefix = "Horadric_Guardian_harmless_Barrier_";
    private const string VesselPrefix = "QST_Skov_Skartara_WardBreakerShrine_";

    // The mage each barrier's symbol shows, and the vessel named after him (research/dungeon-objectives.md §11).
    private static readonly Dictionary<string, string> VesselOfSymbol = new(StringComparer.Ordinal)
    {
        ["Zoltun"] = "Kulle", ["Tal"] = "Rasha", ["Iben"] = "Fahd", ["Jared"] = "Cain",
    };

    /// <summary>Within this of the vessel the player uses it; of the guardian, stands in its barrier (4-6 units, research §11).</summary>
    public const float VesselReachUnits = 3f, BarrierReachUnits = 4f;

    private readonly object _gate = new();
    private uint? _guardian;
    private string? _vessel;
    private (float X, float Y)? _vesselAt, _guardianAt;
    private bool? _vesselStateAtStart;
    private bool _used;

    /// <summary>Rewrites one actor poll: in the guardian's fight, the vessel its barrier names is the quest step until used, every
    /// other vessel is locked (silent).</summary>
    public IReadOnlyList<ActorState> Apply(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
        {
            // The fight starts as the guardian turns hostile, before its first barrier: every vessel sounding then was noise (live
            // 2026-10-23: all four looped until the first barrier).
            var guardian = actors.FirstOrDefault(a => Symbol(a) is not null)
                ?? (_guardian is { } id ? actors.FirstOrDefault(a => a.Id == id) : null)
                ?? actors.FirstOrDefault(a => a.Hostile && nameOf(a.ActorSno) == GuardianName);
            if (guardian is null)
            {
                if (_guardian is not null) Log("guardian gone");
                Clear();
                _guardian = null;
                _guardianAt = null;
                return actors;
            }
            if (_guardian != guardian.Id) Log($"guardian {nameOf(guardian.ActorSno) ?? guardian.ActorSno.ToString()} in the fight");
            _guardian = guardian.Id;
            _guardianAt = (guardian.X, guardian.Y);
            Observe(Symbol(guardian), actors);
            return [.. actors.Select(a => Vessel(a) is not { } vessel ? IsGuardianObject(a) ? a with { QuestLocked = true, QuestStep = false } : a
                : vessel == _vessel && !_used ? a with { QuestStep = true, QuestLocked = false }
                : a with { QuestLocked = true, QuestStep = false })];
        }
    }

    /// <summary>Where the guide leads in the fight: while a barrier stands, its vessel until used, then the guardian, inside the
    /// barrier; between barriers, the guardian (live 2026-10-23: with no barrier the guide led to the chamber's locked door).</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
        {
            if (_vessel is null)
                return _guardian is not null && _guardianAt is { } at
                    ? [new GuideDestination("ward-guardian", at, ObjectiveMemory.QuestTier, Radius: BarrierReachUnits, Located: true)]
                    : [];
            if (!_used && _vesselAt is { } vessel)
                return [new GuideDestination($"ward-vessel:{_vessel}", vessel, ObjectiveMemory.QuestTier, Radius: VesselReachUnits, Located: true)];
            return _used && _guardianAt is { } guardian
                ? [new GuideDestination("ward-barrier", guardian, ObjectiveMemory.QuestTier, Radius: BarrierReachUnits, Located: true)]
                : [];
        }
    }

    /// <summary>A new world: no fight.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            Clear();
            _guardian = null;
            _guardianAt = null;
        }
    }

    private void Observe(string? vessel, IReadOnlyList<ActorState> actors)
    {
        if (vessel != _vessel)
        {
            Clear();
            _vessel = vessel;
            Log(vessel is null ? "barrier down" : $"barrier names the {vessel} vessel");
            if (vessel is not null) announce?.Invoke(VesselPrefix + vessel);
        }
        if (vessel is null) return;
        if (actors.FirstOrDefault(a => Vessel(a) == vessel) is not { } actor) return;
        _vesselAt = (actor.X, actor.Y);
        // Used: its state turns (on, or back off) after the barrier rose; a vessel already on from an earlier barrier turns too.
        bool state = actor.GizmoStateSet;
        _vesselStateAtStart ??= state;
        if (_used || state == _vesselStateAtStart) return;
        _used = true;
        Log($"{vessel} vessel used: on into the barrier");
        used?.Invoke(VesselPrefix + vessel);
    }

    private void Clear()
    {
        _vessel = null;
        _vesselAt = null;
        _vesselStateAtStart = null;
        _used = false;
    }

    // The vessel the barrier this actor shows names, or null.
    private string? Symbol(ActorState actor)
    {
        if (actor.Buffs is not { Count: > 0 } buffs) return null;
        foreach (int buff in buffs)
            if (nameOf(buff) is { } name && name.StartsWith(BarrierPrefix, StringComparison.Ordinal)
                && VesselOfSymbol.TryGetValue(name[BarrierPrefix.Length..], out var vessel))
                return vessel;
        return null;
    }

    // The guardian's own objects in its chamber (not the guardian, which the enemy cue sounds): silent in its fight.
    private bool IsGuardianObject(ActorState actor) =>
        nameOf(actor.ActorSno) is { } name && name.StartsWith(GuardianObjectPrefix, StringComparison.OrdinalIgnoreCase) && name != GuardianName;

    private string? Vessel(ActorState actor) =>
        nameOf(actor.ActorSno) is { } name && name.StartsWith(VesselPrefix, StringComparison.Ordinal) ? name[VesselPrefix.Length..] : null;

    private void Log(string what) => log?.Invoke($"WARD {what}");
}
