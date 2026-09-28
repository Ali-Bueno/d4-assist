using D4Data.Model;

namespace D4Memory.Scan;

/// <summary>A Scene SNO the matcher looks for, with the offline facts needed to validate a hit.</summary>
/// <param name="TileSize">Scene footprint (X, Y world units) from SceneDefinition.flTileX/flTileY; 0 if unknown.</param>
/// <param name="Bounds">Offline local AABB (min/max), from SceneDefinition.aabbBounds; see D4Data.Readers.SceneReader.</param>
public sealed record CandidateScene(int Sno, string? Name, float[] TileSize, int ExitMask, Box Bounds);
