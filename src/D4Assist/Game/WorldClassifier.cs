using D4Data.Casc;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Resolves a world name from the log to its World SNO and kind, offline (research/dungeon-data.md §3:
/// WorldDefinition.eLayoutType == 2 for every dungeon-type world). A non-DRLG world is radar-only only when
/// its exact (case-sensitive) name is in the user's RadarOnlyWorlds list; everything else stays untouched.</summary>
public sealed class WorldClassifier(SnoStore store, WorldReader worldReader, IReadOnlyCollection<string>? radarOnlyWorlds = null) : IWorldClassifier
{
    public (int WorldSno, WorldKind Kind) Classify(string worldName)
    {
        int sno = store.Resolve(worldReader.Group, worldName);
        var (info, _) = worldReader.Read(sno);
        return (sno, KindOf(info.LayoutType, worldName, radarOnlyWorlds));
    }

    public static WorldKind KindOf(int layoutType, string worldName, IReadOnlyCollection<string>? radarOnlyWorlds) =>
        layoutType == Conventions.LayoutDrlg ? WorldKind.Dungeon
        : radarOnlyWorlds?.Contains(worldName, StringComparer.Ordinal) == true ? WorldKind.RadarOnly
        : WorldKind.Other;
}
