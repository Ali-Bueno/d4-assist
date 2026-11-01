using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// Who the game's map marks inside a level area the current quest step waits for the player to enter, as objectives the guide
/// leads to: such a step names no object. Live 2026-11-01, <c>S15_Triad_A_Labyrinth</c> phase 28: the map marked the Summoner in
/// <c>S15_Triad_A_Cave_04_Boss_QST</c>, a dead-end tile named <c>Cave_Lava_S_Arena_01</c> (no <c>BossRoom</c> in it), which counted
/// as seen from its doorway; the guide said all explored with the player never inside. The game lists such a marker only near it
/// (seen 57 units off, gone 86 off), so what was marked stays known in this world. Only quest characters (kind 8) lead, else the
/// monsters listed now: the area's triggers and portals and the player's own marker do not (phase 160 led to the player's own feet
/// and to the trigger boxes by the arrival portal in Diablo's arena, the same day). A place the player stood at leads no more:
/// that phase stays read after its area is entered.
/// </summary>
/// <param name="log">Debug sink for the places as they change; null when not debugging.</param>
public sealed class AwaitedAreaDestination(Action<string>? log = null)
{
    /// <summary>Id prefix of a marked place's guide destination.</summary>
    public const string IdPrefix = "awaited-area";

    /// <summary>The map's marker kind of a quest character (S15_Summoner_QST, S15_QST_Warrior_NPC_NonCombat).</summary>
    public const int KindQuestCharacter = 8;

    /// <summary>The map's marker kind of the local player, whose spot tells which places were reached.</summary>
    public const int KindLocalPlayer = 1;

    /// <summary>A place this near the player counts as reached.</summary>
    public const float ReachedUnits = 6f;

    // A marked monster that moved this far is a new destination.
    private const float SpotUnits = 8f;

    private readonly object _gate = new();
    private IReadOnlyList<MarkedActor> _monsters = [];
    private readonly Dictionary<string, MarkedActor> _characters = [];
    private readonly HashSet<string> _reached = [];
    private string _logged = "";

    /// <summary>Every actor the game's map marks now (GameSession.MapMarkersRead).</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
        {
            _monsters = [.. markers.Where(m => m.Kind == MapMarkerReader.KindBoss)];
            foreach (var marker in markers.Where(m => m.Kind == KindQuestCharacter)) _characters[Id(marker)] = marker;
            if (markers.FirstOrDefault(m => m.Kind == KindLocalPlayer) is not { } player) return;
            foreach (var place in _characters.Values.Concat(_monsters))
                if (MathF.Sqrt(MathF.Pow(place.X - player.X, 2) + MathF.Pow(place.Y - player.Y, 2)) <= ReachedUnits) _reached.Add(Id(place));
        }
    }

    /// <summary>A new world: its markers are read anew.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            (_monsters, _logged) = ([], "");
            _characters.Clear();
            _reached.Clear();
        }
    }

    /// <summary>The marked places in <paramref name="areas"/>, in quest tier: a step's goal even with tiles left to explore.</summary>
    public IReadOnlyList<GuideDestination> Destinations(IReadOnlySet<int> areas)
    {
        lock (_gate)
        {
            var characters = _characters.Values.Where(m => areas.Contains(m.LevelAreaSno)).ToList();
            var marked = characters.Count > 0 ? characters : _monsters.Where(m => areas.Contains(m.LevelAreaSno));
            List<GuideDestination> places = [.. marked.Where(m => !_reached.Contains(Id(m)))
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
