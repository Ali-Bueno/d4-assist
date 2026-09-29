using D4Data.Casc;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Resolves a world name from the log to its World SNO and kind, offline (research/dungeon-data.md §3:
/// WorldDefinition.eLayoutType == 2 for every dungeon-type world). A non-DRLG world is radar-only when its exact
/// (case-sensitive) name is in the user's RadarOnlyWorlds list, otherwise an overworld when reads outside dungeons are
/// on and it isn't a menu or loading world.</summary>
public sealed class WorldClassifier(
    SnoStore store, WorldReader worldReader, IReadOnlyCollection<string>? radarOnlyWorlds = null, bool outsideDungeons = false) : IWorldClassifier
{
    // Non-DRLG worlds that are menus, loading screens, cinematics or test maps, never played in
    // (census of the layout 0/1 worlds, research/dungeon-data.md §3). Matched as case-insensitive name prefixes.
    public static readonly string[] MenuWorldPrefixes =
    [
        "Limbo", "Frontend", "Character_Select", "PaperDoll", "Portrait", "CL_", "Wardrobe", "Heraldry", "Store_",
        "Collection_", "Preview_", "SocialScreen", "SmokeTest_", "IGC_",
    ];

    public (int WorldSno, WorldKind Kind) Classify(string worldName)
    {
        int sno = store.Resolve(worldReader.Group, worldName);
        var (info, _) = worldReader.Read(sno);
        return (sno, KindOf(info.LayoutType, worldName, radarOnlyWorlds, outsideDungeons));
    }

    public static WorldKind KindOf(int layoutType, string worldName, IReadOnlyCollection<string>? radarOnlyWorlds, bool outsideDungeons = false) =>
        layoutType == Conventions.LayoutDrlg ? WorldKind.Dungeon
        : radarOnlyWorlds?.Contains(worldName, StringComparer.Ordinal) == true ? WorldKind.RadarOnly
        : outsideDungeons && !IsMenuWorld(worldName) ? WorldKind.Overworld
        : WorldKind.Other;

    public static bool IsMenuWorld(string worldName) =>
        MenuWorldPrefixes.Any(p => worldName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
