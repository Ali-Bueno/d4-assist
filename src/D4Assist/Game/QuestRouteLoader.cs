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
    /// <param name="questMarkers">The markers a quest's scripts name (QuestScriptMarkerReader.Markers), for the route's end.</param>
    /// <returns>Null, with a log line, when a name is missing or the marker set lacks the quest or target markers.</returns>
    public static LoadedQuestRoute? Load(
        QuestRoute route, QuestRouteGroups groups, Func<int, string, int?> resolve,
        Func<int, IReadOnlyList<PlacedMarker>> readMarkers, Func<int, IReadOnlyList<PlacedMarker>> questMarkers, Action<string> log)
    {
        int? quest = route.Quest is null ? null : resolve(groups.Quest, route.Quest);
        if (resolve(groups.MarkerSet, route.MarkerSet) is not { } markerSet
            || (route.Quest is not null && quest is null)
            || resolve(groups.Actor, route.TargetActor) is not { } target)
        {
            log($"Quest route '{route.Name}': a name is not in CoreTOC; route off.");
            return null;
        }

        var markers = readMarkers(markerSet);
        var points = markers.Where(m => m.Group == groups.Actor && m.Sno == target).Select(m => Flat(m.Position)).ToList();
        var activation = route.ActivationActors.Select(name => resolve(groups.Actor, name)).OfType<int>().ToHashSet();
        if (quest is null)
        {
            if (points.Count == 0)
            {
                log($"Quest route '{route.Name}': no target markers; route off.");
                return null;
            }
            return new LoadedQuestRoute(route, activation, points[0], points); // a trail: the set lists it in walking order
        }

        var start = markers.FirstOrDefault(m => m.Group == groups.Quest && m.Sno == quest);
        if (start is null || points.Count == 0)
        {
            log($"Quest route '{route.Name}': {points.Count} target markers, quest marker {(start is null ? "missing" : "found")}; route off.");
            return null;
        }

        var from = Flat(start.Position);
        var end = End(route, groups, resolve, questMarkers, log);
        return new LoadedQuestRoute(route, activation, from, RouteOrder.FromStart(from, points, end), end);
    }

    private static Vector2? End(
        QuestRoute route, QuestRouteGroups groups, Func<int, string, int?> resolve, Func<int, IReadOnlyList<PlacedMarker>> questMarkers,
        Action<string> log)
    {
        if (route.EndQuest is null || route.EndActor is null) return null;
        if (resolve(groups.Quest, route.EndQuest) is { } quest && resolve(groups.Actor, route.EndActor) is { } actor
            && questMarkers(quest).Where(m => m.Group == groups.Actor && m.Sno == actor).ToList() is [var marker])
            return Flat(marker.Position);
        log($"Quest route '{route.Name}': no single {route.EndActor} marker in {route.EndQuest}; the route ends at its last point.");
        return null;
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Y);
}
