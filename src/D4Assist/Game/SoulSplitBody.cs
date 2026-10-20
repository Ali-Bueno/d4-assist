namespace D4Assist.Game;

/// <summary>
/// Rathma's soul split (research/dungeon-objectives.md §24): the spirit leaves the body and must get back to it. The body has no
/// actor of its own; the resurrection actor appears where the player stands as the split begins, then follows the spirit, as the
/// player position read does (live 2026-10-20: first seen 0.2 units from the player, gone 7 units on, still at the player). So
/// its loop stays where it was first seen: the body.
/// </summary>
public sealed class SoulSplitBody(ObjectiveClassifier objectives)
{
    private readonly object _gate = new();
    private Dictionary<uint, (float X, float Y, float Z)> _bodies = [];

    /// <summary>The actors with each soul-split actor moved back to where it was first seen; called on every actor poll.</summary>
    public IReadOnlyList<ActorState> Apply(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
        {
            if (_bodies.Count == 0 && !actors.Any(IsSplit)) return actors;
            var bodies = new Dictionary<uint, (float X, float Y, float Z)>();
            foreach (var actor in actors.Where(IsSplit))
                bodies[actor.Id] = _bodies.TryGetValue(actor.Id, out var body) ? body : (actor.X, actor.Y, actor.Z);
            _bodies = bodies;
            return [.. actors.Select(a => bodies.TryGetValue(a.Id, out var at) && IsSplit(a) ? a with { X = at.X, Y = at.Y, Z = at.Z } : a)];
        }
    }

    /// <summary>Forgets every body; call on every world change.</summary>
    public void Reset()
    {
        lock (_gate) _bodies = [];
    }

    private bool IsSplit(ActorState actor) => objectives.IsSoulBody(actor.ActorSno);
}
