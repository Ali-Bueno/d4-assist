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
public sealed class HoradricWard(Func<int, string?> nameOf, Action<string>? log = null)
{
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
            var guardian = actors.FirstOrDefault(a => Symbol(a) is not null) ?? (_guardian is { } id ? actors.FirstOrDefault(a => a.Id == id) : null);
            if (guardian is null)
            {
                if (_guardian is not null) Log("guardian gone");
                Clear();
                _guardian = null;
                return actors;
            }
            if (_guardian != guardian.Id) Log($"guardian {nameOf(guardian.ActorSno) ?? guardian.ActorSno.ToString()} in the fight");
            _guardian = guardian.Id;
            _guardianAt = (guardian.X, guardian.Y);
            Observe(Symbol(guardian), actors);
            return [.. actors.Select(a => Vessel(a) is not { } vessel ? a
                : vessel == _vessel && !_used ? a with { QuestStep = true, QuestLocked = false }
                : a with { QuestLocked = true, QuestStep = false })];
        }
    }

    /// <summary>Where the guide leads while a barrier stands: its vessel until used, then the guardian, inside the barrier.</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
        {
            if (_vessel is null) return [];
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
        }
    }

    private void Observe(string? vessel, IReadOnlyList<ActorState> actors)
    {
        if (vessel != _vessel)
        {
            Clear();
            _vessel = vessel;
            Log(vessel is null ? "barrier down" : $"barrier names the {vessel} vessel");
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

    private string? Vessel(ActorState actor) =>
        nameOf(actor.ActorSno) is { } name && name.StartsWith(VesselPrefix, StringComparison.Ordinal) ? name[VesselPrefix.Length..] : null;

    private void Log(string what) => log?.Invoke($"WARD {what}");
}
