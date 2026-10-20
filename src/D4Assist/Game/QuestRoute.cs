using System.Numerics;

namespace D4Assist.Game;

/// <summary>What a route's points are, for the spoken phrases.</summary>
public enum RouteKind { Shelters }

/// <summary>A campaign stretch the assistant leads point by point (QuestRouteBridge). The points are the
/// <paramref name="TargetActor"/> markers of <paramref name="MarkerSet"/>, in world space; the marker the set holds for
/// <paramref name="Quest"/> itself is where the route starts. Every name is a CoreTOC name.</summary>
/// <param name="ActivationActors">Quest actors whose live sighting at the start or at a point starts the route.</param>
/// <param name="EndQuest">The quest that waits past the last point: the one <paramref name="EndActor"/> marker its scripts name is
/// where the route ends. Null for a route that ends at its last point.</param>
public sealed record QuestRoute(
    string Name, RouteKind Kind, string WorldName, string MarkerSet, string Quest, string TargetActor,
    IReadOnlyList<string> ActivationActors, string? EndQuest = null, string? EndActor = null);

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

    public static readonly IReadOnlyList<QuestRoute> All = [SandstormShelters];
}
