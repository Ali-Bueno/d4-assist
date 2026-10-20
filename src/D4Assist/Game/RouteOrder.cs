using System.Numerics;

namespace D4Assist.Game;

/// <summary>Orders a route's points as the shortest walk from its start through all of them, on to its end when it has one.
/// Marker sets list points in no particular order; for the sandstorm shelters this gives the order the quest's phases name
/// them in (research/campaign-routes.md §2).</summary>
public static class RouteOrder
{
    // 8! = 40320 orders is instant; longer routes fall back to nearest-neighbour.
    public const int ExhaustiveLimit = 8;

    public static IReadOnlyList<Vector2> FromStart(Vector2 start, IReadOnlyList<Vector2> points, Vector2? end = null)
    {
        if (points.Count > ExhaustiveLimit) return NearestNeighbour(start, points);

        int[] best = [.. Enumerable.Range(0, points.Count)];
        float bestLength = float.MaxValue;
        foreach (var order in Permutations([.. Enumerable.Range(0, points.Count)], 0))
        {
            float length = WalkLength(start, points, order, end);
            if (length < bestLength) (bestLength, best) = (length, [.. order]);
        }
        return [.. best.Select(i => points[i])];
    }

    private static float WalkLength(Vector2 start, IReadOnlyList<Vector2> points, int[] order, Vector2? end)
    {
        float length = 0;
        var at = start;
        foreach (int i in order) { length += Vector2.Distance(at, points[i]); at = points[i]; }
        return end is { } last ? length + Vector2.Distance(at, last) : length;
    }

    // Yields every ordering of items[from..] in place; callers copy what they keep.
    private static IEnumerable<int[]> Permutations(int[] items, int from)
    {
        if (from >= items.Length - 1) { yield return items; yield break; }
        for (int i = from; i < items.Length; i++)
        {
            (items[from], items[i]) = (items[i], items[from]);
            foreach (var order in Permutations(items, from + 1)) yield return order;
            (items[from], items[i]) = (items[i], items[from]);
        }
    }

    private static List<Vector2> NearestNeighbour(Vector2 start, IReadOnlyList<Vector2> points)
    {
        var left = points.ToList();
        var ordered = new List<Vector2>(left.Count);
        var at = start;
        while (left.Count > 0)
        {
            var next = left.MinBy(p => Vector2.DistanceSquared(at, p));
            left.Remove(next);
            ordered.Add(next);
            at = next;
        }
        return ordered;
    }
}
