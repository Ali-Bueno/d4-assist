using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>A place the stronghold guide may lead to, and its rank: lower first (StrongholdTargeting).</summary>
public sealed record StrongholdTarget(QuestMapPlace Place, int Tier, bool Monster = false);

/// <summary>The running stronghold the player stands in, its quest's phase as last read (null unknown), and its targets.</summary>
public sealed record StrongholdTargets(int QuestSno, string Name, uint? Phase, IReadOnlyList<StrongholdTarget> Targets);

/// <summary>
/// What the guide leads to inside a stronghold that isn't freed yet (user request 2026-11-11: "like in dungeons", no list to step
/// through). Its targets are the map's markers in the stronghold that its quest names: the current step's shown places first, then
/// the next step's (a step that only asks for an item names no place: the Altar of Ruin's "Explore the cave" waits on a keystone the
/// cave's high priest, named by the next step, carries), then the hidden ones of both. Exploring (D-pad left held) takes every place
/// the quest names in any step and every monster the map marks there, all alike.
/// </summary>
public static class StrongholdTargeting
{
    /// <summary>Within this distance of a stronghold's spot (its quest's start) the player is in it, and its targets lie: the Altar
    /// of Ruin's cave priest stands 24 units from it, Xanderos' seals up to 85.</summary>
    public const float StrongholdUnits = 120f;

    /// <summary>The map's marker kind of a monster (MapMarkerReader).</summary>
    public const int MonsterMarkerKind = 10;

    /// <summary>The ranks of the targets (lower first).</summary>
    public const int ShownTier = 0, NextShownTier = 1, HiddenTier = 2, NextHiddenTier = 3;

    /// <param name="running">The quests in progress (owned): a freed stronghold's quest is not.</param>
    /// <param name="phaseOf">A quest's phase as last read; null unknown.</param>
    /// <param name="phasesOf">A quest's phases in order with the objects each names (QuestScriptMarkerReader.PhaseObjects).</param>
    /// <param name="nameOf">A marker's name to speak (its actor's in-game name), else null for the stronghold's name.</param>
    /// <param name="isQuestMarker">The game's own quest marker (Symbol_Quest_Proxy): the step's place, whatever the quest names.</param>
    public static StrongholdTargets? At(
        Vector2 here, IReadOnlyList<MarkedActor> markers, bool explore, IReadOnlyList<Stronghold> strongholds,
        IReadOnlyCollection<int> running, Func<int, uint?> phaseOf, Func<int, IReadOnlyList<QuestPhaseObjects>> phasesOf,
        Func<Stronghold, string> strongholdName, Func<MarkedActor, string?> nameOf, Func<MarkedActor, bool> isQuestMarker)
    {
        var stronghold = strongholds
            .Where(s => running.Contains(s.QuestSno) && Vector2.Distance(here, Spot(s)) <= StrongholdUnits)
            .OrderBy(s => Vector2.Distance(here, Spot(s))).FirstOrDefault();
        if (stronghold is null) return null;
        var phases = phasesOf(stronghold.QuestSno);
        uint? phase = phaseOf(stronghold.QuestSno);
        int index = phase is { } uid ? IndexOf(phases, uid) : -1;
        var step = index >= 0 ? phases[index] : null;
        var next = index >= 0 && index + 1 < phases.Count ? phases[index + 1] : null;
        string name = strongholdName(stronghold);

        var targets = new List<StrongholdTarget>();
        foreach (var marker in markers)
        {
            var at = new Vector2(marker.X, marker.Y);
            if (Vector2.Distance(at, Spot(stronghold)) > StrongholdUnits) continue;
            int? tier = explore ? Explored(marker, at, phases) : Tier(marker, at, step, next, phases, isQuestMarker(marker));
            if (tier is { } t)
                targets.Add(new(new QuestMapPlace(stronghold.QuestSno, nameOf(marker) ?? name, marker.ActorSno, at), t, marker.Kind == MonsterMarkerKind));
        }
        return new StrongholdTargets(stronghold.QuestSno, name, phase, targets);
    }

    private static Vector2 Spot(Stronghold stronghold) => new(stronghold.Position.X, stronghold.Position.Y);

    private static int IndexOf(IReadOnlyList<QuestPhaseObjects> phases, uint uid)
    {
        for (int i = 0; i < phases.Count; i++)
            if (phases[i].PhaseUid == uid) return i;
        return -1;
    }

    private static int? Tier(MarkedActor marker, Vector2 at, QuestPhaseObjects? step, QuestPhaseObjects? next,
        IReadOnlyList<QuestPhaseObjects> phases, bool questMarker)
    {
        if (questMarker) return ShownTier;
        if (step is null) return Named(marker, at, phases) ? ShownTier : null; // the phase unknown: whatever the quest names
        // A monster a step names only hidden is still one of its kills ("Kill 4": the high priests stand at hidden markers).
        bool monster = marker.Kind == MonsterMarkerKind;
        return QuestStep.Of(step, marker.ActorSno, at) switch
        {
            StepPlace.Shown => ShownTier,
            StepPlace.Hidden => monster ? ShownTier : HiddenTier,
            _ => next is null ? null : QuestStep.Of(next, marker.ActorSno, at) switch
            {
                StepPlace.Shown => NextShownTier,
                StepPlace.Hidden => monster ? NextShownTier : NextHiddenTier,
                _ => null,
            },
        };
    }

    private static int? Explored(MarkedActor marker, Vector2 at, IReadOnlyList<QuestPhaseObjects> phases) =>
        marker.Kind == MonsterMarkerKind || Named(marker, at, phases) ? ShownTier : null;

    private static bool Named(MarkedActor marker, Vector2 at, IReadOnlyList<QuestPhaseObjects> phases) =>
        phases.Any(p => QuestStep.Of(p, marker.ActorSno, at) != StepPlace.NotInStep);
}
