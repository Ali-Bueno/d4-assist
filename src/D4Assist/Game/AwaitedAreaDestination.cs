using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// What the game's map marks inside a level area the current quest step waits for the player to enter, as objectives the guide
/// leads to: such a step names no object. Live 2026-11-01, <c>S15_Triad_A_Labyrinth</c> phase 28: the map marked the Summoner in
/// <c>S15_Triad_A_Cave_04_Boss_QST</c>, a dead-end tile named <c>Cave_Lava_S_Arena_01</c> (no <c>BossRoom</c> in it), which counted
/// as seen from its doorway; the guide said all explored with the player never inside. The game lists such a marker only near it
/// (seen 57 units off, gone 86 off), so what was marked stays known in this world; a monster's marker counts only while listed.
/// </summary>
/// <param name="log">Debug sink for the places as they change; null when not debugging.</param>
public sealed class AwaitedAreaDestination(Action<string>? log = null)
{
    /// <summary>Id prefix of a marked place's guide destination.</summary>
    public const string IdPrefix = "awaited-area";

    // The player's own and their pets' markers carry the level area they stand in.
    private const int KindPlayer = 2;

    // A marked monster that moved this far is a new destination.
    private const float SpotUnits = 8f;

    private readonly object _gate = new();
    private IReadOnlyList<MarkedActor> _markers = [];
    private readonly Dictionary<string, MarkedActor> _remembered = [];
    private string _logged = "";

    /// <summary>Every actor the game's map marks now (GameSession.MapMarkersRead).</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
        {
            _markers = markers;
            foreach (var marker in markers.Where(m => m.Kind is not (KindPlayer or MapMarkerReader.KindBoss))) _remembered[Id(marker)] = marker;
        }
    }

    /// <summary>A new world: its markers are read anew.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            (_markers, _logged) = ([], "");
            _remembered.Clear();
        }
    }

    /// <summary>The marked places in <paramref name="areas"/>, in quest tier: a step's goal even with tiles left to explore.</summary>
    public IReadOnlyList<GuideDestination> Destinations(IReadOnlySet<int> areas)
    {
        lock (_gate)
        {
            List<GuideDestination> places = areas.Count == 0 ? [] : [.. _markers.Where(m => m.Kind != KindPlayer).Concat(_remembered.Values)
                .Where(m => areas.Contains(m.LevelAreaSno)).DistinctBy(Id)
                .Select(m => new GuideDestination(Id(m), (m.X, m.Y), ObjectiveMemory.QuestTier, m.Z, Located: true))];
            string text = string.Join(", ", places.Select(p => p.Id));
            if (text != _logged)
            {
                _logged = text;
                log?.Invoke($"AWAITED AREA places: {(places.Count > 0 ? text : "none")}");
            }
            return places;
        }
    }

    private static string Id(MarkedActor m) => $"{IdPrefix}:{m.ActorSno}:{MathF.Round(m.X / SpotUnits)},{MathF.Round(m.Y / SpotUnits)}";
}
