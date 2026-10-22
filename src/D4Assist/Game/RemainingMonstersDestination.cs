using D4Assist.Navigation;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// The monsters the game's map still marks (kind 10), as destinations the guide leads to once nothing is left to explore, in a
/// dungeon whose objective is to kill every monster: its objective quest names no actor, only a kill count. Live 2026-10-22,
/// the seasonal Den of Evil (<c>S15_Rift_DenOfEvil_DPO</c>, "Tötet alle Monster in der Höhle."): every tile explored, the
/// quest not done, and the guide led to the exit; the map marked monsters all over the cave. Not in endgame worlds (GuideSwitch).
/// </summary>
public sealed class RemainingMonstersDestination
{
    /// <summary>Id prefix of a marked monster's guide destination.</summary>
    public const string IdPrefix = "monster";

    /// <summary>Before the boss room and the portals (tier 0): the dungeon isn't done while monsters are left.</summary>
    public const int Tier = -1;

    // A monster that moved this far is a new destination: one the player reached and lost leads again.
    private const float SpotUnits = 8f;

    private readonly Func<string, bool> _killsAll;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private IReadOnlyList<GuideDestination> _monsters = [];
    private bool _active;
    private int _loggedCount = -1;

    /// <param name="killsAll">True for a world whose objective quest asks to kill every monster (it names no actor).</param>
    /// <param name="log">Debug sink for the count of marked monsters as it changes; null when not debugging.</param>
    public RemainingMonstersDestination(IDungeonFeed feed, Func<string, bool> killsAll, Action<string>? log = null)
    {
        _killsAll = killsAll;
        _log = log;
        if (feed.CurrentWorld is { } world) SetWorld(world.Name, world.IsDungeon);
        feed.WorldChanged += (_, name, isDungeon) => SetWorld(name, isDungeon);
    }

    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate) return _monsters;
    }

    /// <summary>Every actor the game's map marks now (GameSession.MapMarkersRead).</summary>
    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
        {
            if (!_active) return;
            _monsters = [.. markers.Where(m => m.Kind == MapMarkerReader.KindBoss).Select(m => new GuideDestination(
                $"{IdPrefix}:{m.ActorSno}:{MathF.Round(m.X / SpotUnits)},{MathF.Round(m.Y / SpotUnits)}", (m.X, m.Y), Tier, m.Z))];
            if (_monsters.Count == _loggedCount) return;
            _loggedCount = _monsters.Count;
            _log?.Invoke($"MONSTERS marked on the map: {_monsters.Count}" +
                (_monsters.Count == 0 ? "" : $" ({string.Join(", ", _monsters.Select(m => $"({m.Position.X:F0},{m.Position.Y:F0})"))})"));
        }
    }

    private void SetWorld(string name, bool isDungeon)
    {
        bool active = isDungeon && !GuideSwitch.IsEndgameWorld(name) && _killsAll(name);
        lock (_gate)
        {
            (_active, _monsters, _loggedCount) = (active, [], -1);
        }
        if (active) _log?.Invoke($"MONSTERS: {name} asks to kill every monster; the guide leads to the ones the map marks once all is explored");
    }
}
