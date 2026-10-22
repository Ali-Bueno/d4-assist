namespace D4Assist.Game;

/// <summary>
/// Rathma's soul split (research/dungeon-objectives.md §25): the spirit leaves the body and must get back to it. The body has no
/// actor of its own: the player is pulled into a rune light beam, which stays on the body for the whole split, then the spirit is
/// thrown out and the resurrection actor appears with it and follows it, as the player position read does (live 2026-10-20: beam
/// at (-70.9,1.8) where the player stood 4 s, the resurrection actor first seen 5.6 units off). So the resurrection actor's loop
/// sounds at the beam; with no beam listed, where the actor was first seen.
/// </summary>
public sealed class SoulSplitBody(ObjectiveClassifier objectives)
{
    private readonly object _gate = new();
    private Dictionary<uint, (float X, float Y, float Z)> _bodies = [];

    /// <summary>The actors with each resurrection actor moved onto the body; called on every actor poll.</summary>
    public IReadOnlyList<ActorState> Apply(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
        {
            if (_bodies.Count == 0 && !actors.Any(IsSplit)) return actors;
            var beam = actors.FirstOrDefault(a => objectives.IsSoulSplitBeam(a.ActorSno));
            var bodies = new Dictionary<uint, (float X, float Y, float Z)>();
            foreach (var actor in actors.Where(IsSplit))
                bodies[actor.Id] = beam is not null ? (beam.X, beam.Y, beam.Z)
                    : _bodies.TryGetValue(actor.Id, out var body) ? body : (actor.X, actor.Y, actor.Z);
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
