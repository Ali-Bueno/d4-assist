using System.Numerics;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>How the active quests' current phases name an actor: <see cref="It"/> is this placed actor (a marker at its spot, or
/// its SNO named outright), <see cref="ItsKind"/> a marker of its SNO elsewhere.</summary>
public enum QuestNaming { None, ItsKind, It }

/// <summary>Keeps the actors the beacons can point at: placed in the world, at a finite position off the origin and inside the world.</summary>
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
    /// <remarks>An actor at the origin, or beyond any world's edge, is junk the pool holds while a world unloads or the game
    /// closes (`gbuffer_*`, `PostFX *`, a `Journey_…_Tracker`, `Duriel_boss`): read as doors and chests, it started loops (live
    /// 2026-09-30, 2026-10-16; 2026-10-17 at the game's close, ~70 actors at a hair off (0,0,0) and a few at ~1e30).</remarks>
    public static IReadOnlyList<ActorState> Placed(
        IEnumerable<RActorRecord> records, Func<RActorRecord, IReadOnlySet<string>?>? flags = null,
        Func<RActorRecord, QuestNaming>? namedByQuest = null) =>
        [.. records
            .Where(r => r.IsPlaced && InWorld(r.Position))
            .Select(r => ToState(r, flags?.Invoke(r), namedByQuest?.Invoke(r) ?? QuestNaming.None))];

    /// <summary>Farther from the origin than any world reaches: Sanctuary spans a few thousand units.</summary>
    public const float WorldEdgeUnits = 100_000f;

    // Closer to the origin than this on every axis is the junk's (0,0,0) up to stray low bits.
    private const float OriginUnits = 0.01f;

    private static bool InWorld(Vector3 p)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return false;
        float farthest = MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z)));
        return farthest >= OriginUnits && farthest <= WorldEdgeUnits;
    }

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

    /// <summary>Locks the objects that belong to a quest not the player's (QuestOwnerIndex): the game lets nobody without that quest
    /// use them, yet flags none of them (live 2026-10-16, the Light's Watch lecterns: <c>flags=none</c>).</summary>
    /// <param name="ownerOf">The quest an actor SNO belongs to; null for none.</param>
    /// <param name="followed">The quests whose objects are the player's (ActiveQuests.Owned).</param>
    public static IReadOnlyList<ActorState> LockOthersQuests(IReadOnlyList<ActorState> actors, Func<int, int?> ownerOf, IReadOnlyList<int> followed) =>
        [.. actors.Select(a => !a.QuestStep && ownerOf(a.ActorSno) is { } quest && !followed.Contains(quest) ? a with { QuestLocked = true } : a)];

    /// <summary>Rotation about Z of a unit quaternion, from +X towards +Y.</summary>
    public static float Yaw(Quaternion q) =>
        MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
