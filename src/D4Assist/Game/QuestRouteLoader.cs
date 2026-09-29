using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>The CoreTOC groups a route's names live in.</summary>
public sealed record QuestRouteGroups(int MarkerSet, int Quest, int Actor);

/// <summary>Resolves a <see cref="QuestRoute"/> against the game data: its marker set's quest marker and target markers.</summary>
public static class QuestRouteLoader
{
    /// <param name="resolve">CoreTOC id of a name in a group; null when the name is missing.</param>
    /// <param name="readMarkers">Every marker of a marker set (MarkerReader.ReadAll).</param>
    /// <returns>Null, with a log line, when a name is missing or the marker set lacks the quest or target markers.</returns>
    public static LoadedQuestRoute? Load(
        QuestRoute route, QuestRouteGroups groups, Func<int, string, int?> resolve,
        Func<int, IReadOnlyList<PlacedMarker>> readMarkers, Action<string> log)
    {
        if (resolve(groups.MarkerSet, route.MarkerSet) is not { } markerSet
            || resolve(groups.Quest, route.Quest) is not { } quest
            || resolve(groups.Actor, route.TargetActor) is not { } target)
        {
            log($"Quest route '{route.Name}': a name is not in CoreTOC; route off.");
            return null;
        }

        var markers = readMarkers(markerSet);
        var start = markers.FirstOrDefault(m => m.Group == groups.Quest && m.Sno == quest);
        var points = markers.Where(m => m.Group == groups.Actor && m.Sno == target).Select(m => Flat(m.Position)).ToList();
        if (start is null || points.Count == 0)
        {
            log($"Quest route '{route.Name}': {points.Count} target markers, quest marker {(start is null ? "missing" : "found")}; route off.");
            return null;
        }

        var activation = route.ActivationActors.Select(name => resolve(groups.Actor, name)).OfType<int>().ToHashSet();
        return new LoadedQuestRoute(route, activation, Flat(start.Position), RouteOrder.FromStart(Flat(start.Position), points));
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Y);
}
