namespace D4Memory.Scan;

/// <summary>
/// Player-class actor SNOs (the value at the player position record's p-8 field; research/client-memory.md
/// §3). Ideally this set is derived offline from d4data's ActorDefinition records (a "this actor is a
/// player" type field), the same way CandidateSceneBuilder derives every Scene SNO from CoreTOC — but no
/// definitions.json was available to this task to confirm which ActorDefinition field marks a player actor,
/// so per the task spec this is the literal set of actor SNOs directly observed on live player records
/// across multiple classes and builds. Replace with an offline-derived set once that field is confirmed.
/// </summary>
public static class PlayerActorSnos
{
    public static readonly IReadOnlySet<int> Known = new HashSet<int>
    {
        72908, 176832, 220940, 223602, 232657, 338122, 421560, 430081, 486910, 502576,
        1241729, 1256872, 2081670, 2131351,
    };
}
