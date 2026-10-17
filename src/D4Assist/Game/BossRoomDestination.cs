using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// The boss room of the dungeon, once its tile loads, as a destination the guide leads to when nothing is left to explore: a
/// sighted player heads there next. A tile named <c>…BossRoom…</c> (84 scenes in 55 tile sets, build 3.2.3); its own
/// <c>Trigger_BossRoom_Entrance</c> (37 of them place one) is the spot, else the tile's middle. Live 2026-10-17, the Light's Watch:
/// past the gate the quest waits on DSH_Standard and names nothing, a doorway peek counted the boss room as explored, and the
/// guide fell silent with the room unentered. Not in endgame worlds, whose guide keeps its tuned order (GuideSwitch).
/// </summary>
public sealed class BossRoomDestination
{
    /// <summary>Id prefix of a boss room's guide destination.</summary>
    public const string IdPrefix = "boss-room";

    /// <summary>Its tier among the portals (PortalMemory.Destinations): with the unused ones, before the way back. Once reached it
    /// holds the tier, so the guide never turns back to the arrival portal from the boss fight.</summary>
    public const int Tier = 0;

    /// <summary>The trigger a boss-room tile places at its door (research/dungeon-objectives.md: 243360, radius 4).</summary>
    public const string EntranceTriggerName = "Trigger_BossRoom_Entrance";

    private const string NameMark = "BossRoom";

    private readonly Func<int, IReadOnlyList<TileActor>> _entrances;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly Dictionary<(int Scene, int X, int Y), GuideDestination> _rooms = [];
    private bool _endgame;

    /// <param name="entrances">The boss-room entrance triggers a scene places, in its own axes (TileActorReader.In).</param>
    /// <param name="log">Debug sink for each boss room found; null when not debugging.</param>
    public BossRoomDestination(IDungeonFeed feed, Func<int, IReadOnlyList<TileActor>> entrances, Action<string>? log = null)
    {
        _entrances = entrances;
        _log = log;
        if (feed.CurrentWorld is { } world) _endgame = GuideSwitch.IsEndgameWorld(world.Name);
        feed.WorldChanged += (_, name, _) => { lock (_gate) { _rooms.Clear(); _endgame = GuideSwitch.IsEndgameWorld(name); } };
        feed.TilesUpdated += OnTiles;
    }

    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate) return [.. _rooms.Values];
    }

    public static bool IsBossRoom(string? tileName) =>
        tileName is not null && tileName.Contains(NameMark, StringComparison.OrdinalIgnoreCase)
        && !tileName.EndsWith(ExplorationGuideBridge.UndercityBossRoomSuffix, StringComparison.Ordinal);

    private void OnTiles(IReadOnlyList<TileState> tiles)
    {
        List<TileState> fresh;
        lock (_gate)
        {
            if (_endgame) return;
            fresh = [.. tiles.Where(t => IsBossRoom(t.Name) && !_rooms.ContainsKey(Key(t)))];
        }
        // The CASC read runs outside the gate, as PortalMemory's.
        var found = fresh.Select(t => (Key: Key(t), Tile: t, Spot: Spot(t))).ToList();
        lock (_gate)
            foreach (var (key, tile, (position, entrance)) in found)
            {
                if (!_rooms.TryAdd(key, new GuideDestination($"{IdPrefix}:{tile.SceneSno}:{key.X},{key.Y}", position, Tier))) continue;
                _log?.Invoke($"BOSS ROOM {tile.Name} at ({position.X:F1},{position.Y:F1}){(entrance ? ", its entrance" : ", its middle")}");
            }
    }

    // The entrance trigger only in an unrotated tile, as PortalMemory places portals: every DRLG tile seen so far is unrotated.
    private ((float X, float Y) Position, bool Entrance) Spot(TileState tile)
    {
        if (TileRotation.QuarterTurns(tile.YawDegrees) == 0 && _entrances(tile.SceneSno) is [var first, ..])
            return ((tile.OriginX + first.Local.X, tile.OriginY + first.Local.Y), true);
        return ((tile.OriginX + tile.SizeX / 2, tile.OriginY + tile.SizeY / 2), false);
    }

    private static (int Scene, int X, int Y) Key(TileState tile) =>
        (tile.SceneSno, (int)MathF.Round(tile.OriginX), (int)MathF.Round(tile.OriginY));
}
