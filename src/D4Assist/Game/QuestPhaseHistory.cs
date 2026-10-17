namespace D4Assist.Game;

/// <summary>The last phase read of each quest, in any world since the app started: in a dungeon the game may hold another quest's
/// pair the whole time (live 2026-10-17: QST_Frac_PaleKnightsKin read at phase 15, bring the journals back, in the overworld, and
/// never in the Light's Watch). Shared by every world session (ActiveQuests); thread-safe.</summary>
public sealed class QuestPhaseHistory
{
    private readonly object _gate = new();
    private readonly Dictionary<int, uint> _last = [];

    public void Record(int quest, uint phase)
    {
        lock (_gate) _last[quest] = phase;
    }

    /// <summary>Null while the quest's phase was never read.</summary>
    public uint? Last(int quest)
    {
        lock (_gate) return _last.TryGetValue(quest, out uint phase) ? phase : null;
    }
}
