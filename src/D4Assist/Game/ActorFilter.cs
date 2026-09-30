using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>Keeps the actors the beacons can point at: placed in the world, with a finite position.</summary>
public static class ActorFilter
{
    /// <summary>The ACD flags the app reads (AcdFlagReader).</summary>
    public static readonly string[] AcdFlags = [AcdFlagReader.GizmoOperated, AcdFlagReader.FollowerQuest];

    /// <param name="flags">The actor's set ACD flags (AcdFlagReader.Read); null when unavailable or not read for it.</param>
    public static IReadOnlyList<ActorState> Placed(IEnumerable<RActorRecord> records, Func<RActorRecord, IReadOnlySet<string>?>? flags = null) =>
        [.. records
            .Where(r => r.IsPlaced && float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y) && float.IsFinite(r.Position.Z))
            .Select(r => ToState(r, flags?.Invoke(r)))];

    private static ActorState ToState(RActorRecord r, IReadOnlySet<string>? flags) =>
        new(r.Id, r.ActorSno, r.Position.X, r.Position.Y, r.Position.Z, r.PreviousState != 0, Yaw(r.Rotation),
            Operated: r.OperatedMark != 0 || flags?.Contains(AcdFlagReader.GizmoOperated) == true,
            QuestFollower: flags?.Contains(AcdFlagReader.FollowerQuest) == true);

    /// <summary>Rotation about Z of a unit quaternion, from +X towards +Y.</summary>
    public static float Yaw(Quaternion q) =>
        MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
