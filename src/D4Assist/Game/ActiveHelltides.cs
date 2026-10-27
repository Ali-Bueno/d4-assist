namespace D4Assist.Game;

/// <summary>A region a helltide can rise in; the base game's in the order of their UberSubzone value.</summary>
public enum HelltideRegion { Scosglen = 1, FracturedPeaks, Hawezar, Kehjistan, DrySteppes, Nahantu, Skovos }

/// <summary>
/// The helltides the server has active, from the session's world states (research/map-points.md §4): UberSubzone holds the base
/// game's region, UberSubzone_Expansions the expansions'; 0 or no state is none. The values are the game data's own
/// (Bounty_Unlock_Condition_Helltide_* test UberSubzone == 1 Scosglen … 5 Dry Steppes, UberSubzone_Expansions == 1 Nahantu,
/// 2 Skovos), confirmed live 2026-10-27 for Kehjistan, Hawezar and the Fractured Peaks.
/// </summary>
public static class ActiveHelltides
{
    public const int UberSubzone = 1055388, UberSubzoneExpansions = 2077464;

    public static IReadOnlyList<HelltideRegion> From(IReadOnlyDictionary<int, int> worldStates)
    {
        var active = new List<HelltideRegion>();
        if (worldStates.GetValueOrDefault(UberSubzone) is >= 1 and <= 5 and var region) active.Add((HelltideRegion)region);
        if (worldStates.GetValueOrDefault(UberSubzoneExpansions) is >= 1 and <= 2 and var expansion) active.Add(HelltideRegion.Nahantu + (expansion - 1));
        return active;
    }

    public static bool IsExpansion(HelltideRegion region) => region >= HelltideRegion.Nahantu;

    /// <summary>Whole minutes, rounded up, until the region's helltides next change: the base game's on the hour, the expansions'
    /// on the half hour (patch 3.3.0). Before that patch a helltide ends five minutes early, so this is when the next one rises.</summary>
    public static int MinutesToChange(HelltideRegion region, DateTime now) => MinutesTo(IsExpansion(region) ? 30 : 0, now);

    /// <summary>Whole minutes, rounded up, until the base game's next helltide rises (on the hour).</summary>
    public static int MinutesToNextBase(DateTime now) => MinutesTo(0, now);

    private static int MinutesTo(int minuteOfHour, DateTime now)
    {
        var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, minuteOfHour, 0, now.Kind);
        if (next <= now) next = next.AddHours(1);
        return (int)Math.Ceiling((next - now).TotalMinutes);
    }
}
