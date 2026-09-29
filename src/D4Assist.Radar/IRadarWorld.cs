namespace D4Assist.Radar;

/// <summary>What one radar cell holds. Unknown means no loaded data covers it, and is never a wall
/// (d2r-access src/radar_passage.cpp:64-69). Door is a closed door over walkable floor (D2R's DOOR collision bit).</summary>
public enum RadarCell : byte { Unknown, Open, Blocked, Door }

/// <summary>
/// The radar's view of the world, one cell per world unit: D2R's Radar::World (d2r-access src/radar.h:113-132)
/// reduced to what a static walk map offers. Cell (x, y) covers [x, x+1) x [y, y+1) in engine axes.
/// </summary>
public interface IRadarWorld
{
    RadarCell Cell(int x, int y);

    /// <summary>Stable id of the room covering the cell, or -1 when none.</summary>
    int RoomAt(int x, int y);

    /// <summary>Whether the player has stood in the room (D2R RoomGrid::visited).</summary>
    bool IsVisited(int room);

    /// <summary>The dungeon's typical doorway width in cells (D2R Rooms::TypicalOpeningWidth), or 0 when unknown.</summary>
    double TypicalOpeningWidth { get; }
}
