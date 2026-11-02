using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// Who the game's map marks inside a level area the current quest step waits for the player to enter, as objectives the guide
/// leads to: such a step names no object. Live 2026-11-01, <c>S15_Triad_A_Labyrinth</c> phase 28: the map marked the Summoner in
/// <c>S15_Triad_A_Cave_04_Boss_QST</c>, a dead-end tile named <c>Cave_Lava_S_Arena_01</c> (no <c>BossRoom</c> in it), which counted
/// as seen from its doorway; the guide said all explored with the player never inside. The game lists such a marker only near it
/// (seen 57 units off, gone 86 off), so what was marked stays known in this world. Quest characters (kind 8) and the area's triggers
/// lead, else the monsters listed now; portals and the player's own marker do not. A place the player reached leads no more: a phase
/// can stay read after its area is entered. Phase 160 in Diablo's arena does, until the hidden quest <c>S15_TriadA_Boss_Intro</c>'s
/// four trigger boxes and sphere, marked by the map on a line from the arrival portal, signal <c>S15_TriadA_Intro_Complete</c>; the
/// straight route to the character missed them, and the fight never began (the same day).
/// </summary>
/// <param name="nameOf">An actor's name, to tell a trigger.</param>
/// <param name="log">Debug sink for the places as they change; null when not debugging.</param>
public sealed class AwaitedAreaDestination(Func<int, string?> nameOf, Action<string>? log = null)
{
    /// <summary>Id prefix of a marked place's guide destination.</summary>
    public const string IdPrefix = "awaited-area";

    /// <summary>The map's marker kind of a quest character (S15_Summoner_QST, S15_QST_Warrior_NPC_NonCombat).</summary>
    public const int KindQuestCharacter = 8;

    /// <summary>The map's marker kind of the local player, whose spot tells which places were reached.</summary>
    public const int KindLocalPlayer = 1;

    /// <summary>A character or monster this near the player counts as reached.</summary>
    public const float ReachedUnits = 6f;

    /// <summary>A trigger counts as stepped on this near: a 2-unit box and the player's own size. The player has to walk into it.</summary>
    public const float TriggerUnits = 1.5f;

    private const string TriggerPrefix = "Trigger_";

    // A marked monster that moved this far is a new destination.
    private const float SpotUnits = 8f;

    private readonly object _gate = new();
    private IReadOnlyList<MarkedActor> _monsters = [];
    private readonly Dictionary<string, MarkedActor> _characters = [];
    private readonly Dictionary<string, MarkedActor> _triggers = [];
    private (float X, float Y)? _lastPlayer;
    private readonly HashSet<string> _reached = [];
    private string _logged = "";

    /// <summary>Every actor the game's map marks now (GameSession.MapMarkersRead).</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
        {
            _monsters = [.. markers.Where(m => m.Kind == MapMarkerReader.KindBoss)];
            foreach (var marker in markers.Where(m => m.Kind == KindQuestCharacter)) _characters[Id(marker)] = marker;
            foreach (var marker in markers.Where(IsTrigger)) _triggers[Id(marker)] = marker;
            if (markers.FirstOrDefault(m => m.Kind == KindLocalPlayer) is not { } player) return;
            // Markers are read about every 1.5 s, in which a running player crosses a trigger: what counts is the way walked since.
            var from = _lastPlayer ?? (player.X, player.Y);
            _lastPlayer = (player.X, player.Y);
            foreach (var place in _characters.Values.Concat(_monsters))
                if (DistanceToWalk(place, from, player) <= ReachedUnits) _reached.Add(Id(place));
            foreach (var place in _triggers.Values)
                if (DistanceToWalk(place, from, player) <= TriggerUnits) _reached.Add(Id(place));
        }
    }

    /// <summary>A new world: its markers are read anew.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            (_monsters, _logged) = ([], "");
            _characters.Clear();
            _triggers.Clear();
            _lastPlayer = null;
            _reached.Clear();
        }
    }

    /// <summary>The marked places in <paramref name="areas"/>, in quest tier: a step's goal even with tiles left to explore.</summary>
    public IReadOnlyList<GuideDestination> Destinations(IReadOnlySet<int> areas)
    {
        lock (_gate)
        {
            var known = _characters.Values.Concat(_triggers.Values).Where(m => areas.Contains(m.LevelAreaSno)).ToList();
            var marked = known.Count > 0 ? known : _monsters.Where(m => areas.Contains(m.LevelAreaSno));
            List<GuideDestination> places = [.. marked.Where(m => !_reached.Contains(Id(m)))
                .Select(m => new GuideDestination(Id(m), (m.X, m.Y), ObjectiveMemory.QuestTier, m.Z, IsTrigger(m) ? TriggerUnits : 0, Located: true))];
            string text = string.Join(", ", places.Select(p => p.Id));
            if (text != _logged)
            {
                _logged = text;
                log?.Invoke($"AWAITED AREA places: {(places.Count > 0 ? text : "none")}");
            }
            return places;
        }
    }

    private bool IsTrigger(MarkedActor m) =>
        m.Kind == MapMarkerReader.KindGizmo && nameOf(m.ActorSno)?.StartsWith(TriggerPrefix, StringComparison.Ordinal) == true;

    // Triggers can stand closer together than a monster's spot grid: theirs is the exact spot.
    private string Id(MarkedActor m) => IsTrigger(m)
        ? $"{IdPrefix}:{m.ActorSno}:{m.X:F1},{m.Y:F1}"
        : $"{IdPrefix}:{m.ActorSno}:{MathF.Round(m.X / SpotUnits)},{MathF.Round(m.Y / SpotUnits)}";

    private static float DistanceToWalk(MarkedActor place, (float X, float Y) from, MarkedActor to)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        float lengthSquared = dx * dx + dy * dy;
        float along = lengthSquared == 0 ? 0 : Math.Clamp(((place.X - from.X) * dx + (place.Y - from.Y) * dy) / lengthSquared, 0, 1);
        return MathF.Sqrt(MathF.Pow(place.X - (from.X + along * dx), 2) + MathF.Pow(place.Y - (from.Y + along * dy), 2));
    }
}
