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

Examples:

| Dungeon quest | Objective actors (SNO, event) |
|---|---|
| `DPO_Step_GuulrahnSlums` | `DGN_Standard_Global_Human_Stake_01_Switch_Dyn` 1120667 (32), `DGN_Standard_Sitting_Skeleton_Switch` 1246560 (32), `ProtoDun_Jail_Door_A_01_Dyn_…` 1091691 (32), `ghost_caster_miniboss` 585342 (0), `cannibal_brute_Unique_…_PosessedCannibal` 1058248 (0) |
| `DPO_Naha_SleeplessHollow` | `DGN_Standard_LoreBook_Corpse_Skeleton` 1216430 (32), `cultist_summoner_dungeon_miniboss` 1617845 (0) |
| `DPO_Frac_KorValarRamparts` | `DGN_Standard_Prisoner_Cage` 1086042 (32), `DGN_Standard_DeadPrisoner_Cage` 1260903 (32), `Generic_Lorebook_Chest_01_Dyn` 1309361 (32), `DGN_Frac_KorValarRamparts_Door_Boss` 711624 (32), `knight_melee_frac_unique_…` 709872 (0) |
| `DPO_Hawe_HeathensKeep` | `vampire_boss` 237232 (0), enter `DGN_Hawe_HeathensKeep_Boss` 854916 (50), `knight_caster_…_HighInquisitor` 854770 (0) |

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
