using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>What an entrance leads into: a dungeon, a cellar, or a boss lair (a portal into a <c>Boss_*</c> world: Varshan,
/// Grigoire, Lord Zir, the Beast in the Ice, Duriel, Andariel, Belial, Urivar, the Harbinger, Mephisto), or another place behind a
/// dungeon-type portal (quest dungeons <c>QST_*</c>, the Undercity, seasonal and event places): only <c>DGN_*</c> level areas are
/// the map's dungeons (146 in the open world, build 3.2.4).</summary>
public enum EntranceKind { Dungeon, Cellar, BossLair, Other }

/// <summary>A dungeon's or cellar's entrance: the portal's world and spot, and the level area it leads to (its map name).</summary>
/// <param name="Kind">Dungeon, cellar or boss lair.</param>
/// <param name="Condition">The internal name of the condition the map shows it under (snoCampaignEnableCondition), null for none.</param>
/// <param name="LevelAreaSno">The level area the portal stands in (its zone is the region spoken after the name); 0 when unknown.</param>
public sealed record DungeonEntrance(int WorldSno, Vector3 Position, int DestLevelAreaSno, string DestLevelAreaName,
    EntranceKind Kind = EntranceKind.Dungeon, string? Condition = null, int LevelAreaSno = 0);

/// <summary>
/// Every dungeon and cellar entrance the world map can show, from the Global <c>global_markers</c> portal markers
/// (research/map-points.md §2–3): dungeon-type portals (ePortalDestSubzoneType 2) and cellars, but not those the map hides
/// (fHiddenMapIcon); each named after the level area it leads to.
/// </summary>
public sealed class DungeonEntranceCatalog(SnoStore store, TypeSchema schema)
{
    // ePortalDestSubzoneType of the 546 dungeon portals; cellars have 0 like town and hub portals, so they go by name.
    private const int DungeonSubzoneType = 2;
    private const string CellarNamePart = "Cellar", BossWorldPrefix = "Boss_", DungeonAreaPrefix = "DGN_";
    private const string SeasonActivePrefix = "Season_Active_Season", SeasonOnlyNotPrefix = "Season_Only_Not_S";
    /// <summary>The condition of an entrance the map shows only on a Torment tier (the Throne of Hatred).</summary>
    public const string TormentCondition = "WorldTier_IsDifficultyTier_AnyTorment";

    private IReadOnlyList<DungeonEntrance>? _all;
    private int? _season;

    /// <summary>Every entrance; empty when the Global record is unreadable.</summary>
    public IReadOnlyList<DungeonEntrance> All => _all ??= Read();

    /// <summary>The entrance's dungeon name in <paramref name="locale"/> (a D4 code such as enUS), else its level area's internal name.</summary>
    public string Name(DungeonEntrance entrance, string locale) =>
        store.Text(locale, $"LevelArea_{entrance.DestLevelAreaName}", "Name") ?? entrance.DestLevelAreaName;

    /// <summary>The season the game data runs: the highest N of a plain <c>Season_Active_SeasonN</c> condition (15 in build
    /// 3.2.4; a later season's conditions such as <c>Season_Active_Season16_Level20plus</c> come before their plain one).</summary>
    public int Season => _season ??= store.AllEntries(schema.SnoGroup("ConditionDefinition"))
        .Select(e => e.Name.StartsWith(SeasonActivePrefix, StringComparison.Ordinal) && int.TryParse(e.Name[SeasonActivePrefix.Length..], out int n) ? n : 0)
        .DefaultIfEmpty(0).Max();

    /// <summary>Whether the map shows the entrance by its condition: a season's entrance only in that season, a
    /// <c>Season_Only_Not_SN</c> one only outside season N, a Torment one only when <paramref name="torment"/> is true (unknown:
    /// not shown); every other condition (campaign progress, capstones) counts as met.</summary>
    public bool Shown(DungeonEntrance entrance, bool? torment = null)
    {
        if (entrance.Condition is not { } condition) return true;
        if (condition == TormentCondition) return torment == true;
        if (condition.StartsWith(SeasonOnlyNotPrefix, StringComparison.Ordinal) && int.TryParse(condition[SeasonOnlyNotPrefix.Length..], out int not))
            return not != Season;
        if (condition.StartsWith(SeasonActivePrefix, StringComparison.Ordinal))
        {
            string digits = new([.. condition[SeasonActivePrefix.Length..].TakeWhile(char.IsAsciiDigit)]);
            return !int.TryParse(digits, out int season) || season == Season;
        }
        return true;
    }

    private List<DungeonEntrance> Read() =>
    [
        .. new GlobalMapPointReader(store, schema).Read()
            .Where(p => p.Kind == MapPointKind.Portal && p.Portal is { HiddenMapIcon: false, DestLevelArea: > 0 } portal
                && (portal.DestSubzoneType == DungeonSubzoneType || store.Name(p.ActorSno)?.Contains(CellarNamePart) == true))
            .Select(p => store.Name(p.Portal!.DestLevelArea) is { } area
                ? new DungeonEntrance(p.WorldSno, p.Position, p.Portal.DestLevelArea, area, KindOf(p), p.EnableConditionSno > 0 ? store.Name(p.EnableConditionSno) : null, p.LevelAreaSno)
                : null)
            .OfType<DungeonEntrance>(),
    ];

    private EntranceKind KindOf(MapPoint point) =>
        point.Portal!.SpecifiedWorld > 0 && store.Name(point.Portal.SpecifiedWorld)?.StartsWith(BossWorldPrefix, StringComparison.Ordinal) == true ? EntranceKind.BossLair
        : store.Name(point.ActorSno)?.Contains(CellarNamePart) == true ? EntranceKind.Cellar
        : store.Name(point.Portal.DestLevelArea)?.StartsWith(DungeonAreaPrefix, StringComparison.Ordinal) == true ? EntranceKind.Dungeon
        : EntranceKind.Other;
}
