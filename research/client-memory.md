# Diablo IV — client memory structures (read-only)

Recovered 2026-09-28 by **offline** analysis of two read-only captures (`D4MemScan scenes --focus ^Step_`)
taken in `DGN_Step_GuulrahnSlums` (World SNO 708553), build 3.2.1.73552: one at the entrance, one after
the player explored. Game code is encrypted on disk and game RTTI is stripped, so every structure here is
located by **value signature** checked against offline scene data ([dungeon-data](dungeon-data.md)).

## 1. Live scene instance ("CScene", confidence: high)

Offsets relative to the u32 `snoScene` at address `a`. Records live in a pool (stride `0xA00` observed).

| Offset | Type | Meaning | Check against offline data |
|---|---|---|---|
| a−52 | u32 | batch id (generation order) | |
| a−48 | u32 | tile hash `(wp.X/32) << 16 \| (wp.Y/32)` (32×32 tiles; larger tiles unconfirmed) | == a+416 |
| a−44 | u32 | eTileType = exit mask N=1 S=2 E=4 W=8 | == offline mask, == a+420 |
| a−40 / a−36 | u32 | DRLG node id / group id (a multi-tile room shares them) | |
| a−20 | u32 | has-DRLG-info flag = 1 | |
| a−16 | u64 | vtable pointer into the main module (constant across records) | |
| a−8 | u64 | pointer to the scene-definition cache entry (first u32 = SNO) | |
| a+80 | PRTransform | quaternion (x,y,z,w) + world position `wp` | unit quaternion |
| a+136 | 6×f32 | local AABB (center, half-extent) | == offline scene AABB exactly |
| a+160 | 6×f32 | world AABB (center = local center + wp) | |
| a+280 / a+292 | vec3 / f32×2 | `wp` copy / tile size X,Y | |

**Static route to the pool** (read offline from game code disassembled out of the stale image view,
which is ~41% decrypted; confidence high on code semantics, **not yet validated live**). RVAs are from the
live view base:
1. `G = u64[base+0x368D188]` (game object), then `P = u64[G+0xC18]` (scene container, 0x2B8 bytes).
   Container fields: `+0x100` capacity, `+0x108` maxIndex (inclusive), `+0x2B0` shift, `+0x2B4` mask, and
   16 chunk tables of 0x18 bytes at `+0x120`.
2. Choose the table: `seed = u32[base+0x37CC63C]`, then `k = ((P/0x2B8) + seed) & 0xF`,
   `stride = (0x92F + 16k) & ~0xF` and `chunks = u64[P+0x120+0x18k]`. The seeded choice explains why the
   pool stride differed between sessions (0xA00 in one, 0x950 in the other).
3. Slots: `X = u64[chunks + 8(idx>>shift)] + (idx & mask)·stride` for `idx = 0..maxIndex`. The slot is free
   when `u32[X+8] == 0xFFFFFFFF`. Within a slot, `X+8` = scene id, `X+0x158` = the CScene vtable
   (base+0x2819030), and `X+0x168` = `snoScene` (the scanner's `a`). The game's iterator is at RVA 0xDA7C00.
4. By id: `idx = id & 0x7FFFF`, and the slot is valid only if `u32[X+8] == id`.

The player's scene id is at `RActor+0x7A8`, and the RActor container is at `[G+0xB78]` (§4; an earlier
note called it the ACD container). The static player record's `p−16` holds that scene's SNO.

**Only scenes near the player are loaded** (about two tiles around them); records are unloaded and their
slots reused as the player moves.

**Preset rooms and stale slots** (live pool dump, `D4MemScan scene-route --diagnose --slots`, 2026-09-28, `S15_Triad_B_Worldstone`):

- Large preset rooms are placed as 96×96 pieces (`Triad_B_ThroneRoom_N02_E03_DGN`, `…_N03_E03_DGN`) sharing one node/group id. Their live exit mask is a **subset** of the offline one (offline 15, live 7 and 6), so matching must accept subsets. The tile hash keeps the 32-unit formula.
- Filler tiles (`*_FILLER_01`) and UI scenes (`PaperDoll_*`, `Portrait_*`) share the pool with `a−20 == 0`; the matcher rejects them.
- Free slots (`id == 0xFFFFFFFF`) keep the old record intact, so a heap scan also finds tiles from earlier floors. Only the route's free-slot check tells live from stale.

## 2. Other records

- **RevealSceneMessage copy** (transient, slots `0x10080` apart): exact d4data wire layout
  (`snoScene` at base+344, transform at base+356, DRLG creation info before it). Disappears soon after load.
- **Revealed-scene list** (96-byte entries): `{u32 idSScene (0x8000n), u32 0xA, u32 sno, u32 1, u32 K}`,
  K constant per dungeon instance. Lists **every** scene of the dungeon from entry on (60 ids in the test
  run) but carries **no position**.
- **Spawn block**: `{id, 0xA, sno, K, vec3 spawn}`; spawn = entrance tile's offline `Start_Location_0` + tile `wp`.

## 3. Local player position

Recovered by position narrowing (`pose-snapshot`/`pose-filter`), then offline analysis of two
`pose-context` captures taken before and after a known move. The player was a Barbarian (`barbarianM`, 176832).

- **Static location record** in the main module's writable `.data` (confidence high; the preferred source).
  Offsets are relative to the position `p`:
  - `p−16` = current Scene SNO. It is the tile containing the player.
  - `p−12` = World SNO.
  - `p−8` = player actor SNO.
  - `p−4` = 0.
  - `p` = vec3 position.
  - `p+12` = the player's number in the game: 1 alone or as party host, 2 after joining a party (live
    2026-09-29, 3-player party: the joiner read 2, then 1 once alone in the Mercenary den). The stale copy
    below holds 0, so "live" means nonzero (`PlayerRecordMatcher.IsLive`). Requiring 1 broke every joiner.
  - `p−8` holds the class actor (`<class>M` / `<class>F`), the same for every character of that class and
    sex. The app derives the 16 player actors from `ActorDefinition.eType == 7` at startup (warlock included).

  This record has one copy in the whole process. It sits at a fixed offset inside the module, so it
  lies at a stable RVA per build. Finding it needs only a scan of the module's writable pages (~8.6 MB),
  and after that each frame reads 32 bytes.
- **Actor record** (heap pool, stride `0x680`; confidence medium-high; it is an **ACD** slot, see §4), with base = `p − 0x7C`:
  - `+0x50` = actor SNO.
  - `+0x6C` = quaternion `(0,0,sin(yaw/2),cos(yaw/2))`, i.e. **heading**: yaw measured from +X towards +Y.
  - `+0x7C` = position.
  - `+0x88` = scale (1,1,1).
  - `+0x580` = LevelArea SNO.

  Monsters probably share this layout, which is unconfirmed.
- The ~270 other "moving" copies are render data (e.g. the six faces of the light's cube-shadow views at
  z+2.5 / z±15) and tick-history rings. The player's **pet** and **mercenary** move with the player too,
  so "moves together" alone does not identify the player. Filter by the player actor SNO instead.

**Live confirmation (2026-09-28, second session and second dungeon `DGN_Naha_SleeplessHollow`):**
- The static record sits at the same RVA `0x3445004` after a game restart.
- It updates live: sampled at 4 Hz, it tracks walking at ~10 units/s.
- The game maps its main image **twice**, both as `MEM_MAPPED` rather than `MEM_IMAGE`:
  - The live view is at `0x7FF798D80000` (same base across restarts). The record there has `p+12 == 1`.
  - A stale view holds the same record with position (0,0,0) and `p+12 == 0`.
- To find the module, match the in-memory PE header (TimeDateStamp + SizeOfImage) against the exe on disk.
- `Client entered world` is logged before the player is placed in the new world (`Warped into destination`
  0.3 s later, `Portal completed` 1.3 s later on the user's PC), so the record can still hold the previous
  world when the log line is read. The app retries the search every second until it matches.

**Screen ↔ world** (fixed camera, measured from four live stick moves):

| Stick | Engine direction | Tile exit it matches |
|---|---|---|
| up | −X −Y | between N and W |
| right | −X +Y | between N and E |
| down | +X +Y | between S and E |
| left | +X −Y | between S and W |

A tile's exit therefore appears diagonal on screen: N = up-right, E = down-right, S = down-left, W = up-left.

## 4. Actors: RActor and ACD pools (both validated live)

Derived 2026-09-28 from game code in the stale image view (same dump and method as §1). Confidence refers
to the code semantics. None of this has been read live yet.

**Both pools use the scene container's 0x2B8-byte template** (H): the same header offsets, 16 seeded chunk
tables, stride `(base + 16k) & ~0xF`, free slot `u32[X+8] == 0xFFFFFFFF`, and lookup `idx = id & 0x7FFFF`.
The evidence is the magic multiply for `P/0x2B8` and the same header reads.

| Pool | Route | Seed RVA | Stride base | Evidence |
|---|---|---|---|---|
| Scene | `[G+0xC18]` | 0x37CC63C | 0x92F | §1 |
| **RActor** (client actor) | `[G+0xB78]`, static | 0x37CC5A0 | 0xC27 | lookup 0x67CF70 ("RActor with ID %d was not found!"), iterator 0xD94C50 |
| **ACD** | `[[G+0xAB0]]`, static (live 2026-09-30, below); the code reaches it per thread via TLS: `[[[[[TEB+0x58]]+0x1A0]]]+0x148`, then `[ ]` | 0x37CC59C | 0x627 | lookup 0x67D0B0, TLS wrapper 0x67D140 ("Missing Player ACD"), iterator 0x15DDAB0 |

**RActor slot** (X = slot start):

| Offset | Meaning | Conf. |
|---|---|---|
| +0x08 | RActor id | H |
| +0x0C | ACD id | H |
| +0x54 | owner/source ACD id (−1 = none) | M |
| +0x5B8 | actor SNO (group 1) | H |
| +0x750 / +0x760 / +0x76C | quaternion / position / scale | H |
| +0x7A4 | world id (SNO or instance id: unknown) | H |
| +0x7A8 | scene id (scene pool) | H |
| +0x7AC | placement: 0 = in the world, −1 = not placed (UI copies) | H |
| +0x140 / +0x148 | gizmo state id / the previous one: 0 at +0x148 until the gizmo is used once (live, below) | M |

**ACD slot** (offline, then live 2026-09-30 on ~35 slots; H): +0x08 ACD id, +0x14 RActor id, +0x48 actor definition
pointer (the same for the same SNO), +0x50 actor SNO, +0x6C/+0x7C/+0x88 quaternion/position/scale (position == the
RActor's), +0xC0 world id (== RActor +0x7A4), +0xC4 scene id, +0xC8 placement (0 in the world, 1 not placed, e.g.
inventory items), +0x2A0 attribute container pointer (getter 0x15F27B0). The block at ACD +0x6C..+0xC8 mirrors
RActor +0x750..+0x7AC.

**ACD pool, live** (2026-09-30, prologue overworld, `D4MemScan acd`):
- Found by a value scan for slots with +0x08 = ACD id, +0x14 = RActor id and +0x50 = SNO of 7 known actors (18.5 GB of
  RW memory in 34 s, 6 hits). Their indices gave stride 0x6A0, the seed formula with k = 8.
- Pointers back: chunk array ← container +0x1E0 (0x120 + 0x18·8) ← a pointer held at `G+0xAB0`. The container
  starts with the ASCII name `ActorCommonData`; its header matches the scene pool (capacity 0x200, shift 9, mask
  0x1FF), and the seed at RVA 0x37CC59C gives k = 8.
- Only networked actors have an ACD (RActor +0x0C != −1): players, monsters, items, server gizmos; decor has none.
  About 35 slots are occupied. An ACD is freed when its actor leaves the client's range and re-created with a new id
  when it comes back.
- `acd` listing: 10 of 10 gizmos' ACD slots held the same RActor id and SNO.

**Attribute container** (`[ACD+0x2A0]`, 0x230-byte pooled object; live, H):
- +0x00 u32 group id. Four hash maps share one header, {u64 bucket count, u64 bucket array, u64 entry count, f32 max
  load 1.0, …}, at +0x08, +0x38, +0xD8 and +0x108.
- The map at +0x08 holds most attributes: a gizmo has 64 buckets and ~10 entries, the player 512 buckets and 337
  entries. The other maps' roles are unknown; +0x38 repeats a few, e.g. `TeamID` and `Last_In_Combat_Time`.
- A bucket holds a pointer to a node's next-link word L, and a chain ends at 0. Around L: L−8 u32 attribute id,
  L−4 i32 parameter (−1 = none), L+8 u32 value kind (0 float, 1 integer), L+0xC u32 value. Every walk matched the
  map's entry count.
- The values read plain (Level 1, TeamID, HP floats, seeds); the offline xor/ror note concerns another path.
- **Names:** a descriptor array in the module's `.data` at RVA 0x36EFC10, stride 0x58: +0x00 u32 id (== index),
  +0x40 pointer to the ASCII name in `.rdata`; 1366 names for ids 0..0x571. Found by a pointer scan for the
  `Gizmo_Has_Been_Operated` string. The ids are build-specific: resolve them by name.

**Local player:** `GetLocalPlayer` (0x6E4020) scans `[G+0xC98]` with an obfuscated key and returns player
data holding the ACD id (+0x28) and the RActor id (+0x2C). The static record (§3) is simpler.

**Live validation (2026-09-28, `D4MemScan actors`, `X1_Undercity_BugCave_03` boss room):** the RActor route
works. Exactly one placed RActor has the player SNO at the record's position. The list is real: 88 placed
actors, including equipped items, pet, mercenary, the boss corpse, `X1_Undercity_Chest_Attunement2` and the
stash. The farthest was 65 units away, so the client only holds actors near the player. `+0x7A4` is an
instance id (0xFA66BCBF), not the World SNO. `+0x7A8` did **not** resolve in the scene pool with the
`id & 0x7FFFF` rule, so its id space is still unknown.

**Gizmo "used" signal (live 2026-09-29, prologue, `D4MemScan actors --watch`):** when the player used a
chest-type gizmo, `+0x140` (and its copy `+0x144`) stepped from `0x61` to `0x62`, then `0x63` for a backpack. `+0x148` took the
previous value (0 before any use). Across 8 nearby chest gizmos (hollow tree, backpack, snow piles), the 4 the player
had used had `+0x148 != 0` and the 4 untouched ones had 0, and it stayed that way minutes later. The app treats
`+0x148 != 0` as "used" for gizmos only (`ActorState.StateChanged`).
- **Doors:** a village door (`FracturedPeaksHU_Town_Door_02_dyn`) starts with state `0x4046D66` and `+0x148` 0. Once
  opened, it holds `+0x140 = 0x3EC7F2` and `+0x148 = 0x4046D66`; an identical closed door kept 0. The app's door loop
  stopped with `cause=used` at 1.3 units. The player cannot close that door again.
- **Blood petals** (`Frac_Prologue_BloodPetals_NoSymbol`, a story switch, live 2026-09-30, one sample):
  - `+0x140`/`+0x148` never moved.
  - At the interaction, `+0x4D4` and `+0x4DC` went 0 → 0x211 and `+0x1F4` went 1.0 → 0.
  - Of the 35 gizmos nearby, only the used petals had `+0x4D4 != 0`.
  - `+0x1F4 = 0` is also on decor books and on a used door, so it is not a signal.
  - It came 9 s after the use, as the cinematic started. **`+0x4D4` is not a used mark** (second sample, 2026-09-30,
    `Ancients_Prop_Lilith_BloodPetal` in the Darkened Way): 0x1CA on the untouched petal, unchanged through the use, and
    the app, which treated `+0x4D4 != 0` as used, silenced that petal on sight. The rule is gone. After that use the ACD
    held `Gizmo_Has_Been_Operated` = 1 and `Gizmo_State` = 1 (below), and `+0x148` was `0x4046D66`.
- **Villager corpse** (`Corpse_Frac_Villager_F_01_Snow`, chest gizmo, live 2026-09-30):
  - `+0x140`/`+0x148`/`+0x4D4` never moved.
  - At the use, `+0x78` went 0 → −1, `+0xA0` lost bit 0x4, `+0x230` went 1.0 → 10.0 and `+0x130` settled from `…83` to `…87`.
  - None of these means "used". An untouched chest and herb nearby read the same `+0x78`/`+0xA0`/`+0x230`, and the
    reusable ritual brazier flips `+0x130` bit 0x4 too. `+0x78 = 0` with `+0xA0` bit 0x4 marks the gizmo the player
    currently targets.
  - A corpse's used state is not in its RActor slot; it is in its ACD (below).
- Levers and switch prisoners are not checked yet.
Noise when diffing a slot:
- `+0x17C`/`+0x180` tick every frame, and `+0x234` is a timer.
- While the player targets the gizmo, `+0x78` is 0 (else −1) and `+0xA0` bit 0x4 is set; `+0x230` (1.0/10.0) is
  not a used flag (above).
- `+0x720` looks like a distance.

**Gizmo "used" signal in the ACD** (live 2026-09-30, `acd` + a 2 Hz watch of every ACD while the user played; H on
presence): attribute `Gizmo_Has_Been_Operated` (id 0x229 in this build) = 1, mostly with `Gizmo_State` (0x21E) = 1.
- **Controlled test:** a miner corpse (`Corpse_Frac_Miner_M_01`) gained both at the poll of the interaction
  (`acd --match Corpse --watch`). The app reads it as `ActorState.Operated` (`D4Memory/Actors/GizmoOperatedReader`,
  gizmo actors only); after a restart the used miner was judged used at once (`USED by the game's operated mark`).
- Untouched chests, corpses, clickies, herbs and signs don't carry it; items on the ground hold it at 0.
- A Biteberry herb gained both at the poll where the app's RActor signal fired (`cause=used`). At the same time the
  player's ACD gained `Gizmo_Player_State[0x9921918B]` = 1.
- A villager corpse (`Corpse_Frac_Villager_F_01`, ACD 0x3D80059) gained both between two reads a minute apart. Another
  one was re-created 4 times as the player moved; each new ACD already carried both, while the app restarted its
  loop each time. Unlike RActor +0x148, the ACD mark survives re-creation.
- A town sign (`TWN_Frac_Yelesna_Sign_VampireHanged`) gained `Gizmo_Has_Been_Operated` = 1 when read, without
  `Gizmo_State`.
- A story door (`GizmoBlockout_GaleValley_NeyrelleDoor`) read `Gizmo_State` = 0 closed; once opened it held
  `Gizmo_State` = 1, `Gizmo_Has_Been_Operated` = 1 and a `Door_Timer`.
- Real chests (`Chest_Open`, id 0x220, never seen set), levers and prisoners are not checked.
- **Quest gizmos** (live 2026-09-30, Darkened Way, 2 samples, M): `Gizmo_Is_Quest_Restricted` (0x226) = 1 marks a gizmo
  the quest controls, usable or not:
  - the scripted entrance gate `Qst_Frac_Ancients_UnderWorld_LargeEntrance_Door_01_Dyn` (gizmo 0, the game offers no
    interaction): quest-restricted, `Gizmo_State` = 0, **no** `MinimapActive`;
  - the demon body `Frac_UnderWorld_OreHoist_HangingCorpse_FallenBrute_Dyn` (gizmo 57 switch, the quest's next step, the
    player could use it): quest-restricted **and** `MinimapActive` = 1 (as are NPCs and an elevator nearby).
  The app reads quest-restricted + minimap as the quest step (quest loop) and quest-restricted alone, or
  `Gizmo_Disabled_By_Script` (0x222, by name only), as not usable yet (silent). The Neyrelle door the player opened
  carried neither. `+0x140` doesn't separate them: a usable village door also starts at `0x4046D66`.
  - **The gate unlocked with no trace:** once the quest advanced (tracked quest 889465 → 486210) the player could open
    it, yet its attributes, `MinimapActive` (absent) and RActor words (`+0xA0` 0x10B) were unchanged, and the player's
    ACD changed only `ManuallyTrackedQuestSNO`. So "quest-restricted off the minimap" is **not** "locked" for doors.
  - At the opening (ACD watch): `Gizmo_Has_Been_Operated` → 1, `Gizmo_State` 0 → 1, `Door_Timer` added; ACD slot
    `+0x518` 1 → 0 (0 on every other gizmo nearby: a closed-door flag, not "usable"), then `+0x258` 0x10 → 0 and
    `+0x614` 7 → 0 about 2.6 s later.
  Other candidates by name, unused: `Gizmo_Visibility_Is_Quest_Restricted` (0x227), `Untargetable` (0x160),
  `Operatable` (0x315), `Disabled` (0x317). `D4MemScan acd --names <regex>` lists the table.
- **Tracked quest:** the player's ACD holds `ManuallyTrackedQuestSNO` = the quest the player tracks (live: 889465
  `QST_Frac_Underworld_01`, then 486210 `QST_Frac_Underworld_02` right after the body was used; `OverrideTrackedQuestSNO`
  exists, never seen set). No per-phase attribute exists (`acd --names Quest|Phase`).
- **Quest → gizmos (offline, H on the 3 live cases):** a quest's callbacks carry a `ScriptMessageMap` whose parameters and
  event filters include `ScriptEventParamMarkerHandle` {`snoMarkerSet`, `nID`} naming a marker in a world layer set
  `<World> (<Layer>)` (world space). `QST_Frac_Underworld_01` names the body (`…_LiftGetsFixed` #34), `_02` names the
  gate (`Hub_Gate` #4), then the city dungeon blocker (`CityDGN_Closed` #4) and the blood petals; script event 32 on all of
  them. The gate is placed twice (`Hub_Gate`, `Hub_Gate_Open`); the live actor kept one ACD across the unlock. Rule the
  app applies (`QuestScriptMarkerReader`, `LiveDungeonSessionReader`): a quest-restricted gizmo is the quest step when on
  the minimap or when the tracked quest names a marker of its SNO within 2 units; otherwise silent. Phase granularity is
  unknown: a later step of the same quest (the city blocker) can sound early. **Solved by the current phase** (below).
- **Quest phases** (live 2026-09-30, build 3.2.2, `D4MemScan quest --sno 486210 --watch` / `--find`): a QuestDefinition's
  `arQuestPhases` each carry a `dwUID` (small numbers: 34, 47, 127, 183, 213, 236, 260, 271, 278, 281 for
  `QST_Frac_Underworld_02`) and their own callback sets, so each phase names its own markers. The array order is not the
  play order: the blood petal is 236, then 271 (`Ancients_Ruins_Blocker_Door` next to it), 260 (a trigger sphere), and
  only then 213, the city blocker that sounded early. The client holds the pair {u32 quest SNO, u32 current phase UID} in
  several places; one is in the module's data (RVA 0x30643CC, the only such pair there), others on the heap, one of them
  a queue of the next phases (236, 271, 260 → 271, 260, 213). All went 236 → 271 when the petal scene's conversation
  ended (not at the use). The app (`QuestPhaseLocator`, `LiveDungeonSessionReader.NamedByTrackedQuests`) finds the pair
  by content in the module data, reads it every actor poll, and names only the current phase's objects (all phases
  while the pair isn't found; a failed search waits 5 s). Live: `Quest tracked: 486210, 1 object(s) named, phase 271`,
  the city blocker silent.
- **Quest companions** (live 2026-09-30, `acd --match .` in Fractured Peaks): `NPC_QST_Neyrelle_Follower` and
  `NPC_QST_Vigo_Follower`, both following the player, carried `Follower_Quest` = 480289 (the quest), `Pet_Owner_ANN` =
  525136 and `Pet_Type`; no other NPC in range had them. The app silences NPCs with `Follower_Quest` (`AcdFlagReader`).
  Still to see: an escort the player must follow (the Vessel of Hatred light carrier).

## 5. UI state: conversations and menus (live 2026-09-29)

**Method.** `D4MemScan player --snapshot-pointers <file> [--snapshot-count N --snapshot-every-ms M]` writes a depth-1
heap snapshot:
- Every 8-byte word in the live module's writable data that points into readable memory (≈32,000 pointers).
- The first 0x400 bytes at each target.
- About 35 MB per snapshot, one every ~0.55 s.

Snapshots taken in alternating states (closed/open/closed/open) are diffed per `[base+rva]+offset` word. That yields a
static route directly. Diffing the module data alone found nothing: UI state lives on the heap. `D4MemScan peek --address`
then watches the candidates live.

**Conversations (build 3.2.1.73552), used by the app (`D4Memory.Ui.ConversationReader`):**
- Dialogue menu: `u64[base+0x31326A0]` points to the option array, and `u32[base+0x31326A8]` is the option count, 0 while the
  menu is closed. Options are 0x20 bytes each, with the ConversationDefinition SNO at +0x10. The prologue menu with Vani had
  5 options: an empty one (SNO 0), `Frac_Prologue_Village_08`, then `Vani_OPT_03`, `_06` and `_05`. The array keeps
  stale entries after the menu closes.
- Playing conversations: a doubly linked list with its head at `base+0x30D28F8` (next, prev). The head points to itself
  when the list is empty, and each node's SNO is at +0x28. It held `Frac_Prologue_Vani_OPT_03` for the ~5 s of the chosen
  line and nothing before or after.
- The app mutes while the menu is open, and while a playing conversation is one of the menu's options (`ConversationMute`),
  so barks and taunts never mute. Live: "funcionó".
- Conversations started without the menu (quest NPCs, scripted story lines) were missed: a tester heard the cues during
  Deckard Cain. `ConversationDefinition.eConvType` (+0x8, census of 8411): 0 barks and floats (2742), 1 hero lines, town
  gossip and talk-menu greetings (1320, incl. `Vani_OPT_*`), 2 companion chatter (863), **3 standard NPC conversations and
  in-engine story scenes** (2498: `*_OPT*`, `*_Cine_*`, the Cain quest lines), 4 vendors (62), 5 lore books and
  announcements (909), 6 emotes (16). The app also mutes while a type-3 conversation plays (live 2026-09-30:
  `Frac_Prologue_Monk01a(3)`). The `TALK` debug line prints `Name(type)`, or `Name(type npc)` for an NPC line. Undercity
  guide lines mix types 0 and 3.
- Type 1 with an NPC speaker also mutes (villagers without a menu). Rule: mute if any line, lineset or case node of the
  ConversationDefinition has a `snoSpeaker` other than the hero's Speaker SNO `Player` (SNO group 72). The nodes are
  polymorphic arrays under `arRootNodes`; each node's `dwType` is its type's hash key in `definitions.json`, so sizes and
  fields come from the schema (`ConversationDefinitionReader.IsNpcTalk`, `SnoBlob.PolymorphicElements`,
  `TypeSchema.TypeName`). Census of the 1320 type-1 conversations: 502 hero-only (all 396 `HRO_*` error lines and the
  `*_Player_*` story lines; not muted), 796 NPC-only, 12 mixed (muted), 10 without lines (not muted). Every `TWN_*` gossip
  line is NPC. `eSpeakerTarget` does not separate hero from NPC. Live: `TWN_Frac_Kyovashad_VLG_G17(1 npc)` muted.

**Menus (build 3.2.1.73552), used by the app:** a heap table of screen records, located by scan.
- Name hash (UI controls and screens): `h = h * 33 + c` over the lowercased ASCII name, u32. `UIControlHandle` is
  `{ snoUI, hash(control name) }` (UI SNOs are group 46, e.g. `Inventory` 4007, root control `inventory_dialog_mainPage`).
  d4data field hashes are the same value `& 0x0FFFFFFF`.
- Screen registry: a hash table on the heap, 24-byte entries `{ u64 next, u32 key @+8, u32 padding, u64 value }`. `key` is
  the name hash, `next` is 0 or a chained entry with the same layout, `value` points to the screen record. The u32 after
  the key is leftover heap padding (0 in one session, 0x258 in the next), so only the u32 is matched. It held ~290 screens
  in 2026-09-30 (292 of the 441 `screen_*` names in memory had a validating record). `screen_DungeonCompanions` flips its
  whole flag word constantly, so it is not a real open bit.
- Screen record: `u32` at +0x10, **bit 0 = open**. +0x58 points into the main module (a callback, RVA 0x64D210 here).
- A second table with the same keys points to name strings. Its +0x58 holds text, so the module-pointer check rejects it.
- No static route. Pointer climbs from the table found only allocator bookkeeping and coincidental paths: owners are
  probably obfuscated like the pools (§1, §4). The app scans private RW memory upward for the key and validates the record.
  The table sat ~72 MB into the first heap arena. The scan is parallel (half the cores): on 16 cores and a 14 GB heap a
  full-heap miss took 8.7 s (57 s sequential) and a hit 0.3 s (3.3 s); the 7 screens were found in 0.3–1.8 s. After a
  character switch the UI is rebuilt and the old records stop validating (the first scan during loading found nothing,
  43 s sequential), so `MenuScreenTracker` retries 5 s after a scan and doubles the wait after each miss up to 60 s
  (`FirstRetryDelay`, `MaxRetryDelay`).
- Which screen opens with what (voice-guided snapshot series, 2–3 toggles each, all consistent):

| Screen | Open for |
|---|---|
| `screen_MenuPanel_Default` | inventory, skills (the character panel; with a controller other panels go through it) |
| `screen_MainInGameMenu` | map, pause menu (`screen_Map` also toggles with the map) |
| `screen_Signs` | reading a sign or notice board |
| `screen_Mailbox_Default` | Horadric shipments |
| `screen_Shop_default` | vendor (buy/sell) |
| `screen_crafting_default` | artisans: blacksmith and occultist live (the name is generic) |
| `screen_Stash_default` | stash |
| `screen_ActionBar_Left` | assigning a skill to a button (pad); the skills panel closes while it is open |
| `screen_InteractPopup` | an NPC's interaction menu (live 2026-09-30, build 3.2.2: Neyrelle, one option; the conversation-options search found no dialogue menu while it was open) |

Each of these 8 was seen opening and closing live 2026-09-30 with a watcher that logged every registered screen's open
bit (the occultist also uses `screen_crafting_default`). The app watches 89 screens (`MenuScreens.Watched`): these 8, every
other side panel and the full menus opened on purpose. A side panel is a `<name>_Default` screen with Left/Right variants
(split-screen layouts, though `ActionBar_Left` opened alone in single player); the registry held 41 such screens. The
`ActionBar` family is the skill assignment, not the HUD bar: its screens stay closed in play. Left out too:
screens seen open during normal play (Chat, SocialNotifications, ConversationSubtitle, GenericSkillTreeTest,
StoreDetails_v2, Mailbox_Right, and after a character switch InGameLogout, WorldTiers, FollowerSwap, ModalRenameLoadout).
- The open flag is one byte: real toggles change only it (`0xE7010100` ↔ `0xE7010101`, `0` ↔ `1`), and all 292 records
  held 0 or 1 in play. During a character switch freed records read junk (`0xFFFFFFFF`, `0x876747B5`), so any other value
  makes the record invalid. The tabs of the character panel (`screen_Mercenary_Default`,
`screen_WarPlanActivities_Default`) open inside `screen_MenuPanel_Default`. `screen_InteractPopup` was open ~4 s during an
interaction: the NPC interaction menu, watched since 2026-09-30.

- Other toggles seen: the inventory's paper-doll camera record (`screen_Inventory_default` + `Camera 000`, flag `0xCE600` →
  `0xCE601`) and `screen_Abilities_Default` (skills). The map and the pause menu also set flags near HUD screen names
  (`ActionBar_Right`, `InteractPopup`).
- Dead ends: two allocator counters that follow what the UI loads (`[base+0x3046290]+0x358`, `[base+0x30434F0]+0x318`),
  and depth-1 module-pointer diffs. Full control-path strings (`Root.NormalLayer.*`) exist only for store screens.
- Method: `screen_*` strings scanned once, then 1 KB around each re-read per state. A Python script spoke each step through
  PRISM ("Abre el mapa.", "Cierra el menú.") and waited 8 s. Words constant within each state and different between them
  were kept.

## 6. Consequences for the assist

- The whole scene list is revealed at entry; positions exist only for loaded scenes. The layout is
  therefore accumulated by polling the loaded CScene window as the player explores. This naturally
  limits knowledge to what the player has approached, matching the "no reveal of unexplored areas" rule.
- Validation in the test run: 10 placed tiles, **12/12 adjacent exit pairs consistent**, and the layout
  matched the player's described route.
- Where the minimap keeps positions of unloaded scenes is still unknown.

**Surviving game updates (live 2026-09-30, build 3.2.1.73552 → 3.2.2.73764, TimeDateStamp 0x6A973496 → 0x6AAD7D16).**
Every RVA moved, by different amounts (G −0x1C870, attribute names −0x206D0, playing list −0x274F0); the structure
offsets (G+0xAB0/0xB78/0xC18, pool header, stride bases, slot and record fields, conversation layout) did not. So the
app keeps only the structure layout (`GameBuildOffsetsTable.Layout`) and finds the addresses at session start:
- **G** (`BuildOffsetsDiscovery`): every heap-looking u64 in the module's writable data (≈425,000) is tried as G; the
  one whose `[[G+0xAB0]]` container starts with `ActorCommonData` (and whose scene and actor containers are set) wins.
  New build: RVA 0x3670918. 118 ms in the app.
- **Attribute names:** the `Gizmo_Has_Been_Operated` string in the module, the data pointer to it (descriptor +0x40),
  back by id × 0x58 to the table; entries 0..2 must hold their own ids. New build: RVA 0x36CF540.
- **Pool seeds are not read** (`PoolContainer`): in the chunk table in use, occupied slots hold ids with
  `id & 0x7FFFF == index`; the other 15 tables (or another stride) don't. The first 64 slots of each table are tested with
  the layout's stride, then every 16-byte stride from 0x100 to 0x2000 if the record size changed; ≥ 2 matches, no
  contradiction. Kept per container. Live: scene k = 6, 9 slots; RActor and ACD pools read (25/25 ACD PASS).
- **CScene vtable:** not checked any more; `CSceneRecordMatcher` validates each record.
- **Conversations** (`ConversationDiscovery`, `ConversationAddresses`): only findable while live. The playing list is a
  data word H whose first node's prev is H and whose +0x28 is a ConversationDefinition SNO; the dialogue menu is a data
  pointer followed by a count 1..16 whose options are all conversations or 0. The app searches every 5 s in the background
  (≈0.5 s per search) until found, and saves the RVAs per build in `d4assist-addresses.json` next to the exe. New build:
  playing list RVA 0x30AB408 (found while an NPC spoke, empty again after); the dialogue menu not seen yet.
- The screen registry (§5) and the player record (§3) were already found by content.
- Build 3.2.1.73552's RVAs stay in this file (§1, §4, §5) for reference.

## 7. Open questions

- The tile-hash units for tiles larger than 32×32, and whether RVAs stay stable across restarts.
- Validate the static CScene route (§1) live with a minimal probe.
- The revealed-list signature (§2) matched 0 entries in the second dungeon; re-check it.
