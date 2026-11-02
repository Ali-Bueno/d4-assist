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
  `HelltideObjects`, Shift+F2 (`WaypointListBridge.OpenHelltide`). The helltide's events are kind 10 (marked as monsters):
  `SMP_Helltide_FallenAltar_Structure` 1926635 ("[ms]Gefallener Altar", a grammar tag before the Name),
  `SMP_Helltide_Structure_DemonicSpire` 1826983 ("Spitze der Verderbnis"); the doomsayer `NPC_QST_S04_Hawe_Doomsayer_Base`
  1818818 kind 8. Hell's prize led the user nowhere and is left out.
- **Projection checked on the gifts (H, screenshot at zoom 1100, 1920×1080):** all nine gifts, drawn as small white dots in the
  helltide's red area, lay within 1–2 px of `MapProjection.ToClient`. The game said a gift under the cursor at zoom 1100, not
  at 1934 or 3046 (the two farthest steps): there the dots are no hover target.
- **The map shows the world boss's countdown** at its top left ("Weltboss: 17 Min. 9 Sek."); the HUD shows the helltide's
  ("Höllenflut: 5:57").
- **Area names (H):** `SubzoneDefinition.snoTerritory` names the helltide's area: `Territory_Hawe_Wetland` "Auen von Fethis",
  its level areas "Lichtschwund", "Das Schlingmoor", "Ebenen des Verfalls", "Verseuchter Deich" (`ZoneCatalog`).
- **The active helltide region is the world state `UberSubzone` (H, live and game data, 2026-10-27).** World state 1055388
  `UberSubzone` in the session's world-state table (16-byte nodes {snoWorldState, nValue, next}, ~500 states) holds the
  base game's helltide region: the conditions `Bounty_Unlock_Condition_Helltide_<territory>` (group 51) test
  `UberSubzone == N`: **1 Scosglen** (Coast, DeepForest), **2 Fractured Peaks** (TundraN, TundraS), **3 Hawezar** (Verge,
  Wetland), **4 Kehjistan** (HighDesert, Oasis, LowDesert), **5 Dry Steppes** (Central, South); `EGB_UberSubzone_Inactive`
  tests 0. The expansions have their own state 2077464 `UberSubzone_Expansions`: **1 Nahantu** (LingeringHatred,
  SevenStones), **2 Skovos** (Celestia, Skartara); unset in build 3.2.3 (season 16 / 3.3.0 starts expansion helltides at the
  half hour). Live: 4 with the helltide in Kehjistan (2026-10-25 14h, 10-27 16h), 3 in Hawezar (10-25 15h, 10-27 17h), 2 in
  the Fractured Peaks (10-27 18h); 17:51–17:55 the node was absent and 17:56–17:59 it read 0 (the five-minute gap); the value
  changes on the hour and reads at once after logging in, anywhere in the world. The table belongs to the game session:
  after a disconnect it froze and a new one was built at another address (find it by the node of a known state). The
  `Bounty_Helltide_*` states are the world-wide bounty rotation, not the region. Not matched: `WE_Sanc_<region>` stayed 1
  for Hawezar after the 17:00 world boss there; `ZE_Sanc_<region>` did not follow the legion (helltides.com).
- **The world-state table is a node pool of several blocks (H, live 2026-10-28, build 3.2.3).** `[G+0xA58]` is the pool:
  `+0x28` → its block holders, 0x18 bytes apart, `+0x30` their count (u32, 5 here), `+0x18` the live states' count (775),
  `+0x10` the last node handed out. Each holder's first word is a block {first node, u32 count at +0x10, node size 16 at
  +0x14, end at +0x30}. The blocks held 60, 20, 10, 540 and 180 nodes; `UberSubzone` = 1 (Scosglen) only in the last,
  `UberSubzone_Expansions` = 1 in the 540 one although no Nahantu helltide runs before 3.3.0. 35 states sat in two blocks,
  each with the same value (810 nodes for 775 states). On 2026-10-27 the first block alone held every state read; reading
  only the first block on 2026-10-28 found the expansions' state without the base game's, and the helltide list named
  Nahantu. `WorldStateReader` reads every block (a state held twice: the later block's value, logged `WORLD STATE … twice`);
  `ActiveHelltides` counts the expansions' state only from build 3.3.
- **The helltide's bounties are the game's own table (game data, 2026-10-29, build 3.2.3).** The Global `bounties` (588203,
  `GlobalBountyData.arBountyGroups`) groups each territory's bounties under its unlock condition
  `Bounty_Unlock_Condition_Helltide[_]<territory>`; each `BountyData` names the quest, the world state and the value that put it
  up. Ten per territory (15 territories, 150): cull cultists (`Tier1_Kill`), cull demons (`Tier2_Kill`), cultist rituals,
  doomsayers, harbingers (`Harbinger`), Ironwolves, tortured gifts (`Chests`), cinders, demonic structures (`Structure`) and the
  Blood Maiden (`Succuboss`, quest Tier3, state Tier2). Only the Blood Maiden is the accursed ritual's altar (Baneful Hearts; the
  quest also counts the war plans' `Warplans_Helltide_Duriel_boss`); the others count kills, events, gifts, cinders or structures
  anywhere in the territory. Their map spots are `Bounty_Pin` markers of `<territory> (UberSubzone)`, shared: on the Scosglen coast
  one pin for gifts/cinders/structures, one for rituals/doomsayers/harbingers/Ironwolves, one for the culls, one for the Blood
  Maiden. Live 2026-10-27 16:15 the coast had `Tier1_Kill`, `Cinders` and `Succuboss` at 1, the others 0. Seasons' bounties (S10
  chaos rifts, S12 kill streaks, S14 ruptures) name their season in `tRequirementsToBeActive.arSeasons`, and their world states
  stay 1 out of season. War plans (Tyrael's "Helltide Harvest": open tortured gifts) change the helltide's content (node 3B
  Hell's Prize, Hellborne, Butchers, rats, Duriel's brood), not its bounties. `HelltideBountyCatalog`, Shift+F2.
- **Server messages in the definitions:** `WorldStateUpdateData {snoWorldState, nValue}`, `WorldEventStartingMessage
  {snoQuest, bIsWorldEvent, snoWorld, snoSubzone, snoReward, wpLocation}`, `WorldEventWarmupWarningMessage {snoSubzone}`.
  World bosses are quests `WE_WorldBoss_<name>` (Ashava 366701). Not pursued: the game's own world-boss announcement names
  several places (user, 2026-10-25).

## 4a. Treasure goblins on the map (game data, build 3.2.3, 2026-10-29)

- **Who (H):** the monsters of the family `Treasure_Goblin` (24 actors: `treasure_goblin` "Schatzgoblin", the named ones
  `treasure_goblin_gold` "Goldener Baron", `_obols` "Murl der Neugierige", `_gelatinous`, `_socketable`, `_resplendent`,
  `_crafting`, the Infernal Hordes' `BSK_treasure_goblin*` "Äthergoblin", the Undercity's `X1_Undercity_Treasure_Goblin` and
  the expansion's `X2_EA_*` "Portal-Streichbold", seasonal ones). All team 19; none sets a monster rarity.
- **The map (H):** every goblin has a map icon (`ActorUIData.hMinimapIcon`, 2609695548; the Undercity's 889161053). Most
  respect the fog of war and the reveal radius around the player (`fMinimapRespectsFoW`, `fMinimapRespectsRevealRadius`):
  shown only near the player. `X1_Undercity_Treasure_Goblin`, `X2_EA_Treasure_Goblin` and `X2_EA_Portal_Goblin*` are active by
  default with `flMinimapMaxVisRange` / `flMinimapDiscoveryRadius` 2048: shown from anywhere. Ordinary monsters
  (`cultist_melee_female_phys`) have no icon; the Blood Maiden has her own.
- **Subo (H, text; M, mechanism):** the bounty hunter's passive "Seeker" (`NPC_Mercenary_BountyHunter_Perk`) "reveals all
  enemies and materials in the area" through a buff (a script formula of 85, likely its radius): with him the map shows
  ordinary monsters too (marker kind 10). Goblins need no Subo.
- **Live (H, `--debug` logs of 2026-10-29/30):** a goblin's marker is in the client's map-marker list (dungeon-objectives.md
  §19) long before its actor streams in: first read 25-46 units off, last read 75 off, with Subo along on 2026-10-30 (his 85
  may be that range: M). The actor streamed only now and then within ~40. Flags 0x0030, sometimes 0x8030 (meaning unknown).
  So the app lists a marked goblin within 75 units, streamed in or not (`GoblinWatch.ListedUnits`, user decision 2026-10-30).
- **March of the Goblins (H, game data; not checked live, 2026-10-30):** the event quest `SME_MOTG_PE` (2237027, "Marsch der Goblins") has only
  hidden steps, no text, no marker and names no goblin; `LTE_MotG_Connect_RepTracking` (2647019) tracks the event's reputation. Nothing ties tracking a
  quest to a goblin's map icon, and the app reads no tracked quest. The event has its own spawner
  (`DRLG_Spawner_Special_treasureGoblin_MarchOfTheGoblinEvent`) and shrine (`SB_MarchOfTheGoblins_Shrine_Gold`, "Schrein der Gier", map icon, range 60).
- **Open (L):** the reveal radius itself (`flMinimapRevealRadius`, not found in `globals`), and whether the game's map draws
  a goblin as far off as its marker is read, with and without Subo.

## 4b. Whispers (game data + live world states, build 3.2.3, 2026-11-01)

- **Table (H):** the whispers stand in the Global `bounties` beside the helltide's (§4): groups under `Bounty_Unlock_{BaseGame,X1,X2}_OW_Whispers`,
  `..._Dungeons`, `..._WorldEventsAndPvp`. `BountyData` = {snoQuest, snoWorldState, nValue, eBountyType, snoBountySubzone, gbidBountyDefinition}. Spot and
  name come from the quest (`vecStartLocation`, `Quest_<name>`.Name: "Ernte: Caldeum", "Weltboss", "Legionsereignis"). All 721 kept lie in the open world.
  `WhisperCatalog`. Left out: PvP, `..._Breadcrumb`, `S07_*`, `Condition_And_Season*`, LNY and quests with `arSeasons`.
- **Grim favors (H):** GameBalance `BountyDefinitions` (`BountyDefinitionEntry.dwBountyPoints`), by `gbidBountyDefinition`, the hash of the lowercased
  entry name (h*33+c): Culling, Assassinate, LocalEvent_Tier1 1; Harvest, Ritual, ZoneEvent, LocalEvent_Tier2 3; Dungeon, WorldBoss 5. Whispers 470x1, 88x3;
  dungeons 144x5; world bosses 7x5; legions 12x3. In-game payout not compared yet.
- **What is up (M, live 2026-11-01):** a whisper is up while its world state holds its value (`Bounty_OWC_Tier1_Frac_TaigaW` = 2 puts up `Bounty_OWC_Frac_TaigaW_02`). 60 plain
  whispers read up, one area per region (Step_South, Frac_TaigaW, Hawe_Crossway, Kehj_Oasis, Scos_HighForest...). **World bosses' and legions' states read 1 at every
  place at once**, so they say where one can rise, not where one does; the list offers them all (user decision 2026-11-01). Not checked yet against the game's map, nor what a finished whisper does to its state.
- **Rotation (M, dumps of 2026-10-27):** whisper states change per region at no fixed clock: 17:10 Skovos, 17:19 Fractured Peaks, 17:56 Hawezar + Kehjistan, 18:00 Nahantu. No hourly rule like the helltide's.
- **End time (open):** `BountyDataMessage {tHeader, snoBounty +16, snoRewardItem +20, dwEndTime +24}` exists in the definitions. A whole-memory scan with the map closed found, for the 10 whispers looked at, only
  the static table (quest SNO beside its state and value) and quest-file copies. Scanned again 2026-11-01 with the map open, all 84 up quests: no {quest, reward item, ...} record anywhere (0 hits with an item SNO after the quest). Not found, so no minutes are spoken (user decision: never estimated). The game may fetch it only for a pin under the cursor; not tried. **Found later the same day, see below.**
- **End times in the heap (M, live 2026-11-01 17:35, map open, build 3.2.3):** 168-byte (0xA8) objects, first word of interest the vtable (RVA differs per run), then at their SNO field: quest SNO; +64 x, y, z floats (the spot); **+80 the end as unix seconds (u32)**, always a round five minutes. 87 read: whispers, dungeons, helltide bounties, Pit, Undercity, BSK, Harbinger; no legion (`ZE`) and no world boss (`WE`). Per region one end (Kehjistan 17:50, Nahantu 18:10, Skovos and Dry Steppes coast 18:30, Fractured Peaks glacier 18:40). The helltide's bounties read 17:55, which is the known end of a base-game helltide (§4), an independent check. Ended entries stay readable for a while (17:20 and 17:30 still there at 17:35), so a past end must count as gone. Earlier scans missed them by looking for {quest, reward item}. Compared with the game (H, 17:54): the cursor on a whisper said 56 minutes, and `Bounty_OWC_Hawe_Verge_01` read 18:50; the objects are rewritten in place as the rotation moves on (same addresses, other quests, 20 minutes later). `MapBountyTableReader` reads them (the SNO, 0x34 on -1, 0x50 on the end) and the whispers' list says the minutes left, rounded up. Not checked yet: whether they stay with the map closed, a pointer path. The game's cursor text `Flüstern verfällt in 12 Min.` (one UTF-8 string, at 0x1ABC5ADDFD8) did not change from 17:21 to 17:41, so it is the last text written, not a clock. Legions and world bosses: see the next point.
- **Event times in the heap (M, live 2026-11-01 18:31, map open, build 3.2.3):** an array of 0x50-byte objects: id (0x6344F or 0x20A204 for the two legion places, 0xAA8EF for the world boss; meaning unknown), 0x10DCC, another id, x, y, z floats, then three times as unix seconds (u64): warmup, start, a third. Read at 18:31: legions 17:40, 18:05 and 18:30 (the last at two places, (323,-424) and (2754,-1399)), warmup 601 s before the start, third time 180 s after it; world boss start 19:30 at (-2072,-600), warmup 18:29:59 (3601 s before, third time +120 s). What the third time means is unknown. Spots match the bounty quests' (`Bounty_ZE_Tier2_Step_SteppesOverrun` (-629,-1159) was the object for 17:15; the 808-byte record's +0xE4 points at its object). Legions read so far fall on a 25-minute grid: 14:20, 14:45, 17:15, 17:40, 18:05, 18:30. A coming legion's object is not seen before its warmup. The map's countdown is text too, `Legion: {c_highlight}{s1}` / `Weltboss: {c_highlight}{s1}` (a formatted `Legion: {c_highlight}1 Sek.` stayed after the start, so again the last written). They stay with the map closed (18:34), as do the whispers' ends. All five spots lie within 5 units of a `WhisperCatalog` quest of the right kind (`An_event_objects_spot_is_its_quests`). `MapBountyTableReader` reads them, `MapBountyWatch` keeps them; the events' list says the minutes to the start and lists an event at its spot from warmup to third time, besides the pins. The world boss of 19:30 and its place were confirmed by the game (user, 18:4x). Live 18:46 the map's table held `Kehj_DilapidatedAqueducts`, `Naha_Chakhir` and `Step_SteppesOverrun`, while the objects named only `Step_SteppesOverrun` (-629,-1159) and (2405,-193) for 18:55: the pins keep past legions and miss a coming one, so a legion now lists by its object alone (the place of the 18:55 legion matched the game, user). A legion's end object reads the daily reset (10:00 next day, as do PvP's), so its minutes left aren't said.
- **The map's bounty table (M, live 2026-11-01, map open, zoomed out):** an array of 808-byte records, 80 used, in the heap (seen at 0x2075D5245A4, gone from the scan with the map closed: quests then had 2-5 hits, open 8-46). Record, from its start: +0x04 x, y, z floats (the quest's spot); +0x80 quest SNO; +0x90 0x7F7FFFFF (FLT_MAX); +0x9C the quest SNO again; pointers at +0x2C and +0x350 change every few seconds. Held: 14 Assassinate, 12 LE, 9 OWC, 9 Dungeon, 8 Culling, 8 Helltide, 6 Harvest, 4 Ritual, 4 PvP, 1 each Pit, BSK, Undercity, and **2 of the 12 legions (`Bounty_ZE_Tier2_Hawe_HauntedWreckage`, `Bounty_ZE_Tier2_Skov_PirateInvasion`) and no world boss**. So for plain whispers the world states and this table agree (68 of 68 up in both, counting PvP and legions), and the table tells the coming legion's places apart.
- **Legion confirmed (H for one place, live 2026-11-01, event of 14:20):** the table held `Scos_Carrowcrest` and `Naha_Chakhir` at 14:13:53, and the game's cursor text at Carrowcrest named the legion (user), while helltides.com named no place yet. Before: `Hawe_HauntedWreckage` + `Skov_PirateInvasion` (13:xx, the last event: nothing there, user), none at 14:03. So the table names the coming event from a few minutes before it, and the last one for a while after. World states: `ZE_Sanc_Hawe 2->0`, `ZE_IslandDefense 1->0`, `ZE_Sanc_Scos 0->1`, `ZE_Sanc_SevenStones 0->1` at 14:10:03, ten minutes before; but they stay on the last event until then, so alone they mislead. `MapBountyTableReader` + `MapBountyWatch`. A second legion was named in time too: `ZE_Sanc_Step 0->1`, `ZE_Sanc_Restless 0->1` at 14:35:08, `Step_SteppesOverrun` in the table and the app's list by 14:36:30 (Dry Steppes, user: listed before its start, cursor on it). Which state goes with which legion: `ZE_Sanc_SevenStones` Chakhir, `ZE_Sanc_Restless` Crater. **World boss (M, boss of 16:00, user + helltides.com):** at 15:00:17 `WE_Sanc_*` changed, 60 minutes before, to `WE_Sanc_Step` = 3 and `WE_Sanc_Skov` = 3; at 15:06 the table held `Bounty_WE_Tier3_Step_Boss` and not the Skovos one. So a boss also lists where its region's `WE_Sanc_*` is set once one has a pin (`WhisperCatalog.Listed`). The state alone stays on the last boss (10-27). It is one boss: it sometimes rises at two places at once so that players without the expansions can reach it too (user 2026-11-01). Why Skovos has no pin is unknown. **Entries go soon after the event (M, 2026-11-01):** `Bounty_WE_Tier3_Step_Boss` was held 15:31-15:50 and gone at 16:00:56, right after the boss of 16:00 fell (user). `Step_SteppesOverrun` was held at 15:46 and gone at 15:50, when the next legion's `Kehj_DilapidatedAqueducts` came. Both are map reads, so the exact moment isn't known. No pointer path found from G (BFS depth 3 only hit unrelated objects); the heap scan takes about 10 s, then only the regions found are read. **Scan cadence (review of PR #3, 2026-11-01, not live-tested):** the first version scanned the whole heap whenever pins, ends or events were missing (events usually are), at each map opening and every minute, and forgot the regions at each world change; now the whole heap only when neither pins nor ends are where they were, every 2 minutes while no event is known (a legion's object appears 10 minutes before its start) and every 5 otherwise, with the regions kept for the game process, and reads only in the open world.

## 5. Open questions

- Which of these the live map actually shows, and how it marks locked waypoints (unlocked ones: `PlayerSavedData.
  arWaypointsActivated`, 64 level-area SNOs, not located in memory yet).
- The map cursor and view in memory (live): snap the cursor onto Kyovashad and scan for (−1398.3, 154.3).
