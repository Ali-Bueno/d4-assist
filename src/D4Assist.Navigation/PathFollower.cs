using System.Numerics;

namespace D4Assist.Navigation;

/// <summary>
/// Follows a path (a polyline) as the player walks it: where along it they are, how far off it, and a point a little
/// further on to aim at. Aiming ahead of the player's spot on the path, not at the next path point, never points back:
/// a player passing a point 5 units aside never "reached" it within 4, and the ping kept pulling them back to it (live
/// 2026-10-19, the Dry Steppes).
/// </summary>
public sealed class PathFollower
{
    /// <summary>How far along the path past the player's last spot on it a step may skip ahead: the player cuts corners, but a
    /// path that doubles back near itself must not let them jump to its far end.</summary>
    private const float SearchAheadUnits = 15f;

    private readonly IReadOnlyList<Vector2> _path;
    private readonly float[] _lengthTo; // path length from the start to each point
    private int _leg;
    private float _along;

    /// <param name="path">At least one point; the first is where the walk starts.</param>
    public PathFollower(IReadOnlyList<Vector2> path)
    {
        if (path.Count == 0) throw new ArgumentException("A path needs a point.", nameof(path));
        _path = path;
        _lengthTo = new float[path.Count];
        for (int i = 1; i < path.Count; i++) _lengthTo[i] = _lengthTo[i - 1] + Vector2.Distance(path[i - 1], path[i]);
    }

    public Vector2 End => _path[^1];

    /// <summary>Where the player stands against the path: <paramref name="lookahead"/> units on from their nearest spot on it
    /// (the end at most), their distance from it, and the walk left from that spot.</summary>
    public (Vector2 Aim, float OffPath, float Remaining) Follow(Vector2 here, float lookahead)
    {
        if (_path.Count == 1) return (_path[0], Vector2.Distance(here, _path[0]), Vector2.Distance(here, _path[0]));
        float best = float.MaxValue, along = _along;
        int first = _leg;
        for (int leg = first; leg <= _path.Count - 2 && _lengthTo[leg] <= _along + SearchAheadUnits; leg++)
        {
            var (distance, t) = Project(here, _path[leg], _path[leg + 1]);
            if (distance >= best) continue;
            (best, _leg, along) = (distance, leg, _lengthTo[leg] + t * (_lengthTo[leg + 1] - _lengthTo[leg]));
        }
        _along = along;
        return (PointAt(along + lookahead), best, _lengthTo[^1] - along);
    }

    private Vector2 PointAt(float length)
    {
        if (length >= _lengthTo[^1]) return _path[^1];
        int i = _leg;
        while (_lengthTo[i + 1] < length) i++;
        float span = _lengthTo[i + 1] - _lengthTo[i];
        return span > 0 ? Vector2.Lerp(_path[i], _path[i + 1], (length - _lengthTo[i]) / span) : _path[i + 1];
    }

    private static (float Distance, float T) Project(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f) : 0f;
        return (Vector2.Distance(p, a + t * ab), t);
    }
}
