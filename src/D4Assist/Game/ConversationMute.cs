using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Mutes the cues while the dialogue menu is open, while a conversation picked from it plays and while an NPC talks to
/// the player (user requests 2026-09-29: "hasta que ella terminase de hablar"; 2026-09-30: NPCs without a dialogue menu).
/// </summary>
/// <param name="typeOf">eConvType of a conversation SNO, null if unreadable; logged only.</param>
/// <param name="npcTalks">True for a conversation in which an NPC talks to the player (not a bark, not a hero line).</param>
public sealed class ConversationMute(
    Action<bool> setMuted, Func<int, string?> nameOf, Func<int, int?> typeOf, Func<int, bool> npcTalks, Action<string>? log = null)
{
    // SNO 0 is an option with no conversation (e.g. leaving the menu).
    private const int NoConversation = 0;

    private readonly HashSet<int> _offered = [];
    private readonly object _gate = new();
    private ConversationSnapshot? _last;

    public bool IsMuted { get; private set; }

    public void OnSnapshot(ConversationSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_last is { } last && last.MenuOptions.SequenceEqual(snapshot.MenuOptions) && last.Playing.SequenceEqual(snapshot.Playing)) return;
            _last = snapshot;

            bool menuOpen = snapshot.MenuOptions.Count > 0;
            if (menuOpen) _offered.UnionWith(snapshot.MenuOptions.Where(sno => sno != NoConversation));
            bool chosenPlaying = snapshot.Playing.Any(_offered.Contains);
            bool npcTalking = snapshot.Playing.Any(npcTalks);
            if (!menuOpen && snapshot.Playing.Count == 0) _offered.Clear();

            log?.Invoke($"TALK menu=[{Names(snapshot.MenuOptions)}] playing=[{Names(snapshot.Playing, withType: true)}]");
            Set(menuOpen || chosenPlaying || npcTalking);
        }
    }

    /// <summary>Forgets the menu and unmutes; call on world change or when the session ends.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _offered.Clear();
            _last = null;
            Set(false);
        }
    }

    private void Set(bool muted)
    {
        if (muted == IsMuted) return;
        IsMuted = muted;
        setMuted(muted);
    }

    private string Names(IEnumerable<int> snos, bool withType = false) =>
        string.Join(", ", snos.Select(s => (nameOf(s) ?? s.ToString()) + (withType ? $"({typeOf(s)?.ToString() ?? "?"}{(npcTalks(s) ? " npc" : "")})" : "")));
}
