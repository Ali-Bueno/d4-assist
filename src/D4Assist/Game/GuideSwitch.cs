namespace D4Assist.Game;

/// <summary>
/// Whether the exploration guide is on for the current world: endgame dungeons default on, other dungeons
/// follow the config, and a toggle flips the current kind for the rest of the run.
/// </summary>
public sealed class GuideSwitch
{
    // CoreTOC world names: 13 Undercity worlds, 58 Pit worlds (research/dungeon-objectives.md §5).
    public static readonly string[] EndgameWorldPrefixes = ["X1_Undercity_", "PIT_"];

    private readonly bool _otherDungeonsDefault;
    private bool? _endgameOverride;
    private bool? _otherOverride;
    private bool _inDungeon;
    private bool _endgame;

    public GuideSwitch(bool otherDungeonsDefault) => _otherDungeonsDefault = otherDungeonsDefault;

    public static bool IsEndgameWorld(string worldName) =>
        EndgameWorldPrefixes.Any(p => worldName.StartsWith(p, StringComparison.Ordinal));

    public bool InDungeon => _inDungeon;

    public bool IsOn => _inDungeon && (_endgame ? _endgameOverride ?? true : _otherOverride ?? _otherDungeonsDefault);

    public void WorldChanged(string worldName, bool isDungeon)
    {
        _inDungeon = isDungeon;
        _endgame = isDungeon && IsEndgameWorld(worldName);
    }

    /// <summary>Flips the current kind of world and returns the new state; null (no change) outside a dungeon.</summary>
    public bool? Toggle()
    {
        if (!_inDungeon) return null;
        bool now = !IsOn;
        if (_endgame) _endgameOverride = now; else _otherOverride = now;
        return now;
    }
}
