namespace D4Assist.Game;

/// <summary>One CScene tile, as exposed to the app (extends CSceneHit with the DRLG node/group id it carries).</summary>
/// <param name="YawDegrees">Placement yaw decoded from the CScene quaternion (client-memory.md §3 convention).</param>
public sealed record TileState(
    int SceneSno, string? Name, float OriginX, float OriginY, float SizeX, float SizeY,
    int ExitMask, uint NodeId, uint GroupId, float YawDegrees = 0f);

/// <summary>One placed RActor (world coordinates), as exposed to the app.</summary>
public sealed record ActorState(uint Id, int ActorSno, float X, float Y, float Z);

/// <summary>The local player's live position, as exposed to the app.</summary>
public sealed record PlayerState(float X, float Y, float Z, int SceneSno)
{
    /// <summary>The static record reads exactly (0,0,0) while a world unloads (live 2026-09-28, dungeon → Limbo): not a real position.</summary>
    public bool IsWorldUnloaded => X == 0 && Y == 0 && Z == 0;
}

/// <summary>
/// Accessible snapshots of the current game state, decoupled from how they're produced (GameSession).
/// Gameplay/UI code (Program, future audio-cue/announcement services) consumes only this.
/// </summary>
public interface IDungeonFeed
{
    /// <summary>Fires on every world transition, dungeon or not — see GameSession's state machine.</summary>
    event Action<int, string, bool>? WorldChanged;

    /// <summary>The last world WorldChanged reported, for subscribers created after it fired (e.g. the startup world); null before any.</summary>
    (string Name, bool IsDungeon)? CurrentWorld { get; }

    /// <summary>True in a radar-only world (config RadarOnlyWorlds): radar and loops run, announcements and guide stay off.
    /// CurrentWorld/WorldChanged report IsDungeon=false there.</summary>
    bool CurrentWorldRadarOnly => false;

    /// <summary>Fires only while inside a dungeon, at the tile-poll interval or after an on-demand rescan.</summary>
    event Action<IReadOnlyList<TileState>>? TilesUpdated;

    /// <summary>Fires only while inside a dungeon, at the player-poll interval.</summary>
    event Action<PlayerState>? PlayerMoved;

    /// <summary>Fires only while inside a dungeon, at the actor-poll interval, with every placed actor.</summary>
    event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
}
