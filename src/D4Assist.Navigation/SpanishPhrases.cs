namespace D4Assist.Navigation;

/// <summary>Every spoken string, in one place, in Spanish, kept short for NVDA.</summary>
public static class SpanishPhrases
{
    private const string ListSeparator = ", ";
    private const string SingularHeader = "Salida";
    private const string PluralHeader = "Salidas";
    public const string Unexplored = "sin explorar";
    private const string DeadEnd = "Callejón sin salida.";
    private const string Entrance = "Entrada.";
    private const string NoOtherExits = "Sin otras salidas.";

    // Spanish number words for grouped repeated openings (task spec: spell out 2-4, larger counts use
    // the digit — no dungeon room observed so far has more same-direction exits than this).
    private static readonly Dictionary<int, string> GroupCountWords = new() { [2] = "dos", [3] = "tres", [4] = "cuatro" };

    private static readonly Dictionary<StickDirection, string> Directions = new()
    {
        [StickDirection.Up] = "arriba",
        [StickDirection.UpRight] = "arriba a la derecha",
        [StickDirection.Right] = "derecha",
        [StickDirection.DownRight] = "abajo a la derecha",
        [StickDirection.Down] = "abajo",
        [StickDirection.DownLeft] = "abajo a la izquierda",
        [StickDirection.Left] = "izquierda",
        [StickDirection.UpLeft] = "arriba a la izquierda",
    };

    private static readonly Dictionary<RoomKind, string> Labels = new()
    {
        [RoomKind.Junction] = "Cruce.",
        [RoomKind.BigRoom] = "Sala grande.",
        [RoomKind.Corridor] = "Pasillo.",
    };

    public static string Direction(StickDirection direction) => Directions[direction];

    public static string DirectionPhrase(StickDirection direction, bool unexplored) =>
        unexplored ? $"{Direction(direction)} {Unexplored}" : Direction(direction);

    /// <summary>Builds the full announcement for a room kind and its ordered, spoken opening phrases.
    /// <paramref name="isEntrance"/> overrides the label with "Entrada." regardless of kind (the first
    /// room of a run must never read as a dead end).</summary>
    public static string Announcement(RoomKind kind, IReadOnlyList<string> openingPhrases, bool isEntrance = false)
    {
        if (isEntrance) return WithLabel(Entrance, openingPhrases);
        if (kind == RoomKind.DeadEnd) return DeadEnd;
        return WithLabel(Labels[kind], openingPhrases);
    }

    private static string WithLabel(string label, IReadOnlyList<string> openingPhrases)
    {
        if (openingPhrases.Count == 0) return $"{label} {NoOtherExits}";
        var header = openingPhrases.Count == 1 ? SingularHeader : PluralHeader;
        return $"{label} {header}: {string.Join(ListSeparator, GroupRepeats(openingPhrases))}.";
    }

    // Collapses repeated identical phrases into "N salidas <phrase>", keeping first-occurrence order
    // (bug: the same phrase was spoken three times running instead of once, grouped).
    private static List<string> GroupRepeats(IReadOnlyList<string> phrases)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var phrase in phrases)
        {
            if (!counts.ContainsKey(phrase)) order.Add(phrase);
            counts[phrase] = counts.GetValueOrDefault(phrase) + 1;
        }
        return order.Select(phrase => counts[phrase] == 1 ? phrase : $"{CountWord(counts[phrase])} salidas {phrase}").ToList();
    }

    private static string CountWord(int count) => GroupCountWords.TryGetValue(count, out var word) ? word : count.ToString();
}
