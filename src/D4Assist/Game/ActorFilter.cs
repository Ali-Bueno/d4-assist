using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>How the active quests' current phases name an actor: <see cref="It"/> is this placed actor (a marker at its spot, or
/// its SNO named outright), <see cref="ItsKind"/> a marker of its SNO elsewhere.</summary>
public enum QuestNaming { None, ItsKind, It }

/// <summary>Keeps the actors the beacons can point at: placed in the world, with a finite position.</summary>
public static class ActorFilter
{
    /// <summary>The ACD flags the app reads (AcdFlagReader).</summary>
    public static readonly string[] AcdFlags =
        [AcdFlagReader.GizmoOperated, AcdFlagReader.FollowerQuest, AcdFlagReader.GizmoQuestRestricted, AcdFlagReader.GizmoDisabledByScript,
         AcdFlagReader.MinimapActive, AcdFlagReader.TrackedQuest, AcdFlagReader.OverrideTrackedQuest, AcdFlagReader.GizmoState,
         AcdFlagReader.NpcHasInteractOptions, AcdFlagReader.ConversationIcon, AcdFlagReader.ConversationIconQuest,
         AcdFlagReader.Invulnerable];

    /// <param name="flags">The actor's set ACD flags (AcdFlagReader.Read); null when unavailable or not read for it.</param>
    /// <param name="namedByQuest">How the active quests' scripts name an actor (QuestScriptMarkerReader); null for none.</param>
    public static IReadOnlyList<ActorState> Placed(
        IEnumerable<RActorRecord> records, Func<RActorRecord, IReadOnlySet<string>?>? flags = null,
        Func<RActorRecord, QuestNaming>? namedByQuest = null) =>
        [.. records
            .Where(r => r.IsPlaced && float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y) && float.IsFinite(r.Position.Z))
            .Select(r => ToState(r, flags?.Invoke(r), namedByQuest?.Invoke(r) ?? QuestNaming.None))];

    private static ActorState ToState(RActorRecord r, IReadOnlySet<string>? flags, QuestNaming naming)
    {
        bool Has(string flag) => flags?.Contains(flag) == true;
        // A quest gizmo is the player's to use when the game shows it on the minimap or an active quest names it; otherwise
        // the quest doesn't let the player use it yet (research/client-memory.md §4). An NPC is the one to talk to when a quest
        // names its kind (it may stand off its marker) and it shows a conversation icon.
        bool questGizmo = Has(AcdFlagReader.GizmoQuestRestricted);
        bool follower = Has(AcdFlagReader.FollowerQuest);
        bool conversation = Has(AcdFlagReader.ConversationIcon) || Has(AcdFlagReader.ConversationIconQuest);
        bool current = questGizmo
            ? Has(AcdFlagReader.MinimapActive) || naming == QuestNaming.It
            : conversation && !follower && naming != QuestNaming.None;
        return new(r.Id, r.ActorSno, r.Position.X, r.Position.Y, r.Position.Z, r.PreviousState != 0, Yaw(r.Rotation),
            Operated: Has(AcdFlagReader.GizmoOperated),
            QuestFollower: follower,
            QuestLocked: questGizmo && !current,
            ScriptDisabled: Has(AcdFlagReader.GizmoDisabledByScript),
            QuestStep: current,
            ClientOnly: r.AcdId == RActorRecord.NoAcd,
            GizmoStateSet: Has(AcdFlagReader.GizmoState),
            Talkable: Has(AcdFlagReader.NpcHasInteractOptions) || conversation,
            NamedByQuest: naming == QuestNaming.It,
            Invulnerable: Has(AcdFlagReader.Invulnerable));
    }

    /// <summary>Locks the objects that belong to a quest not followed now (QuestOwnerIndex): the game lets nobody without that quest
    /// use them, yet flags none of them (live 2026-10-16, the Light's Watch lecterns: <c>flags=none</c>).</summary>
    /// <param name="ownerOf">The quest an actor SNO belongs to; null for none.</param>
    /// <param name="followed">The quests followed now (ActiveQuests.Followed).</param>
    public static IReadOnlyList<ActorState> LockOthersQuests(IReadOnlyList<ActorState> actors, Func<int, int?> ownerOf, IReadOnlyList<int> followed) =>
        [.. actors.Select(a => !a.QuestStep && ownerOf(a.ActorSno) is { } quest && !followed.Contains(quest) ? a with { QuestLocked = true } : a)];

    /// <summary>Rotation about Z of a unit quaternion, from +X towards +Y.</summary>
    public static float Yaw(Quaternion q) =>
        MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
