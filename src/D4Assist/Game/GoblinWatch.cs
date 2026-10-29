using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Which treasure goblins the map shows a sighted player, for the Shift+F3 list (user request 2026-10-29: only those). The game
/// data says how the map shows each goblin (ActorUIData, <see cref="MinimapInfo"/>): most respect the fog of war and the reveal
/// radius around the player, so they show only near him; the Undercity's and an expansion event's are active by default and show
/// from anywhere within their range (2048 units). The client's map-marker list holds what the map can show, streamed in or not
/// (research/dungeon-objectives.md §19), so a reveal-radius goblin counts as shown only while its actor is streamed in around the
/// player as well. Debug GOBLIN lines say each change (marker, flags, streamed, shown) to check that rule against the map.
/// </summary>
/// <param name="definitionOf">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">An SNO's name, for the monster family and the log.</param>
public sealed class GoblinWatch(Func<int, ActorDefinitionInfo?> definitionOf, Func<int, string?> nameOf, Action<string>? log = null)
{
    /// <summary>A streamed goblin this close to a marker of its kind is that marker's (goblins run; a poll apart they moved).</summary>
    public const float SameGoblinUnits = 12f;

    private readonly object _gate = new();
    private readonly Dictionary<int, bool> _isGoblin = [];
    private IReadOnlyList<ActorState> _streamed = [];
    private PlayerState? _player;
    private string _logged = "";

    public void OnPlayerMoved(PlayerState player)
    {
        lock (_gate) _player = player;
    }

    public void OnActors(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
            _streamed = [.. actors.Where(a => !a.ClientOnly && IsGoblinLocked(a.ActorSno))];
    }

    /// <summary>Logs the goblins the map marks and the ones streamed in, whenever that changes.</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        if (log is null) return;
        string line;
        lock (_gate)
        {
            var marked = markers.Where(m => IsGoblinLocked(m.ActorSno)).ToList();
            if (marked.Count == 0 && _streamed.Count == 0)
            {
                if (_logged.Length > 0) log("GOBLIN none");
                _logged = "";
                return;
            }
            line = string.Join("; ", marked.Select(m =>
                $"{nameOf(m.ActorSno) ?? m.ActorSno.ToString()} marker kind {m.Kind} flags 0x{m.Flags:X4} {Distance(m.X, m.Y):F0} away " +
                $"streamed {(Streamed(m) ? "yes" : "no")} shown {(ShownLocked(m) ? "yes" : "no")}")
                .Concat(_streamed.Where(a => !marked.Any(m => m.ActorSno == a.ActorSno && Near(a, m)))
                    .Select(a => $"{nameOf(a.ActorSno) ?? a.ActorSno.ToString()} streamed {Distance(a.X, a.Y):F0} away, no marker")));
            // Distances change every poll: log the whole picture only when something but a distance changed.
            string key = System.Text.RegularExpressions.Regex.Replace(line, @" \d+ away", "");
            if (key == _logged) return;
            _logged = key;
        }
        log($"GOBLIN {line}");
    }

    /// <summary>A treasure goblin the map shows the player now.</summary>
    public bool IsShown(MarkedActor marker)
    {
        lock (_gate) return IsGoblinLocked(marker.ActorSno) && ShownLocked(marker);
    }

    public bool IsGoblin(int actorSno)
    {
        lock (_gate) return IsGoblinLocked(actorSno);
    }

    private bool ShownLocked(MarkedActor marker) =>
        definitionOf(marker.ActorSno)?.Minimap is { ActiveByDefault: true } map
            ? map.MaxVisRange <= 0 || Distance(marker.X, marker.Y) <= map.MaxVisRange
            : Streamed(marker);

    private bool Streamed(MarkedActor marker) => _streamed.Any(a => a.ActorSno == marker.ActorSno && Near(a, marker));

    private static bool Near(ActorState actor, MarkedActor marker) =>
        MathF.Sqrt(MathF.Pow(actor.X - marker.X, 2) + MathF.Pow(actor.Y - marker.Y, 2)) <= SameGoblinUnits;

    private float Distance(float x, float y) =>
        _player is { } p ? MathF.Sqrt(MathF.Pow(p.X - x, 2) + MathF.Pow(p.Y - y, 2)) : float.NaN;

    private bool IsGoblinLocked(int actorSno)
    {
        if (_isGoblin.TryGetValue(actorSno, out bool known)) return known;
        return _isGoblin[actorSno] = definitionOf(actorSno) is { } info && TreasureGoblins.Is(info, nameOf);
    }
}
