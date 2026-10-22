using System.Numerics;

namespace D4Assist.Game;

/// <summary>What a route's points are: shelters to wait in, a trail of markers to walk along, or the strokes of a pentagram
/// to trace (a trail spoken as one).</summary>
public enum RouteKind { Shelters, Trail, Pentagram }

/// <summary>A campaign stretch the assistant leads point by point (QuestRouteBridge). The points are the
/// <paramref name="TargetActor"/> markers of <paramref name="MarkerSet"/>, in world space; the marker the set holds for
/// <paramref name="Quest"/> itself is where the route starts. Without a quest the markers are a trail laid in walking
/// order: the route starts at the first. Every name is a CoreTOC name.</summary>
/// <param name="ActivationActors">Quest actors whose live sighting at the start or at a point starts the route.</param>
/// <param name="EndQuest">The quest that waits past the last point: the one <paramref name="EndActor"/> marker its scripts name is
/// where the route ends. Null for a route that ends at its last point.</param>
/// <param name="StartWhenUsed">Only an activation actor the player has used starts it (the ritual begins when it is used).</param>
/// <param name="ReachUnits">How close counts as reaching a point; null for the kind's default.</param>
/// <param name="Sections">How many points each part of the route has, in order, when the quest asks for one part at a time:
/// the guide holds at a part's end, stops when the quest moves on there, and later goes on with the next part. Null for one.</param>
public sealed record QuestRoute(
    string Name, RouteKind Kind, string WorldName, string MarkerSet, string? Quest, string TargetActor,
    IReadOnlyList<string> ActivationActors, string? EndQuest = null, string? EndActor = null, bool StartWhenUsed = false,
    float? ReachUnits = null, IReadOnlyList<int>? Sections = null);

/// <summary>A route resolved offline: its activation actor SNOs, its start, its points in visiting order and where it ends
/// past them, if anywhere (world X/Y).</summary>
public sealed record LoadedQuestRoute(
    QuestRoute Route, IReadOnlySet<int> ActivationSnos, Vector2 Start, IReadOnlyList<Vector2> Points, Vector2? End = null)
{
    /// <summary>What the guide walks: the points, then the end.</summary>
    public IReadOnlyList<Vector2> Path { get; } = End is { } end ? [.. Points, end] : Points;
}

/// <summary>The routes the assistant knows (research/campaign-routes.md).</summary>
public static class QuestRouteCatalog
{
    /// <summary>The campaign's desert crossing with Meshif (user: act 3): storms hit on the way and the player must stand in
    /// one of three safe zones, 80-120 units apart, farther than the live actor list reaches (~65 units). It ends on the palace
    /// steps, where the next quest has Meshif waiting.</summary>
    public static readonly QuestRoute SandstormShelters = new(
        "sandstorm-shelters", RouteKind.Shelters, "Sanctuary_Eastern_Continent",
        "Kehj_Ridge (Kehj_Ridge_Subzone_EliasPalace_Sandstorm)", "VGN_Kehj_EliasPalace_Sandstorm", "BCD_Trigger_EliasPalace_SafeZone",
        [
            "NPC_QST_Kehj_EliasPalace_Guide", "BCD_Trigger_EliasPalace_SafeZone", "Trigger_EliasPalace_SandStorm_Highwind",
            "VGN_Kehj_EliasPalace_Sandstorm_Tornado_Invisible_Parent", "environmentVFX_sandstorm_safeZone_wisps",
        ],
        EndQuest: "QST_Kehj_EliasPalace_03", EndActor: "NPC_QST_Kehj_EliasPalace_Guide");

    /// <summary>"Auf blutigem Pfad" (QST_Skov_Philios_06, Skovos): follow Lilith's blood petals from Lorath to the waterfall
    /// shrine, 21 petals about 10 units apart, through an area the game's own navigation doesn't cover (user, 2026-10-18).
    /// The petal trigger is placed by other quests too, far from here (Lycander, Celestia).</summary>
    public static readonly QuestRoute PhiliosBloodPetals = new(
        "philios-blood-petals", RouteKind.Trail, "Sanctuary_Eastern_Continent",
        "Skov_Philios (Skov_Philios_Campaign_Design)", null, "QST_X2_Skov_Lilith_BloodPetal_Trigger_Sphere",
        ["QST_X2_Skov_Lilith_BloodPetal_Trigger_Sphere"]);

    /// <summary>"Hexenmeister: Bauch der Bestie" (QST_Warlock_ClassQ_02 2449617, phase 5 waits on QST_Warlock_ClassQ_Trace_Ritual
    /// 2538551, "Folgt dem Pentagramm."): after the sacrifice is used, trace a pentagram ~10 units across in the Western Flats by
    /// walking its strokes, 20 Trigger_Sphere_8m markers ~2.5 units apart that the set lists in walking order (start, then
    /// strokes of 4, 7 and 8). The game marks none of them on its map, so its navigation stays silent (user, 2026-10-19). Live the
    /// quest moved on to its dungeon after the start and the first stroke: the rest are later parts.</summary>
    public static readonly QuestRoute WarlockPentagram = new(
        "warlock-pentagram", RouteKind.Pentagram, "Sanctuary_Eastern_Continent",
        "Step_WesternFlats (QST_Warlock_ClassQ_2a)", null, "Trigger_Sphere_8m",
        ["QST_Warlock_ClassQ_Trace_Sacrifice"], StartWhenUsed: true, ReachUnits: 1.5f, Sections: [5, 7, 8]);

    public static readonly IReadOnlyList<QuestRoute> All = [SandstormShelters, PhiliosBloodPetals, WarlockPentagram];
}
