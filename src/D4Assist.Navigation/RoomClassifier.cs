namespace D4Assist.Navigation;

/// <summary>
/// Classifies a room by shape. Mirrors D2R's `Rooms::Describe` semantics (opening
/// count decides dead end / corridor / junction) with one deliberate addition:
/// D2R has no "big room" case because its maze rooms are one fixed preset size
/// (research/d2r-radar-port.md §1); D4's DRLG tiles vary, so a multi-tile room
/// (several tiles sharing a NodeId) always classifies as BigRoom, ahead of its
/// opening count.
/// </summary>
public static class RoomClassifier
{
    public static RoomKind Classify(Room room)
    {
        if (room.IsMultiTile) return RoomKind.BigRoom;
        return room.Openings.Count switch
        {
            <= 1 => RoomKind.DeadEnd,
            2 => RoomKind.Corridor,
            _ => RoomKind.Junction,
        };
    }
}
