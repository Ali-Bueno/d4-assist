using System.Diagnostics;
using System.Numerics;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>
/// Leads through a campaign route (<see cref="QuestRouteCatalog"/>) with the guide ping, straight at the next point:
/// the overworld has no detailed walkable grid to path on (research/campaign-routes.md). A route starts when one of its
/// quest actors shows up in the live actor list at its start or at one of its points (the guide NPC also stands at other
/// quests' places), or when the player stands at the quest's own marker. It ends at its end (or last point), when the
/// player leaves its area, or on a world change; a finished route doesn't restart in the same world visit. After each
/// point the ping holds while the player stays there (a shelter is where they wait out the storm) and leads on once they
/// step away. D-pad left mutes and unmutes the ping while a route leads; a route that ends drops the mute, so the next
/// one starts audible.
/// </summary>
public sealed class QuestRouteBridge
{
    /// <summary>A point counts as reached this close to its marker; the safe-zone trigger's own size isn't in the data.</summary>
    public const float ReachRadius = 4f;

    /// <summary>Standing this close to the quest's marker starts the route when none of its actors is visible live; a quest actor
    /// starts it this close to the marker or to a point.</summary>
    public const float StartRadius = 15f;

    /// <summary>Farther than this from the start and every point, the player has left the route.</summary>
    public const float AreaRadius = 150f;

    /// <summary>The ping holds while the player stays this close to the point just reached.</summary>
    public const float HoldRadius = 10f;

    private readonly IReadOnlyList<LoadedQuestRoute> _routes;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private readonly HashSet<string> _finished = [];
    private IReadOnlyList<LoadedQuestRoute> _worldRoutes = [];
    private LoadedQuestRoute? _active;
    private RouteGuide? _guide;
    private Vector2? _player;
    private Vector2? _holdAt;
    private bool _muted;

    /// <summary>True while a route leads, when D-pad left mutes and unmutes it.</summary>
    public bool IsActive { get { lock (_gate) return _active is not null; } }

    /// <param name="pad">D-pad left mutes the route's ping; null for no toggle.</param>
    /// <param name="nameOf">CoreTOC name of an actor SNO, for the log.</param>
    /// <param name="log">Debug sink for start, point and stop lines; null when not debugging.</param>
    public QuestRouteBridge(
        IDungeonFeed feed, IReadOnlyList<LoadedQuestRoute> routes, GuidePingPlayer ping, ISpeechSink speech,
        LanguageSetting? language = null, IGuideTogglePad? pad = null, Func<int, string?>? nameOf = null, Action<string>? log = null)
    {
        _routes = routes;
        _ping = ping;
        _speech = speech;
        _language = language ?? new LanguageSetting();
        _nameOf = nameOf ?? (_ => null);
        _log = log;

        if (feed.CurrentWorld is { } startWorld) EnterWorld(startWorld.Name); // app started mid-world
        feed.WorldChanged += (_, name, _) => { lock (_gate) EnterWorld(name); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { lock (_gate) OnPlayer(new Vector2(p.X, p.Y)); };
        if (pad is not null) pad.DpadLeftPressed += OnTogglePressed;
    }

    private void EnterWorld(string worldName)
    {
        Stop();
        _finished.Clear();
        _player = null;
        _worldRoutes = [.. _routes.Where(r => r.Route.WorldName == worldName)];
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        if (_active is not null || _player is not { } player) return;
        foreach (var route in _worldRoutes.Where(r => !_finished.Contains(r.Route.Name) && InArea(r, player)))
        {
            if (actors.FirstOrDefault(a => route.ActivationSnos.Contains(a.ActorSno) && AtStartOrPoint(route, new Vector2(a.X, a.Y)))
                is not { } seen) continue;
            Start(route, $"actor {_nameOf(seen.ActorSno) ?? seen.ActorSno.ToString()} at ({seen.X:F1},{seen.Y:F1})");
            return;
        }
    }

    private void OnPlayer(Vector2 player)
    {
        _player = player;
        if (_active is null)
        {
            var atStart = _worldRoutes.FirstOrDefault(r => !_finished.Contains(r.Route.Name) && Vector2.Distance(player, r.Start) <= StartRadius);
            if (atStart is null) return;
            Start(atStart, "player at the quest marker");
        }
        Lead(player);
    }

    private void Start(LoadedQuestRoute route, string reason)
    {
        _active = route;
        _guide = new RouteGuide(route.Start, route.Path, ReachRadius);
        if (_player is { } player)
        {
            _guide.JoinAt(player);
            if (_guide.Reached > 0 && Vector2.Distance(player, route.Path[_guide.Reached - 1]) <= HoldRadius)
                _holdAt = route.Path[_guide.Reached - 1]; // started while waiting at a point
        }
        Log($"ROUTE {route.Route.Name} started ({reason}), next point {_guide.Reached + 1} of {_guide.Count}");
        _speech.Speak(AppStrings.For(_language.Current).RouteStarted(route.Route.Kind), interrupt: false);
        if (_player is { } now) Lead(now);
    }

    private void Lead(Vector2 player)
    {
        if (_active is not { } route || _guide is not { } guide) return;
        if (!InArea(route, player))
        {
            Log($"ROUTE {route.Route.Name} stopped: player left its area at ({player.X:F1},{player.Y:F1})");
            Stop();
            return;
        }

        if (guide.Update(player))
        {
            if (!_muted) _ping.PlayArrived();
            Log($"ROUTE {route.Route.Name} reached point {guide.Reached} of {guide.Count} at ({player.X:F1},{player.Y:F1})");
            var strings = AppStrings.For(_language.Current);
            _speech.Speak(guide.Reached <= route.Points.Count
                ? strings.RoutePointReached(route.Route.Kind, guide.Reached, route.Points.Count)
                : strings.RouteEndReached(route.Route.Kind), interrupt: false);
            if (guide.Finished)
            {
                _finished.Add(route.Route.Name);
                Stop();
                return;
            }
            _holdAt = route.Path[guide.Reached - 1];
        }

        if (_holdAt is { } hold && Vector2.Distance(player, hold) > HoldRadius)
        {
            _holdAt = null;
            Log($"ROUTE {route.Route.Name} leading on to point {guide.Reached + 1} of {guide.Count}");
        }
        if (_muted || _holdAt is not null || guide.Target is not { } target) { _ping.Silence(); return; }
        var (right, up) = ScreenDirections.ToScreen(target.X - player.X, target.Y - player.Y);
        _ping.Aim(right, up, Vector2.Distance(player, target));
    }

    private static bool InArea(LoadedQuestRoute route, Vector2 player) =>
        Vector2.Distance(player, route.Start) <= AreaRadius || route.Path.Any(p => Vector2.Distance(player, p) <= AreaRadius);

    private static bool AtStartOrPoint(LoadedQuestRoute route, Vector2 at) =>
        Vector2.Distance(at, route.Start) <= StartRadius || route.Points.Any(p => Vector2.Distance(at, p) <= StartRadius);

    private void Stop()
    {
        _active = null;
        _guide = null;
        _holdAt = null;
        _muted = false; // the mute is for the route leading now: the next one starts audible, as its announcement says
        _ping.Silence();
    }

    private void OnTogglePressed()
    {
        lock (_gate)
        {
            if (_active is null) return;
            _muted = !_muted;
            var strings = AppStrings.For(_language.Current);
            Log($"ROUTE {_active.Route.Name} {(_muted ? "muted" : "unmuted")}");
            _speech.Speak(_muted ? strings.GuideOff : strings.GuideOn, interrupt: true);
            if (_muted) _ping.Silence();
            else if (_player is { } player) Lead(player);
        }
    }

    private void Log(string message) => _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] {message}");
}
