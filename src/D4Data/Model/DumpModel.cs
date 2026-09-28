namespace D4Data.Model;

/// <summary>A SNO reference with its CoreTOC name.</summary>
public sealed record SnoRef(int Sno, string? Name);

/// <summary>Axis-aligned box as min/max corners, in world units.</summary>
public sealed record Box(float[] Min, float[] Max);

/// <summary>Position + rotation quaternion (x, y, z, w).</summary>
public sealed record Pose(float[] Position, float[] Rotation);

public sealed record ServerDataInfo(int Seed, int SceneChunkCount, Box Bounds);

public sealed record WorldInfo(
    SnoRef World,
    int LayoutType,
    string LayoutKind,
    bool RandomDungeon,
    float GridSize,
    SnoRef? DefaultSubzone,
    IReadOnlyList<SnoRef> DrlgFloors,
    IReadOnlyList<SnoRef> DrlgLevelAreas,
    IReadOnlyList<ServerDataInfo> ServerData);

/// <summary>A scene instance placed by the World SNO (static layouts only).</summary>
public sealed record PlacedScene(SnoRef Scene, Pose Pose, SnoRef? LevelArea, Box? WorldBounds);

/// <summary>Walkable box of one AIPathData nav cell, in scene-local coordinates.</summary>
public sealed record NavCell(string CellId, Box Walkable);

/// <summary>Top-down walkable map; row r is X = Origin[0] + r * Resolution (north to south), char c is Y (west to east).</summary>
public sealed record WalkableMap(float Resolution, float[] Origin, string[] Rows);

/// <summary>Nav data of a scene: coarse per-cell boxes; walkable area and map when the voxel grid is present.</summary>
public sealed record Walkability(float CellWidth, float CellHeight, IReadOnlyList<NavCell> Cells, float? WalkableArea, WalkableMap? Map);

/// <summary>An actor marker; <c>World</c> is set when the scene has a known placement.</summary>
public sealed record MarkerInfo(string Layer, int MarkerType, SnoRef Actor, float[] Local, float[]? World);

/// <summary>Cheap scene facts (no markers/walkability) for candidate enumeration over many scenes.</summary>
public sealed record SceneSummary(SnoRef Scene, float[] TileSize, int ExitMask, string[] Exits, Box Bounds);

public sealed record SceneInfo(
    SnoRef Scene,
    float[] TileSize,
    Box Bounds,
    int ExitMask,
    string[] Exits,
    Walkability? Walkability,
    IReadOnlyList<MarkerInfo> Markers);

public sealed record DumpResult(
    string Tool,
    string GameBuild,
    WorldInfo World,
    IReadOnlyList<PlacedScene> PlacedScenes,
    IReadOnlyList<SceneInfo> Scenes,
    IReadOnlyList<MarkerInfo> WorldMarkers,
    IReadOnlyList<string> Notes);
