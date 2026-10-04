namespace D4Assist.Navigation;

/// <summary>Every navigation string for one language, kept short for NVDA; add a phrase once per language instance.</summary>
public sealed partial class NavigationPhrases
{
    // Spelled-out counts for grouped repeated openings; larger counts use the digit (no room observed exceeds 4).
    private static readonly int[] SpelledCounts = [2, 3, 4];

    public static readonly NavigationPhrases Spanish = new()
    {
        SingularHeader = "Salida",
        PluralHeader = "Salidas",
        UnexploredFormat = "{0} sin explorar",
        GroupFormat = "{0} salidas {1}",
        CountWords = ["dos", "tres", "cuatro"],
        DeadEnd = "Callejón sin salida.",
        Entrance = "Entrada.",
        NoOtherExits = "Sin otras salidas.",
        AllExplored = "Todo explorado.",
        Rechecking = "Salida no encontrada. Revisando los callejones.",
        ToQuestObjective = "Hacia el objetivo de la misión.",
        ToDungeonObjective = "Hacia un objetivo de la mazmorra.",
        ToUnexplored = "Hacia zona sin explorar.",
        QuestObjectiveBelow = "El objetivo está más abajo.",
        QuestObjectiveAbove = "El objetivo está más arriba.",
        Directions = Map("arriba", "arriba a la derecha", "derecha", "abajo a la derecha", "abajo", "abajo a la izquierda", "izquierda", "arriba a la izquierda"),
        CompassDirections = Map("norte", "noreste", "este", "sureste", "sur", "suroeste", "oeste", "noroeste"),
        Labels = new() { [RoomKind.Junction] = "Cruce.", [RoomKind.BigRoom] = "Sala grande.", [RoomKind.Corridor] = "Pasillo." },
    };

    public static readonly NavigationPhrases English = new()
    {
        SingularHeader = "Exit",
        PluralHeader = "Exits",
        UnexploredFormat = "{0} unexplored",
        GroupFormat = "{0} exits {1}",
        CountWords = ["two", "three", "four"],
        DeadEnd = "Dead end.",
        Entrance = "Entrance.",
        NoOtherExits = "No other exits.",
        AllExplored = "All explored.",
        Rechecking = "Exit not found. Rechecking dead ends.",
        ToQuestObjective = "To the quest objective.",
        ToDungeonObjective = "To a dungeon objective.",
        ToUnexplored = "To unexplored ground.",
        QuestObjectiveBelow = "The objective is further down.",
        QuestObjectiveAbove = "The objective is further up.",
        Directions = Map("up", "up-right", "right", "down-right", "down", "down-left", "left", "up-left"),
        CompassDirections = Map("north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"),
        Labels = new() { [RoomKind.Junction] = "Junction.", [RoomKind.BigRoom] = "Big room.", [RoomKind.Corridor] = "Corridor." },
    };

    public static NavigationPhrases For(SpeechLanguage language) => language switch
    {
        SpeechLanguage.English => English,
        SpeechLanguage.French => French,
        SpeechLanguage.German => German,
        SpeechLanguage.Italian => Italian,
        SpeechLanguage.Polish => Polish,
        SpeechLanguage.Portuguese => Portuguese,
        SpeechLanguage.Russian => Russian,
        SpeechLanguage.Turkish => Turkish,
        SpeechLanguage.Japanese => Japanese,
        SpeechLanguage.Korean => Korean,
        SpeechLanguage.ChineseSimplified => ChineseSimplified,
        SpeechLanguage.ChineseTraditional => ChineseTraditional,
        _ => Spanish,
    };

    private NavigationPhrases() { }

    /// <summary>Spoken once when the exploration guide has no unvisited part left to lead to.</summary>
    public string AllExplored { get; private init; } = "";

    /// <summary>Spoken once per round when the guide walks back to the dead ends that could hold the missing exit.</summary>
    public string Rechecking { get; private init; } = "";

    /// <summary>Spoken when the guide turns to the tracked quest's step seen earlier.</summary>
    public string ToQuestObjective { get; private init; } = "";

    /// <summary>Spoken when the guide turns to a dungeon objective seen earlier (prisoner, altar, lever...).</summary>
    public string ToDungeonObjective { get; private init; } = "";

    /// <summary>Spoken when the guide turns (back) to exploring, or is switched on while exploring.</summary>
    public string ToUnexplored { get; private init; } = "";

    /// <summary>Spoken once when the player stands over a quest place on another floor (it is lower).</summary>
    public string QuestObjectiveBelow { get; private init; } = "";

    /// <summary>Spoken once when the player stands under a quest place on another floor (it is higher).</summary>
    public string QuestObjectiveAbove { get; private init; } = "";

    // How a room's announcement is put together: {0} label, {1} exit header, {2} the exits; and the label with no exits.
    // Japanese and Chinese use their own punctuation (、 ： 。) and no spaces.
    private string ExitsFormat { get; init; } = "{0} {1}: {2}.";
    private string NoExitsFormat { get; init; } = "{0} {1}";
    private string ListSeparator { get; init; } = ", ";
    private string SingularHeader { get; init; } = "";
    private string PluralHeader { get; init; } = "";
    private string UnexploredFormat { get; init; } = "";
    private string GroupFormat { get; init; } = "";
    private string[] CountWords { get; init; } = [];
    private string DeadEnd { get; init; } = "";
    private string Entrance { get; init; } = "";
    private string NoOtherExits { get; init; } = "";
    private Dictionary<StickDirection, string> Directions { get; init; } = new();
    // North is screen up: the same eight screen sectors, named as compass points (not the game world's own axes).
    private Dictionary<StickDirection, string> CompassDirections { get; init; } = new();
    private Dictionary<RoomKind, string> Labels { get; init; } = new();

    // Clockwise from up, matching the StickDirection order.
    private static Dictionary<StickDirection, string> Map(params string[] clockwise) =>
        clockwise.Select((text, i) => (Key: (StickDirection)i, text)).ToDictionary(p => p.Key, p => p.text);

    public string Direction(StickDirection direction) => Direction(direction, DirectionStyle.Screen);

    public string Direction(StickDirection direction, DirectionStyle style) =>
        (style == DirectionStyle.Compass ? CompassDirections : Directions)[direction];

    public string DirectionPhrase(StickDirection direction, bool unexplored, DirectionStyle style = DirectionStyle.Screen) =>
        unexplored ? string.Format(UnexploredFormat, Direction(direction, style)) : Direction(direction, style);

    /// <summary>Builds the full announcement for a room kind and its ordered, spoken opening phrases.
    /// <paramref name="isEntrance"/> overrides the label with the entrance label regardless of kind (the first
    /// room of a run must never read as a dead end).</summary>
    public string Announcement(RoomKind kind, IReadOnlyList<string> openingPhrases, bool isEntrance = false)
    {
        if (isEntrance) return WithLabel(Entrance, openingPhrases);
        if (kind == RoomKind.DeadEnd) return DeadEnd;
        return WithLabel(Labels[kind], openingPhrases);
    }

    private string WithLabel(string label, IReadOnlyList<string> openingPhrases)
    {
        if (openingPhrases.Count == 0) return string.Format(NoExitsFormat, label, NoOtherExits);
        var header = openingPhrases.Count == 1 ? SingularHeader : PluralHeader;
        return string.Format(ExitsFormat, label, header, string.Join(ListSeparator, GroupRepeats(openingPhrases)));
    }

    // Collapses repeated identical phrases into "N exits <phrase>", keeping first-occurrence order.
    private List<string> GroupRepeats(IReadOnlyList<string> phrases)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var phrase in phrases)
        {
            if (!counts.ContainsKey(phrase)) order.Add(phrase);
            counts[phrase] = counts.GetValueOrDefault(phrase) + 1;
        }
        return order.Select(phrase => counts[phrase] == 1 ? phrase : string.Format(GroupFormat, CountWord(counts[phrase]), phrase)).ToList();
    }

    private string CountWord(int count)
    {
        int i = Array.IndexOf(SpelledCounts, count);
        return i >= 0 ? CountWords[i] : count.ToString();
    }
}
