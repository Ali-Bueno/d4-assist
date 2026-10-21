# Diablo IV dungeon objectives — which actors progress a dungeon (offline)

Research spike, 2026-09-28. Game build **3.2.1.73552**, local install read **read-only** through CASC,
parsed with the build's own type registry (`data/definitions.json`, same method as
[dungeon-data](dungeon-data.md) §1). Field offsets are relative to the definition body (after the 16-byte
Meta header). Confidence: **H** verified on game data from several angles, **M** a few samples, **L**
inference.

## 1. From the world name to the dungeon's objective quest (H)

- Each dungeon's objective quest is a Quest SNO (group 31) named `DPO_*` (161 of 3732 quests).
- The link is **not** on the World or LevelArea: it is the Global SNO `zone_progress` (group 21, id
  1004219), array `arWorldToDPOQuestList` of `WorldToDPOQuestList {snoWorld +0, arQuests +8}`.
  147 entries, all `DGN_*` worlds, one quest each (e.g. `DGN_Hawe_HeathensKeep` 843152 →
  `DPO_Hawe_HeathensKeep` 843450). 17 `DGN_` worlds and all `PIT_`/`CSD_`/`QST_` worlds have none.
- Parser caveat: a generic walk from the Global's root type returns 0 entries. Walked manually:
  24-byte elements, the element's array descriptor offset at element+16 is body-relative, quest ids are i32.
- `QuestDefinition.snoDPOWorld` (+336) and `.snoLevelArea` (+80) are empty on DPO quests, and
  `DRLGDungeonQuestEntry` does not exist in this build's registry.

Chain the app can follow: `FenrisDebug.txt` world name → World SNO → `zone_progress` → DPO quest.

## 2. Objective actors inside a DPO quest (H)

`QuestDefinition.arQuestPhases` (+136, `QuestPhase`, 144 B) → `.arCallbackSets` (+40,
`QuestObjectiveSet`, 72 B) → `.arCallbacks` (+24, `QuestCallback`, 296 B) → `tScriptMessageMap` (+16:
`eEventType`, `arEventFilters` +24 of `ScriptMessageMapFilter`, 32 B) → polymorphic `pMessageFilter`
(+8). The actor filter class is `ScriptEventParamActor.snoActor` (+24). Shortcut: every callback also
lists its SNO references in `arSnonameTokens` (+224, `DT_SNO_NAME[]`, group + id).

Across all DPO quests: 658 actor filters, 316 distinct actors. Event types (L/M, inferred from which
actors carry them): **0** = kill (bosses, minibosses), **32** = interact/operate (switches, doors, chests,
cages), **77/78** = carryable placed in a receptacle, **50** = enter a LevelArea.

More types (offline census of the 147 DPO quests, build 3.2.2, M): **19** = collect items (motes, `nNeeded` 18), **42** = N of the
callbacks beside it (the kill callbacks flagged 0x6), **51** = player in range of an actor, **52** = enter a trigger, **72** = kill
inside a level area, **74** = no filters (unknown). `QuestCallback.nNeeded` (+8) is the count; `eIndicatorType` (+88) is 2 for an
objective the game shows and 3 for a hidden one ([client-memory](client-memory.md) §4).

Examples:

| Dungeon quest | Objective actors (SNO, event) |
|---|---|
| `DPO_Step_GuulrahnSlums` | `DGN_Standard_Global_Human_Stake_01_Switch_Dyn` 1120667 (32), `DGN_Standard_Sitting_Skeleton_Switch` 1246560 (32), `ProtoDun_Jail_Door_A_01_Dyn_…` 1091691 (32), `ghost_caster_miniboss` 585342 (0), `cannibal_brute_Unique_…_PosessedCannibal` 1058248 (0) |
| `DPO_Naha_SleeplessHollow` | `DGN_Standard_LoreBook_Corpse_Skeleton` 1216430 (32), `cultist_summoner_dungeon_miniboss` 1617845 (0) |
| `DPO_Frac_KorValarRamparts` | `DGN_Standard_Prisoner_Cage` 1086042 (32), `DGN_Standard_DeadPrisoner_Cage` 1260903 (32), `Generic_Lorebook_Chest_01_Dyn` 1309361 (32), `DGN_Frac_KorValarRamparts_Door_Boss` 711624 (32), `knight_melee_frac_unique_…` 709872 (0) |
| `DPO_Hawe_HeathensKeep` | `vampire_boss` 237232 (0), enter `DGN_Hawe_HeathensKeep_Boss` 854916 (50), `knight_caster_…_HighInquisitor` 854770 (0) |

**Census of the actors DPOs name** (offline, build 3.2.2, 2026-10-05; 147 worlds, one DPO each, 272 distinct actors, H): 184
monsters (all hostile team 19; `druid_miniboss` 1000166 and `golem_bramble_hollow_boss` 2363345 also carry `IsNpc`), 41 doors, 20
chests, 15 switches, 4 carryables, 4 receptacles, 1 portal, and 3 **triggers** (gizmo 44), named only with event 52 "enter a trigger":
`Trigger_Sphere_8m` 42298 (6 DPOs, radius 4), `DRLG_Generic_Boss_Trigger_Sphere_20m` 1102733 (5 DPOs, radius 10),
`Trigger_BossRoom_Entrance` 243360 (1 DPO, radius 4). No effects, props or spawners. Event outliers: the door
`DGN_Step_DarkRavine_DemonStatue` 969149 is named with event 0 (kill), the monolith `MegaDemon_Monolith_01_Dyn_DGN_Step_GrinningLabyrinth`
1059548 with event 51 (player in range). The triggers looped as objectives until 2026-10-05: invisible, never "used", every copy of
the generic SNO in the dungeon, from every phase at once (tester: "objetivos invisibles que se quedaron ahí todo el rato"). They now
get no loop; the followed phase leads into them (map marker or quest place).

**Gaps:** the list is not complete. `DPO_Hawe_HeathensKeep` does not name its boss door
(`DGN_Hawe_HeathensKeep_Lockable_Boss_Door` 854889). Generic dungeon events are separate quests with no
actor references. Examples: `DGN_Standard_NPC_PrisonerAndStake` 991162, `…_PrisonerInCage` 1086039,
`…_Prisoner_Follower` 1892247 and `…_PrisonerInTrance` 2329531. Their actors are spawned by the server.

## 3. Classifying any actor SNO (H for fields, enum names are derived from the gizmo data class)

`ActorDefinition` (group 1, 62,490 actors):

- `eType` (+8): **1** monster/NPC (has `ptMonsterData`/`ptNPCData`; prisoners and bosses), **2** gizmo,
  3/5 client effects and static props (M).
- `eActorGizmoType` (+392, −1 for non-gizmos). The data class at `ptGizmoData` (+400) names each value.
  `definitions.json` itself has no enum strings.

| Gizmo type | Data class | Seen on |
|---|---|---|
| 0 | `DoorGizmoData` | `DRLG_Blocker_*`, boss doors, monoliths, `DGN_Standard_Prisoner_Cage` |
| 1 | `ChestGizmoData` | lore-book chests/corpses |
| 2 | `PortalGizmoData` | portals |
| 11 | `ShrineGizmoData` | shrines |
| 23 | `BreakableContainerGizmoData` | breakables |
| 28 | `SpawnerGizmoData` | spawners |
| 44 | `TriggerGizmoData` | triggers |
| 48 | `DestroyableObjectGizmoData` | destroyables |
| 57 | `SwitchGizmoData` | levers, `DGN_Standard_MoteJar_01`, `Human_Stake_Switch`, `ChainReleaseWinch`, `SacrificePillar`, `DGN_Switch_Standard_NPC_Prisoner{Entangled,InRubble,InCaveBug}_01` |
| 94 / 95 | `CarryableGizmoData` / `CarryableReceptacleGizmoData` | `Carryable_*` / `Receptacle_*` (StoneCarving, Bloodstone, AncientsStatue, Mechanical) |
| 98 | `SignGizmoData` | signs |
| 100 | `QuestSwitchGizmoData` | campaign bound prisoners (`QST_Kehj_Tide_Stake_BoundPrisoner`) |
| 105 | `QuestChestGizmoData` | quest chests |

`dwFlags` (+12) / `dwFlagsEx` (+16) do not separate quest or interactable actors.

**Carryables in hand** (live 2026-09-30, `Carryable_HolyRelic_QST_Frac_Glacier_Cursed_01`/`_02`, M): picking one up
removes the ground actor and creates a new RActor of the same SNO that follows the player (0.1–1.2 units away). That
copy has **no ACD** (RActor +0x0C = −1), like the weapons the player wields; the app gives it no loop. Not yet checked
by a dump: that the ground copy has an ACD (inferred: picking it up goes through the server, and the altar, a
receptacle, read quest flags).

**Receptacles** (same quest, H for the data, M live): `CarryableReceptacleGizmoData.arCarryables` (+208 in the gizmo
data, a DT_SNO array) lists the carryables a receptacle takes (`Receptacle_HolyRelic_QST_Frac_Glacier_Cursed_01`
1240714 ← `Carryable_…_Cursed_01` 1240645, `_02` 1240716 ← 1240647); `nMaxCount` +224. The empty altar was
`Gizmo_Is_Quest_Restricted` without `MinimapActive`, so the quest-step rule left it silent while the player carried its
idol. Filled, it holds `Carryable_Receptacle_Count` = 1 and `Gizmo_Has_Been_Operated` = 1.

Each new idol spawns on `Receptacle_HolyRelic_QST_Frac_Glacier_Cursed_Dummy` 1249716, a bare switch (57) at the altar
that stays quest-restricted **and** `MinimapActive` after the pickup, yet the player can't use it. Its SwitchGizmoData
holds no non-operable mark: no power, zero radius and use time, the same as ~110 other switches, some of them usable
(offline diff, 2026-09-30). Only 4 switches are named `*Dummy*`. The app: while the player holds a carryable,
delivering it is the step. A receptacle that takes it is the quest step even when quest-locked, and falls silent once
operated. Every other quest step waits. Unseen: whether the Dummy stays on the minimap once the idol is delivered.

**Prisoners:** 94 actor names contain "Prisoner". 62 are free-able NPCs
`DGN_NPC_<Region>_Prisoner_{F,M}_0N[_Invulnerable]` (eType 1, NPC data). No quest references them, so they
are recognised by name + eType 1. The switch-gizmo variants listed in the table above are recognised by
gizmo type 57.

## 4. Consequence for the assist

- Offline, per dungeon: an allow-list of objective actor SNOs taken from the DPO quest.
- Offline, for any actor: a category taken from `eType` / `eActorGizmoType`, with a name rule only for the
  prisoner NPCs.
- Live: actor SNO and position come from the RActor pool at `[G+0xB78]` ([client-memory](client-memory.md)
  §4), which was derived offline and is not yet validated live. The "still usable" signal (prisoner freed,
  lever pulled) is unknown: `D4MemScan actors --watch` diffs the actor's slot while the player uses it.
- Untested: whether tile MarkerSets place any of these actors statically.

## 5. Undercity floor exits (offline + live, 2026-09-28)

- **Exit actor:** `X1_Undercity_WarpPad` (Actor 2108010, gizmo 57 Switch). Confirmed live on all three floors
  of `X1_Undercity_BugCave`: the player walked onto it and the world changed. Its companions are
  `X1_Undercity_PortalSwitch` 2088206 and the minimap icon `X1_Undercity_AreaExit_MapIcon` 1981923 (eType 4).
- **Static placement (H):** the pad is a prefab marker (markerset 2054412; 2088442 `_Boss` on final floors)
  in the tile layers `<tile> (X1_Undercity_AreaExits)` / `(X1_Undercity_BossExits)`. It appears only on the
  dead-end tiles `*_E_01` and `*_S_01`. Examples of scene-local positions: `Cave_Bug_E_01` 397246 (17.0, 23.0);
  `Sewers_Kurast_E_01` 1659744 (16.0, 25.5). Live floor 1 placed the pad at exactly that point of its
  `Sewers_Kurast_E_01` tile.
- **Not usable alone:** a floor has several E_01/S_01 tiles (floor 2 of one run loaded 6), and the active
  layer is not in the scene's own `arLayers`. Only the live actor tells which one is the exit.
- **Visibility (live):** the pad RActor appeared only when the player was within 22–34 units, while other
  props were visible up to ~70. Quest `X1_Undercity_WarpPad` (31/2053962) has a player-in-range trigger
  (`ScriptEventParamFloatRangeInside`), so the pad is probably activated on approach. Sighted players
  therefore also find it by exploring.
- **Ziggurat_02 (live 2026-09-29):** all 14 tiles explored, no frontier, and the pad never appeared although the player passed 1.5-3.4 units from the pad spot in all three
  pad-capable dead ends; unknown whether it spawns later or the floor continued unseen. The guide now re-checks the exit-candidate tiles (20 scenes / 26 layers in this build) and `--debug` logs `ACTOR seen:` lines.
- **Pit:** no static portal marker. Its portals and guardian are spawned by the server (L).
- **Attunement "enticements" (offline census 2026-10-01, M):** what the user calls the spirit braziers. All are eType 2,
  gizmo 57 (Switch): `X1_Undercity_Enticement_SpiritHearth_Switch` 2036342 (summons elites; single-use,
  `fSwitchCanOperateMultipleTimes` 0), `S11_Undercity_Enticement_AndarielSpiritHearth_Switch` 2440216 (same, season 11),
  `X1_Undercity_Enticements_SpiritBeaconSwitch` 1995876 (holdout, own quest 1995815, multi-operate 1). They sit in
  `X1_Undercity_SMPEnticement_*` marker sets with a `X1_Undercity_Enticement_MapIcon` (eType 4, `_Inactive` after use) and
  elite spawners; no tile layer, quest or world references those sets, so the server places them (count per floor: live).
  No DPO names them. The app loops them by name token `Enticement` on a switch (`ObjectiveReason.Enticement`). The
  attunement chests `X1_Undercity_Chest_Attunement0..4` are chests (gizmo 1). `X1_Undercity_Brazier_MarkerLocation_Lit`
  2093221 is entrance/town decor.

## 6. Doors, chests and readables (offline census, build 3.2.1.73552, 2026-09-29)

Census of all 62,490 `ActorDefinition` records. 1,087 are TACT-encrypted (unreleased content); `SnoStore` treats
them as missing. Gizmos (`eType` 2): 22,725.

| Gizmo type | Count | What it holds | Assist loop |
|---|---|---|---|
| 0 `DoorGizmoData` | 383 | 112 `DRLG_Blocker_*`, small doors, gates, portcullises, boss doors, cages. Also 13 `*StartOpen*` arena doors | door (wood / metal / stone by name), except `StartOpen` |
| 1 `ChestGizmoData` | 954 | chests, weapon/armor racks, lootable corpses, clicky rocks, loose stones, harvest nodes, `Generic_Lorebook_Chest_01_Dyn` | chest; `*Lorebook*` → readable |
| 19 (class unread) | 11 | `LE_`/`ZE_Chest_Reward*` event chests | chest |
| 106 (class unread) | 88 | `*_Chest_Rare_Locked_*` key chests, `usz_rewardGizmo_*` | chest |
| 98 `SignGizmoData` | 1,399 | lore books, notes, notices, `QST_*_Lore_*`, `X_Collectibles_*` | readable (scroll) |
| 29 (class unread) | 1 | `POI_Look_At`, a "look" (mirar) spot, usable again and again (live 2026-09-30, Darkened Way; ACD `Gizmo_Has_Been_Operated` set after a look) | point of interest (scroll, never silenced by use) |
| 105 `QuestChestGizmoData` | 125 | quest chests, jugs, corpses, pages | objective (quest item), §3 |
| 23 `BreakableContainerGizmoData` | 603 | baskets, jars, `*AnyLoot*` arrangements | none: broken, not opened |
| 48 `DestroyableObjectGizmoData` | 14,616 | crates, barrels, urns, cabinet doors | none |
| 110 (class unread) | 17 | boss chests mixed with summon altars | none yet |

- **Bodies:** 170 chest and 94 readable gizmos are bodies, named `*Corpse*`, `*Remains*` or `*Body*`
  (e.g. `corpse_knight_melee_frac`, `TWN_Frac_Margrave_Sign_MauledCorpse`). They loop the corpse sound.
  `Dead` is not used as a token because it also names quests (`QST_Skov_DeadWeight_*`).
- **Door material:** `DoorGizmoData` has no material field. The assist picks the sound from the name:
  Gate/Portcullis/Cage/Bars/Grate/Iron → metal, Stone/Crypt/Tomb/Statue/Blocker → stone, else wood. D2R derives
  it from the game's own open sound; the D4 equivalent (animation/sound data) is not traced.
- **Other door fields** (`DoorGizmoData`, 280 B): `fGizmoStartsDisabled` +24, `flOperationRadius` +40,
  `tLockData` +208, `fGizmoPlayerOperable` +244, `fPlayerCanCloseDoor` +252, `fMonsterCanOpenDoor` +256,
  `flOpenDuration` +264. None gives the live open/closed state.
- **Doorways are baked walkable.** For 202 door markers found in scene MarkerSets, the 1-unit walk map is walkable
  at the door (140 cases). The 19 blocked cases are props offset in Z, and 43 fall outside the tile footprint.
  The same holds under `DRLG_Blocker_*`. Example: `Mage_Halls_NSEW_01 (DGN_Frac_LostArchives_MainRoom)` has one
  blocker per exit, 3.5 units in from each tile edge. So an **open door is already an exit** for the radar and
  the room announcements, and a **closed door is invisible** to them. D2R instead reads the game's DOOR collision
  bit. Matching it here needs the live open/closed state plus the door's footprint.
- **Door footprint:** `ActorDefinition.aabbBounds` @220 (centre @220, half-extents @232, actor-local). The thin
  axis is local X (the passage direction), the width is local Y. Examples:
  - `ProtoDun_Door`: half-extents (0.25, 1.2, 1.4)
  - `Prison_Prop_Portcullis_01_Dyn`: (0.5, 2.1, 3.75)
  - `DRLG_Blocker_Mage_Halls_01`: (0.675, 2.0, 2.9)

  Rotate by the placement quaternion (RActor +0x750, [client-memory](client-memory.md) §4).
  `ActorCollisionData` holds the same default cylinder for every door, so it is useless here.
- **Live "used" state:** RActor `+0x148 != 0` for clickies (snow piles, backpacks) and doors
  ([client-memory](client-memory.md) §4). **Real chests don't show it** (live 2026-09-30, `ProtoDunKeep_Chest_01`
  356000, `D4MemScan actors --watch` while opening): `+0x140`/`+0x148` stayed `0x4046D66`/0. Instead, a closed chest
  carries a glow effect actor 0.015 units from its centre (`vfx_genericChestGlow_hardpointLight_square` 1357946 and
  `fxKit_chest_genericWedgeRectangular_glow` 1839569), and the game removes it on opening. The game data has 8 such
  actors (`*chest*glow*`, eType 3), referenced only by particles, marker sets and appearances, never by a chest's
  ActorDefinition: the server spawns them, so the kinds that glow are learned live and saved (`d4assist-chests.json`).
- **Re-creation:** chests left the actor list at 16–18 units and came back at ~15 under a new id (same slot, new
  generation), with their state words reset. The app therefore remembers a used gizmo by SNO + spot for the world
  visit (`Game/GizmoUsedSignal`).
- Levers, switch prisoners and blockers: still unchecked (blockers vanish about 10 s after opening).

## 7. Portals, level transitions and the Worldstone (offline, build 3.2.1.73552, 2026-09-30)

- **Portal class (H):** `eActorGizmoType` 2 = `PortalGizmoData`: 227 actors, 209 named `*Portal*`. Switch-type
  pads (gizmo 57) do the same job: `X1_Undercity_WarpPad`, `First_Khazra_WarpPad_*`, `TWR_ExitPortalSwitch`,
  `S14_DRLG_Chamber_PortalSwitch_Exit`. The app's rule (`Game/PortalClassifier`): gizmo 2, or gizmo 57 named
  `*Portal*`/`*WarpPad*`, never `*TownPortal*` (`Portal_Quest_TownPortal` 470136 is gizmo 2).
- **No destination to tell "on" from "back" (H):** `CommonPortalGizmoData` (PortalGizmoData +208) holds
  `eWorldToChoose` +16, `snoSpecifiedWorld` +20, `snoPortalDestLevelArea` +24, `dwEntranceNameToFind` +28.
  117 portals use 7 (generic, e.g. `Portal_Dungeon_Generic` 152702, `Prefab_Portal_Dungeon_Generic` 1839980) and
  the dynamic ones use 0 with world −1: the server resolves both. Markers carry no destination either. The app
  therefore calls a portal "used" when the player appeared or vanished within 15 units of it (`Game/PortalMemory`).
- **Where generic portals sit:** `Prefab_Portal_Dungeon_Generic` in the tile layers of 180 `*_Entrance_01`
  tiles, `Portal_Dungeon_Generic` in 39 more, and about 30 POI, boss-room and arena tiles.
- **`S15_Triad_B_Worldstone` (2638399, H):** one DRLG world with five DRLG level areas (`S15_Triad_B_Worldstone`
  2638402 and `_01`–`_04`) plus `_01_Boss`, `_03_Boss`, `_04_Boss`. The floor warps stay inside the world
  ([runtime-signals](runtime-signals.md) §2) and no floor-to-floor portal actor is placed in client data (M: most
  likely a generic portal placed by the server). Preset tiles `Triad_B_ThroneRoom_*` and
  `Triad_B_WorldStoneChamber_*` hold only start and checkpoint markers. `S15_WorldStoneChamber_Entrance` 2609366
  (gizmo 2) is placed by marker set `S15_TriadB_WaveEvent` 2608075 (quest 2608081, level area `_04`): the throne →
  chamber portal most likely appears with the wave event (M). After Baal, `S15_Triad_B_Boss_QST` 2671537 places
  `S15_Portal_ToLibrary` 2669439 (gizmo 57).
- **Objectives without DPO (H):** the world has no `zone_progress` entry. Its quest is named after it
  (`S15_Triad_B_Worldstone`, 31/2638473): `S15_Arreat_Summit_Altar_A_Gizmo` 2647200 (the Ancients' altar, floor 01),
  the Ancients Korlic 2679638, Madawc 2679640 and Talic 2679642, Baal `S15_Triad_B_Boss_QST` 2671540,
  `S15_TriadB_QST_Soulshard` 2671217 and Tal Rasha 2671529. `DungeonObjectiveReader` falls back to that quest, so the
  altar and the soulshard get objective loops (monsters never do).
- **Still to confirm live:** the real floor-exit actor names (`--debug` logs `PORTAL seen` and `ACTOR seen`), and the
  distance at which the player appears from the portal they came through (the 15-unit "used" radius).

## 8. Story switches and quest corpses (offline, build 3.2.1.73552, 2026-09-30)

- **Blood petals (H):** 26 actors are named `*BloodPetal*`. The interactable ones are switches (gizmo 57):
  `Frac_Prologue_BloodPetals_NoSymbol` 1096320 (Nevesk), `Ancients_Prop_Lilith_BloodPetal` 761338,
  `..._BloodPetal_Multifire` 991227 and `IGC_Prop_Lilith_BloodPetal_Persist` 1279804. `QST_Frac_LilithBlood_BloodPetals`
  1457636 is a QuestChest (105), and the `QST_X2_Skov_*_Trigger_Sphere*` ones are triggers (44).
- **No data field tells a story switch from a lever (H):** 1096320 matches `GizmoBlockout_Lever_A` field for field:
  - dwFlags 14680072.
  - No on-click effect and no operate power.
  - `fSwitchCanOperateMultipleTimes` 1.
  - It is placed by the prologue base MarkerSet, not by a quest `_Subzone_` set.

  Candidate rules over the 847 gizmo-57 actors:

  | Rule | Matches | Problem |
  |---|---|---|
  | dwFlags 0x800 | 22 (lootable villager corpses) | misses the petals |
  | on-click effect | 111 | also hits generic props |
  | `SoundGroup_QUE_*` on-click | 13 | too narrow |
  | name `QST_`/`Prologue`/`Quest` | 226 | too broad |

  The app therefore goes by name: `ObjectiveClassifier` makes a gizmo 57 an objective when its name holds `QST_`,
  `Quest`, `Prologue` or `BloodPetal` (e.g. `Frac_Prologue_RitualBonfire` 669960, the prologue brazier). A switch is
  always interactable, so the 226 name matches are quest switches rather than false positives.
- **Quest notes (H for the census, 2026-09-30):** the same name rule makes a sign gizmo (98) an objective: 808 of the
  1,399 are named `QST_` (e.g. `QST_Frac_Glacier_Zak_Triptych_01`–`_03`, the Glacier idol quest's messages, which
  looped the scroll sound before), plus `Quest_Frac_Prologue_Lorath_Clickies_*`, `Prologue_SignClicky_LorathClue_*`
  and other prologue story props. Live, the Triptych carried no quest flag and no `MinimapActive`; reading it sets
  `Gizmo_Has_Been_Operated`, which silences the loop. Town notes (`TWN_*_Sign_*`) stay readables.
- **Quest corpses (H):** 166 of the 954 chest gizmos (1) are bodies (`Corpse`/`Remains`/`Body`). Their marks:
  - dwFlags 0x800 marks 126 (124 bodies).
  - A `SoundGroup_QUE_*` on-click marks 131 (127 bodies); `Prologue_RitualCorpse_Decapitated` 652894 plays
    `SoundGroup_QUE_FracPrologue_RitualCorpse_Interact`.
  - No glow actor exists for them: the 8 `*chest*glow*` actors are chest VFX only.
  - Nothing in the definition says "used", so the signal has to come from memory (client-memory.md §4).

## 9. Friendly NPCs (offline, build 3.2.1.73552, 2026-09-30)

- **Signal (H):** `ActorMonsterData.fIsNPC` (the block behind `ActorDefinition.ptMonsterData`) is 1 on 3498 of the 7510
  eType-1 actors. It covers the prologue monk, Vani, Oswen, Lorath, Neyrelle, Donan, the `TWN_*` vendors, blacksmiths,
  healers, followers, escorts and prisoners. Presence of `ptNPCData`/`ptVOData` and `dwFlags` separate nothing.
- **Hostile stand-ins (H):** 188 fIsNPC actors are on `eDefaultTeam` 19, the hostile monsters' team, e.g.
  `Monster_Frac_Prologue_Vani`. Friendly NPCs are on team 0 (neutral), 2 (friendly) or 1 (critters, prisoners).
  `fIsNPC && team != 19` leaves 3310.
- **Walk beside the player:** hired mercenaries (`snoMonsterFamily` `NPC_Mercenary`) and cinematic stand-ins (`IGC_*`).
  The app leaves those out (`Game/NpcClassifier`), but keeps quest followers and escorts: some must be followed
  (user, 2026-09-30).
- **Not covered:** player summons, pets, mounts and ambient animals (fIsNPC 0).
- **Live check:** `NPC_Frac_Prologue_Monk` was picked at 1.9 units in Nevesk (2026-09-30).
- **Only NPCs the player can use (live 2026-09-30, IllusionWoods, M):** the ACD tells them apart. The Bloodied Wolf to talk
  to (`NPC_Wolphisto_Beast`) held `NPC_Has_Interact_Options` = 1, `Conversation_Icon` = 2 and `Conversation_Icon_Quest` =
  577793. The quest's hell spirits (`QST_Frac_GV2_Wolphisto_HellSpirit_*`), which offer nothing, held `Conversation_Icon` =
  0 and no interact options; all four had `NPC_Is_Operatable` = 1, so that one doesn't separate them. The tap keeps NPCs
  with any of the three set. Unchecked: vendors, townsfolk gossip, escorts (the Vessel of Hatred light carrier).
- **A door with no use mark (live 2026-09-30, M):** `Hell_Fort_Door_A_01_Dyn` 1247789, which the player couldn't use,
  held only `Gizmo_State` = 0. Its DoorGizmoData matches usable doors field for field (`fGizmoPlayerOperable` 1, not
  starting disabled, no condition). Level scripting keeps it shut, so it still loops.

## 10. Boss-fight safe zones (offline, 2026-10-01)

An ally raises a bubble and the player must stand in it to survive the boss's big attack. The app loops a sound at it
from afar and falls silent inside (`Game/SafeZoneClassifier`).

- **Trigger size is readable (H):** `ActorDefinition.ptGizmoData` → `TriggerGizmoData.tTriggerData` (+208) →
  `TriggerData.tTriggerProxData.ptProxData`, a polymorphic array of `SphereProximityData.flRadius`,
  `CapsuleProximityData` (radius, height) or `BoxProximityData.wvOBB`. Checked on named triggers: `Trigger_Sphere_8m`
  r 4, `_16m` 8, `_32m` 16, `_5m` 2.5, so the name gives the **diameter**. Entered/pulse/exited powers at
  `TriggerData` +72/+76/+84. Read as `ActorDefinitionInfo.TriggerRadius` (a box: its narrower horizontal half-extent).

| SNO | Actor | Kind | Fight | Size |
|---|---|---|---|---|
| 1327442 | `QST_Hell_Battlefield_Donan_SuppressingBubble` | trigger (gizmo 44), team 2 | Hell battlefield, quest `QST_Hell_Main_01B_Battlefield` 1120730, boss `QST_Hell_Battlefield_MegaDemon` 1221013 | sphere r 5.25 (H) |
| 2440181 | `QST_X2_Lilith_Bubble_Caster` | monster, team 2, anim `QST_X2_Lilith_BubbleCast` | Skovos mindscape, quest `QST_X2_Skov_Skartara_Mindscape_01` 2418726, boss `…_CouncilBoss_TerrorDemon` 2519459 (wipe power 2558201) | none readable (power `QST_X2_Lilith_CastBubble` 2440191 has no formulas) |
| 972175 | `BCD_Trigger_EliasPalace_SafeZone` | trigger, team 1 | act 3 sandstorm ([campaign-routes](campaign-routes.md) §2) | box (5, 5, 5), half-extents (M) |
| 1277801 | `knight_brute_barrier_border` | effect (eType 3), team 0 | Vigo's dome against Lilith's Lament: act 1, quest `QST_Frac_Underworld2_02` 549310, world `CSD_Frac_Underworld_AncientMausoleum`, boss `grandfather_vampire_boss` 1244924 (its flood attack); also `QST_Caldeum_Knight_Brute_VGN` | ring ≈ 6.4 across from particle scale (L-M); no logic actor |
| 1095630, 1095786 | `CAP_Tier4_Scos_ArchivesofIssalia_SuppressingShrineTrigger` (+`_Entrance`) | trigger, team 1, player-only | Archives of Issalia: entry power nullifies the dungeon's health drain | sphere r 4 / 8 (H) |
| 1950387 | `X1_Raid_Switch_BetaMiniBoss_SafeZone` | switch (gizmo 57), team 0, 4 placed spots | Vessel of Hatred raid, beta-wing miniboss | none readable |
| 1905698 | `QST_Naha_Lingering_Light` | NPC, team 2, placed | "Lingering Hatred" 3 (Travincal): conditions `…_EruLightFollowCheck`, `…_Globe_of_Light`; darkness power `…_Travincal_Darkness` | none readable |
| 1706514, 2469089 | `CMP_Naha_GatesOfTheNecropolis_MovingLight`, `QST_Skov_RathmasArchive_MovingLight` | monster, team 2, spawned | light-buff escorts (`…_MovingLightBuff`) | none readable |

- None of the boss bubbles is placed by a marker: the server spawns them during the fight (H). Which fight each one
  belongs to comes from the quest data; that they shelter from the named attack is read from the names (M).
- **Vigo's dome (2026-10-01):** "Lilith's Lament" is the boss, not an attack. Vigo's power `knight_brute_barrier` 816018
  has no trigger; its effect group `knight_brute_barrier_v2` holds the boss's `lilithsLament_flood_*` effects, so it shelters
  from the flood (H for the link, M that the flood is the "wail"). Only the border effect can show where it is.
  `NPC_Follower_Vigo_Barrier_Trigger` 1045678 is another Vigo (the follower, `GaleValley`/`KorValar`/`Underworld_01`): a
  thin box (0.17 × 1.5 × 1.75, `DetectProjectiles` like the firewall), a cover that stops projectiles, not an area; left out.
- **Akarat's lights** are pickups, not shelters: §11. The Travincal braziers `Kurast_BrazierTravincal_Hatred_01` 1759569
  (switches; weather `…_BrazierSafeZone`) are left out until their lit state is known.
- **Rule:** not on the hostile team (19); a trigger named `SafeZone`/`Bubble`/`Suppressing` with its shape's radius; any other
  gizmo named `SafeZone`; an actor named `Bubble_Caster`, `Barrier_Border`, `Lingering_Light` or `MovingLight`; the last
  three with a fallback radius of 5 (shelter zones in the data measure 5–5.25: Donan 5.25, an Undercity
  safe-zone effect 5.25, `Hell_HoradrimBubble` bounds 5, Elias ±5). Inside = within the radius minus 1 unit.
- **Rejected:** the sandstorm visuals `environmentVFX_sandstorm_safeZone_*` (their trigger already loops), the Undercity snake
  boss pillars `S06_Undercity_SnakePillar_*` (line-of-sight cover), the conversation bubble `QST_Conv_Kehj2_03_Horadrim_Bubble` (eType 4), `npc_mephisto_bloodbubble` (captive Mephisto), enemy
  shields on team 19 (`NPC_QST_X2_Lucion_Bubble`, `…BubbleWrap_*`), `npc_Eru_spiritShield_bubble` (eType 3, one target).
  `Hell_HoradrimBubble` 1090010 (placed next to Donan, Lorath and Neyrelle) is on team 19 with the destructible barriers:
  role unknown, left out.
- **Live checks:** whether the client's actor list shows the spawned trigger and the caster; whether the actor position is
  the bubble's centre and the radius scales with the RActor scale (+0x76C); a box's half or full extent; the Lilith
  bubble's real size; whether the entered power shows on the player as a buff (an exact "inside" signal).

## 11. Boss-fight objects to reach (offline + web, 2026-10-01)

Things the player must go to during a fight, looped until gone or locked (`ObjectiveClassifier`, own settings cue each).
`--debug` logs `ACTOR seen` for `Akarat`/`Orb`/`Barrier`/`SafeZone` actors to confirm them live.

- **Mephisto's stolen skills (Lord of Hatred, campaign and "Echo of Mephisto"):** phase 2 opens by taking every skill and
  scattering them as orbs, walked over to take back; again in the "Eaten" phase (web: Icy Veins, Skycoach guides). Orb actor
  `Mephisto_Boss_P2_OrbActor` 2435143 (monster, team 19, one SNO for every slot); its pickup prop
  `enc_utility_playerPowerSeal_orb` 2665317 (eType 4, half-extent 1.6) and visuals `mephisto_boss_Orb*` (eType 3). Powers
  `Mephisto_Boss_P2_PlayerPowerSeal` 2396108 / `_v2` 2665956, `Mephisto_Boss_P2_OverrideSkill` 2396140 + `_1`..`_5` (one per
  slot); the action bar has `Storyboard_ActionBar_MephistoSkillSteal_*`. Bosses `Mephisto_Boss_P1/P2` 2139279/2220133,
  `UberMephisto_Boss_P1/P2` 2575338/2575340 (world `Boss_Skov_UberMephisto`). The app loops each orb until it leaves the
  actor list and speaks the theft at the first orb and the recovery 2 s after the last (`StolenSkillsAnnouncer`).
  Player-state signal (unconfirmed): the exe's attribute table has `Skill_Override_Active(_Any)`, `Power_Disabled`
  (exe `.rdata` strings are readable offline).
- **Akarat's lights (Vessel of Hatred, Harbinger of Hatred; user: "las luces de Akarat"):** no pool actor or trigger exists
  for `Harbinger_Akarat_HolyPool` 1791011 / `EGB_…` 2148005. The visible lights are `Akarat_Orb` 2004489 (+ `Akarat_Orb_Pillar`
  2005586 on the same spot), spawned by `Harbinger_Akarat_Holy_OrbPickup` 1703471: picking one up gives the player
  `Harbinger_Player_SuperSaiyan` 1572027 / `EGB_…` 2148055, which unleashes a holy barrage at the boss (`harbinger_hatredImmune`).
  Read as empowerment (M-L). Four `Harbinger_ShieldLoc` markers sit 5 units around `Harbinger_Center` (possible spawn spots, L).
  The app loops `Akarat_Orb` (an effect: no "used" signal; whether effects of this kind reach the actor list is unchecked).
- **Horadric Guardian, Keeper of the Cube (Lord of Hatred, quest `QST_Skov_Skartara_06_VaultBoss` 2420639, world
  `CSD_Skov_HoradricVault_Boss` 2556182):** the boss `QST_Horadric_Guardian_boss` 2301098 raises a barrier showing one Horadric
  sigil; the player uses the vessel with that sigil, then stands inside the barrier to break it (web: Fextralife). Vessels
  `QST_Skov_Skartara_WardBreakerShrine_Kulle` 2480807 (−10.7, −10.7), `_Rasha` 2481232, `_Fahd` 2481234, `_Cain` 2481230:
  switches, team 1, set `QST_Horadric_Guardian_Boss` 2329502, `fSwitchCanOperateMultipleTimes` 1, not starting disabled.
  Symbol → vessel: Zoltun → Kulle, Tal → Rasha, Iben → Fahd, Jared → Cain. The barrier and its symbols are effect groups
  (`Horadric_Guardian_harmless_Barrier_<Mage>`), not actors, so the client actor list can't tell the symbol. The app loops
  the vessels as reusable (silent only while locked). **Live check:** do the four vessels' ACD flags
  (`Gizmo_Disabled_By_Script`, quest restriction, `MinimapActive`) single out the matching one? `--debug` logs it as
  `LOOP stopped … cause=unusable` / `started`. No barrier loop yet (size unknown, 4–6 L).

## 12. Structures to break: hatred tumors (offline + live, 2026-10-01)

- **Live** (`CSD_Frac_Underworld_AncientMausoleum`, user: "son esenciales para avanzar"): a hatred tumor is
  `Frac_Cursed_Tumor_01_Corner_2x2_01_Multistage` (actor 1185647), eType 1 (monster), no gizmo, team 19 (hostile), monster
  family `Default`; its ACD has monster hit points (1.5) and no quest or minimap flag. Around it stand its breakable pieces
  `Frac_Cursed_Tumor_{Core,Base,Skirt,Tube,Zit,Small,Pus}_*_Dyn` (gizmo 48, destroyable, no ACD). **Destroyed**, the tumor
  stays in the actor list (it moved to a new RActor id) with **no ACD**; only client remains are left.
- **Offline** (build 3.2.2): 184 actors are named `*_Multistage`; the hostile ones are structures broken by attacking them
  (walls, barricades, doors, totems, blood boils, rubble, props such as Mage Halls mummies). Only **10** are named by a quest's
  callbacks (`arSnonameTokens`, all 3,733 quests read in ~0.1 s): the tumor (`QST_Frac_Underworld2_02`,
  `CSD_VGN_AncientMausoleum_CentralChamberLogic`, `QST_X2_Skov_Philios_Mindscape_01`), the Wardstone altar, Bishibosh's altar,
  the Temple of the Death Speaker's pillar bridge, Elias's door, the Sodden Pact remains, the Prologue Hollow Growth, the
  S07 gnarled cocoon, the Skartara sphinx cracked wall and the S11 Azmodan demon gate. No quest was tracked when the tumor
  was met, so the tracked quest can't be the test.
- **App rule** (`ObjectiveClassifier`, reason `destructible`): a hostile non-gizmo `*_Multistage` actor some quest names gets
  the quest loop; it falls silent once it has no ACD (destroyed). Live: two tumors looped (`reason=destructible`) and
  stopped as `gone` within seconds of being broken. Not yet heard: the other nine.

## 13. Barrier walls and the targets that open them (offline + live, 2026-10-01)

Live case: `DGN_Frac_Maulwood` (SNO 486082), objective "kill the sentinel to release the barrier". The guide kept leading
the player to unexplored ground behind a bone wall near the entrance, because the floor under it is baked walkable.

- **DPO** `DPO_Frac_Maulwood` 598972 names two kill targets (event 0, H): `bandit_ambusher_female_fire_unique_DGN_Frac_Maulwood`
  1393337 (phase 76, the sentinel the player fought; a unique elite named after the dungeon) and the structure monster
  `DRLG_Structure_Skeleton_SkeletalConstruct_Snow_Thick` 1045720 (phases 63 and 91; eType 1, team 19, family
  `Global_Structure`). No objective text is readable offline (the StringLists are empty in this install).
- **Census (H):** 147 DPO quests, 185 distinct kill targets (event 0): 115 named `*unique*`, 33 `*miniboss*`; 41 dungeons have a
  `DRLG_Structure_*` target. Only `DRLG_Structure_ProtoDunCrypt_SentinelStatues` (LubansRest) is named *Sentinel*.
- **No live mark (live, H):** the sentinel's ACD attributes differ from ordinary monsters' only by fight state
  (`Summoner_ID`, `Last_Time_Inflicted_Damage`, power effect sizes); nothing says "objective". What tells it apart is the DPO.
  A `Symbol_Quest_Proxy` actor stood 14 units away; its role is unknown.
- **The barrier:** three live actors at (48.6,-101): `DRLGKit_fxKit_harmless_barrierWall_bone_01` 726308 (eType 4, ptCollData
  present, collision shape box, nav behaviour 1, aabbBounds centre (0,0,2.5) half-extents (0.5,3,3.5): a wall 1 thick, 6
  wide, 7 tall) plus two pure effects without collision, `fxKit_harmless_persistentBarrier_B` 571328 and
  `fxKit_bone_barrierWall_bonewall_frags` 684342. No quest names any of them and no Maulwood marker set places them: the
  server spawns the wall and nothing in client data links it to its target (L for any other link).
- **Opening (live, H):** after the sentinel died, the player stood 6 units from the spot with the start locations listed 9
  units away: all three barrier actors were gone. So "listed" means standing.
- **Other barriers (offline, H):** 52 `*barrierWall*` actors (demonic 27 marker sets, fog 13, spirits 9, blood 6, bone 5, Khazra
  raid 18) share the same box (0.5,3,3.5) or (0.5,3,3); `OWV_Skeleton_BoneWallSpawner` (111 sets) are ambush walls.
- **App rules:** `ObjectiveClassifier` makes a monster the world's DPO names an objective (reason `KillTarget`, quest loop,
  a guide target once heard); other monsters stay silent. `BarrierObstacles` lays every non-gizmo `*barrierWall*` with a box
  on the 1-unit grid (the door footprint, rotated by yaw), remembers it while the client drops it far away, and drops it once
  the player is within 20 units of its spot without it; `BarrierBridge` hands the cells to the guide's routes
  (`NavigationBridge.SetBlockedCells`) and to the radar (wall cells).

## 14. Carry-and-place dungeons and the doors their receptacles open (offline, build 3.2.2, 2026-10-03)

- **Census (H):** 32 DPO quests ask the player to carry items to receptacles. Every `Receptacle_*` has `nMaxCount` 1, so "place 2"
  means two receptacles side by side: the placing callback (event 77/78) carries `nNeeded` = how many, the pickup callback event
  32 with flags 0x3.
  - Statues ×2 (`Receptacle_AncientsStatue` 466091 ← `Carryable_AncientsStatue` 466092): Hawe_AncientReservoir, Kehj_CollapsedVault,
    Kehj_HakansRefuge, Step_AncientsLament, Step_CarrionFields, Step_ShiftingCity.
  - Bloodstone ×2: AkkhansGrasp, FetidMausoleum, LeviathansMaw, BetrayersRow; ×3: PathoftheBlind, Frac_Zenith.
  - Stone carving ×2: HauntedRefuge, SunkenRuins; ×3: ChampionsDemise. Mechanical ×2: LostArchives, MercysReach, SanguineChapel.
- **Where:** Carrion Fields' pedestals are in `Step_Grassland_FLEX_POI_SilentMonolith_01 (DGN_Step_CarrionFields_SilentMonolith)`,
  25 units apart; Ancient Reservoir's in `Ancients_Flooded_FLEX_POI_BossRoom (DGN_Hawe_AncientReservoir)`.
- **Receptacles beside a door (H):** `DOV_Kehj_HakansRefuge_PedestalsAndDoor` 1199321 (`DRLG_Blocker_Protodun_South_01`),
  `DGN_Step_AncientsLaments_DoorandReceptacles` 1303486 and `DGN_Step_ShiftingCity_SaintsGate` 1093330
  (`DRLG_Blocker_Ancients_Sand_01`), `DGN_Frac_AnicasClaim_BossDoor` 1202279 (one receptacle), `DGN_Scos_Underroot_BlockerAndReceptacle`
  1988089, the S03 Infernal Device vault doors (four receptacles). Door to receptacle: 2.2–6.0 units. No DPO names these doors and
  their data says player-operable, not starting disabled: the script opens them once the receptacles are filled.
- **Live report (2026-10-03, tester):** in a two-statue dungeon the guide led to the pedestal instead of the second statue, and the
  door beside the pedestals looped the quest sound. The app (`Game/CarryGoals`): a receptacle leads the guide only while the player
  holds an item it takes (then in the quest tier), and a closed door within 8 units of a receptacle still empty, not named by the
  DPO, waits silent (quest-locked). Filled receptacles carry `Gizmo_Has_Been_Operated` (§3).

## 15. Boss summoners: monoliths (offline, build 3.2.2, 2026-10-03)

- **Placement (H):** each sits on the very spot of a boss spawner, in a `DOV_*` marker set no other set nests (server-placed):
  `SilentMonolith_DYN` 540576 (door gizmo 0) with `DRLG_Spawner_Skeleton_Boss_Monolith` (`DOV_Step_CarrionFields_SilentMonolith`
  466280, `DOV_Hawe_AncientReservoir_Boss` 691662; Halls of the Damned has its own spawner), `FrozenMonolith_DYN` 1039288 with the
  vampire boss spawner (`DOV_Frac_Zenith_FrozenMonolith`), `MegaDemon_Monolith_01_Dyn_DGN_Step_GrinningLabyrinth` 1059548 (starts
  disabled) and, as a switch, `DGN_Frac_LostArchives_Coffin` with the ghost boss spawner.
- **DPO naming varies (H):** Carrion Fields names none; Ancient Reservoir and Zenith: event 32, shown; Halls of the Damned: event 32,
  hidden; Grinning Labyrinth: event 51 (player in range).
- **Live report (2026-10-03, tester):** a monolith was silent until used, then "looped": most likely the boss (`skeleton_boss`, a DPO
  kill target) standing on the same spot. The app makes every door gizmo named `*Monolith*` an objective (reason `monolith`). A
  door-type objective counts as used only by the ACD marks (`Gizmo_Has_Been_Operated`, `Gizmo_State`), not by the RActor state word,
  which is validated on chests and clickables only. `--debug` logs `LOOP silent … cause=used|unusable` once for an objective silent
  at first sight.

## 16. Bosses a quest phase names, and monster rarity (offline, build 3.2.2, 2026-10-03)

- **Rarity (H):** `ActorMonsterData.eOverriddenMonsterRarity` (+532) holds only -1 (5,991 ordinary monsters), 2 (969 uniques,
  `DRLG_Structure_*`) and 4 (552 bosses and minibosses, e.g. `skeleton_boss`, `Frac_Prologue_demon_mega_miniboss`).
- **Quests naming monsters (H):** 1,459 callback tokens name a hostile monster, 716 distinct; a few are ordinary ones by the dozen
  (`zombie_brute_phys` in `QST_Naha_Ormus1_02`, event 5, need 2; `Flayer` in `SMP_Ambush_Swarm`). So the app loops a monster the
  followed quest's current phase names only when it is a hostile unique or boss (reason `killtarget`, quest tier); its remains
  (no ACD, as the tumor's, §12) are silent.
- **The first campaign dungeon (M):** `CSD_Frac_Prologue_AbandonedTower` 450383 (DRLG, no DPO, no quest named after it). Its quest is
  `QST_Frac_Prologue_Taiga_01` 449539: phase uid 0 opens `Frac_Prologue_Tower_Gate`, uid 8 kills `Frac_Prologue_demon_mega_miniboss`
  1227632 (shown, level area `CSD_Frac_Prologue_AbandonedTower_03`), uid 34 a corpse in `ProtoDun_Keep_FLEX_POI_BossRoom`. The boss
  comes from `Prologue_ManualBossFight` 652902, which no set nests: no offline position.
- **Elites and champions (open, 2026-10-19):** rolled at spawn, so no offline field tells them from ordinary monsters. The
  client binary (build of 2026-10-14) holds names to check live (`D4MemScan acd --names`, an elite next to an ordinary
  monster of the same SNO): attributes `Monster_Affixes`, `Elite_Engaged`, `Unique_Invulnerable`; nameplate templates
  `NameplateTemplate_Champion|SuperElite|Unique`, `Nameplate_Boss`; map markers `Marker_Hostile_SuperElite`, `Marker_Boss`
  (the marker list's kind 10 is a boss, §19). The app's enemy cue covers rarity 4 and 2 only until one is confirmed.
- **DPO phases:** the statue and monolith DPOs name actors by SNO only (no markers). Since 2026-10-04 the world's objective quests
  are `ActiveQuests` candidates (§18), followed whenever the module data holds their phase pair; otherwise DPO kill targets stay
  phase-blind (§13).

## 17. Fire-bowl puzzles: switches lit again and again (offline, build 3.2.2, 2026-10-03)

- `QST_Naha_TombOfAkarat_EruTrial_LightsOut` 1807947: 4× `QST_Naha_TombOfAkarat_EruTrial_Brazier` 1957286 (switch 57,
  `fSwitchCanOperateMultipleTimes` 1, team 1) with `BrazierVFX` markers 1808569. Base game: `SMP_LightsOut` 1257632 with
  9× `SMP_Brazier` 1257617 (same kind).
- 200 of the 847 switches are multi-use (`SwitchGizmoData.fSwitchCanOperateMultipleTimes` +248).
- **Lit signal unknown.** `--debug` logs every multi-use switch once seen and on each change:
  `SWITCH <name> sno=<sno> at (x,y): seen PreviousState=… Gizmo_State=… Gizmo_Has_Been_Operated=… Gizmo_Disabled_By_Script=…
  Operatable=… Disabled=…`, then `SWITCH … : <signal>=<old>→<new>` (`Game/SwitchStateLog`). Light and unlight one to find it.

## 18. Hub dungeons: side-dungeon portals and quests that hand over (live, build 3.2.2, 2026-10-04)

- **`S15_Triad_C_Travincal` 2638641 (H):** one DRLG world. A 7-tile hub (`S15_ZigOut_Naha_Trav_POI_*`) places four portals in
  its tiles' "Game" layers: `S15_TriadC_Portal_KurastSewers` 2647367 and `_FlayerDungeon` 2647355 (tile `__NW_01`),
  `_SpiderCavern` 2647347 (`NE_01`) and `_DuranceOfHate` 2647374 (`SEW_01`). Each one's `CommonPortalGizmoData` has
  `eWorldToChoose` 1, `snoSpecifiedWorld` = this world and its own `snoPortalDestLevelArea` (Sewer01 2638650, Flayer01 2638654,
  Spider01 2638652, Durance01 2638656). The side dungeons are level areas of the same world: warping in and out logs
  `World: back in … (same instance)`.
- **Tile portals (H):** tile origin + marker local position matched the live actor within 0.1 units for three portals (all the
  tiles were unrotated). Portals stream in about 25 units away, so a hub tile counted as visited long before its far portal was
  seen, and the guide then led back to the entrance. `ScenePortalReader` and `PortalMemory` now place portals at tile load.
  Durance's portal appeared only after the three side dungeons (M), so a placed portal still missing when the player stands
  within 12 units is dropped until it shows.
- **Objective portals:** the world has no DPO entry. The quest named after it, `S15_Triad_C_Travincal` 2605635, names all four
  portals, so they lead as dungeon objectives until used (`PortalMemory.ObjectiveDestinations`).
- **Hand-over (H):** the tracked `S15_QST_Main_03_TriadC_01` 2651255 phase 81 has one callback, whose event filter is
  `ScriptEventParamQuest.snoQuest` = 2605635, and names no object. Once the hub quest started, the module-data pair the app reads
  (`QuestPhaseLocator`, RVA 0x30643C8 in this build) switched from {2651255, 81} to {2605635, 8}. The game shows the sub-quest's
  phase in the parent's place, and the parent's phase is no longer found. Two kinds of quest are now "in progress here"
  candidates: any quest a phase of a tracked quest waits on (`QuestPhaseObjects.SubQuests`, recursively), and the world's
  objective quests (`DungeonObjectiveReader.QuestSnos`). Live, the app followed 2605635 through phases 8 → 9 → 16 → 19 → 30 → 27
  → 37 to its end, then the next main quest, 2651257.
- 2605635's phases:

  | Phase | What it names |
  |---|---|
  | 0 | the three side-dungeon portals (use), Khalim's parts, Vilehand |
  | 8 | `S15_Prop_Compelling_Orb_Altar_01_Dyn` 2597583 |
  | 9 | the Durance portal; entering Durance01 |
  | 16 | entering Durance03 |
  | 19 | `Triad_C_Boss_QST` 2670877 |
  | 27, 30 | `S15_Sankekur_NPC` 2643907 |
  | 37 | the soulshard 2671193 |

- **The Compelling Orb** (switch 57, `Hitpoints_Max` 0.001):
  - While it was the step, it held `Gizmo_Is_Quest_Restricted` = 1 without `MinimapActive` (`D4MemScan acd --match Compelling`).
  - It looped silent (`cause=unusable`) until the hub quest was followed. Named by phase 8, it is the quest step.
  - It replaced `S15_Prop_Compelling_Orb_Dummy` 2679384 at the same spot when phase 8 began.
- `D4MemScan quest --sno <quest>` lists each phase's `waits on quest …` and `--find` its live pair.

## 19. Quest objective markers (live, build 3.2.2, 2026-10-04)

- **The list (H):** the client keeps one doubly-linked list of map markers, 0xA0-byte nodes from a pool, for actors the map
  shows whether streamed or not: quest gizmos (levers, doors, portals), invisible quest triggers (`Trigger_Box_2m`,
  `Trigger_Sphere_8m`), quest NPCs, followers and their pets, the player, the mercenary, the boss, DRLG blockers, dungeon doors.
  In `S15_Triad_A_Labyrinth` 2624849: 21 nodes at 08:54, 11 after a rebuild at 09:10. `S15_TriadA_Door1` 2640816 was there at
  (−1115, −219) while the player was 800 units away and the door was no actor. No waypoint in this world, so whether overworld
  waypoints and dungeon entrances are listed is unknown; the dungeon portal `S15_Portal_ToLibrary` 2669439 is.
- **Node** (offsets from the node; next/prev first):

  | Off | Meaning | Conf. |
  |---|---|---|
  | +0x00 / +0x08 | next / prev (the last node's next and the first's prev are the head) | H |
  | +0x10, +0x18 | same u32 in every node, 19 → 21 when the list was rebuilt (generation?) | M |
  | +0x14 | marker id; not the ACD or RActor id; reissued when an actor re-registers (`gen << 19 \| index` is a guess) | M |
  | +0x1C | actor SNO | H |
  | +0x20 | position x, y, z (moving units update live) | H |
  | +0x2C | 0xA2252272 in every node (world instance id?) | L |
  | +0x38 | level area SNO (group 22) of the position | H |
  | +0x4C | TeamID (boss 19, player/followers 2, gizmos 1, a quest NPC 0; matches the ACD attribute) | M |
  | +0x50 | kind: 1 player, 2 follower/pet/mercenary, 8 NPC, 10 boss, 11 gizmo/trigger | M |
  | +0x54 | flags; 0x80 below | M |
  | +0x58 | game tick of the last update | M |
  | +0x68 | SNO of group 51 on the quest NPC (`S15_QST_OgdensSign_SnotspillVis`), else −1 | L |
  | +0x6C, +0x70 | portals: destination world and level area (`Portal_Hell_Boss`: 2624849, `S15_Triad_A_Hell_04` 2611573) | M |
  | +0x74, +0x78 | icon override: Texture SNO (group 44, `2DUIMinimapIcons*`) and frame, else −1 | M |
  | +0x88 | float seconds of the last move for moving units, else −1.0 | L |

- **Head (H):** a 0x68-byte allocation {+0 pool descriptor, +8 first, +0x10 last, +0x18 count}. The head H (what the nodes point
  back to) is that allocation + 8. The pool descriptor right before it (0x38 bytes) holds the chunk array, chunk count, node
  size 0xA0 at +0x20, and `0x600DF00D` at +0x34.
- **No pointer route (H):** process-wide pointer scans (heap and image regions, write-copy pages included) find nothing outside
  the cluster pointing at the head, its pool or the 0x18-byte block {pool, list, arena} before them. The only references are the
  first and last nodes, those sibling allocations and stale stack slots. 32-bit offsets: none. A 240,000-object BFS from G (depth
  4) reaches only the next allocation (a `make_shared` control block at H + 0x68, via G+0xB10 → +0x2E0), never the list, so that
  is adjacency, not ownership. The on-disk `.text` is encrypted, so the owner cannot be found statically.
- **Route = structure scan (H):** the head is in the startup arena of G's own heap reservation, next to job-system allocations:
  the AllocationBase of G is one 883 MB `MEM_PRIVATE` region, and H = base + 0x41DF70 = G + 0x24D230, so it is allocated once
  per process. Scan only that region for H with: [H−8] = pool P with u32 [P+0x20] = 0xA0; [H], [H+8] = first, last; [H+0x10] =
  count 1..20000; [first+8] = H; [last] = H; walking next from first returns to H after count nodes, all with the same +0x10.
  In a scratch tool that took 1.0 s to scan plus 0.9 s to check ≈ 4,500 doubly-linked candidates, with exactly one hit. A
  full-process scan (18.9 GB, 11.5 s) found the same single head. Resolved from scratch three times (about 09:08, 09:09, 09:13):
  always 0x1BD8041DF70. Base + 0x41DF70 can be tried first and validated (untested across restarts).
- **Liveness (H):** H stayed fixed from 08:54 to 09:13 while the nodes changed. Pool slots are reused and ids reissued
  (`Portal_ToLibrary` 0x805C4 → 0xC00A03 → 0xA80162 → 0xF80A0A). The boss node came and went with new ids (respawns), and the
  09:10 rebuild dropped the door, the levers, the triggers and `Portal_Hell_Boss`.
- **Which marker is the objective:** no node holds a quest SNO or an objective field; the quest phase names the actor SNO and
  the list gives its position. Hub quest 2625608: phase 91 names Door1 (and `S15_Warrior_NPC_Combat`), phase 134
  `S15_TriadA_Portal_Hell_Boss` 2641510, phase 0 `Triad_A_Boss_QST` 2669049 (the module pair read phase 0 from about 09:01).
  Flag 0x80 (M): Door1 had it (0xB4) while phase 91 named it, and had lost it (0x34) by 09:00. Both portals had it all along:
  `Portal_Hell_Boss` 0xB0 from when it appeared until the rebuild, though phase 0 had already begun, and `Portal_ToLibrary`
  0x80BC, never an objective. The boss objective never had it (0x430 / 0x8430). So 0x80 means "pinned on the map" (portals,
  and a gizmo while it is the objective), not "objective". Match the phase's actor SNOs instead.

## 20. Server-placed objectives and boss clone gates (live, build 3.2.2, 2026-10-04)

- **Levers (H):** `S15_TriadA_Lever1` 2640808 (hub quest 2625608 phase 49, by SNO only) is in no tile layer: the marker sets
  `S15_TriadA_Hell_Ambush1` 2641387 (two levers, 4.2 apart), `_Ambush2` 2641422 and `_Ambush3` 2641423 (one each) place them,
  and the server picks where those ambushes go. They are known only once heard, about 28 units away; a restart of the app used
  to forget them, so heard objectives not done are now saved with the exploration (`ObjectiveMemory.Export/Restore`).
- **The lair door** `S15_TriadA_Door1` 2640816 (phase 91) is in no marker set at all; its position was only in the map-marker
  list (§19).
- **Tile-placed quest gizmos:** a gizmo a phase names by SNO and a loaded tile places (Game/Shared layers) is an objective before
  it is heard (`QuestPlace.Ahead`); dropped unheard when the phase moves on. None in the Labyrinth; the earlier case is
  QST_Frac_GaleValley2_HoradricBook. Since 2026-10-05: near the player it is done only when its actor there is a silent objective
  (used) or, for a tile's copy the map doesn't mark, when no actor of its SNO stands there (never spawned). Until then a copy that
  loops as something else (a door) or not at all waits for the phase: the old "silent within 20 units" rule dropped those 20 units
  short of them. Triggers are not taken from tiles (a generic SNO is everywhere), only from the map list (§19).
- **The Echo of Diablo** (`Triad_A_Boss_QST` 2669049): while `MorluGate_Triad_A_Boss_terrorCloneSource` 2628237 gates stand
  (hostile monsters, 18 HP, not invulnerable, up to ~47 units away), the boss and its `Triad_A_Boss_terrorClone` 2628190 copies
  read ACD `Invulnerable` = 1. Breaking the gates ends it (user). `*CloneSource*` hostile monsters (2628237 and its `_jakarta`
  variant 2650056) loop as `clonegate` with the beacon's 60-unit reach; kill-target loops fall silent while `Invulnerable`,
  now read on bosses and uniques too.
- **Quest companions:** `S15_Warrior_NPC_Combat` 2628769 (`Follower_Quest` 2625608, `Is_NPC`, `Invulnerable`) is named by the
  phase and was looping as a kill target; a quest follower never loops now.

## 21. Key doors and sealed doors: Derelict Lodge (offline, build 3.2.2, 2026-10-16)

- **Report (tester, 2026-10-16, no log):** in `DGN_Frac_DerelictLodge` the guide kept leading to the locked door while the key
  lay on a villager corpse, turned back to the first area's corners once the door was open, did the same at the second area's
  sealed door (open once two monsters died), and never led on to the boss room.
- **World (H):** `DGN_Frac_DerelictLodge` 486112 (DRLG, no placed scenes), level areas 486114, `_02` 603366, `_Boss` 607913
  (named only by the quest), DPO `DPO_Frac_DerelictLodge` 604299.
- **Phases (H for the callbacks, M for the order, read from the pairing of callbacks):** #4 pick up `DGN_Frac_DerelictLodge_Key`
  1172070 (ev 18) by searching `Corpse_Frac_Villager_M_01` 528403 / `_F_01` 528405 (ev 32, need 5; gizmo 1, corpse sound);
  #5 use `DRLG_Blocker_Protodun_Keep_DGN_Frac_DerelictLodge` 1172068 (ev 32; door gizmo, half-extents (0.5, 2.2, 2.2));
  #6 enter `_02`; #0 kill `fallen_brute_cold_unique_DGN_Frac_derelictlodge_01` 742179 and `_02` 742180 (ev 42, need 2);
  #1 hidden lore corpse 1216430; #2 kill `fallen_shaman_miniboss_cold` 732608; #3 enter `_Boss` 607913.
- **The sealed door (L):** no quest actor names it. Either a door gizmo the game unlocks when the brutes die, or a server
  spawned wall as in §13. Unconfirmed until a log of this dungeon.
- **The boss room (H):** nothing offline places it (the DRLG graph is server-only, [dungeon-data](dungeon-data.md)); only the
  map-marker list (§19, +0x38 level area) or the phase's enter trigger could locate it live.
- **Cause in the app (M, from code):** a doorway is baked walkable and a closed door was a wall for the radar only, so the guide
  routed through the locked door to the unexplored ground behind it. Once a door opened, nearest-first exploring preferred the
  first area's leftovers. **App rules since 2026-10-16:** `LockedDoors` lays closed doors the game won't let the player use
  (`QuestLocked` = quest-restricted and not the current step, or `ScriptDisabled`) and doors tried and left closed on the guide's
  routes; an objective at such a door never leads; when any barrier falls the guide explores what it held back first. Not
  live-tested: whether the blocker reads quest-locked during phase #4 is unknown (if it reads usable and loops as the quest step,
  the "tried" rule covers it after one visit). `DOOR …` lines in the log tell which.

## 22. Healing wells and the boss room (offline, build of 2026-10-14, 2026-10-19)

- **Report (tester, 2026-10-16, no guide log):** the guide still never leads into a dungeon's boss room; missing the healing
  well, the player is led back to the dungeon portal. Asked for a healing-well sound.
- **Wells (H):** `Healing_Well_Basic` 141546 (the dungeon well), `_Outdoor` 227733, `_Reusable` 2097390, `_Milk` 2549074 (seasonal):
  eType 2, gizmo type 11, which shrines and pylons share, so the name tells a well. `Healing_Well_Empty` 520224 and
  `Healing_Well_Cursed_MapIcon` 1438267 are eType 4 markers, no gizmo; Empty may be the used look (unconfirmed).
  `Experience_Well_Basic` 2025174 is an XP well. Not wells: `SMP_WishingWell`, `*Inkwell*`, `S02_Bloodwell*`, `S06_Undercity_Prop_Well_*`.
- **Where (H):** only 6 of 4,042 scenes place a well, none in the Game/Shared layers the app reads: `ProtoDun_Keep_FLEX_POI_BossRoom`,
  `Hell_Fort_S_Arena_01`, `ProtoDun_Root_W_Arena_01` (boss rooms or arenas), `Mage_Halls_NE_01`,
  `TT_Hell_Fort_to_Cave_Lava_NS_02`, `Cellar_Skov_W_Entrance_01`. ~70 other boss and arena tiles place none: the server spawns
  most wells, so only the live actor list (or the map marker list, unchecked) finds them.
- **Boss tiles (H):** every `*POI_BossRoom*` tile checked places `Trigger_BossRoom_Entrance` and two `TrackedCheckpoint_8m`; no
  boss spawner is in the marker sets.
- **Likely cause in the app (M, from code):** `LockedDoors` and `BarrierObstacles` learn that a door or barrier is gone only
  within 15-20 units of it. A boss room's door that opens by script when the objective is done, while the player is elsewhere,
  stayed a wall, so the guide ran out of ground and led to the portal. **Since 2026-10-19:** a followed quest moving on from a
  known phase re-arms every locked or tried door and drops every barrier not listed now. Switching the guide off and on already
  re-armed tried doors. Not live-tested; `DOOR re-armed (quest moved on)` and `MAP marker kind 10` lines tell.

## 23. Prisoners that follow the player (offline census + tester report, 2026-10-20)

- **Report (tester, 2026-10-19, nightmare Hallowed Stones, 0.1.13, no `--debug`):** after a portal to another level, an
  objective "right by my character" played the prisoner (corpse) cue, kept following the player, and the guide held there,
  pointing behind them. The log only shows the same-instance warp (`World: back in DGN_Naha_HallowedStones`).
- **Census (`D4MemScan gizmos --match Prisoner`, build 3.2.2, H):** free-able prisoners `DGN_NPC_<Region>_Prisoner_*` are
  NPCs on team 1 (Nahantu has only `…_Naha_{Teg,Umba}_Prisoner_{F,M}_0N_Invulnerable`); `*_Prisoner_Follower_*` actors exist
  (CAMP_Step_Cannibal, CMP_Hawe_Crusader) on team 2, the player's. The dungeon event `DGN_Standard_NPC_Prisoner_Follower`
  1892247 (§2) names no actor. Which actor followed is not known (no `--debug` log).
- **Likely cause (M, from code):** a prisoner NPC has no "used" signal and loops until it leaves the actor list; the guide's
  objective takes the actor's position every poll and is set aside only once the player is 20 units from it, which a follower
  never allows.
- **Since 2026-10-20 (`FollowerWatch`):** a prisoner NPC that walks 8 units while never more than 10 from the player follows
  for good in that world (a prisoner still to free never moves), and a copy of a follower's SNO first seen within 10 units
  (re-created after a warp) follows at once: silent (`LOOP stopped … cause=follows`), so the guide counts it done. Both
  distances unmeasured. Prisoner switches (gizmos) are not watched. Unknown: whether `DGN_Standard_NPC_PrisonerInTrance`
  2329531's prisoner walks while still to free, which would silence it if the player walks beside it.

## 24. Overworld quests that hand over: Wrack & Ruin's eye sockets (offline + tester log, 2026-10-21)

- **Report (tester, 0.1.14, no `--debug`, Hawezar):** at "Following Whispers" (the codex group header
  `CampaignCodexGroup174_Header`, not a quest), objective "Slay the serpents. Return the Serpent's Right Eye", no quest loop
  told where the eye sockets are. The log: `Quest tracked: none` the whole time; `594326` came and went as "in progress here"
  at phase 24 for ~9 minutes near the villager; serpents only as map markers kind 10 (`snake_serpent_poison` 691069,
  `snake_caster_poison` 691072, level area `Hawe_Marsh_Altar`, about (−932, 726)).
- **Quests (H, offline):** `QST_Hawe_Marsh_03` 594326 "Wrack & Ruin": phase 24 ("Find the eyes for the engraved stone at
  Yngovani") names `QST_Hawe_Marsh_YngovaniVillager` (−775, 839) and waits on two hidden quests, both also named "Wrack & Ruin":
  `QST_VGN_Hawe_Marsh_03_BanditScene` 1080159 (survivor, "Slay the serpents", Left Eye) and
  `QST_VGN_Hawe_Marsh_03_DestroyedAltar` 1222498 (Right Eye, "Search the cultist corpses": `corpse_serpentcultist_melee`
  (−840, 946), phase 32). Phase 21 of each, "Return the eye", names its socket. No phase names the serpents. The player
  sees both hidden quests' objectives at once.
- **Actors (H):** `Hawe_Marsh_Snake_Temple_Blocker_Door_Socket_Right_gizmo` 1302768 ("Right Eye Socket") and `…_Left_gizmo`
  1302770, gizmo 57 (switch), team 0, both at about (−777, 825) in `Hawe_Marsh (Hawe_Marsh_Campaign_TempleClosed)`, at the
  `…_Blocker_Door_gizmo` 1261703 ("Engraved Stone Door"). The eyes are inventory items (`Item_QST_Hawe_Marsh_Temple{Left,Right}Eye`
  943276/943277), not carryables. Whether the sockets are `Gizmo_Is_Quest_Restricted` or `MinimapActive`: unknown (ACD flags).
- **Cause (H, code):** the hand-over to hidden quests (§18) was followed from tracked quests only, so an untracked 594326 never
  made 1080159/1222498 candidates; and an overworld quest is found only while one of its objects is loaded around the player,
  so it dropped in and out at the edge of that range.
- **Since 2026-10-21 (`ActiveQuests`):** the hand-over is followed from every followed quest (tracked or found here), and a
  quest found in progress stays followed while its {quest, phase} pair still reads (until it ends or the world changes).
  Not live-tested. Still open: the sockets loop only within the ~25 units the game streams; nothing leads to them from afar in
  the overworld.
