using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A dungeon's or cellar's entrance: the portal's world and spot, and the level area it leads to (its map name).</summary>
public sealed record DungeonEntrance(int WorldSno, Vector3 Position, int DestLevelAreaSno, string DestLevelAreaName);

/// <summary>
/// Every dungeon and cellar entrance the world map can show, from the Global <c>global_markers</c> portal markers
/// (research/map-points.md §2–3): dungeon-type portals (ePortalDestSubzoneType 2) and cellars, but not those the map hides
/// (fHiddenMapIcon); each named after the level area it leads to.
/// </summary>
public sealed class DungeonEntranceCatalog(SnoStore store, TypeSchema schema)
{
    // ePortalDestSubzoneType of the 546 dungeon portals; cellars have 0 like town and hub portals, so they go by name.
    private const int DungeonSubzoneType = 2;
    private const string CellarNamePart = "Cellar";

    private IReadOnlyList<DungeonEntrance>? _all;

    /// <summary>Every entrance; empty when the Global record is unreadable.</summary>
    public IReadOnlyList<DungeonEntrance> All => _all ??= Read();

    /// <summary>The entrance's dungeon name in <paramref name="locale"/> (a D4 code such as enUS), else its level area's internal name.</summary>
    public string Name(DungeonEntrance entrance, string locale) =>
        store.Text(locale, $"LevelArea_{entrance.DestLevelAreaName}", "Name") ?? entrance.DestLevelAreaName;

    private List<DungeonEntrance> Read() =>
    [
        .. new GlobalMapPointReader(store, schema).Read()
            .Where(p => p.Kind == MapPointKind.Portal && p.Portal is { HiddenMapIcon: false, DestLevelArea: > 0 } portal
                && (portal.DestSubzoneType == DungeonSubzoneType || store.Name(p.ActorSno)?.Contains(CellarNamePart) == true))
            .Select(p => store.Name(p.Portal!.DestLevelArea) is { } area ? new DungeonEntrance(p.WorldSno, p.Position, p.Portal.DestLevelArea, area) : null)
            .OfType<DungeonEntrance>(),
    ];
}
