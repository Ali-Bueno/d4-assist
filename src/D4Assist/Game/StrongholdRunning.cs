using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The quests in progress for the stronghold guide: the owned ones, read at most every <see cref="OwnedInterval"/>, plus the quest of
/// the stronghold the player is in when the owned read misses it (Kor Dragan, live 2026-11-12). That one runs while its phase pair
/// reads in the module data or the map marks a monster in it, until the player leaves or the character's completed quests hold it.
/// Thread-safe: asked from the pad's thread and the map read's.
/// </summary>
/// <param name="readCompleted">The character's completed quests (CompletedQuestsReader); null unread.</param>
/// <param name="findPhases">The {quest, phase} pairs the module data holds for the quests and phase UIDs asked (a scan).</param>
/// <param name="recordPhase">Keeps a quest's phase as last read (QuestPhaseHistory): the targets go by it.</param>
public sealed class StrongholdRunning(
    Func<IReadOnlyList<int>?> readOwned, Func<IReadOnlySet<int>?> readCompleted,
    Func<IReadOnlyDictionary<int, IReadOnlySet<uint>>, IReadOnlyDictionary<int, uint>?> findPhases,
    Func<int, IReadOnlyList<QuestPhaseObjects>> phasesOf, Action<int, uint> recordPhase, Func<TimeSpan> clock, Action<string>? log = null)
{
    /// <summary>The owned quests are read at most this often (each map read asks).</summary>
    public static readonly TimeSpan OwnedInterval = TimeSpan.FromSeconds(3);

    /// <summary>A stronghold quest the owned read misses has its phase pair searched at most this often.</summary>
    public static readonly TimeSpan PairInterval = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private IReadOnlyList<int> _owned = [];
    private TimeSpan? _read, _searched;
    private int? _unread;

    /// <summary>The player is in no stronghold: one counted from its pair or its monsters stops counting.</summary>
    public void Left()
    {
        lock (_gate) _unread = null;
    }

    /// <summary>The quests in progress, with <paramref name="near"/>'s own when it counts as running.</summary>
    public IReadOnlyList<int> InProgress(Stronghold near, IReadOnlyList<MarkedActor> marks)
    {
        lock (_gate)
        {
            if (_read is not { } read || clock() - read >= OwnedInterval)
            {
                _read = clock();
                if (readOwned() is { } owned) _owned = owned;
                if (!_owned.Contains(near.QuestSno)) CheckUnread(near, marks);
            }
            return _unread is { } unread && !_owned.Contains(unread) ? [.. _owned, unread] : _owned;
        }
    }

    private void CheckUnread(Stronghold near, IReadOnlyList<MarkedActor> marks)
    {
        if (readCompleted()?.Contains(near.QuestSno) == true)
        {
            if (_unread == near.QuestSno) log?.Invoke($"PATH stronghold quest {near.QuestSno} completed, no longer counts as running");
            _unread = null;
            return;
        }
        IReadOnlyDictionary<int, uint>? found = null;
        if (_searched is not { } searched || clock() - searched >= PairInterval)
        {
            found = findPhases(new Dictionary<int, IReadOnlySet<uint>> { [near.QuestSno] = phasesOf(near.QuestSno).Select(p => p.PhaseUid).ToHashSet() });
            _searched = clock();
        }
        uint phase = 0;
        bool reads = found is not null && found.TryGetValue(near.QuestSno, out phase);
        if (reads) recordPhase(near.QuestSno, phase);
        bool monsters = marks.Any(m => m.Kind == StrongholdTargeting.MonsterMarkerKind
            && (near.LevelAreas is { Count: > 0 } areas ? areas.Contains(m.LevelAreaSno)
                : Vector2.Distance(new(m.X, m.Y), new(near.Position.X, near.Position.Y)) <= StrongholdTargeting.StrongholdUnits));
        if (_unread == near.QuestSno || !(reads || monsters)) return;
        _unread = near.QuestSno;
        log?.Invoke($"PATH stronghold quest {near.QuestSno} not among the owned: {(reads ? $"its phase reads {phase}" : "monsters marked in it")}, counts as running");
    }
}
