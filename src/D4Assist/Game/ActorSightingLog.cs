using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Debug log: once per dungeon visit and actor SNO, every placed gizmo or portal-like actor, to learn what an exit really is.</summary>
public sealed class ActorSightingLog(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf, Action<string> log)
{
    private static readonly string[] NameTokens = ["Portal", "Warp", "Exit", "Beacon"];
    private const int NoGizmo = -1;

    private readonly HashSet<int> _seen = [];

    /// <summary>Forget what was logged; call on dungeon change.</summary>
    public void Reset() => _seen.Clear();

    public void Note(IEnumerable<ActorState> actors, PlayerState? player)
    {
        foreach (var actor in actors)
        {
            if (_seen.Contains(actor.ActorSno)) continue;
            int gizmo = definition(actor.ActorSno)?.GizmoType ?? NoGizmo;
            string name = nameOf(actor.ActorSno) ?? actor.ActorSno.ToString();
            if (gizmo < 0 && !NameTokens.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;

            _seen.Add(actor.ActorSno);
            string distance = player is null ? "?" : FormattableString.Invariant($"{Math.Sqrt(Sq(actor.X - player.X) + Sq(actor.Y - player.Y)):F1}");
            log(FormattableString.Invariant($"ACTOR seen: {name} sno={actor.ActorSno} gizmo={gizmo} at ({actor.X:F1},{actor.Y:F1}) distance={distance}"));
        }
    }

    private static double Sq(double v) => v * v;
}
