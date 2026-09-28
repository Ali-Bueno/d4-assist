using D4Data.Casc;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Resolves a world name from the log to its World SNO and whether it's a DRLG dungeon, offline
/// (research/dungeon-data.md §3: WorldDefinition.eLayoutType == 2 for every dungeon-type world).</summary>
public sealed class WorldClassifier(SnoStore store, WorldReader worldReader) : IWorldClassifier
{
    public (int WorldSno, bool IsDungeon) Classify(string worldName)
    {
        int sno = store.Resolve(worldReader.Group, worldName);
        var (info, _) = worldReader.Read(sno);
        return (sno, info.LayoutType == Conventions.LayoutDrlg);
    }
}
