using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// One dungeon session's memory access — opened on dungeon entry, closed on exit (GameSession). Lets tests
/// supply a fake instead of touching the real process (production: LiveDungeonSessionReader).
/// </summary>
public interface IDungeonSessionReader : IDisposable
{
    /// <summary>Null when the position can no longer be read (process likely exited) — GameSession then leaves the dungeon.</summary>
    PlayerState? ReadPlayer();

    /// <summary>Null when the static scene route is unavailable for this build/session — caller falls back to a full scan.</summary>
    IReadOnlyList<TileState>? ReadTilesViaRoute();

    /// <summary>Placed actors; null when the actor pool route is unavailable for this build/session.</summary>
    IReadOnlyList<ActorState>? ReadActors();

    /// <summary>The expensive full CScene heap scan; called at most once per entry, plus on demand.</summary>
    IReadOnlyList<TileState> ReadTilesViaFullScan();

    /// <summary>The dialogue menu and the playing conversations; null when unknown for this build or unreadable.</summary>
    ConversationSnapshot? ReadConversation() => null;

    /// <summary>Names of the open watched menu screens; null while they are not located or unreadable.</summary>
    IReadOnlyList<string>? ReadMenus() => null;

    /// <summary>True while an in-engine cinematic plays; null while the player's state is unreadable.</summary>
    bool? ReadCinematic() => null;

    /// <summary>The places the active quests' current phases name, as of the last actor read; null when unknown.</summary>
    IReadOnlyList<QuestPlace>? ReadQuestPlaces() => null;
}

/// <summary>Opening a session failed with the game running: no access to the process, no game module, or no player record.
/// Thrown in place of a plain InvalidOperationException, which means the game is not running.</summary>
public sealed class GameUnreadableException(string message) : InvalidOperationException(message);
