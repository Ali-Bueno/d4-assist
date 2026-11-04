using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>What a legion target is: the guide names it by its kind.</summary>
public enum LegionTargetKind { Boss, Structure, Captain, Chest }

/// <summary>A target of a legion event the game's map marks: its kind, the marked actor and where the map marks it now.</summary>
public sealed record LegionTarget(LegionTargetKind Kind, int ActorSno, Vector2 Position);

/// <summary>
/// The targets of a legion event (a zone event, quests <c>Bounty_ZE_*</c>) among the game's map markers, for the guide inside the
/// event: the map marks every one of them in the event's level area (<c>Step_ZoneEvent</c>; live 2026-11-04, the Dry Steppes legion
/// of 07:45, <c>ZE_Invasion_Demon</c>): the boss (<c>Goatman_abom_boss</c>), the structures to destroy
/// (<c>Structure_Goatman_CorruptedShrine</c>, <c>Structure_Fallen_IdolOfTheShaman</c>, <c>Structure_Demon_DemonicSpire</c>), the
/// captains beside them (<c>…_ACD_ZE_Invasion_Demon_FallenCaptain</c>) and at the end the reward chests (<c>ZE_Chest_Reward</c>,
/// <c>ZE_Chest_Reward_Tier2</c>). The event's plain monsters, spawners and bonfire there are no targets. Other kinds of legion
/// (Chakhir's <c>ZE_TowerDefense</c>, Skovos' <c>ZE_IslandDefense</c>) are not seen live yet: their bosses match, their other goals may not.
/// </summary>
public sealed class LegionTargets(Func<int, string?> nameOf)
{
    /// <summary>Targets farther than this from the player aren't offered: the player isn't at that event.</summary>
    public const float RangeUnits = 150f;

    private const string EventAreaMark = "ZoneEvent";

    /// <summary>The legion targets the map marks within <see cref="RangeUnits"/> of <paramref name="here"/>: nearest first, the
    /// reward chests last (they come when all else is done, beside the boss's spot).</summary>
    public IReadOnlyList<LegionTarget> Near(IReadOnlyList<MarkedActor> markers, Vector2 here) =>
        [.. markers.Select(Target).OfType<LegionTarget>()
            .Where(t => Vector2.Distance(here, t.Position) <= RangeUnits)
            .OrderBy(t => t.Kind == LegionTargetKind.Chest)
            .ThenBy(t => Vector2.Distance(here, t.Position))];

    /// <summary>The legion target a map marker is, or null for any other marker.</summary>
    public LegionTarget? Target(MarkedActor marker)
    {
        if (nameOf(marker.LevelAreaSno) is not { } area || !area.Contains(EventAreaMark, StringComparison.OrdinalIgnoreCase)) return null;
        if (nameOf(marker.ActorSno) is not { } name) return null;
        return KindOf(name, marker.Kind) is { } kind ? new LegionTarget(kind, marker.ActorSno, new Vector2(marker.X, marker.Y)) : null;
    }

    /// <summary>A marked actor's kind of legion target by its name and map marker kind, or null for none.</summary>
    public static LegionTargetKind? KindOf(string name, int markerKind)
    {
        if (name.StartsWith("ZE_Chest_Reward", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Chest;
        if (markerKind != MapMarkerReader.KindBoss) return null; // spawners, the bonfire: gizmos at the targets' spots
        if (name.StartsWith("Structure_", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Structure;
        if (name.Contains("_ZE_", StringComparison.OrdinalIgnoreCase) && name.Contains("Captain", StringComparison.OrdinalIgnoreCase))
            return LegionTargetKind.Captain;
        if (name.EndsWith("_boss", StringComparison.OrdinalIgnoreCase)) return LegionTargetKind.Boss;
        return null;
    }
}
