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
  - `p+12` = 1.

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

**Screen ↔ world** (fixed camera, measured from four live stick moves):

| Stick | Engine direction | Tile exit it matches |
|---|---|---|
| up | −X −Y | between N and W |
| right | −X +Y | between N and E |
| down | +X +Y | between S and E |
| left | +X −Y | between S and W |

A tile's exit therefore appears diagonal on screen: N = up-right, E = down-right, S = down-left, W = up-left.

## 4. Actors: RActor and ACD pools (RActor validated live; ACD offline only)

Derived 2026-09-28 from game code in the stale image view (same dump and method as §1). Confidence refers
to the code semantics. None of this has been read live yet.

**Both pools use the scene container's 0x2B8-byte template** (H): the same header offsets, 16 seeded chunk
tables, stride `(base + 16k) & ~0xF`, free slot `u32[X+8] == 0xFFFFFFFF`, and lookup `idx = id & 0x7FFFF`.
The evidence is the magic multiply for `P/0x2B8` and the same header reads.

| Pool | Route | Seed RVA | Stride base | Evidence |
|---|---|---|---|---|
| Scene | `[G+0xC18]` | 0x37CC63C | 0x92F | §1 |
| **RActor** (client actor) | `[G+0xB78]`, static | 0x37CC5A0 | 0xC27 | lookup 0x67CF70 ("RActor with ID %d was not found!"), iterator 0xD94C50 |
| **ACD** | per-thread game context via TLS: `[[[[[TEB+0x58]]+0x1A0]]]+0x148`, then `[ ]`; no static pointer found | 0x37CC59C | 0x627 | lookup 0x67D0B0, TLS wrapper 0x67D140 ("Missing Player ACD"), iterator 0x15DDAB0 |

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

**ACD slot:** +0x08 ACD id, +0x14 RActor id, +0x48 actor definition pointer, +0x50 actor SNO, +0x6C/+0x7C/+0x88
quaternion/position/scale, +0xC0 world id, +0xC4 scene id, +0xC8 placement (all H). The attribute
container pointer is at +0x2A0 and its getter is at 0x15F27B0. Values are obfuscated with xor/ror and the
decoding is not derived (M). The block at ACD +0x6C..+0xC8 mirrors RActor +0x750..+0x7AC.

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
- Levers and switch prisoners are not checked yet.
Noise when diffing a slot:
- `+0x17C`/`+0x180` tick every frame, and `+0x234` is a timer.
- While the player hovers or targets the gizmo, `+0x78` goes 0/−1, `+0xA0` bit 0x4 flips and `+0x230` goes 1.0/10.0.
- `+0x720` looks like a distance.

## 5. UI state: conversations found, menus not yet (live 2026-09-29, prologue)

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

**Menus (inventory, map, skills): not found.**
- Two words count up while a menu is open: `[base+0x3046290]+0x358` (3 closed, 4 inventory, 7 skills) and
  `[base+0x30434F0]+0x318` (7 closed, 9 inventory, 10-11 skills, 6 for the map). Both sit in allocator-like objects
  (`0x600DF00D` markers). The second is the entry count of a hash cache that holds item-name strings. They follow what
  the UI loads, not what is open.
- Screen names (`screen_*` strings, 233 in the exe) are only referenced from static tables.
- UI-group SNOs (group 46, e.g. `Inventory` 4007) never toggled.
- Screens are addressed by name through behavior plugs and `UIControlHandle`, probably a hash of the control path. That
  is the next lead.

## 6. Consequences for the assist

- The whole scene list is revealed at entry; positions exist only for loaded scenes. The layout is
  therefore accumulated by polling the loaded CScene window as the player explores. This naturally
  limits knowledge to what the player has approached, matching the "no reveal of unexplored areas" rule.
- Validation in the test run: 10 placed tiles, **12/12 adjacent exit pairs consistent**, and the layout
  matched the player's described route.
- Where the minimap keeps positions of unloaded scenes is still unknown.

## 7. Open questions

- The tile-hash units for tiles larger than 32×32, and whether RVAs stay stable across restarts.
- Validate the static CScene route (§1) live with a minimal probe.
- The revealed-list signature (§2) matched 0 entries in the second dungeon; re-check it.
