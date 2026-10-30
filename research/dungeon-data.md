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

Coverage census (local install, 2026-09-29, H): expansion dungeons are layout 2 like the base game's.
Layout-2 prefixes also include `OWC_` 175, `S<nn>_` seasonal, `MJD_` 16, `X1_` 13, `Boss_` 10, `X2_` 6,
`X1R_` 3 and `CMP_` 3. Nahantu content is in there too (`DGN_Naha_*`, `CSD_Naha_*`, `X1R_Naha_*Wing`).
Gameplay worlds that are **not** layout 2, so the app stays off in them: layout 1 `Sanctuary_Eastern_Continent`
(the overworld), `Frac_Underworld`, `Kehj_Hell`, `Kehj_Hell_Sightless_Eye`, `BSK_Hell`, `S03_Hub_TWN`,
`X1R_Khazra_Hub_KeyRoom`, `X2_Eclipse_Bookend03`, `S11_Heavenly_Forge_Grid`, `S15_Tristram`, `Mercenary_Hideout`;
layout 0 `Hellgate`, `Rivergate`, `Tejal_Scene`, `Looping_QST_Skov_Temis_Bookend1`.

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
- Detail grid `unk_5d4c076` = `Type_5f1859b4` (73,808 B per cell): `m_cellId`, a hash (equal to
  `AICellData+64` in only 653/2107 cells: pair by `m_cellId`), `m_detailGrid` and `m_detailGridWalkable`
  (4608 × u64 each), `m_slicesInternal`, `m_slicesExtents`, AABBs. **Present only for DRLG tiles**; open-world
  and static-world scenes (every layout 0/1 gameplay world, census 2026-09-30) carry only `m_cellBytes`.
- `AICellData+48` (`unk_c225718`) = index into `m_cellBytes`; cells share blobs (H).

### 4.1b `m_cellBytes` (decoded 2026-09-30, `D4Data.Readers.CellBytesDecoder`)

Validated on 2107 cells of 408 DRLG tiles from 132 tilesets, which carry both forms.
- Blob: 0 bytes = no data. Header: 17 u64 section byte offsets (section i = [off_i, off_i+1), the last runs to the
  end), then 4 u64: surface root mask, surface region count, free-space root mask, `1<<32 | free region count` (H).
- Two trees of the same shape (H): **S = walkable surfaces** (sections: 1 blocks, 3 octant stream, 4 nodes,
  5 leaf refs, 7 word dictionary) and **F = free (non-solid) space** (10, 11, 12, 13, 15). Sections 0, 2, 6, 8, 9,
  14, 16 are region/graph metadata (roles L).
- Only the 64 core levels (detail levels 4–67); margins aren't stored (H). Root mask: 4×4×4 blocks of 16³ voxels,
  bit = 4·(by·4+bx)+bz.
- Block entries (u64), one per root bit in bit order: bit 63 set = direct; clear = `count = e>>32` direct entries
  from index `low32`. Direct entry: 11-bit octant-stream offset (u16 index) at bit 29 (S) / bit 36 (F) (H).
- Octant stream (u16): 8-bit mask of the 8³ octants (bit = y + 2x + 4z), then one node index per set bit.
  Node = (u16 first leaf, u16 mask of its 8 bricks of 4³, same bit order); leaves are consecutive (H).
- Leaf ref u32: bit 31 set = dictionary index in the low bitlen(dictCount−1) bits; clear = OR of `r>>16` refs
  from `r & 0xFFFF`. Dictionary word = one 4×4×4 brick in the detail u64 layout; core brick (zb, yb, xb) →
  detail word (zb+1)·256 + yb·16 + xb (H).
- F = exact complement of `m_detailGrid` on levels 4–67 (H, 2107/2107 cells, bit-exact).
- S ⊆ `m_detailGridWalkable`; each S voxel tops a walkable run (H). `m_detailGridWalkable` itself isn't in the blob:
  it also holds floor buried under walls/props and the scene's outermost voxel ring. Top-down, S covers 89.5% of
  the walkable columns with no false ones (M); the missing ring is 0.5 units, inside the app's 1-unit map cells.
- The app's walk map for scenes without detail grids = S columns, top-down (`SceneReader.SurfaceColumns`). Floors
  at different heights merge in that projection (bridges, ledges); S keeps the heights for a later 2.5D map.
- Reference decoder (Python) and validation harness: scratchpad of 2026-09-30 (`cb/dec.py`, `cbval`), not kept.

### 4.1c Traversals split the surfaces (offline, 2026-10-23, `Kehj_Hell_Sightless_Eye` 929413)

- Static world (layout 1, grid 96, 40 chunks, none rotated; 27 scenes have no surface voxels). Its S surfaces
  (§4.1b) split into islands wherever the walk uses a **traversal**: a jump, a duck-under, a slide. The game's
  floor doesn't cross them, so `SurfacePathfinder` found no path across (H: every split of the quest's walk had one).
- Each traversal is a group of scene markers in any layer: `Traversal_Controller`, two `Traversal_Gizmo_<Kind>` ends
  (one for a slide) and a `MarkerLocation_TraversalExit` by each end (or the slide's foot). Jump ends 4.9–6.0 apart,
  duck-under 2.0, slide 7.0 and 5 units down. Seen: `_Jump`, `_DuckUnder`, `_Slide`.
- Pairing: two jumps on one ledge stood 4.5 apart (-518.4 and -522.8, -289.5), nearer than their own far ends, so
  mutual-nearest pairing (`JumpLinkCollector`, DRLG tiles) would join the ledge to itself and miss both. Linking
  every two ends (gizmos and exits) within 8 units joins exactly the islands; the extra links are steps on one floor.
- Replay of "The Blind Eye" (593914) in phase order: without links 6 of 16 legs had no path, with links all but two:
  the statue to the escape (the game teleports the player) and the campfire (a 36-unit gap: its cinematic moves the
  player down).
- Rising bridges (`SightlessEye_Rising_Bridge_01_Dyn`, door-like gizmos) did not split the surfaces of that walk.
- **Traversals in a world's own marker sets** (offline, 2026-10-28, `Frac_Underworld` 439872, the Darkened Way): its floors split
  in two at a 5-unit strip (−96…−91, 50), both sides at Z≈5: a jump whose ends (−96.3,50.2) and (−91.4,50.2) sit in the world set
  `Frac_Underworld (QST_Frac_Underworld_02_JumpTraversal)`, not in a scene; a ladder (`Traversal_Gizmo_RungClimb_Down/_Up`,
  (−25.2,56.9,21.9)/(−25.1,58.2,13.9)) in `Frac_Underworld (QST_Frac_Underworld_01_DownClimb)` joins the upper arrival ledge to
  the plateau. Read from scenes only, 14 of 16 quest legs pathed and 2 of 7 from where the tester pressed D-pad left (it pointed
  straight across the gap, "dropoffs"); with the world sets' traversals (world space, `<World> (` prefix, the naming of
  `QuestScriptMarkerReader.FrameOf`) 16 of 16 and 7 of 7. Census: only `Frac_Underworld` and `X1R_Khazra_Hub_KeyRoom` have
  traversals in world sets; a few open-world subzone sets hold some too (`Hawe_Bog` 8, `Kehj_Caldeum` 6), not read yet.
- **A locked gate is floor** (same replay): `Ancients_Ruins_Blocker_Door` (−103.7,19.9), in `Frac_Underworld (ShortcutToLake_Gate)`,
  is the only overland link from the upper ruins to the Mourning Shore and stood locked through quest 486210; the path leads
  through it (blocked, 12 of 16 legs path: the real way is the Cradle dungeon, whose exit lands at (−78.5,−39.9)). Not handled.
- **DRLG tiles** link every traversal kind but the slide too (`JumpLinkCollector`, 2026-10-28): the Necropolis's tumor platforms
  (ladders) and pocket (duck-under), [dungeon-objectives](dungeon-objectives.md) §12.
- **Traversals are prefabs** (census of 448 scenes, 2026-10-30): each is one `Traversal_*` marker-set prefab: `Jump_6m_Gizmo`,
  `DuckUnder_2m`, `RungClimb`/`FreeClimb_5m|8m` (2 exits + Down/Up), `Slide_5m` (slide + exit), `HandOverHand_8–30m`,
  `Jump_6m_OneWay` (one jump point + its landing exit 5.9 on). Mutual-nearest pairing missed 46 of 297 two-ended prefabs: the
  Temple of the Deathspeaker's `Ancients_Flooded_SW_POI_CSD_Hawe_Swamp_TempleoftheDeathspeaker` chains four jumps whose ends stand
  4.1/4.6 apart on a ledge against 4.9 across each gap, and got 0 of 4 (tester 2026-10-29: the guide pointed straight at the last
  Echoes of Elias). `JumpLinkCollector` now links each prefab's own ends (`MarkerInfo.Prefab`), nearest pairing only outside one.
- **Floors at different heights merge** in the top-down DRLG walk map: in `Cave_Flooded_FLEX_POI_GrottoEntrance_01` (the Snake
  Den's entrance, 3 ladders, 2 jumps, a duck-under) 4 of 6 traversals have no surface walk between their exits but a 2D walk of
  3–26 units, so routes skip the ladders and point across cliffs (tester 2026-10-29). Not handled: needs a 2.5D DRLG map, as §4.1b
  notes for story areas.

### 4.1d Wall-less tiles: floor islands at the edges (offline, 2026-10-30, `CSD_Hawe_Verge_CorrosiveSwamp` 600744)

- The 14 `Hawe_Swamp_*` tiles have no walls: floor of 17–64 cells touches edges with no exit bit, in scattered islands.
  `Hawe_Swamp_FLEX_POI_SeaHagFight_01` (64×64, exits N S E, Tysa's clearing at (26,24)): its 2053-cell floor meets the north edge's
  west half, the south edge's west half and the east edge's south half; the north edge's east half (islands of 49 and 71 cells)
  and the east edge's north half (the 71-cell island) touch it only on islands.
- An exit side's floor runs become openings (`OpeningFinder`); one whose far cell holds no tile is a frontier, and the tile
  graph counts every opening of a tile reachable from all of it. The guide led the tester east to such an island exit
  ("unexplored", no way there). Now a frontier of the player's room that no walk reaches (jumps included, live barriers
  ignored) is spent while another opening of the room is walked to (`ExplorationGuide.SpendCutOffFrontiers`).

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
3. ~~Full `m_cellBytes` decode~~ done (§4.1b); the Appearance collision meshes are still undecoded.
4. World → tile-set mapping for a given dungeon (server-side); only observable via runtime scene SNOs.
5. Whether a given `DGN_*` regenerates differently every run (layout hash evidence so far is from PIT/Limbo lines).
6. Whether scenes are revealed all at once or progressively (per-scene `RevealSceneMessage` suggests streaming).
