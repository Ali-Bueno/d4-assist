using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

public sealed class StrongholdRunningTests
{
    private const int Quest = 500, Other = 7, Area = 90, OutsideArea = 91;
    private static readonly Stronghold KorDragan = new(Quest, "Kor Dragan", 69068, new Vector3(0, 0, 0), new HashSet<int> { Area });
    private static readonly MarkedActor Monster = new(1, 10, 0, 0, Area, StrongholdTargeting.MonsterMarkerKind);

    private TimeSpan _now = TimeSpan.Zero;
    private IReadOnlyList<int>? _owned = [Other];
    private IReadOnlySet<int>? _completed = new HashSet<int>();
    private IReadOnlyDictionary<int, uint>? _pairs;
    private int _searches;
    private readonly Dictionary<int, uint> _recorded = [];

    private StrongholdRunning New() => new(
        () => _owned, () => _completed, _ => { _searches++; return _pairs; },
        _ => [new QuestPhaseObjects(11, [], new HashSet<int>(), [])], (q, p) => _recorded[q] = p, () => _now);

    [Fact]
    public void A_stronghold_quest_the_owned_read_misses_runs_while_the_map_marks_a_monster_in_it()
    {
        var running = New();
        Assert.Equal([Other], running.InProgress(KorDragan, []));

        _now += StrongholdRunning.OwnedInterval;
        Assert.Equal([Other, Quest], running.InProgress(KorDragan, [Monster]));
    }

    [Fact]
    public void A_monster_outside_its_level_areas_does_not_count()
    {
        Assert.Equal([Other], New().InProgress(KorDragan, [Monster with { LevelAreaSno = OutsideArea }]));
    }

    [Fact]
    public void Its_phase_pair_reads_it_as_running_and_records_the_phase()
    {
        _pairs = new Dictionary<int, uint> { [Quest] = 11 };
        Assert.Equal([Other, Quest], New().InProgress(KorDragan, []));
        Assert.Equal(11u, _recorded[Quest]);
    }

    [Fact]
    public void A_completed_stronghold_quest_never_runs_and_stops_running_once_completed()
    {
        _completed = new HashSet<int> { Quest };
        var freed = New();
        Assert.Equal([Other], freed.InProgress(KorDragan, [Monster]));

        _completed = new HashSet<int>();
        var running = New();
        Assert.Equal([Other, Quest], running.InProgress(KorDragan, [Monster]));
        _completed = new HashSet<int> { Quest };
        _now += StrongholdRunning.OwnedInterval;
        Assert.Equal([Other], running.InProgress(KorDragan, [Monster]));
    }

    [Fact]
    public void Leaving_the_stronghold_stops_counting_it()
    {
        var running = New();
        running.InProgress(KorDragan, [Monster]);
        running.Left();
        Assert.Equal([Other], running.InProgress(KorDragan, []));
    }

    [Fact]
    public void Reads_are_rate_limited()
    {
        var running = New();
        running.InProgress(KorDragan, []);
        _now += StrongholdRunning.OwnedInterval;
        running.InProgress(KorDragan, []);
        Assert.Equal(1, _searches); // the pair search waits PairInterval

        _now += StrongholdRunning.PairInterval;
        running.InProgress(KorDragan, []);
        Assert.Equal(2, _searches);
    }
}
