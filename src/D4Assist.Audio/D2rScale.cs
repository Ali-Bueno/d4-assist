namespace D4Assist.Audio;

/// <summary>Conversion from D2R distances (subtiles) to Diablo IV world units, for ported reach/cadence values.</summary>
public static class D2rScale
{
    /// <summary>Guess: ~0.6 D4 world units per D2R subtile (first used for the quest loop reach; re-tune by ear).</summary>
    public const double D4UnitsPerSubtile = 0.6;
}
