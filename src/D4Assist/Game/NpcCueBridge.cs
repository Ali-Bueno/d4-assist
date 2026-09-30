using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Aims D2R's friendly-NPC tap (NpcCuePlayer) at the nearest NPC within range, on every player poll; one NPC at a time,
/// like D2R's single NPC class (d2r-access docs/entity-cues.md). Direct bearing, no line of sight: the overworld has no
/// walk grid. Only NPCs the player can talk to or trade with get it (ActorState.Talkable); quest companions following
/// the player (ActorState.QuestFollower) stay silent (user, 2026-09-30).
/// Silent on a world change.
/// </summary>
public sealed class NpcCueBridge
{
    private readonly NpcCuePlayer _player;
    private readonly Func<ActorState, bool> _isNpc;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private readonly HashSet<uint> _loggedSilent = [];
    private IReadOnlyList<ActorState> _npcs = [];
    private uint? _loggedId;

    /// <param name="isNpc">True for an actor that gets the tap (NpcClassifier, minus actors with their own loop).</param>
    /// <param name="log">Debug sink for target changes; null when not debugging.</param>
    public NpcCueBridge(IDungeonFeed feed, NpcCuePlayer player, Func<ActorState, bool> isNpc, Func<int, string?> nameOf, Action<string>? log = null)
    {
        _player = player;
        _isNpc = isNpc;
        _nameOf = nameOf;
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _npcs = []; _loggedId = null; _loggedSilent.Clear(); _player.Silence(); } };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) Aim(p); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        var npcs = actors.Where(_isNpc).ToList();
        foreach (var npc in npcs.Where(n => (n.QuestFollower || !n.Talkable) && _loggedSilent.Add(n.Id)))
            _log?.Invoke($"NPC {(npc.QuestFollower ? "follows the player" : "offers no interaction")}, silent: {Name(npc)}");
        _npcs = [.. npcs.Where(n => n.Talkable && !n.QuestFollower)];
    }

    private void Aim(PlayerState p)
    {
        var nearest = _npcs.MinBy(n => Distance(n, p));
        if (nearest is null || Distance(nearest, p) > _player.RangeUnits)
        {
            _player.Silence();
            _loggedId = null;
            return;
        }
        if (_loggedId != nearest.Id) _log?.Invoke($"NPC tap: {Name(nearest)} at ({nearest.X:F1},{nearest.Y:F1}) distance={Distance(nearest, p):F1}");
        _loggedId = nearest.Id;
        var (right, up) = ScreenDirections.ToScreen(nearest.X - p.X, nearest.Y - p.Y);
        _player.Aim(right, up, Distance(nearest, p));
    }

    private string Name(ActorState npc) => _nameOf(npc.ActorSno) ?? npc.ActorSno.ToString();

    private static float Distance(ActorState a, PlayerState p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Y - p.Y) * (a.Y - p.Y));
}
