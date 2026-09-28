namespace D4Assist.Navigation;

/// <summary>
/// A room as the announcer keys it, ported from D2R's `Rooms::RoomKey` (rooms.h:36-40).
/// The anchor is the room's lowest-origin member tile: deterministic and stable
/// across rebuilds of the same accumulated tile set. Invalid means "no room here",
/// which the hysteresis treats as "keep the current room" (not knowing is not leaving).
/// </summary>
public readonly record struct RoomKey(float AnchorX, float AnchorY, bool IsValid)
{
    public static readonly RoomKey Invalid = new(0, 0, false);
}
