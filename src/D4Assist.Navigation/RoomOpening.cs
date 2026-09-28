namespace D4Assist.Navigation;

/// <summary>
/// A gap in a room's border that leads outside the room. Mirrors D2R's
/// `Rooms::Opening` (rooms.h:16-21), minus the D2R-specific width/across cell fields
/// that D4's tile exits don't need.
/// </summary>
/// <param name="Side">The world side (N/E/S/W) this exit sits on.</param>
/// <param name="Position">World midpoint of the walkable run through the gap (OpeningFinder), or the
/// tile edge's own midpoint when <paramref name="UsedFallback"/> is true.</param>
/// <param name="Unexplored">True when the tile beyond the gap has never been loaded.</param>
/// <param name="Width">Width of the walkable run, in world units; the tile's full edge length on fallback.</param>
/// <param name="UsedFallback">True when no walkability grid/run was available and this opening used the
/// old side-midpoint placement instead (task spec: flag it).</param>
public sealed record RoomOpening(ExitSide Side, (float X, float Y) Position, bool Unexplored, float Width = 0f, bool UsedFallback = false);
