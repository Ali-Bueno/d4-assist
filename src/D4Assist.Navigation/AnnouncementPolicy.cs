namespace D4Assist.Navigation;

/// <summary>
/// Builds the spoken text for a room, or null when nothing should be said.
/// Junctions, dead ends and big rooms are announced; corridors are silent by
/// default (toggle below). Openings are listed as stick directions, clockwise from up, following a
/// BFS path from the player's position (RoomRouteCache) rather than a straight line, which can cross
/// walls on an irregular room. Diff-gated: an unchanged announcement text is never repeated.
/// </summary>
public sealed class AnnouncementPolicy
{
    /// <summary>How far past the opening's edge the "did we come from there" probe lands: one cell past
    /// the boundary is inside the neighbouring tile, per the same convention OpeningFinder uses to test
    /// whether an opening is internal.</summary>
    public const float EntryProbeUnits = 1f;

    public bool Enabled { get; set; } = true;
    public bool IncludeCorridors { get; set; } = false;
    public bool MentionUnexplored { get; set; } = true;

    private readonly LanguageSetting _language;
    private readonly RoomRouteCache _routes = new();
    private string? _lastText;

    public AnnouncementPolicy(LanguageSetting? language = null) => _language = language ?? new LanguageSetting();

    /// <summary>The text to announce for entering <paramref name="room"/> (or null to stay silent), plus
    /// each opening's resolved route for diagnostics. <paramref name="cameFrom"/> is the room the player
    /// just left (its entry opening is excluded from the spoken list); <paramref name="isEntrance"/>
    /// marks the very first room of the run ("Entrada." instead of the usual room-kind label).</summary>
    public (string? Text, IReadOnlyList<OpeningRoute> Openings) Describe(Room room, PlayerInput player, Room? cameFrom = null, bool isEntrance = false)
    {
        if (!Enabled) return (null, []);
        var kind = RoomClassifier.Classify(room); // classification uses every opening, entry or not
        if (kind == RoomKind.Corridor && !IncludeCorridors) return (null, []);

        var routes = _routes.Resolve(room, player)
            .Select(r => r with { IsEntry = IsEntryRoute(r.Opening, cameFrom) })
            .ToList();

        var words = NavigationPhrases.For(_language.Current);
        var phrases = routes
            .Where(r => !r.IsEntry) // don't list the exit the player just walked through as if new
            .OrderBy(r => (int)r.Direction) // clockwise from up
            .Select(r => words.DirectionPhrase(r.Direction, MentionUnexplored && r.Opening.Unexplored))
            .ToList();

        var text = words.Announcement(kind, phrases, isEntrance);
        if (text == _lastText) return (null, routes);
        _lastText = text;
        return (text, routes);
    }

    private static bool IsEntryRoute(RoomOpening opening, Room? cameFrom)
    {
        if (cameFrom is null) return false;
        var (nx, ny) = ExitMaskBits.Normal(opening.Side);
        return cameFrom.Contains(opening.Position.X + nx * EntryProbeUnits, opening.Position.Y + ny * EntryProbeUnits);
    }

    public void Reset() => _lastText = null;
}
