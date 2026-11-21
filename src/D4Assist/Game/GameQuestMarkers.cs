using System.Numerics;

namespace D4Assist.Game;

/// <summary>The game's own quest markers on the map (Symbol_Quest_Proxy, MarkerLocation), which no quest script names.</summary>
public static class GameQuestMarkers
{
    /// <summary>The map marker kind of quest markers.</summary>
    public const int Kind = 12;

    private static readonly string[] Actors = ["Symbol_Quest_Proxy", "MarkerLocation"];

    public static bool Is(MarkedActor marker, Func<int, string?> nameOf) =>
        marker.Kind == Kind && nameOf(marker.ActorSno) is { } name && Actors.Contains(name);

    /// <summary>The quest places plus each game quest marker no place already stands at, as a place the map marks.</summary>
    public static IReadOnlyList<QuestPlace> WithMarkers(IReadOnlyList<QuestPlace> places, IEnumerable<MarkedActor> markers, Func<MarkedActor, bool> isGameMarker)
    {
        List<QuestPlace> added = [.. markers.Where(isGameMarker)
            .Where(m => !places.Any(p => Vector2.Distance(new(p.X, p.Y), new(m.X, m.Y)) <= ActiveQuests.MarkerReach))
            .Select(m => new QuestPlace(m.ActorSno, m.X, m.Y, m.Z, Marked: true))];
        return added.Count == 0 ? places : [.. places, .. added];
    }
}
