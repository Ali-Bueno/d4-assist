using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>Keeps the actors the beacons can point at: placed in the world, with a finite position.</summary>
public static class ActorFilter
{
    /// <summary>The ACD flags the app reads (AcdFlagReader).</summary>
    public static readonly string[] AcdFlags =
        [AcdFlagReader.GizmoOperated, AcdFlagReader.FollowerQuest, AcdFlagReader.GizmoQuestRestricted, AcdFlagReader.GizmoDisabledByScript,
         AcdFlagReader.MinimapActive, AcdFlagReader.TrackedQuest, AcdFlagReader.OverrideTrackedQuest];

    /// <param name="flags">The actor's set ACD flags (AcdFlagReader.Read); null when unavailable or not read for it.</param>
    /// <param name="namedByQuest">True for an actor the tracked quest's scripts name (QuestScriptMarkerReader); null for none.</param>
    public static IReadOnlyList<ActorState> Placed(
        IEnumerable<RActorRecord> records, Func<RActorRecord, IReadOnlySet<string>?>? flags = null, Func<RActorRecord, bool>? namedByQuest = null) =>
        [.. records
            .Where(r => r.IsPlaced && float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y) && float.IsFinite(r.Position.Z))
            .Select(r => ToState(r, flags?.Invoke(r), namedByQuest))];

    private static ActorState ToState(RActorRecord r, IReadOnlySet<string>? flags, Func<RActorRecord, bool>? namedByQuest)
    {
        bool Has(string flag) => flags?.Contains(flag) == true;
        // A quest gizmo is the player's to use when the game shows it on the minimap or the tracked quest names it; otherwise
        // the quest doesn't let the player use it yet (research/client-memory.md §4).
        bool questGizmo = Has(AcdFlagReader.GizmoQuestRestricted);
        bool current = questGizmo && (Has(AcdFlagReader.MinimapActive) || namedByQuest?.Invoke(r) == true);
        return new(r.Id, r.ActorSno, r.Position.X, r.Position.Y, r.Position.Z, r.PreviousState != 0, Yaw(r.Rotation),
            Operated: Has(AcdFlagReader.GizmoOperated),
            QuestFollower: Has(AcdFlagReader.FollowerQuest),
            Unusable: (questGizmo && !current) || Has(AcdFlagReader.GizmoDisabledByScript),
            QuestStep: current);
    }

    /// <summary>Rotation about Z of a unit quaternion, from +X towards +Y.</summary>
    public static float Yaw(Quaternion q) =>
        MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
