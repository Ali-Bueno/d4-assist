using System.Text.RegularExpressions;

namespace D4Assist.Game;

/// <summary>Scene SNOs whose tile layer places the Undercity exit pad, read from MarkerSet names (research/dungeon-objectives.md section 5).</summary>
public static partial class ExitCandidateScenes
{
    // Tile layers are named "<scene name> (<layer>)"; the layers hold X1_Undercity_WarpPad (MarkerSet group 24).
    public const string AreaExitsLayer = "X1_Undercity_AreaExits";
    public const string BossExitsLayer = "X1_Undercity_BossExits";

    [GeneratedRegex($@"^(?<scene>.+) \((?:{AreaExitsLayer}|{BossExitsLayer})\)$")]
    private static partial Regex LayerName();

    /// <param name="markerSetNames">CoreTOC names of every MarkerSet.</param>
    /// <param name="resolveScene">Scene name to SNO; throws <see cref="ArgumentException"/> when unknown (SnoStore.Resolve).</param>
    public static IReadOnlySet<int> Build(IEnumerable<string> markerSetNames, Func<string, int> resolveScene)
    {
        var snos = new HashSet<int>();
        foreach (var name in markerSetNames)
        {
            if (LayerName().Match(name) is not { Success: true } match) continue;
            try { snos.Add(resolveScene(match.Groups["scene"].Value)); }
            catch (ArgumentException) { } // a layer of a scene the build no longer ships
        }
        return snos;
    }
}
