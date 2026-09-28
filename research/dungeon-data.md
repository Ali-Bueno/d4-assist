# Diablo IV dungeon data — offline track (World → Scene → nav/markers)

Research spike, 2026-09-26. Game build **3.2.1.73552** (`.build.info` Version). All claims come from the local
install read **read-only** through CASC, parsed with the type registry of the same build.
Confidence: **H** = verified on game data from several independent angles, **M** = verified on a few samples,
**L** = inference.

## 1. Tooling and sources

| What | Source | Notes |
|---|---|---|
| CASC/TVFS + CoreTOC reader | NuGet `WiseOwl.Casc.Diablo4` **0.8.0** (net8/net10) | `Diablo4Storage.Open(install)` ~1.6 s; CoreTOC = 881,520 entries. SNOs are addressed by id: `Base\Meta\<id>`, `Base\Payload\<id>`. |
| Type layouts (offsets, sizes, groups) | `DiabloTools/d4data` `definitions.json`, `buildVersion.txt` = 3.2.1.73552 (matches install) | Reflection dump of the game's own type registry. Sparse-cloned only `definitions.json`, `names.txt`, `parse.js`. |
| Primitive encodings | d4data `parse.js`; WiseOwl `docs/casc-diablo4-format.md` §2 | Meta = 16-byte header (`0xDEADBEEF`), fields relative to byte 16. **Payload has no header**; array offsets flagged external (field flags `0x200000`/`0x400000`) point into the Payload file. |
| Marker/transform chain reference | `Dakota628/d4parse` `pkg/d4/mrk/markers.go`, `transform.go` (2024, read only) | World → SceneChunk → Scene.arLayers → MarkerSet → Marker, parent-first T·R·S composition. |
| Dumper | `tools/D4DungeonDump/` (this repo) | Reads offsets from `definitions.json` at runtime — no hardcoded layouts. See §8. |

SNO groups used (from `definitions.json` `snoGroup`): Actor 1, LevelArea 22, MarkerSet 24, Scene 33, World 48,
Subzone 80, Appearance 9. **TileSet (83) and Population (84) have zero CoreTOC entries** (H).

## 2. From the runtime log to a World SNO

`FenrisDebug.txt`: `[Game] Client entered world | world: DGN_Hawe_HeathensKeep | CWorldID: 4194306`.
The `world:` value is a CoreTOC name in group 48: `DGN_Hawe_HeathensKeep` → **843152**,
`S15_QST_Intro_Tavern` → **2655857**, `PIT_Step_City` → **2527286** (H). `CWorldID` is a per-instance client id, not a SNO.
Note: `snoWorld(2655860)` in the log is an error line about a UI radio button; 2655860 is the **LevelArea**
`S15_QST_Intro_Tavern` (group 22), not the world.

World naming (880 worlds): `OWC_` 175, `DGN_` 164 (dungeons), `QST_` 130, `CSD_` 66 (campaign side dungeons /
cellars), `PIT_` 58, `TWR_` 28, `S<nn>_` seasonal. Cellars appear as `CSD_*_Cellar` / `QST_*_Cellar`.

## 3. World SNO (`WorldDefinition`, meta only, no payload) — static vs generated

Key fields (offsets from `definitions.json`): `ptServerData` VARARRAY<`WorldServerData`{`ptSceneChunks`
VARARRAY<`SceneChunk`>, `nSeed`, `flMinX/Y`, `flMaxX/Y`}>, `eLayoutType` ENUM, `flGridSize` f32,
`snoSubzoneDefault`, `arDRLGLevelAreas` SNO[], `arDRLGFloors` World SNO[], `bRandomDungeonWorld` INT.
`SceneChunk` = {`snoname` Scene SNO, `transform` PRTransform{`q` bcQuat xyzw, `wp` vec3}, `tSceneSpec`{`snoLevelArea`, …}}.

Census over all 880 World SNOs (H):

| eLayoutType | Worlds | ptSceneChunks | Examples |
|---|---|---|---|
| 0 | 42 | 1 fixed scene (41), empty (1) | Limbo, portraits, front-end |
| 1 | 36 | filled, grid-aligned | Sanctuary_Eastern_Continent (1220 chunks, 96-unit grid, identity rotations), S15_Tristram (9), Mercenary_Hideout (1) |
| 2 | 802 | **always empty**, `nSeed` 0, bounds 0 | every DGN_/CSD_/QST_/PIT_/TWR_ world; 86 of them have `bRandomDungeonWorld=1` (PIT/TWR, no floors) |

Chosen samples:
- **DGN_Hawe_HeathensKeep (843152)**: eLayoutType 2, gridSize 96, subzone 843153, `arDRLGLevelAreas`
  [843154 `DGN_Hawe_HeathensKeep`, 854917 `_2`], `arDRLGFloors` [843152], random 0, `ptSceneChunks` empty.
- **PIT_Step_City (2527286)**: eLayoutType 2, `bRandomDungeonWorld=1`, floors [], level area 2527438.
- **S15_Tristram (2591553)**: eLayoutType 1, 9 chunks named `S15_Tristram_{N01,S01,S02}_{W01,E01,E02}`.

**Verdict (H): dungeon/cellar layouts are not stored in client data.** Every dungeon-type world is a DRLG world
whose scene list is empty offline; the generator's inputs (tile sets, node graphs) are not shipped (group 83
absent, DRLG node types absent from the client type registry — §6). The "handcrafted fixed layout" belief is
**refuted at the data level**: even if a dungeon happens to generate the same shape each run, its placement is
decided server-side and only reaches the client at runtime. Supporting runtime evidence (M): the log line
`Clearing UIMinimap scenes for World <n>: its DRLG layout hash (A) does not match its new hash (B)` recurs for
the same world across PIT runs — the client caches minimap scenes per world keyed by a layout hash that changes
between instances. Subzone 843153 has no `arWorldMarkerSets`; MarkerSet 843156 `DGN_Hawe_HeathensKeep` only
holds the overworld entrance prefab (`Portal_Dungeon_Generic`, `Dungeon_Location_0`).

## 4. Scenes = DRLG tiles (`SceneDefinition`, meta + payload)

Dungeon scenes are tile-set pieces, e.g. `ProtoDun_Keep_{N,S,E,W,NS,EW,…,NSEW}_01`, `_Entrance_01`,
`_Arena_01`, `ProtoDun_Keep_FLEX_POI_BossRoom` (64×64), `_FILLER_01` (75 Keep scenes). No offline link maps
HeathensKeep to the Keep tile set (only some worlds have world-named theme layers, e.g.
`ProtoDun_Keep_NSEW_01 (CSD_Frac_Prologue_AbandonedTower)`); the Keep set is used as an illustrative library.

Recovered fields (meta unless noted):

| Field | Type | Meaning | Conf. |
|---|---|---|---|
| `aabbBounds` | AABB {center, halfExtent} | scene-local bounds, e.g. Keep_N_01 ≈ X −2.3…34.5, Y −2.2…34.2, Z −5.9…7.2 | H |
| `flTileX`, `flTileY` | f32 | tile footprint: 32×32 (3671 scenes), 64×64, 96×96, 64×32 … | H |
| `eDRLGTileType` | ENUM | **exit bitmask N=1, S=2, E=4, W=8**; matches 1739 of 1766 direction-named scenes (misses are naming quirks) | H |
| `arLayers[].snoLayerMarkerset` | MarkerSet SNO[] | layers `(Game)`, `(Shared)`, `(Merged)`, `(Terrain)`, `(Lighting*)`, `(Theme_*)`, world-specific | H |
| `snoAppearance` | Appearance SNO | render/collision model; its `Structure` has `ptCollisionMeshes`, `ptSceneData` (height fields, surface octree) in payload — not decoded | M |
| `ptAIPathData` (**payload**) | AIPathData[1] | walkability, §4.1 | H/M |
| `tLevelAreaGrid`, `tFogMask` (payload), `arLevelAreaEdges` | painted grid / mask / edge polylines | empty on Keep tiles; not explored | L |

### 4.1 Walkability (`AIPathData` in the Scene payload)

`AIPathData` {`m_cellData` AICellData[], `m_cellBytes` AICellBytes[] (opaque byte blob per cell),
`unk_5d4c076` detail grids[], `m_cellWidth` = 32, `m_cellHeight` = 32}.
- The scene is divided into **32×32×32-unit nav cells**. `AICellData.m_cellId` packs biased indexes:
  bits 31–24 = z + 0x80, 23–12 = x + 0x800, 11–0 = y + 0x800 (H: 64×32 tile → x ∈ {0,1}; Limbo cells −2…1
  decode to boxes spanning −50…50.5; 96×96 Tristram scenes → 3×3×2 cells).
- `aabbWalkableBounds` / `aabbPreventWalkableBounds` are **cell-local and stored as (Y, Z, X)** (H: for all 100
  sampled single-exit tiles the walkable box touches the edge of the exit — N ↔ X=0, S ↔ X=32, E ↔ Y=32, W ↔ Y=0).
  Scene-local box = (ix·32 + c2, iy·32 + c0, iz·32 + c1). Empty cells use the inverted ±FLT_MAX sentinel.
  Typical tile: a z=0 cell (floor, Z 0…1) and a z=−1 cell (Z −1.5…0).
- Detail grid `unk_5d4c076` = `Type_5f1859b4` (73,808 B per cell): `m_cellId`, a hash equal to
  `AICellData+64`, `m_detailGrid` and `m_detailGridWalkable` (4608 × u64 each), `m_slicesInternal`,
  `m_slicesExtents`, AABBs. **Present only for DRLG tiles**: 30/30 sampled tiles have it; 10/10 open-world
  and 22/30 other scenes carry only the compressed `m_cellBytes` (M, 70-scene sample).
- `m_cellBytes` (≈3–30 KB per cell) is a compressed sparse-voxel brick DAG (17 u64 section offsets, 4×4×4
  8-unit occupancy mask, 2×2 node hierarchy, unique-brick dictionaries; no floats), not a polygon navmesh.
  Partly decoded (M/L); not needed where the detail grid exists.

### 4.2 Detail voxel grid layout (H, validated on 104 tiles)

- Per cell: **64 × 64 columns × 72 levels, 0.5-unit voxels**. Each u64 is a 4×4×4 brick holding one nibble per
  column (4 levels per nibble): `word = (level/4)·256 + (yi/4)·16 + (xi/4)`,
  `bit = 4·((yi%4)·4 + xi%4) + level%4`.
- Scene-local: X = ix·32 + xi·0.5 (xi 0 = north edge), Y = iy·32 + yi·0.5 (yi 0 = west edge),
  Z = iz·32 + (level − 4)·0.5. Levels 0–3 and 68–71 are 2-unit margins copied from the cells below/above.
- `m_slicesInternal` bit s ⇔ level s+4 non-empty; `m_slicesExtents` bits 0–3 / 4–7 ⇔ margin levels 0–3 / 68–71.
- `m_detailGrid` = all geometry (floor band ~3 voxels, walls/props as tall runs); `m_detailGridWalkable` =
  walkable voxels only (subset). A walkable run is the floor surface ±1 voxel, so surface height ≈ run middle
  (±0.5). A column can hold several runs (bridges/platforms over lower floors).
- Plain `*_N_01`-style caps are mostly unwalkable scenery with a short walkable stub at the exit edge;
  `_Entrance_`/`_Arena_` variants are full rooms. Example (`ProtoDun_Keep_NSEW_01`, 1-unit map from the dumper):
  a cross with the N/S corridor at Y≈9–23 and the E/W band at X≈8–23, 571 units² walkable.
- Reference decoder (Python): scratchpad `offline/navdecode/navgrid.py`; C#: `NavGridReader`.

### 4.3 Markers (MarkerSet → Marker)

`Marker` (208 B): `eType` ENUM, `transform` PRTransform (scene-local), `snoname` DT_SNO_NAME {group, id},
`vecScale`, `ptBase` polymorphic data. eType ↔ data class (from `ptBase` type, M): 0 = SNO reference
(actor/prefab/decal/light/VFX — the referenced group disambiguates), 2 camera, 3 fade volume, 4 reflection
probe, 7 level-area volume, 8 height field, 50 spawn location, 51 unknown (no data).
Nested MarkerSet references are prefabs; compose parent·T·R·S (d4parse `AddPRAndScale`).

Gameplay actors found in `(Game)` layers of the Keep tiles (sample JSON): `Start_Location_0` (arrival points:
the 4 `_Entrance_01` tiles, 2 in the boss room, 4 each in the `POI_HoldoutTunnel` tiles),
`Prefab_Portal_Dungeon_Generic` (dungeon exit portal, only on the 4 entrance tiles, next to the start location), `TrackedCheckpoint_Marker` (at the tile's exit edge, e.g. Keep_N_01 at (1.7, 15.1, 0)),
`TrackedCheckpoint_Volume` (tile centre), `TrackedCheckpoint_8m`, `Trigger_BossRoom_Entrance`.
Doors/objectives are not tile markers: e.g. `DGN_Hawe_HeathensKeep_Lockable_Boss_Door` is an Actor SNO (854889)
placed by server logic; objectives come from Quest SNO `DPO_Hawe_HeathensKeep` (843450) at runtime.

## 5. Coordinate system (H)

- World and scene-local frames share axes: **X = south, Y = east, Z = up** (engine compass N = −X, S = +X,
  E = +Y, W = −Y). Right-handed. Evidence: (a) exit markers/walkable boxes of 100 tiles; (b) open-world chunk
  `Sanctuary_Eastern_Continent_N25_W04` at (−2400, −384, 0) = (−25·96, −4·96); (c) `S15_Tristram_S02_E02` at
  (96, 96, 0).
- A scene's local origin is its tile corner: tile occupies [0, tileX] × [0, tileY]; bounds overhang ~2 units.
- **world = chunk.wp + rotate(chunk.q, local)** (d4parse transform chain; all static chunks have identity
  rotation and grid-aligned positions). DRLG tile variants are authored per direction (N/S/E/W files), so
  runtime placements are probably unrotated too (L).
- Units: "world units"; likely metres (L: actor name `TrackedCheckpoint_8m`, ~1–2 unit walkable slabs).

## 6. DRLG types: offline vs runtime

| Type (`definitions.json`) | Where it lives | Key fields | Class |
|---|---|---|---|
| `WorldDefinition.eLayoutType/arDRLGFloors/arDRLGLevelAreas/bRandomDungeonWorld` | World SNO | see §3 | **offline** |
| `SceneDefinition.eDRLGTileType` | Scene SNO | exit mask | **offline** |
| `GlobalData.nDRLGSeedType` | Global SNO | seed policy | offline config |
| `RevealWorldMessage` | server→client msg | `snoWorld`, **`nDRLGLayoutHash`**, `vDRLGTileDims` (opt vec2), dungeon states/affixes/quests, `nSeed` (optional) | **runtime** |
| `RevealSceneMessage` | server→client msg | `idSWorld`, **`snoScene`, `transform` PRTransform**, `tSceneSpec` (level area, subzones, opt `DRLGSceneCreationInfo`), `arAppliedLabels`[256], `arDRLGTiles` `DRLGPartialTileData`[32] {`dwTilePosHash`, `eTileType`}, `arDRLGElevations`[4] | **runtime** — this is the instance layout |
| `DRLGSceneCreationInfo` | inside the above | `nBatchId`, `dwTilePosHash`, `eTileType`, `dwNodeId`, `dwGroupId`, `dwFlags`, `dwRevealGroup` | runtime |
| `WorldSyncedData.arRevealedPortalInfo` (`WorldRevealedPortalInfo`) | synced world state | `wpPosition`, `uEntranceGUID`, `snoWorld`, `snoLevelArea`, `hMinimapIcon` | runtime |
| `DRLGWorldDebugInfo(Message)`, `DRLGTileDebugInfo`, `DRLGEventDebugInfo`, `DRLGForcedWorldParamsDebugInfo` | debug request/response msgs | `dwSeedInitial`, `arTiles[256]` {`snoScene`, `vWorldPos`, AABB, creation info, level area, labels} | **debug-only** runtime |
| `DRLGDungeonState`, `DRLGNodeLink`, `DRLGPopulationGroup`, `DRLGPortalInfo`, `DRLGSpawningData`, `DRLGSpawnGroupTypeData`, `DRLGSpawningPool` | defined, **referenced by no field** in the client registry | e.g. `DRLGNodeLink.dwDestNodeId`; `DRLGPortalInfo` {resolve method, specified world, entrance name, dest level area, main entrance} | server-side remnants |
| `DRLGNodeBase`, `DRLGNodeConnectionData`, `DRLGNodeRandomRoomsData`, `DRLGNodeStartData`, `DRLGGroupData`, `DRLGNodeTileSetTransitionData`, … | only in d4data `names.txt`; **absent** from this build's `definitions.json` | — | **server-only** generator graph |

## 7. Central question: offline reconstruction?

**No (H).** A DRLG dungeon instance cannot be reconstructed from offline data alone, and not from offline data
plus a seed either: the generator graph and tile sets are server-only. What offline data *does* give, per tile
SNO: footprint, exits (bitmask), coarse walkable boxes (plus a detail grid), entrance/exit/checkpoint markers,
all in a known frame. The missing piece is the instance's **list of (scene SNO, PRTransform)** — what
`RevealSceneMessage` carries (M, from the schema); the client holds it at runtime (L: the minimap scene cache
is keyed by the DRLG layout hash, per the log line in §3). Given that list, the offline tile data composes into
a full dungeon graph: tiles on a grid, connected through matching exit bits of neighbours (M).
Static worlds (eLayoutType 0/1, e.g. S15_Tristram) *are* fully reconstructable offline.

## 8. Dumper — `tools/D4DungeonDump`

`dotnet run -- <world name|id> --defs <d4data definitions.json> [--install <dir>] [--tileset <prefix>]
[--scene <name|id>]... [--all-layers] [--out file.json]` (`D4DATA_DEFINITIONS` env var also accepted).
Output: world summary (§3), placed scenes with world pose/bounds (static worlds), per-scene tile size, exits,
nav-cell walkable boxes (scene-local), walkable area from the voxel grid (DRLG tiles), with `--grid` a 1-unit
top-down ASCII map, actor markers of `(Game)`/`(Shared)` layers (all layers with `--all-layers`), and
world-space markers for placed scenes. Samples (scratchpad, not repo):
`sample_DGN_Hawe_HeathensKeep.json` (`--tileset ProtoDun_Keep --grid`, 75 tiles), `sample_S15_Tristram.json`,
`sample_PIT_Step_City.json`.

## 9. Still unknown / next steps

1. **Instance layout at runtime** — (scene SNO, transform) list for the current world. Needs a runtime provider
   (read-only) or another non-invasive channel; none found in logs (they expose world name and layout-hash
   changes only).
2. **Player pose** (world X/Y/Z, heading) — not in offline data or logs.
3. Full `m_cellBytes` decode (needed only for non-tile scenes) and the Appearance collision meshes.
4. World → tile-set mapping for a given dungeon (server-side); only observable via runtime scene SNOs.
5. Whether a given `DGN_*` regenerates differently every run (layout hash evidence so far is from PIT/Limbo lines).
6. Whether scenes are revealed all at once or progressively (per-scene `RevealSceneMessage` suggests streaming).
