namespace D4Assist.Navigation;

public enum GuideStatus { Unknown, Guiding, AllExplored, Rechecking }

public enum GuideTargetKind { UnvisitedTile, Frontier, ExitCandidate, Destination }

/// <summary>A known point to lead to once nothing is left to explore (e.g. a portal seen earlier).</summary>
/// <param name="Tier">Lower tiers win: the guide picks among the reachable destinations of the lowest tier.</param>
public sealed record GuideDestination(string Id, (float X, float Y) Position, int Tier = 0);

/// <param name="Id">Stable identity across recomputes (a tile cell, or an exit's tile/side/position).</param>
/// <param name="Position">World point to reach: where the route enters the tile, or the frontier opening.</param>
public sealed record GuideTarget(GuideTargetKind Kind, string Id, (float X, float Y) Position);

/// <summary>The guide's answer: where to head next and how far the whole route still is.</summary>
/// <param name="Waypoint">Look-ahead world point on the path to the opening that leaves the current room.</param>
/// <param name="RemainingLength">Route length to the target, in world units.</param>
/// <param name="Round">Re-check round (1-based) while <see cref="GuideStatus.Rechecking"/>; otherwise 0.</param>
public sealed record GuideStep(GuideStatus Status, GuideTarget? Target = null, (float X, float Y) Waypoint = default, float RemainingLength = 0f, int Arrivals = 0, int Round = 0)
{
    public static readonly GuideStep Unknown = new(GuideStatus.Unknown);
    public static readonly GuideStep AllExplored = new(GuideStatus.AllExplored);
}
