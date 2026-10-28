namespace D4Assist.Game;

/// <summary>A region a helltide can rise in; the base game's in the order of their UberSubzone value.</summary>
public enum HelltideRegion { Scosglen = 1, FracturedPeaks, Hawezar, Kehjistan, DrySteppes, Nahantu, Skovos }

/// <summary>
/// The helltides the server has active, from the session's world states (research/map-points.md §4): UberSubzone holds the base
/// game's region, UberSubzone_Expansions the expansions'; 0 or no state is none. The values are the game data's own
/// (Bounty_Unlock_Condition_Helltide_* test UberSubzone == 1 Scosglen … 5 Dry Steppes, UberSubzone_Expansions == 1 Nahantu,
/// 2 Skovos), confirmed live 2026-10-27 for Kehjistan, Hawezar and the Fractured Peaks. The expansions' helltides come with
/// patch 3.3.0; before it UberSubzone_Expansions reads 1 with no helltide in Nahantu (live 2026-10-28, build 3.2.3).
/// </summary>
public static class ActiveHelltides
{
    public const int UberSubzone = 1055388, UberSubzoneExpansions = 2077464;

    /// <param name="expansionsHaveHelltides">Patch 3.3.0 or later (<see cref="ExpansionsHaveHelltides"/>).</param>
    public static IReadOnlyList<HelltideRegion> From(IReadOnlyDictionary<int, int> worldStates, bool expansionsHaveHelltides)
    {
        var active = new List<HelltideRegion>();
        if (worldStates.GetValueOrDefault(UberSubzone) is >= 1 and <= 5 and var region) active.Add((HelltideRegion)region);
        if (expansionsHaveHelltides && worldStates.GetValueOrDefault(UberSubzoneExpansions) is >= 1 and <= 2 and var expansion)
            active.Add(HelltideRegion.Nahantu + (expansion - 1));
        return active;
    }

    /// <summary>Patch 3.3.0 brings the expansions' helltides, on the half hour.</summary>
    public static bool ExpansionsHaveHelltides(string? gameBuild) => !BaseEndsEarly(gameBuild);

    public static bool IsExpansion(HelltideRegion region) => region >= HelltideRegion.Nahantu;

    /// <summary>Whole minutes, rounded up, a helltide in the region has left: the base game's end on the hour, five minutes
    /// before it while <paramref name="baseEndsEarly"/> (before patch 3.3.0: live 2026-10-27, 0 from 17:56 to 17:59); the
    /// expansions' (patch 3.3.0) on the half hour.</summary>
    public static int MinutesLeft(HelltideRegion region, DateTime now, bool baseEndsEarly) =>
        MinutesTo(IsExpansion(region) ? 30 : baseEndsEarly ? 55 : 0, now);

    /// <summary>Patch 3.3.0 removes the five minutes between two base-game helltides.</summary>
    public static bool BaseEndsEarly(string? gameBuild) => !Version.TryParse(gameBuild, out var build) || build < new Version(3, 3);

    /// <summary>Whole minutes, rounded up, until the base game's next helltide rises (on the hour).</summary>
    public static int MinutesToNextBase(DateTime now) => MinutesTo(0, now);

    private static int MinutesTo(int minuteOfHour, DateTime now)
    {
        var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, minuteOfHour, 0, now.Kind);
        if (next <= now) next = next.AddHours(1);
        return (int)Math.Ceiling((next - now).TotalMinutes);
    }
}
