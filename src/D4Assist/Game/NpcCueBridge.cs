using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Aims D2R's friendly-NPC tap (NpcCuePlayer) at the nearest NPC within range, on every player poll; one NPC at a time,
/// like D2R's single NPC class (d2r-access docs/entity-cues.md). Direct bearing, no line of sight: the overworld has no
/// walk grid. An NPC seen walking toward the player follows them and stays silent until it walks away, leading
/// (user, 2026-09-30). Silent on a world change.
/// </summary>
public sealed class NpcCueBridge
{
    /// <summary>Movement is judged over this window; shorter steps are animation jitter.</summary>
    public const double WindowSeconds = 1.5;

    /// <summary>Less movement than this over the window is standing still.</summary>
    public const float MinStepUnits = 1f;

    // A step within 60 degrees of the player's bearing heads toward them, one within 60 degrees of the opposite heads away.
    private const double HeadingCosine = 0.5;

    private readonly NpcCuePlayer _player;
    private readonly Func<ActorState, bool> _isNpc;
    private readonly Func<int, string?> _nameOf;
    private readonly Func<double> _now;
    private readonly Action<string>? _log;
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private readonly Dictionary<uint, Queue<(double T, float X, float Y)>> _tracks = [];
    private readonly HashSet<uint> _following = [];
    private IReadOnlyList<ActorState> _npcs = [];
    private PlayerState? _lastPlayer;
    private uint? _loggedId;

    /// <param name="isNpc">True for an actor that gets the tap (NpcClassifier, minus actors with their own loop).</param>
    /// <param name="nowSeconds">Clock for the movement window; null uses a stopwatch.</param>
    /// <param name="log">Debug sink for target changes; null when not debugging.</param>
    public NpcCueBridge(
        IDungeonFeed feed, NpcCuePlayer player, Func<ActorState, bool> isNpc, Func<int, string?> nameOf, Action<string>? log = null,
        Func<double>? nowSeconds = null)
    {
        _player = player;
        _isNpc = isNpc;
        _nameOf = nameOf;
        _log = log;
        var clock = Stopwatch.StartNew();
        _now = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        feed.WorldChanged += (_, _, _) => { lock (_gate) Reset(); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) { _lastPlayer = p; Aim(p); } };
    }

    private void Reset()
    {
        _npcs = [];
        _tracks.Clear();
        _following.Clear();
        _loggedId = null;
        _player.Silence();
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        double now = _now();
        _npcs = [.. actors.Where(_isNpc)];
        var present = _npcs.Select(n => n.Id).ToHashSet();
        foreach (var gone in _tracks.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _tracks.Remove(gone);
            _following.Remove(gone);
        }
        foreach (var npc in _npcs)
        {
            if (!_tracks.TryGetValue(npc.Id, out var track)) _tracks[npc.Id] = track = new();
            track.Enqueue((now, npc.X, npc.Y));
            while (track.Count > 1 && now - track.Peek().T > WindowSeconds) track.Dequeue();
            if (_lastPlayer is { } p) Judge(npc, track.Peek(), p);
        }
    }

    // Toward the player: a companion following. Away from the player: leading the way, so it sounds again.
    private void Judge(ActorState npc, (double T, float X, float Y) from, PlayerState p)
    {
        float dx = npc.X - from.X, dy = npc.Y - from.Y, step = MathF.Sqrt(dx * dx + dy * dy);
        float tx = p.X - npc.X, ty = p.Y - npc.Y, bearing = MathF.Sqrt(tx * tx + ty * ty);
        if (step < MinStepUnits || bearing < MinStepUnits) return;
        double cosine = (dx * tx + dy * ty) / (step * bearing);
        bool changed = cosine >= HeadingCosine ? _following.Add(npc.Id) : cosine <= -HeadingCosine && _following.Remove(npc.Id);
        if (changed) _log?.Invoke($"NPC {(_following.Contains(npc.Id) ? "follows the player, silent" : "leads, sounding")}: {Name(npc)}");
    }

    private void Aim(PlayerState p)
    {
        var nearest = _npcs.Where(n => !_following.Contains(n.Id)).MinBy(n => Distance(n, p));
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
