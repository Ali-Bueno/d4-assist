using System.Numerics;

namespace D4Assist.Game;

/// <summary>
/// Walks an ordered route one point at a time. A point counts as reached within the reach radius, and reaching a
/// later point passes the earlier ones (the player may cut a corner).
/// </summary>
public sealed class RouteGuide(Vector2 start, IReadOnlyList<Vector2> points, float reachRadius)
{
    // Distances to two legs sharing the point the player stands near differ only by float rounding.
    private const float TieTolerance = 0.01f;

    public int Count => points.Count;

    /// <summary>How many points, in order, are behind the player.</summary>
    public int Reached { get; private set; }

    public bool Finished => Reached >= points.Count;

    public Vector2? Target => Finished ? null : points[Reached];

    /// <summary>Joins the route where the player stands: the target becomes the end of the nearest leg of
    /// start, point 1, point 2 and so on; on a tie (standing on a point) the later leg wins.</summary>
    public void JoinAt(Vector2 player)
    {
        float best = float.MaxValue;
        var from = start;
        for (int leg = 0; leg < points.Count; leg++)
        {
            float distance = DistanceToSegment(player, from, points[leg]);
            if (distance <= best + TieTolerance) (best, Reached) = (Math.Min(distance, best), leg);
            from = points[leg];
        }
    }

    /// <summary>True when this position reaches a new point.</summary>
    public bool Update(Vector2 player)
    {
        for (int i = points.Count - 1; i >= Reached; i--)
        {
            if (Vector2.Distance(points[i], player) > reachRadius) continue;
            Reached = i + 1;
            return true;
        }
        return false;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + t * ab);
    }
}
