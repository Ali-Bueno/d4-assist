using System.Numerics;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Scan;

namespace D4Assist.Game;

/// <summary>Where the quests in progress are looked for: the world, the scenes placed in it so far, and the actors loaded around
/// the player (an overworld quest is found by the objects it names, research/client-memory.md §4).</summary>
public sealed record QuestSearchArea(int WorldSno, IReadOnlyCollection<int> Scenes, IReadOnlyList<(int ActorSno, Vector3 Position)> Actors);

/// <summary>What the followed quests' current phases name: markers (world space, or scene-local until the scene is placed),
/// matched by spot, actors named by SNO alone (any placed copy), and blueprint markers, naming a kind until the blueprint is placed.</summary>
public sealed record QuestNames(
    IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers, IReadOnlyList<BlueprintMarker> KindMarkers)
{
    public static readonly QuestNames None = new([], new HashSet<int>(), [], []);

    public static QuestNames Of(IEnumerable<QuestPhaseObjects> phases) =>
        new([.. phases.SelectMany(p => p.Markers)], phases.SelectMany(p => p.ActorSnos).ToHashSet(), [.. phases.SelectMany(p => p.SceneMarkers)],
            [.. phases.SelectMany(p => p.KindMarkers ?? [])]);

    public int Count => Markers.Count + ActorSnos.Count + SceneMarkers.Count + KindMarkers.Count;
}

/// <summary>
/// The quests whose current phase names the objects to cue: the player's tracked quests, plus the quests in progress here,
/// whose {quest, phase} pair the module data holds. A sighted player sees every active quest's objectives on the map, not only
/// the tracked one's (research/client-memory.md §4, quest phases).
/// </summary>
/// <param name="phases">The objects each phase of a quest names (QuestScriptMarkerReader.PhaseObjects).</param>
/// <param name="questsHere">The quests that name something in an area (QuestScriptMarkerReader); null follows tracked quests only.</param>
/// <param name="clock">UTC now; null for the system clock.</param>
/// <param name="worldSno">The world this session reads.</param>
/// <param name="history">The last phase of each quest read in any world; null keeps this session's own.</param>
public sealed class ActiveQuests(
    IMemoryReader memory, IReadOnlyList<MemoryRegion> moduleData, Func<int, IReadOnlyList<QuestPhaseObjects>> phases,
    Func<QuestSearchArea, IReadOnlyList<int>>? questsHere, Func<DateTime>? clock = null, int worldSno = 0, QuestPhaseHistory? history = null)
{
    private readonly QuestPhaseHistory _history = history ?? new();

    /// <summary>A quest's marker and the actor it placed share a spot (live: the Darkened Way gate, 0.0 apart).</summary>
    public const float MarkerReach = 2f;

    // The phase pair can move (a new quest record); a failed search waits this long before the next scan of the module data.
    public static readonly TimeSpan PhaseSearchInterval = TimeSpan.FromSeconds(5);

    private int[] _followed = [];

    /// <summary>The quests followed now: the tracked ones and those in progress here.</summary>
    public IReadOnlyList<int> Followed => _followed;

    /// <summary>The quests whose objects are the player's to use here: those followed now, and any found in progress here before,
    /// while the step they are at (read here, else last read anywhere) is shown in this world or in none. The module data holds one
    /// quest's pair at a time (live 2026-10-17, the Light's Watch: the side quest QST_Frac_PaleKnightsKin and the dungeon's own
    /// quest took turns at one address), so a quest still running drops out of <see cref="Followed"/> while another one's step is
    /// shown, and its third lectern stayed silent. Its journals found, the side quest stays tracked until handed in, and its
    /// lecterns in a new Light's Watch drew the guide away from the guards (the same day).</summary>
    public IReadOnlyList<int> Owned => _owned;

    private readonly HashSet<int> _seenHere = [];
    private int[] _owned = [];
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
        _seenHere.UnionWith(here.Keys);
        var waitedOn = WaitedOn().Where(q => !here.ContainsKey(q) && !tracked.Contains(q)).ToHashSet();
        int[] followed = [.. tracked.Concat(here.Keys).Concat(waitedOn).Distinct().Order()];
        if (followed.SequenceEqual(_followed))
        {
            RefreshOwned();
            return _names;
        }
        _followed = followed;
        _quests = [.. followed.Select(q => _quests.FirstOrDefault(old => old.Sno == q && old.RequirePhase == RequiresPhase(q))
            ?? new FollowedQuest(q, phases(q))
            {
                RequirePhase = RequiresPhase(q), PhaseAddress = here.TryGetValue(q, out long at) ? at : null,
            })];
        UpdatePhases(log: false);
        RefreshOwned();
        _names = QuestNames.Of(_quests.SelectMany(q => q.Current));
        Console.WriteLine($"Quest tracked: {List(tracked)}; in progress here: {List(here.Keys.Except(tracked))}; " +
            $"{(waitedOn.Count > 0 ? $"waited on: {List(waitedOn.Order())}; " : "")}{_names.Count} object(s) named{PhaseText()}.");
        return _names;

        // A quest found here names nothing until its phase reads; a tracked or waited-on one names every phase's objects till then.
        bool RequiresPhase(int quest) => !tracked.Contains(quest) && !waitedOn.Contains(quest);
    }

    // The quests the followed quests' current phases wait on. The game shows such a quest's step instead, but its {quest, phase} pair
    // may be nowhere in the module data: the Orbei Monastery's step waits on DOV_Step_OrbeiMonastery_SermonHallWaves, whose priest
    // the guide never led to (tester, 2026-10-28; research/dungeon-objectives.md §29).
    private IEnumerable<int> WaitedOn() =>
        _quests.Where(q => q.Phase is not null).SelectMany(q => q.Current.SelectMany(p => p.SubQuests ?? Enumerable.Empty<int>()));

    // Logged whenever it changes, with the quests left out for a step shown elsewhere.
    private void RefreshOwned()
    {
        var candidates = _seenHere.Union(_followed).Order().ToList();
        int[] owned = [.. candidates.Where(StepIsHere)];
        if (owned.SequenceEqual(_owned)) return;
        _owned = owned;
        var elsewhere = candidates.Except(owned).ToList();
        Console.WriteLine($"Quest objects owned: {List(owned)}" +
            (elsewhere.Count > 0 ? $"; step elsewhere: {string.Join(", ", elsewhere.Select(q => $"{q} at {_history.Last(q)}"))}" : "") + ".");
    }

    // The quest's step, read here or else last read anywhere, is shown in this world or in none; true while it was never read.
    private bool StepIsHere(int quest)
    {
        if ((_quests.FirstOrDefault(q => q.Sno == quest)?.Phase ?? _history.Last(quest)) is not { } phase) return true;
        var worlds = phases(quest).FirstOrDefault(p => p.PhaseUid == phase)?.Worlds;
        return worlds is null || worlds.Contains(worldSno);
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

        // The current phase. Before the phase is ever found, every phase's actors (a later step can then sound early), but no
        // world-space marker: those of every phase span worlds, and the guide led to one in another world (live 2026-10-16:
        // tracked quest 1931517 named Neyrelle's archive spots in the Light's Watch); once seen, a lost phase names nothing: the quest ended while it
        // stays tracked (live 2026-09-30, ~50 s).
        public IEnumerable<QuestPhaseObjects> Current =>
            Phase is { } current ? Phases.Where(p => p.PhaseUid == current) : PhaseSeen || RequirePhase ? [] : _anyPhase ??= [.. Phases.Select(p => p with { Markers = [] })];

        private IReadOnlyList<QuestPhaseObjects>? _anyPhase;
    }

    // The quests named in the area, or handed over to by a followed quest, whose {quest, phase} pair the module data holds, plus
    // the followed ones whose phase still reads: an overworld quest is found only near its objects (research/dungeon-objectives.md
    // §24). One pass every PhaseSearchInterval (live 2026-10-01: QST_Frac_Underworld2_02 at phase 80 in the Ancient Mausoleum).
    private IReadOnlyDictionary<int, long> InProgressHere(IReadOnlyList<int> tracked, Func<QuestSearchArea> area)
    {
        if (questsHere is null || Now < _nextSearch) return _inProgressHere;
        _nextSearch = Now + PhaseSearchInterval;
        var running = _quests.Where(q => q.Phase is not null && q.PhaseAddress is not null)
            .ToDictionary(q => q.Sno, q => q.PhaseAddress!.Value);
        var named = questsHere(area());
        var candidates = named.Union(SubQuests([.. tracked, .. named, .. running.Keys])).Where(q => !running.ContainsKey(q))
            .ToDictionary(q => q, q => (IReadOnlySet<uint>)phases(q).Select(p => p.PhaseUid).ToHashSet());
        if (candidates.Count == 0) return _inProgressHere = running;
        var found = QuestPhaseLocator.FindAllCandidates(memory, moduleData, candidates);
        foreach (var (quest, pairs) in found) LogCandidates(quest, pairs);
        return _inProgressHere = running.Concat(found.Select(kv => KeyValuePair.Create(kv.Key, QuestPhaseLocator.Pick(kv.Value))))
            .ToDictionary();
    }

    // Which pairs a search found, whenever that changes: a quest holding its pair in several spots (or a stray {quest, 0}) shows
    // which one the phase is read at (live 2026-10-16: the Lost Archives read phase 0 from the entrance on).
    private readonly Dictionary<int, string> _loggedCandidates = [];

    private void LogCandidates(int quest, IReadOnlyList<PhaseCandidate> pairs)
    {
        if (pairs.Count == 0) return;
        long read = QuestPhaseLocator.Pick(pairs);
        string text = string.Join(", ", pairs.Select(p => $"0x{p.Address:X}={p.Phase}{(p.Address == read ? " (read)" : "")}"));
        if (_loggedCandidates.GetValueOrDefault(quest) == text) return;
        _loggedCandidates[quest] = text;
        Console.WriteLine($"Quest phase pairs: {quest}: {text}.");
    }

    // The quests some phase of these quests waits on, and theirs: while one runs the game shows its phase in place of the
    // tracked quest's (live 2026-10-04: S15_QST_Main_03_TriadC_01 phase 81 → S15_Triad_C_Travincal). Any phase, since the
    // tracked quest's own phase is no longer readable then.
    private IEnumerable<int> SubQuests(IReadOnlyList<int> quests)
    {
        var seen = quests.ToHashSet();
        var pending = new Queue<int>(quests);
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
            if (phase is null && Now >= quest.NextSearch)
            {
                var pairs = QuestPhaseLocator.FindCandidates(memory, moduleData, quest.Sno, quest.Uids);
                LogCandidates(quest.Sno, pairs);
                quest.PhaseAddress = pairs.Count > 0 ? QuestPhaseLocator.Pick(pairs) : null;
                quest.NextSearch = Now + PhaseSearchInterval;
                phase = quest.PhaseAddress is { } a ? QuestPhaseLocator.Read(memory, a, quest.Sno, quest.Uids) : null;
            }
            if (phase is { } read) _history.Record(quest.Sno, read);
            if (phase == quest.Phase) continue;
            if (quest.Phase is not null) PhaseChanges++; // moved on from a known phase, not just found
            quest.Phase = phase;
            quest.PhaseSeen |= phase is not null;
            changed = true;
            if (log) Console.WriteLine($"Quest phase: {quest.Sno} {(phase is { } v ? $"at {v}" : "unknown")}, {QuestNames.Of(quest.Current).Count} object(s) named.");
        }
        return changed;
    }

    private DateTime Now => clock?.Invoke() ?? DateTime.UtcNow;

    private static string List(IEnumerable<int> quests) => quests.Any() ? string.Join(", ", quests) : "none";

    private string PhaseText() =>
        string.Concat(_quests.Select(q => q.Phase is { } v ? $", {q.Sno} at phase {v}" : $", {q.Sno} phase unknown"));
}
