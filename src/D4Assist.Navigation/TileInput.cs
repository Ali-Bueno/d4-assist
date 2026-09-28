namespace D4Assist.Navigation;

/// <summary>
/// A loaded DRLG tile as reported by the memory reader, ~1 Hz, for the tiles
/// currently in the loaded window around the player (research/client-memory.md §1).
/// </summary>
/// <param name="SceneSno">The scene's static numeric object id.</param>
/// <param name="Name">Optional scene definition name, for diagnostics.</param>
/// <param name="OriginX">World-space origin corner, X (engine axis: south).</param>
/// <param name="OriginY">World-space origin corner, Y (engine axis: east).</param>
/// <param name="SizeX">Tile size along X, in world units (32 or 64).</param>
/// <param name="SizeY">Tile size along Y, in world units (32 or 64).</param>
/// <param name="ExitMask">Exit bits: North=1, South=2, East=4, West=8 (client-memory.md §1, a-44).</param>
/// <param name="NodeId">DRLG node id; tiles sharing it form one multi-tile room.</param>
/// <param name="GroupId">DRLG group id, observed alongside NodeId on multi-tile rooms.</param>
/// <param name="YawDegrees">CScene placement yaw (client-memory.md §3 quaternion convention). Every
/// tile seen so far is 0 (identity quaternion); non-zero values only matter for TileRotation.</param>
public sealed record TileInput(
    int SceneSno,
    string? Name,
    float OriginX,
    float OriginY,
    float SizeX,
    float SizeY,
    int ExitMask,
    uint NodeId,
    uint GroupId,
    float YawDegrees = 0f);

/// <summary>Live player world position (engine axes: X = south, Y = east, Z = up).</summary>
public sealed record PlayerInput(float X, float Y, float Z);
