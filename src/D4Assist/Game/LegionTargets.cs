using System.Numerics;
using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>What a legion target is: the guide names it by its kind.</summary>
/// <remarks><see cref="Attack"/> and <see cref="Defend"/> are the defense legions' goals (Skovos' <c>ZE_IslandDefense</c>, Chakhir's
/// <c>ZE_TowerDefense</c>): a spot under attack, and a building to keep standing.</remarks>
public enum LegionTargetKind { Boss, Structure, Captain, Chest, Attack, Defend }

/// <summary>A target of a legion event the game's map marks: its kind, the marked actor and where the map marks it now.</summary>
/// <param name="Side">For a spot under attack: its side of the defended place, seen on the screen from the place's middle; null in
/// the middle or with no middle known.</param>
public sealed record LegionTarget(LegionTargetKind Kind, int ActorSno, Vector2 Position, StickDirection? Side = null);

/// <summary>
/// The targets of a legion event (a zone event, quests <c>Bounty_ZE_*</c>) among the game's map markers, for the guide inside the
/// event: the map marks every one of them in the event's level area (<c>Step_ZoneEvent</c>; live 2026-11-04, the Dry Steppes legion
/// of 07:45, <c>ZE_Invasion_Demon</c>): the boss (<c>Goatman_abom_boss</c>), the structures to destroy
/// (<c>Structure_Goatman_CorruptedShrine</c>, <c>Structure_Fallen_IdolOfTheShaman</c>, <c>Structure_Demon_DemonicSpire</c>), the
/// captains beside them (<c>…_ACD_ZE_Invasion_Demon_FallenCaptain</c>) and at the end the reward chests (<c>ZE_Chest_Reward</c>,
/// <c>ZE_Chest_Reward_Tier2</c>). The event's plain monsters, spawners and bonfire there are no targets. Other kinds of legion
/// (Chakhir's <c>ZE_TowerDefense</c>, Skovos' <c>ZE_IslandDefense</c>) differ:
/// <para>Defense legions (user report 2026-11-05: Skovos' island defense, "attacked from several directions"; no target was found):
/// per the game data (marker set <c>ZE_IslandDefense</c>) a high-HP building in the middle (<c>ZE_IslandDefense_Structure_HighHP</c>)
/// and three sides (north, south, west of it), each with a landing ship, a catapult, three buildings
/// (<c>ZE_IslandDefense_Structure</c>, marked on the map) and one map ping actor (<c>ZE_IslandDefense_MapPing_1..3</c>; the middle's
/// is <c>ZE_IslandDefense_MapPing</c>); Chakhir's tower defense has <c>ZE_TowerDefense_MapPing_1..4</c> and four
/// <c>ZE_Towerdefense_Inhibitor_*</c>. A marked map ping is taken for the spot under attack (assumed, not seen live): those come first,
/// nearest first, said with their side; with none marked the buildings still standing are the goals.</para>
/// </summary>
public sealed class LegionTargets(Func<int, string?> nameOf)
{
    /// <summary>Targets farther than this from the player aren't offered: the player isn't at that event.</summary>
    public const float RangeUnits = 150f;

    /// <summary>A spot under attack this close to the defended place's middle is in the middle: said without a side.</summary>
    public const float MiddleUnits = 10f;

    private const string EventAreaMark = "ZoneEvent";

    /// <summary>The legion targets the map marks within <see cref="RangeUnits"/> of <paramref name="here"/>: spots under attack
    /// first, then the standard legion's goals, then (only with no spot under attack) the buildings to defend, the reward chests
    /// last (they come when all else is done, beside the boss's spot); nearest first within each.</summary>
    public IReadOnlyList<LegionTarget> Near(IReadOnlyList<MarkedActor> markers, Vector2 here)
    {
        var near = markers.Where(m => Vector2.Distance(here, new Vector2(m.X, m.Y)) <= RangeUnits).ToList();
        var targets = near.Select(Target).OfType<LegionTarget>().ToList();
        bool attacked = targets.Any(t => t.Kind == LegionTargetKind.Attack);
        if (attacked && Middle(near, targets) is { } middle)
            targets = [.. targets.Select(t => t.Kind == LegionTargetKind.Attack && Vector2.Distance(t.Position, middle) > MiddleUnits
                ? t with { Side = ScreenDirections.Bin(t.Position.X - middle.X, t.Position.Y - middle.Y) } : t)];
        return [.. targets.Where(t => !(attacked && t.Kind == LegionTargetKind.Defend))
            .OrderBy(t => Rank(t.Kind))
            .ThenBy(t => Vector2.Distance(here, t.Position))];
    }

    /// <summary>Every marker the map holds in a legion's level area within <see cref="RangeUnits"/> of <paramref name="here"/>,
    /// for the debug log (which of them a defense legion marks, and when, is not seen live yet); null outside one.</summary>
    public string? Describe(IReadOnlyList<MarkedActor> markers, Vector2 here)
    {
        var inEvent = markers.Where(m => Vector2.Distance(here, new Vector2(m.X, m.Y)) <= RangeUnits && InEventArea(m)).ToList();
        if (inEvent.Count == 0) return null;
        return string.Join("; ", inEvent.Select(m => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{nameOf(m.ActorSno) ?? m.ActorSno.ToString()} kind {m.Kind} flags 0x{m.Flags:X} ({m.X:F1},{m.Y:F1}) {Vector2.Distance(here, new Vector2(m.X, m.Y)):F0}")));
    }

    private static int Rank(LegionTargetKind kind) => kind switch
    {
        LegionTargetKind.Attack => 0,
        LegionTargetKind.Defend => 2,
        LegionTargetKind.Chest => 3,
        _ => 1,
    };

    // The defended place's middle: the high-HP building, else the middle's map ping, else the buildings' midpoint; null for none.
    private Vector2? Middle(List<MarkedActor> near, List<LegionTarget> targets)
    {
        foreach (var marker in near)
            if (InEventArea(marker) && nameOf(marker.ActorSno) is { } name
                && (name.EndsWith("_Structure_HighHP", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_MapPing", StringComparison.OrdinalIgnoreCase)))
                return new Vector2(marker.X, marker.Y);
        var buildings = targets.Where(t => t.Kind == LegionTargetKind.Defend).ToList();
        return buildings.Count >= 2 ? buildings.Aggregate(Vector2.Zero, (sum, t) => sum + t.Position) / buildings.Count : null;
    }

    private bool InEventArea(MarkedActor marker) =>
        nameOf(marker.LevelAreaSno) is { } area && area.Contains(EventAreaMark, StringComparison.OrdinalIgnoreCase);

    /// <summary>The legion target a map marker is, or null for any other marker.</summary>
    public LegionTarget? Target(MarkedActor marker)
    {
        if (!InEventArea(marker)) return null;
        if (nameOf(marker.ActorSno) is not { } name) return null;
        return KindOf(name, marker.Kind) is { } kind ? new LegionTarget(kind, marker.ActorSno, new Vector2(marker.X, marker.Y)) : null;
    }

    /// <summary>A marked actor's kind of legion target by its name and map marker kind, or null for none.</summary>
    public static LegionTargetKind? KindOf(string name, int markerKind)
    {
        if (name.StartsWith("ZE_Chest_Reward", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Chest;
        // Defense legions: their map pings (any marker kind) and the buildings to keep standing, never their spawners.
        if (name.Contains("Defense_MapPing", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Attack;
        if (name.StartsWith("ZE_", StringComparison.OrdinalIgnoreCase) && !name.Contains("Spawner", StringComparison.OrdinalIgnoreCase)
            && (name.Contains("Defense_Structure", StringComparison.OrdinalIgnoreCase) || name.Contains("Defense_Inhibitor_", StringComparison.OrdinalIgnoreCase)))
            return LegionTargetKind.Defend;
        if (markerKind != MapMarkerReader.KindBoss) return null; // spawners, the bonfire: gizmos at the targets' spots
        if (name.StartsWith("Structure_", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Structure;
        if (name.Contains("_ZE_", StringComparison.OrdinalIgnoreCase) && name.Contains("Captain", StringComparison.OrdinalIgnoreCase))
            return LegionTargetKind.Captain;
        if (name.EndsWith("_boss", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_miniboss", StringComparison.OrdinalIgnoreCase))
            return LegionTargetKind.Boss; // the island defense's pirate_ranged_dreg_miniboss
        return null;
    }
}
