# Map points — waypoints and dungeon entrances on the world map (offline)

Research spike, 2026-10-11, local install build 3.2.2.73764, read **read-only** through CASC with `data/definitions.json`
(build 3.2.1.73552, still reads cleanly). Confidence: **H** verified on game data from several angles, **M** a few samples,
**L** inference. Reproduce with `D4DungeonDump --map-points [--locale enUS,esES]` (`GlobalMapPointReader`,
`LevelAreaNameReader`).

Purpose: a screen reader for the game's own map screen (players: dungeons and waypoints are hard to find). The design is in
`STATUS.md` (Map points row); this note covers the offline data only.

## 1. One global list of map actors (H)

- The Global SNO `global_markers` (332242) is the only one whose `GlobalDefinition.ptContent` is `GlobalWaypointData`. Its
  `arGlobalMarkerActors` holds 3,260 `GlobalMarkerActor` records: actor SNO, actor/gizmo type, world SNO, world transform,
  level area SNO, fog of war, subzones and a polymorphic `ptData`.
- By `ptData` kind: 53 waypoints, 656 portals, 1,064 portal destinations (arrival points, not map points), 220 hidden
  caches (shrines and caches, all overworld), 1,267 others (1,257 gizmo-type-86 "Graveyard" markers, ~10 icons/pins).
- 3,117 entries are in `Sanctuary_Eastern_Continent` (624 of the 656 portals); the rest sit in small hubs and hells
  (`Frac_Underworld` 27, `S03_Hub_TWN` 22, `Kehj_Hell` 21, …).
- Waypoint actors: 51 `Waypoint_Temp`, 2 `FirstBorn_Skov_WayPoint_A_01`. Example: Kyovashad at (−1398.3, 154.3, 101.0),
  level area `Frac_Kyovashad`.

## 2. Names (H)

- The map name of a level area is the StringList `LevelArea_<level area CoreTOC name>` (group 42), label `Name`; that list
  has its own SNO id, found by CoreTOC name. Text comes from the per-locale bundle `Base\StringList-Text-<locale>.dat`.
- A waypoint is named after its own `snoLevelArea` (Kyovashad; `Naha_Kurast_Bazaar` → "Kurast Bazaar" / "Zoco de Kurast").
- A dungeon entrance is named after `PortalData.snoPortalDestLevelArea`, not its own area (that one is the surrounding
  overworld): `Portal_Dungeon_Generic` at (−1240.7, 424.4) → "Conniving Tunnels" / "Túneles Confabuladores".
- Every waypoint and overworld dungeon entrance has a name. Ten points have none (the Caldeum "Gates to Hell" portal,
  2 `S15_Portal_Dungeon_Rifts`, 3 `S03_Portal_Dungeon_Vault`, markers in worlds without a CoreTOC name).
- `hZoneLabel` is a region label hash shared by several waypoints and `dwEntranceName` a small id; neither decoded (L: the
  region names are in the `Zones` table 163963, e.g. ZONE_NAHANTU).

## 3. Portals are a superset of what the map shows (M)

- Cellars: `Portal_Cellar_Flat` / `Portal_Dungeon_Cellar` (~190), `ePortalDestSubzoneType` 0. Dungeon-type portals: 546 with
  subzone type 2. Also town returns, hub portals, `*_exitportal`, Rifts, Tristram, Vault, Pandemonium, OWCSMP and retired
  or joke content ("Temple of Baaaaaaal").
- 30 positions hold more than one portal (Kurast's Temple, Ziggurat and Cave districts; the Horadric Vault's Dormitory and
  Foyer): 601 distinct destination areas for 624 overworld portals.
- `fHiddenMapIcon` hides some (`Hell_Nightmare_Mephisto_Portal`, "Vision of Sescheron").
- Nightmare-capable dungeons are not flagged directly. 261 portals have no `snoPortalDestObjectiveQuest`; archetypes
  1240235451 and 517241146 always come with an objective quest and subzone type 2, 3009254850 and 2338479632 never
  (2338479632 is always a cellar). Whether "has an objective quest" means nightmare-capable is unverified (archetype gbids
  not decoded).

## 4. Live events: helltide, world boss, legion (live, build 3.2.3, 2026-10-25)

- **Helltide objects are map markers (H).** In the helltide (Hawezar, `Hawe_Wetland`, player at (-513,533)) the client's
  map-marker list (dungeon-objectives.md §19) held, as kind 11: nine `Helltide_RewardChest_Random` 1858869 ("Gequältes
  Geschenk der Geheimnisse", UIName; Name adds the cinder cost) over the whole territory (`Hawe_WetlandNorth/South/Center`,
  ~450 units apart), `S4_PROTOTYPE_SummonLocPin` 1833920 ("Verfluchtes Ritual"), `Warplan_Helltide_HellsPrize_PreTorment`
  2582811 ("Preis der Hölle"), and the season's `S14_Rupture_SMP_*`; the helltide's monsters (`cultist_*_helltide`) as kind 10.
  In Zarbinzet (a town of the same region) and Temis none of them: the map marks them only out in the helltide's area.
  `HelltideObjects`, Shift+F2 (`WaypointListBridge.OpenHelltide`).
- **Area names (H):** `SubzoneDefinition.snoTerritory` names the helltide's area: `Territory_Hawe_Wetland` "Auen von Fethis",
  its level areas "Lichtschwund", "Das Schlingmoor", "Ebenen des Verfalls", "Verseuchter Deich" (`ZoneCatalog`).
- **The active helltide zone is not a world state (M).** A live table of 16-byte nodes {snoWorldState, nValue, next} holds
  ~540 world states (`D4MemScan sno-scan` + `words`). Every territory's `Bounty_Helltide_*` states dropped 1 → 0 at the
  helltide's end (14:55) and were set again at 15:00, for all regions at once: the bounty rotation, not the active zone.
  Other live values there: `SME_S08_BelialTide` counting 5..1 every 5 minutes, `LocalEvent_OverlandBoss_<region>`,
  `S07_WhisperRotation_<region>`, `RS_<region>`. Not found: the helltide's zone from afar.
- **Server messages in the definitions:** `WorldStateUpdateData {snoWorldState, nValue}`, `WorldEventStartingMessage
  {snoQuest, bIsWorldEvent, snoWorld, snoSubzone, snoReward, wpLocation}`, `WorldEventWarmupWarningMessage {snoSubzone}`.
  World bosses are quests `WE_WorldBoss_<name>` (Ashava 366701). Not pursued: the game's own world-boss announcement names
  several places (user, 2026-10-25).

## 5. Open questions

- Which of these the live map actually shows, and how it marks locked waypoints (unlocked ones: `PlayerSavedData.
  arWaypointsActivated`, 64 level-area SNOs, not located in memory yet).
- The map cursor and view in memory (live): snap the cursor onto Kyovashad and scan for (−1398.3, 154.3).
