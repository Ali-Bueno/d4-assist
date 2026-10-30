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

    /// <summary>Id of the stretch of floor the open cell belongs to (cells a walk joins share one), or -1 when it isn't floor or unknown.</summary>
    int FloorAt(int x, int y) => -1;

    /// <summary>Whether the player has stood in the room (D2R RoomGrid::visited).</summary>
    bool IsVisited(int room);

    /// <summary>The dungeon's typical doorway width in cells (D2R Rooms::TypicalOpeningWidth), or 0 when unknown.</summary>
    double TypicalOpeningWidth { get; }

    /// <summary>Every room's doorways, one entry per side (D2R Radar::World::exits, fed with room doorways instead of level exits).</summary>
    IReadOnlyList<RadarDoorway> Doorways => [];
}

/// <summary>A doorway of <paramref name="Room"/>: its centre in cells, its width, the room beyond (-1 when not loaded), and the
/// floor it opens from (IRadarWorld.FloorAt; -1 when unknown).</summary>
public readonly record struct RadarDoorway(RadarVector Centre, double Width, int Room, int Beyond, int Floor = -1);
