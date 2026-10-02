using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Aims D2R's friendly-NPC tap (NpcCuePlayer) at the nearest NPCs within range, on every player poll: as many as
/// NpcCuePlayer.Voices (a setting, two by default; D2R taps one, d2r-access docs/entity-cues.md). Direct bearing, no line of
/// sight: the overworld has no walk grid. Only NPCs the player can talk to or trade with get it (ActorState.Talkable); quest companions following
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
    private HashSet<uint> _tapping = [];

    /// <param name="isNpc">True for an actor that gets the tap (NpcClassifier, minus actors with their own loop).</param>
    /// <param name="log">Debug sink for target changes; null when not debugging.</param>
    public NpcCueBridge(IDungeonFeed feed, NpcCuePlayer player, Func<ActorState, bool> isNpc, Func<int, string?> nameOf, Action<string>? log = null)
    {
        _player = player;
        _isNpc = isNpc;
        _nameOf = nameOf;
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _npcs = []; _tapping = []; _loggedSilent.Clear(); _player.Silence(); } };
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
        var nearest = _npcs.Select(n => (Npc: n, Distance: Distance(n, p))).Where(n => n.Distance <= _player.RangeUnits)
            .OrderBy(n => n.Distance).Take(_player.Voices).ToList();
        foreach (var (npc, distance) in nearest.Where(n => !_tapping.Contains(n.Npc.Id)))
            _log?.Invoke($"NPC tap: {Name(npc)} at ({npc.X:F1},{npc.Y:F1}) distance={distance:F1}");
        _tapping = [.. nearest.Select(n => n.Npc.Id)];
        _player.Aim([.. nearest.Select(n =>
        {
            var (right, up) = ScreenDirections.ToScreen(n.Npc.X - p.X, n.Npc.Y - p.Y);
            return new NpcAim(n.Npc.Id, right, up, n.Distance);
        })]);
    }

    private string Name(ActorState npc) => _nameOf(npc.ActorSno) ?? npc.ActorSno.ToString();

    private static float Distance(ActorState a, PlayerState p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Y - p.Y) * (a.Y - p.Y));
}
