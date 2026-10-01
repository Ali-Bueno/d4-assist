using System.Diagnostics;

namespace D4Assist.Game;

/// <summary>The memory reads the self-check watches. A game update that moves a structure breaks one of them.</summary>
public enum ReadCheck { Position, Addresses, Rooms, Actors, Attributes, Menus }

/// <summary>
/// Self-check of the memory reads (research/client-memory.md §6): each read reports whether it worked. The first success of
/// each is logged. One that never works in a world for its grace time is logged with the reason, and spoken once per app
/// run, so a player whose game update broke a read hears what stopped working instead of silence.
/// </summary>
public sealed class ReadHealth(Func<TimeSpan> clock, Action<string> log, Action<ReadCheck> warn)
{
    // How long a read may fail in a world before it counts: the player record shows up once the world has loaded (slow PCs
    // take longer) and the menu scan backs off; rooms and actors come within a few polls; addresses and attribute names
    // are found or not.
    private static readonly IReadOnlyDictionary<ReadCheck, TimeSpan> Grace = new Dictionary<ReadCheck, TimeSpan>
    {
        [ReadCheck.Position] = TimeSpan.FromSeconds(60),
        [ReadCheck.Addresses] = TimeSpan.Zero,
        [ReadCheck.Rooms] = TimeSpan.FromSeconds(20),
        [ReadCheck.Actors] = TimeSpan.FromSeconds(20),
        [ReadCheck.Attributes] = TimeSpan.Zero,
        [ReadCheck.Menus] = TimeSpan.FromSeconds(60),
    };

    private sealed class CheckState
    {
        public TimeSpan? FailingSince;
        public bool WorkedInWorld, FailedInWorld, Logged, Warned;
    }

    private readonly Dictionary<ReadCheck, CheckState> _states = Enum.GetValues<ReadCheck>().ToDictionary(c => c, _ => new CheckState());
    private readonly object _gate = new();

    public static ReadHealth Live(Action<string> log, Action<ReadCheck> warn)
    {
        var watch = Stopwatch.StartNew();
        return new ReadHealth(() => watch.Elapsed, log, warn);
    }

    /// <summary>One read's outcome; <paramref name="detail"/> says why it failed. A read that worked once in this world
    /// stays good until the next one: a structure only moves with a game update, which restarts the game.</summary>
    public void Report(ReadCheck check, bool ok, string? detail = null)
    {
        string? line = null;
        bool speak = false;
        lock (_gate)
        {
            var state = _states[check];
            if (state.WorkedInWorld || state.FailedInWorld) return;
            var now = clock();
            if (ok)
            {
                state.WorkedInWorld = true;
                if (!state.Logged) line = $"Self-check: {check} ok.";
                state.Logged = true;
            }
            else
            {
                state.FailingSince ??= now;
                var failing = now - state.FailingSince.Value;
                if (failing < Grace[check]) return;
                state.FailedInWorld = true;
                line = $"Self-check: {check} FAILED after {failing.TotalSeconds:F0} s: {detail ?? "no reason given"}.";
                speak = !state.Warned;
                state.Warned = true;
            }
        }
        if (line is not null) log(line);
        if (speak) warn(check);
    }

    /// <summary>A new world opens its own reads: each check starts again (a failure is still spoken only once per run).</summary>
    public void NewWorld()
    {
        lock (_gate)
            foreach (var state in _states.Values)
                (state.FailingSince, state.WorkedInWorld, state.FailedInWorld) = (null, false, false);
    }
}
