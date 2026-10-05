namespace D4Assist.Game;

/// <summary>One CScene tile, as exposed to the app (extends CSceneHit with the DRLG node/group id it carries).</summary>
/// <param name="YawDegrees">Placement yaw decoded from the CScene quaternion (client-memory.md §3 convention).</param>
/// <param name="AuthoredExitMask">The scene's offline exit mask (TileInput.AuthoredExitMask).</param>
public sealed record TileState(
    int SceneSno, string? Name, float OriginX, float OriginY, float SizeX, float SizeY,
    int ExitMask, uint NodeId, uint GroupId, float YawDegrees = 0f, int AuthoredExitMask = 0);

/// <summary>One placed RActor (world coordinates), as exposed to the app.</summary>
/// <param name="StateChanged">The actor's state moved at least once; for a gizmo, it was used (research/client-memory.md §4).</param>
/// <param name="Yaw">Rotation about Z in radians, from +X towards +Y (the RActor quaternion).</param>
/// <param name="Operated">The game marked it used: ACD Gizmo_Has_Been_Operated (research/client-memory.md §4).</param>
/// <param name="QuestFollower">A companion NPC following the player for a quest: ACD Follower_Quest (research/client-memory.md §4).</param>
/// <param name="QuestLocked">A quest gizmo (Gizmo_Is_Quest_Restricted) that isn't the quest step.</param>
/// <param name="ScriptDisabled">ACD Gizmo_Disabled_By_Script.</param>
/// <param name="QuestStep">A quest gizmo (Gizmo_Is_Quest_Restricted) on the minimap or named by an active quest's scripts, or an
/// NPC with a conversation icon an active quest names.</param>
/// <param name="ClientOnly">No game record (ACD): a client-side prop, or what the player holds (research/client-memory.md §4).</param>
/// <param name="GizmoStateSet">ACD Gizmo_State is nonzero: an open door, a used corpse or herb (research/client-memory.md §4).</param>
/// <param name="Talkable">An NPC with interact options or a conversation icon (research/client-memory.md §4).</param>
/// <param name="NamedByQuest">The followed quests' current phase names this very actor (QuestNaming.It), whatever kind it is.</param>
/// <param name="Invulnerable">ACD Invulnerable: read on gizmos, NPCs, bosses and uniques only (Program readsFlags).</param>
public sealed record ActorState(
    uint Id, int ActorSno, float X, float Y, float Z, bool StateChanged = false, float Yaw = 0, bool Operated = false,
    bool QuestFollower = false, bool QuestLocked = false, bool ScriptDisabled = false, bool QuestStep = false, bool ClientOnly = false,
    bool GizmoStateSet = false, bool Talkable = false, bool NamedByQuest = false, bool Invulnerable = false)
{
    /// <summary>The game doesn't let the player use this gizmo yet.</summary>
    public bool Unusable => QuestLocked || ScriptDisabled;
}

/// <summary>A place the active quest's current phase names (a marker in world space): where its step happens.</summary>
/// <param name="Ahead">A gizmo the phase names by SNO alone, where a loaded tile places it or the map marks it: an objective to use, known before
/// it streams in, rather than a spot to stand in (research/dungeon-objectives.md §18).</param>
/// <param name="Marked">The game's map marks it (an Ahead gizmo): the server has it there, even before it streams in (§19).</param>
public sealed record QuestPlace(int ActorSno, float X, float Y, float Z = 0, bool Ahead = false, bool Marked = false);

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

    /// <summary>The current world's CWorldID from the log (research/runtime-signals.md §2); null when unknown.</summary>
    string? CurrentInstance => null;

    /// <summary>True in a radar-only world (config RadarOnlyWorlds): radar and loops run, announcements and guide stay off.
    /// CurrentWorld/WorldChanged report IsDungeon=false there.</summary>
    bool CurrentWorldRadarOnly => false;

    /// <summary>What the current world is; the overworld feeds player and actors only, and the radar and announcements stay silent there.</summary>
    WorldKind CurrentWorldKind =>
        CurrentWorldRadarOnly ? WorldKind.RadarOnly : CurrentWorld?.IsDungeon == true ? WorldKind.Dungeon : WorldKind.Other;

    /// <summary>Fires only while inside a dungeon or radar-only world, at the tile-poll interval or after an on-demand rescan.</summary>
    event Action<IReadOnlyList<TileState>>? TilesUpdated;

    /// <summary>Fires only while a memory session is open (dungeon, radar-only world or overworld), at the player-poll interval.</summary>
    event Action<PlayerState>? PlayerMoved;

    /// <summary>Fires only while a memory session is open (dungeon, radar-only world or overworld), at the actor-poll interval, with every placed actor.</summary>
    event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
}
