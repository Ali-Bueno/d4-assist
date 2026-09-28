namespace D4Assist.Game;

/// <summary>One CScene tile, as exposed to the app (extends CSceneHit with the DRLG node/group id it carries).</summary>
/// <param name="YawDegrees">Placement yaw decoded from the CScene quaternion (client-memory.md §3 convention).</param>
public sealed record TileState(
    int SceneSno, string? Name, float OriginX, float OriginY, float SizeX, float SizeY,
    int ExitMask, uint NodeId, uint GroupId, float YawDegrees = 0f);

/// <summary>The local player's live position, as exposed to the app.</summary>
public sealed record PlayerState(float X, float Y, float Z, int SceneSno);

/// <summary>
/// Accessible snapshots of the current game state, decoupled from how they're produced (GameSession).
/// Gameplay/UI code (Program, future audio-cue/announcement services) consumes only this.
/// </summary>
public interface IDungeonFeed
{
    /// <summary>Fires on every world transition, dungeon or not — see GameSession's state machine.</summary>
    event Action<int, string, bool>? WorldChanged;

    /// <summary>Fires only while inside a dungeon, at the tile-poll interval or after an on-demand rescan.</summary>
    event Action<IReadOnlyList<TileState>>? TilesUpdated;

    /// <summary>Fires only while inside a dungeon, at the player-poll interval.</summary>
    event Action<PlayerState>? PlayerMoved;
}
