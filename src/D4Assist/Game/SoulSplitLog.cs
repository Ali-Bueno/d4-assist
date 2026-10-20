using System.Diagnostics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Debug log for Rathma's soul split (research/dungeon-objectives.md §24): while any soul-split actor is listed, twice a second the
/// player position read and every soul-split actor, and once each actor that turns up near the player meanwhile, to learn where
/// the body and the spirit are (the second live run, 2026-10-20: the loop sounded far right and never moved).
/// </summary>
public sealed class SoulSplitLog
{
    private static readonly string[] SplitTokens = ["SoulSplit", "SpiritSplit"];
    private const float NearUnits = 40f;
    private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(500);

    private readonly Func<int, string?> _nameOf;
    private readonly Func<int, ActorDefinitionInfo?> _definition;
    private readonly Action<string> _log;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _gate = new();
    private readonly HashSet<uint> _seen = [];
    private PlayerState? _player;
    private bool _active;
    private TimeSpan? _last;

    public SoulSplitLog(IDungeonFeed feed, Func<int, string?> nameOf, Func<int, ActorDefinitionInfo?> definition, Action<string> log)
    {
        _nameOf = nameOf;
        _definition = definition;
        _log = log;
        feed.PlayerMoved += p => { lock (_gate) _player = p; };
        feed.ActorsUpdated += OnActors;
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
        {
            var split = actors.Where(a => IsSplit(a.ActorSno)).ToList();
            // The boss's five Location_Markers stand from the fight's start: only another split actor starts the log.
            bool active = split.Any(a => !Name(a.ActorSno).Contains("Location_Marker", StringComparison.OrdinalIgnoreCase));
            if (active != _active)
            {
                _active = active;
                _seen.Clear();
                _last = null;
                _log($"SOULSPLIT {(active ? "began" : "ended")}: player {Where(_player)}");
            }
            if (!active || _clock.Elapsed - _last < Every) return; // null: never logged yet
            _last = _clock.Elapsed;
            _log($"SOULSPLIT player {Where(_player)}; " + string.Join("; ", split.Select(Describe)));
            foreach (var actor in actors.Where(a => !IsSplit(a.ActorSno) && _seen.Add(a.Id) && Near(a)))
                _log($"SOULSPLIT near: {Describe(actor)} eType={_definition(actor.ActorSno)?.EType}");
        }
    }

    private bool Near(ActorState actor) =>
        _player is { } p && MathF.Sqrt((actor.X - p.X) * (actor.X - p.X) + (actor.Y - p.Y) * (actor.Y - p.Y)) <= NearUnits;

    private bool IsSplit(int sno) => SplitTokens.Any(t => Name(sno).Contains(t, StringComparison.OrdinalIgnoreCase));

    private string Name(int sno) => _nameOf(sno) ?? sno.ToString();

    private string Describe(ActorState a) =>
        FormattableString.Invariant($"{Name(a.ActorSno)} id={a.Id} at ({a.X:F1},{a.Y:F1},{a.Z:F1}){(a.ClientOnly ? " clientOnly" : "")}");

    private static string Where(PlayerState? p) =>
        p is null ? "?" : FormattableString.Invariant($"({p.X:F1},{p.Y:F1},{p.Z:F1}) scene={p.SceneSno}");
}
