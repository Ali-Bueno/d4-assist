using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// The place a quest step that counts waves of kills is fought at: such a step names no place, so the last quest place the player
/// stood at leads and holds the guide there, also after a death (research/dungeon-objectives.md §33). It gives way when nothing
/// comes: the kills then lie elsewhere and the guide explores.
/// </summary>
/// <param name="log">Debug sink for the hold's changes; null when not debugging.</param>
/// <param name="clock">Time source; tests pass their own.</param>
public sealed class WaveStepDestination(Action<string>? log = null, Func<TimeSpan>? clock = null)
{
    /// <summary>Id prefix of the place's guide destination.</summary>
    public const string IdPrefix = "wave-step";

    /// <summary>How close counts as at the place: a fight moves the player around it.</summary>
    public const float HoldUnits = 12f;

    /// <summary>A marked monster this near the place belongs to its fight, and the player this near waits for the next wave.</summary>
    public const float FightUnits = 30f;

    /// <summary>The player waited this long at the place with no monster marked: the Condemned Mines' waves come seconds apart.</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(20);

    private readonly object _gate = new();
    private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    private IReadOnlyList<MarkedActor> _monsters = [];
    private MarkedActor? _player;
    private (float X, float Y, float? Z)? _spot;
    private TimeSpan? _quietSince;
    private bool _gaveWay;

    private TimeSpan Now => clock?.Invoke() ?? _stopwatch.Elapsed;

    /// <summary>Every actor the game's map marks now (GameSession.MapMarkersRead).</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
        {
            _monsters = [.. markers.Where(m => m.Kind == MapMarkerReader.KindBoss)];
            _player = markers.FirstOrDefault(m => m.Kind == AwaitedAreaDestination.KindLocalPlayer);
        }
    }

    /// <summary>A new world: its step starts anew.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            (_monsters, _player) = ([], null);
            EndStep();
        }
    }

    /// <summary>The place to hold, in quest tier, while the current step counts waves.</summary>
    /// <param name="countsWaves">ActiveQuests.StepCountsWaves.</param>
    /// <param name="lastQuestSpot">ObjectiveMemory.LastQuestSpot, taken as the step starts.</param>
    public IReadOnlyList<GuideDestination> Destinations(bool countsWaves, (float X, float Y, float? Z)? lastQuestSpot)
    {
        lock (_gate)
        {
            if (!countsWaves)
            {
                EndStep();
                return [];
            }
            if (_spot is null && lastQuestSpot is { } start)
            {
                _spot = start;
                log?.Invoke($"WAVES held at ({start.X:F1},{start.Y:F1})");
            }
            if (_spot is not { } spot) return [];
            Watch(spot);
            return _gaveWay ? [] : [new GuideDestination($"{IdPrefix}:{spot.X:F0},{spot.Y:F0}", (spot.X, spot.Y), ObjectiveMemory.QuestTier, spot.Z, HoldUnits, Located: true)];
        }
    }

    // Only the time spent there counts: a player sent back to a checkpoint is led to the place, however long the walk.
    private void Watch((float X, float Y, float? Z) spot)
    {
        if (_monsters.Any(m => Apart(m, spot) <= FightUnits))
        {
            if (_gaveWay) log?.Invoke("WAVES fight back at the place");
            (_quietSince, _gaveWay) = (null, false);
        }
        else if (_player is not { } player || Apart(player, spot) > FightUnits) _quietSince = null;
        else if (!_gaveWay && (_quietSince ??= Now) is var since && Now - since >= QuietTime)
        {
            _gaveWay = true;
            log?.Invoke("WAVES none came: the guide goes on");
        }
    }

    private void EndStep() => (_spot, _quietSince, _gaveWay) = (null, null, false);

    private static float Apart(MarkedActor actor, (float X, float Y, float? Z) spot) =>
        MathF.Sqrt((actor.X - spot.X) * (actor.X - spot.X) + (actor.Y - spot.Y) * (actor.Y - spot.Y));
}
