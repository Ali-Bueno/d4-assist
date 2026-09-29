namespace D4Assist.Navigation;

public enum GuideStatus { Unknown, Guiding, AllExplored }

public enum GuideTargetKind { UnvisitedTile, Frontier }

/// <param name="Id">Stable identity across recomputes (a tile cell, or an exit's tile/side/position).</param>
/// <param name="Position">World point to reach: where the route enters the tile, or the frontier opening.</param>
public sealed record GuideTarget(GuideTargetKind Kind, string Id, (float X, float Y) Position);

/// <summary>The guide's answer: where to head next and how far the whole route still is.</summary>
/// <param name="Waypoint">Look-ahead world point on the path to the opening that leaves the current room.</param>
/// <param name="RemainingLength">Route length to the target, in world units.</param>
public sealed record GuideStep(GuideStatus Status, GuideTarget? Target = null, (float X, float Y) Waypoint = default, float RemainingLength = 0f, int Arrivals = 0)
{
    public static readonly GuideStep Unknown = new(GuideStatus.Unknown);
    public static readonly GuideStep AllExplored = new(GuideStatus.AllExplored);
}
