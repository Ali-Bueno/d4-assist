using System.Diagnostics;
using D4Assist.Navigation;
using D4Assist.Speech;

namespace D4Assist.Game;

/// <summary>Feeds live dungeon snapshots into the NavigationEngine and routes its announcements to speech.</summary>
public sealed class NavigationBridge
{
    private readonly NavigationEngine _engine;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads; the engine is single-threaded.
    private readonly object _gate = new();
    private readonly bool _diagnostics;
    private string _lastTileSignature = "";
    private readonly HashSet<int> _rotatedScenesLogged = [];
    private PlayerInput? _lastPlayer;

    public NavigationBridge(IDungeonFeed feed, ISpeechSink speech, bool announcementsEnabled, bool diagnostics, ITileWalkability? walkability = null, LanguageSetting? language = null,
        Func<int, bool>? isExitCandidate = null, DirectionStyle directionStyle = DirectionStyle.Screen)
    {
        _diagnostics = diagnostics;
        _engine = new NavigationEngine(walkability, language, isExitCandidate);
        _engine.Settings.Enabled = announcementsEnabled;
        _engine.Settings.DirectionStyle = directionStyle;
        _engine.Announce += (text, interrupt) =>
        {
            if (_diagnostics) LogOpenings(text);
            speech.Speak(text, interrupt);
        };

        feed.WorldChanged += (_, _, _) => { lock (_gate) _engine.Reset(); };
        feed.TilesUpdated += tiles =>
        {
            lock (_gate)
            {
                LogRotatedTiles(tiles);
                if (feed.CurrentWorldRadarOnly) return;
                LogTilesIfChanged(tiles);
                _engine.OnTiles([.. tiles.Select(ToInput)]);
            }
        };
        feed.PlayerMoved += p =>
        {
            lock (_gate)
            {
                if (feed.CurrentWorldKind is WorldKind.RadarOnly or WorldKind.Overworld) return; // no rooms to announce there
                _lastPlayer = new PlayerInput(p.X, p.Y, p.Z);
                _engine.OnPlayer(_lastPlayer, _clock.Elapsed);
            }
        };
    }

    /// <summary>The exploration guide's next step for a live player position (serialized with the feed handlers).</summary>
    public GuideStep NextGuideStep(
        PlayerInput player, bool exitMissing = false, IReadOnlyList<GuideDestination>? destinations = null, IReadOnlyList<GuideDestination>? objectives = null)
    {
        lock (_gate) return _engine.Guide.Step(player, exitMissing, destinations, objectives);
    }

    /// <summary>Live barriers the guide routes around, as 1-unit world cells; an equal set is a no-op (ExplorationGuide.SetBlockedCells).</summary>
    public void SetBlockedCells(IReadOnlySet<(int X, int Y)> cells)
    {
        lock (_gate) _engine.Guide.SetBlockedCells(cells);
    }

    /// <summary>Breakable props the guide walks around, as 1-unit world cells; an equal set is a no-op (ExplorationGuide.SetPropCells).</summary>
    public void SetPropCells(IReadOnlySet<(int X, int Y)> cells)
    {
        lock (_gate) _engine.Guide.SetPropCells(cells);
    }

    /// <summary>The doors the game keeps shut, for the guide; an equal list is a no-op (ExplorationGuide.SetGates).</summary>
    public void SetGates(IReadOnlyList<GuideGate> gates)
    {
        lock (_gate) _engine.Guide.SetGates(gates);
    }

    /// <summary>Lets the shut doors the player walked away from lead again (ExplorationGuide.RearmGates).</summary>
    public void RearmGates()
    {
        lock (_gate) _engine.Guide.RearmGates();
    }

    /// <summary>The jump links of the loaded tiles, for the guide's routes; an equal list is a no-op (ExplorationGuide.SetJumpLinks).</summary>
    public void SetJumpLinks(IReadOnlyList<JumpLink> jumps)
    {
        lock (_gate) _engine.Guide.SetJumpLinks(jumps);
    }

    public ExplorationState ExportExploration()
    {
        lock (_gate) return _engine.ExportExploration();
    }

    public void RestoreExploration(ExplorationState state)
    {
        lock (_gate) _engine.RestoreExploration(state);
    }

    // No tile seen live so far is rotated, and rooms, openings and the guide take every tile's footprint as unrotated
    // (TileRotation; STATUS.md Known issues). One line per scene, debug or not, so a tester's log shows when one turns up.
    private void LogRotatedTiles(IReadOnlyList<TileState> tiles)
    {
        foreach (var tile in tiles)
            if (RotatedTileNote(tile) is { } note && _rotatedScenesLogged.Add(tile.SceneSno)) Log(note);
    }

    /// <summary>The log line for a tile placed at a yaw other than 0, or null for an unrotated one.</summary>
    public static string? RotatedTileNote(TileState tile) =>
        TileRotation.QuarterTurns(tile.YawDegrees) == 0 ? null
        : $"TILE ROTATED {tile.Name} (scene {tile.SceneSno}) yaw={tile.YawDegrees:F1} at ({tile.OriginX:F0},{tile.OriginY:F0}) " +
          $"{tile.SizeX:F0}x{tile.SizeY:F0}: rooms, openings and the guide take its footprint as unrotated.";

    private void LogTilesIfChanged(IReadOnlyList<TileState> tiles)
    {
        if (!_diagnostics) return;
        var lines = tiles.OrderBy(t => t.OriginX).ThenBy(t => t.OriginY)
            .Select(t => $"{t.Name}@({t.OriginX:F0},{t.OriginY:F0}) {t.SizeX:F0}x{t.SizeY:F0} exit={t.ExitMask} node=0x{t.NodeId:X} group=0x{t.GroupId:X}");
        string signature = string.Join("; ", lines);
        if (signature == _lastTileSignature) return;
        _lastTileSignature = signature;
        Log($"TILES at {Format(_lastPlayer)}: {signature}");
    }

    private void LogOpenings(string text)
    {
        Log($"SAY \"{text}\" at {Format(_lastPlayer)}");
        foreach (var route in _engine.LastOpenings)
        {
            var o = route.Opening;
            Log($"  opening {o.Side} at ({o.Position.X:F1},{o.Position.Y:F1}) width={o.Width:F1} " +
                $"pathLength={route.PathLength:F1} {(route.UsedPathFallback ? "straight-line fallback" : "path")}{(route.IsEntry ? " entry" : "")}");
        }
    }

    private void Log(string message) => Console.WriteLine($"[{_clock.Elapsed.TotalSeconds,7:F1}s] {message}");

    private static string Format(PlayerInput? p) => p is null ? "(?)" : $"({p.X:F1},{p.Y:F1})";

    private static TileInput ToInput(TileState t) =>
        new(t.SceneSno, t.Name, t.OriginX, t.OriginY, t.SizeX, t.SizeY, t.ExitMask, t.NodeId, t.GroupId, t.YawDegrees, t.AuthoredExitMask);
}
