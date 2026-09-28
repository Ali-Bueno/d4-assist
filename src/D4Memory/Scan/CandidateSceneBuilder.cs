using D4Data.Casc;
using D4Data.Readers;

namespace D4Memory.Scan;

/// <summary>Builds the matcher's candidate set from every Scene SNO in CoreTOC, so any dungeon's tiles are covered.</summary>
public static class CandidateSceneBuilder
{
    /// <param name="tilesOnly">Keep only DRLG tiles (non-zero exit mask). Off by default: transition/filler
    /// tiles (zero exit mask) can still appear in a revealed dungeon and matter for the layout.</param>
    public static Dictionary<int, CandidateScene> Build(
        SnoStore store, SceneReader sceneReader, int sceneGroup, bool tilesOnly)
    {
        var candidates = new Dictionary<int, CandidateScene>();
        foreach (var (id, name) in store.AllEntries(sceneGroup))
        {
            D4Data.Model.SceneSummary summary;
            try { summary = sceneReader.ReadSummary(id); }
            catch (InvalidDataException) { continue; } // malformed/placeholder scene definition; skip, don't abort the scan
            if (tilesOnly && summary.ExitMask == 0) continue;

            candidates[id] = new CandidateScene(id, name, summary.TileSize, summary.ExitMask, summary.Bounds);
        }
        return candidates;
    }
}
