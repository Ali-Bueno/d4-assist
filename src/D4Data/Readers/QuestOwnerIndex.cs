namespace D4Data.Readers;

/// <summary>
/// The quest an actor belongs to by name: <c>QST_Frac_PaleKnightsKin_Lectern_02</c> belongs to the quest
/// <c>QST_Frac_PaleKnightsKin</c>. Quest scripts name almost no quest chest (1 of 125 in build 3.2.3; 118 are called
/// <c>QST_&lt;quest&gt;_…</c>), so the name is the only link from such an object to its quest (live 2026-10-16: the side quest's
/// lecterns in the Light's Watch looped where the player, without that quest, found nothing to use).
/// </summary>
public sealed class QuestOwnerIndex
{
    private readonly Dictionary<string, int> _questsByName;

    /// <param name="quests">Every QuestDefinition entry (SnoStore.AllEntries).</param>
    public QuestOwnerIndex(IEnumerable<(int Id, string Name)> quests)
    {
        _questsByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, name) in quests) _questsByName.TryAdd(name, id);
    }

    /// <summary>The quest whose name is the longest <c>_</c>-separated prefix of <paramref name="actorName"/>; null for none.</summary>
    public int? OwnerOf(string actorName)
    {
        for (int end = actorName.LastIndexOf('_'); end > 0; end = actorName.LastIndexOf('_', end - 1))
            if (_questsByName.TryGetValue(actorName[..end], out int quest)) return quest;
        return null;
    }
}
