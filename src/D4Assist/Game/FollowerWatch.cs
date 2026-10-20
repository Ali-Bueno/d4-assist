namespace D4Assist.Game;

/// <summary>Tells the actors that follow the player, like a freed prisoner: one that walks beside the player, or a re-created
/// copy of a follower's kind first seen beside them (research/dungeon-objectives.md §23).</summary>
public sealed class FollowerWatch
{
    /// <summary>A follower keeps within this distance of the player, even while the player runs (unmeasured).</summary>
    public const float BesideUnits = 10f;

    /// <summary>How far an actor must walk beside the player to count as following: a prisoner still to free never moves.</summary>
    public const float TravelUnits = 8f;

    // Where each actor was when it came beside the player; dropped when it falls behind.
    private readonly Dictionary<uint, (float X, float Y)> _starts = [];
    private readonly HashSet<uint> _seen = [];
    private readonly HashSet<uint> _followers = [];
    private readonly HashSet<int> _followerSnos = [];

    /// <summary>Forgets everything: a new world.</summary>
    public void Reset()
    {
        _starts.Clear();
        _seen.Clear();
        _followers.Clear();
        _followerSnos.Clear();
    }

    /// <summary>Takes one actor poll of the actors to watch.</summary>
    public void Observe(IEnumerable<ActorState> actors, PlayerState? player)
    {
        if (player is not { IsWorldUnloaded: false } p) return;
        foreach (var actor in actors)
        {
            bool first = _seen.Add(actor.Id);
            if (_followers.Contains(actor.Id)) continue;
            if (Distance((actor.X, actor.Y), (p.X, p.Y)) > BesideUnits)
            {
                _starts.Remove(actor.Id);
                continue;
            }
            if (!(first && _followerSnos.Contains(actor.ActorSno)))
            {
                if (!_starts.TryGetValue(actor.Id, out var start)) _starts[actor.Id] = start = (actor.X, actor.Y);
                if (Distance(start, (actor.X, actor.Y)) < TravelUnits) continue;
            }
            _followers.Add(actor.Id);
            _followerSnos.Add(actor.ActorSno);
            _starts.Remove(actor.Id);
        }
    }

    /// <summary>True for an actor found following the player in this world.</summary>
    public bool Follows(ActorState actor) => _followers.Contains(actor.Id);

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
