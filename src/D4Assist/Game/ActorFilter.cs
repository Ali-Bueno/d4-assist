using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>Keeps the actors the beacons can point at: placed in the world, with a finite position.</summary>
public static class ActorFilter
{
    public static IReadOnlyList<ActorState> Placed(IEnumerable<RActorRecord> records) =>
        [.. records
            .Where(r => r.IsPlaced && float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y) && float.IsFinite(r.Position.Z))
            .Select(r => new ActorState(r.Id, r.ActorSno, r.Position.X, r.Position.Y, r.Position.Z))];
}
