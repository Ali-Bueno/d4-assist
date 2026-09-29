using System.Numerics;

namespace D4Assist.Game;

/// <summary>What a route's points are, for the spoken phrases.</summary>
public enum RouteKind { Shelters }

/// <summary>A campaign stretch the assistant leads point by point (QuestRouteBridge). The points are the
/// <paramref name="TargetActor"/> markers of <paramref name="MarkerSet"/>, in world space; the marker the set holds for
/// <paramref name="Quest"/> itself is where the route starts. Every name is a CoreTOC name.</summary>
/// <param name="ActivationActors">Actors that exist only while the quest runs: seeing one live starts the route.</param>
public sealed record QuestRoute(
    string Name, RouteKind Kind, string WorldName, string MarkerSet, string Quest, string TargetActor,
    IReadOnlyList<string> ActivationActors);

/// <summary>A route resolved offline: its activation actor SNOs, its start and its points in visiting order (world X/Y).</summary>
public sealed record LoadedQuestRoute(QuestRoute Route, IReadOnlySet<int> ActivationSnos, Vector2 Start, IReadOnlyList<Vector2> Points);

/// <summary>The routes the assistant knows (research/campaign-routes.md).</summary>
public static class QuestRouteCatalog
{
    /// <summary>The campaign's desert crossing with Meshif (user: act 3): storms hit on the way and the player must stand in
    /// one of three safe zones, 80-120 units apart, farther than the live actor list reaches (~65 units).</summary>
    public static readonly QuestRoute SandstormShelters = new(
        "sandstorm-shelters", RouteKind.Shelters, "Sanctuary_Eastern_Continent",
        "Kehj_Ridge (Kehj_Ridge_Subzone_EliasPalace_Sandstorm)", "VGN_Kehj_EliasPalace_Sandstorm", "BCD_Trigger_EliasPalace_SafeZone",
        [
            "NPC_QST_Kehj_EliasPalace_Guide", "BCD_Trigger_EliasPalace_SafeZone", "Trigger_EliasPalace_SandStorm_Highwind",
            "VGN_Kehj_EliasPalace_Sandstorm_Tornado_Invisible_Parent", "environmentVFX_sandstorm_safeZone_wisps",
        ]);

    public static readonly IReadOnlyList<QuestRoute> All = [SandstormShelters];
}
