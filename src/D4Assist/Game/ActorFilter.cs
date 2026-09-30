using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>Keeps the actors the beacons can point at: placed in the world, with a finite position.</summary>
public static class ActorFilter
{
    public static IReadOnlyList<ActorState> Placed(IEnumerable<RActorRecord> records) =>
        [.. records
            .Where(r => r.IsPlaced && float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y) && float.IsFinite(r.Position.Z))
            .Select(r => new ActorState(r.Id, r.ActorSno, r.Position.X, r.Position.Y, r.Position.Z, r.PreviousState != 0, Yaw(r.Rotation), r.OperatedMark != 0))];

    /// <summary>Rotation about Z of a unit quaternion, from +X towards +Y.</summary>
    public static float Yaw(Quaternion q) =>
        MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
