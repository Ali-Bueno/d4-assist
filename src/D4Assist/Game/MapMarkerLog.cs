using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// Logs each actor the game's map marks, once per world: bosses always, every kind with --debug. Tells whether a dungeon's
/// boss, its room's door or a healing well is known before it streams in (STATUS: the guide never reaches the boss room).
/// </summary>
public sealed class MapMarkerLog(Func<int, string?> nameOf, Action<string> log, bool everyKind)
{
    private readonly object _gate = new();
    private readonly HashSet<(int Sno, int Kind)> _logged = [];

    public void Reset()
    {
        lock (_gate) _logged.Clear();
    }

    public void OnMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate)
            foreach (var m in markers.Where(m => (everyKind || m.Kind == MapMarkerReader.KindBoss) && _logged.Add((m.ActorSno, m.Kind))))
                log($"MAP marker kind {m.Kind}: {nameOf(m.ActorSno) ?? m.ActorSno.ToString()} ({m.ActorSno}) at ({m.X:F1},{m.Y:F1},{m.Z:F1}), " +
                    $"level area {nameOf(m.LevelAreaSno) ?? m.LevelAreaSno.ToString()}.");
    }
}
