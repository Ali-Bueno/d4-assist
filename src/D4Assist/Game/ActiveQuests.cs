using System.Numerics;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Scan;

namespace D4Assist.Game;

/// <summary>Where the quests in progress are looked for: the world, the scenes placed in it so far, and the actors loaded around
/// the player (an overworld quest is found by the objects it names, research/client-memory.md §4).</summary>
public sealed record QuestSearchArea(int WorldSno, IReadOnlyCollection<int> Scenes, IReadOnlyList<(int ActorSno, Vector3 Position)> Actors);

/// <summary>What the followed quests' current phases name: markers (world space, or scene-local until the scene is placed),
/// matched by spot, and actors named by SNO alone (any placed copy).</summary>
public sealed record QuestNames(IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers)
{
    public static readonly QuestNames None = new([], new HashSet<int>(), []);

    public static QuestNames Of(IEnumerable<QuestPhaseObjects> phases) =>
        new([.. phases.SelectMany(p => p.Markers)], phases.SelectMany(p => p.ActorSnos).ToHashSet(), [.. phases.SelectMany(p => p.SceneMarkers)]);

    public int Count => Markers.Count + ActorSnos.Count + SceneMarkers.Count;
}

/// <summary>
/// The quests whose current phase names the objects to cue: the player's tracked quests, plus the quests in progress here,
/// whose {quest, phase} pair the module data holds. A sighted player sees every active quest's objectives on the map, not only
/// the tracked one's (research/client-memory.md §4, quest phases).
/// </summary>
/// <param name="phases">The objects each phase of a quest names (QuestScriptMarkerReader.PhaseObjects).</param>
/// <param name="questsHere">The quests that name something in an area (QuestScriptMarkerReader); null follows tracked quests only.</param>
public sealed class ActiveQuests(
    IMemoryReader memory, IReadOnlyList<MemoryRegion> moduleData, Func<int, IReadOnlyList<QuestPhaseObjects>> phases,
    Func<QuestSearchArea, IReadOnlyList<int>>? questsHere)
{
    /// <summary>A quest's marker and the actor it placed share a spot (live: the Darkened Way gate, 0.0 apart).</summary>
    public const float MarkerReach = 2f;

    // The phase pair can move (a new quest record); a failed search waits this long before the next scan of the module data.
    private static readonly TimeSpan PhaseSearchInterval = TimeSpan.FromSeconds(5);

    private int[] _followed = [];
    private List<FollowedQuest> _quests = [];

    /// <summary>How often a followed quest has moved on from a known phase; read from other threads.</summary>
    public int PhaseChanges { get; private set; }
    private QuestNames _names = QuestNames.None;
    private IReadOnlyDictionary<int, long> _inProgressHere = new Dictionary<int, long>();
    private DateTime _nextSearch = DateTime.MinValue;

    /// <summary>Reads the current phases and returns what they name.</summary>
    /// <param name="tracked">The quests the player tracks (player ACD); null when the player's record wasn't read this poll.</param>
    /// <param name="area">Where to look for quests in progress; built at most once every <see cref="PhaseSearchInterval"/>.</param>
    public QuestNames Update(IReadOnlyList<int>? tracked, Func<QuestSearchArea> area)
    {
        if (UpdatePhases(log: true)) _names = QuestNames.Of(_quests.SelectMany(q => q.Current));
        if (tracked is null) return _names;
        var here = InProgressHere(tracked, area);
        int[] followed = [.. tracked.Concat(here.Keys).Distinct().Order()];
        if (followed.SequenceEqual(_followed)) return _names;
        _followed = followed;
        _quests = [.. followed.Select(q => _quests.FirstOrDefault(old => old.Sno == q && old.RequirePhase != tracked.Contains(q))
            ?? new FollowedQuest(q, phases(q))
            {
                RequirePhase = !tracked.Contains(q), PhaseAddress = here.TryGetValue(q, out long at) ? at : null,
            })];
        UpdatePhases(log: false);
        _names = QuestNames.Of(_quests.SelectMany(q => q.Current));
        Console.WriteLine($"Quest tracked: {List(tracked)}; in progress here: {List(here.Keys.Except(tracked))}; " +
            $"{_names.Count} object(s) named{PhaseText()}.");
        return _names;
    }

    // A quest's phases (offline) and where its current phase is read (found in the module's data).
    private sealed class FollowedQuest(int sno, IReadOnlyList<QuestPhaseObjects> phases)
    {
        public int Sno { get; } = sno;
        public IReadOnlyList<QuestPhaseObjects> Phases { get; } = phases;
        public HashSet<uint> Uids { get; } = [.. phases.Select(p => p.PhaseUid)];
        public long? PhaseAddress { get; set; }
        public uint? Phase { get; set; }
        public bool PhaseSeen { get; set; }
        // Found in progress rather than tracked: names nothing until its phase is read.
        public bool RequirePhase { get; init; }
        public DateTime NextSearch { get; set; } = DateTime.MinValue;

        // The current phase. Before the phase is ever found, every phase (a later step can then sound early);
        // once seen, a lost phase names nothing: the quest ended while it stays tracked (live 2026-09-30, ~50 s).
        public IEnumerable<QuestPhaseObjects> Current =>
            Phase is { } current ? Phases.Where(p => p.PhaseUid == current) : PhaseSeen || RequirePhase ? [] : Phases;
    }

    // The quests named in the area, or handed over to by a tracked quest, whose {quest, phase} pair the module data holds; one pass
    // every PhaseSearchInterval (live 2026-10-01: QST_Frac_Underworld2_02 at phase 80 in the Ancient Mausoleum, none tracked).
    private IReadOnlyDictionary<int, long> InProgressHere(IReadOnlyList<int> tracked, Func<QuestSearchArea> area)
    {
        if (questsHere is null || DateTime.UtcNow < _nextSearch) return _inProgressHere;
        _nextSearch = DateTime.UtcNow + PhaseSearchInterval;
        var candidates = questsHere(area()).Union(SubQuests(tracked))
            .ToDictionary(q => q, q => (IReadOnlySet<uint>)phases(q).Select(p => p.PhaseUid).ToHashSet());
        return _inProgressHere = candidates.Count == 0
            ? new Dictionary<int, long>()
            : QuestPhaseLocator.FindAll(memory, moduleData, candidates);
    }

    // The quests some phase of a tracked quest waits on, and theirs: while one runs the game shows its phase in place of the
    // tracked quest's (live 2026-10-04: S15_QST_Main_03_TriadC_01 phase 81 → S15_Triad_C_Travincal). Any phase, since the
    // tracked quest's own phase is no longer readable then.
    private IEnumerable<int> SubQuests(IReadOnlyList<int> tracked)
    {
        var seen = tracked.ToHashSet();
        var pending = new Queue<int>(tracked);
        while (pending.TryDequeue(out int quest))
            foreach (int sub in phases(quest).SelectMany(p => p.SubQuests ?? Enumerable.Empty<int>()))
                if (seen.Add(sub))
                {
                    pending.Enqueue(sub);
                    yield return sub;
                }
    }

    // True when a quest's phase changed.
    private bool UpdatePhases(bool log)
    {
        bool changed = false;
        foreach (var quest in _quests)
        {
            uint? phase = quest.PhaseAddress is { } at ? QuestPhaseLocator.Read(memory, at, quest.Sno, quest.Uids) : null;
            if (phase is null && DateTime.UtcNow >= quest.NextSearch)
            {
                quest.PhaseAddress = QuestPhaseLocator.Find(memory, moduleData, quest.Sno, quest.Uids) is [var found, ..] ? found : null;
                quest.NextSearch = DateTime.UtcNow + PhaseSearchInterval;
                phase = quest.PhaseAddress is { } a ? QuestPhaseLocator.Read(memory, a, quest.Sno, quest.Uids) : null;
            }
            if (phase == quest.Phase) continue;
            if (quest.Phase is not null) PhaseChanges++; // moved on from a known phase, not just found
            quest.Phase = phase;
            quest.PhaseSeen |= phase is not null;
            changed = true;
            if (log) Console.WriteLine($"Quest phase: {quest.Sno} {(phase is { } v ? $"at {v}" : "unknown")}, {QuestNames.Of(quest.Current).Count} object(s) named.");
        }
        return changed;
    }

    private static string List(IEnumerable<int> quests) => quests.Any() ? string.Join(", ", quests) : "none";

    private string PhaseText() =>
        string.Concat(_quests.Select(q => q.Phase is { } v ? $", {q.Sno} at phase {v}" : $", {q.Sno} phase unknown"));
}
