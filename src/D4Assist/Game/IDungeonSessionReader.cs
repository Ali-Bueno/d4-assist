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
}
