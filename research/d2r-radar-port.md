# Porting the D2R radar to Diablo IV Dungeon Assist

Source: `D:\code\c++\diablo 2 access project\d2r-access` (read-only study, nothing there was changed).
Ground truth is the source code; several docs describe superseded designs (flagged below).

## 1. Radar behaviour (what the player hears, D2R as built)

**Direction model.** Four fixed **screen** rays, never heading-relative: `Direction{Up,Left,Right,Down}`,
`Radar::Axes` (`src/radar.h:24-27`). Two selectable **frames**: `Cardinal` (the stick's own up/left/
right/down — default since 2026-09-25, `docs/decisions.md`) and `Diagonal` (the stick's diagonals,
which is where D2's walls actually run, `docs/radar-readings.md:22-75`). Both are compile-time ray
tables (`Radar::Rays`, `radar.h:43-46`); `[radar] diagonals` / `access-radar diagonal|cardinal` swaps
which one drives the engine. Eight-direction and heading-relative rays were both tried live and
**rejected by the user** (`docs/decisions.md` 2026-09-16/25).

**Cue types**, one `Sensor` state machine per direction (`radar.h:16-18`, `radar_synth.cpp`,
`docs/radar-readings.md:12-20`):

| Reading | Sound | Meaning |
|---|---|---|
| Exit | bright tin, 1100 Hz | route leads into another chamber or a level exit |
| Open | soft low-passed-noise breath | floor, leads nowhere new |
| Blocked | dull tock, 260 Hz | wall within the closing depth |
| Door | two 180 Hz knocks | closed door (game's own DOOR bit) |

A passable reading can carry an **unexplored** qualifier: two 900 Hz pips appended after a 30 ms gap
(`radar.h:57-59`, `docs/exploration.md`). Down is every cue lowered by `DownPitch`=0.86 (breath:
`BreathDownPitch`=0.55, ~an octave); left/right pan ±0.9, up/down centred (`radar_redesign.md` Parameters
table). Cue = 120 ms, gap between simultaneous directions = 50 ms (`radar.h:53`).

**Ranges/thresholds**, fixed, no speed scaling (rejected 2026-09-16, `decisions.md`):
`openDepth`=10 cells, `blockedDepth`=3, `close = max(blockedDepth, openDepth − bodySpan)` = 7 (3-cell
body) or 9 (point body); `reachDepth`=20; hysteresis band with no dead zone — inside it "the committed
reading stands" (`radar.h:66-80`, `radar-readings.md:176-185`).

**Anti-spam / timing:** confirm-by-travel, not time — a changed verdict must persist over
`confirmDistance`=1 cell of movement, or `stationaryConfirmMs`=100 ms if not moving (`radar.h:73-75`).
`cueIntervalMs`=500 ms **between openings only** (walls never delayed) (`radar.h:76-78`). `Frontier`
(`radar.h`, `radar_redesign.md:51-77`): once a wall is confirmed, its axis projection is remembered so
backing straight away doesn't re-read "open" — only reaching past it by half the hysteresis band does.
A "way-back" mark takes the door/exit you just walked through as read (`radar-readings.md:186-193`).

**Classification (the actual algorithm):** `ScanPassage` (`src/radar_passage.cpp`) runs a BFS with
1-cell lateral tolerance over `StructuralWorld` (`radar_structure.cpp`) — the terrain collision layer
with small clutter (≤1 tile, only while the game slides) erased — then `PassageQuery` decides Exit vs
Open vs Door vs Blocked from: a `LevelExit` sitting on the route, or (maze levels only) the route
crossing into another streamed room past `wayThrough` (the level's median opening width) (`radar.h:82-
92`, `radar-readings.md:154-175`).

**Exploration/explored-state tracking:** `ExploredRooms::Memory` — two per-level rings keyed by room
rect: **seen** (streamed) and **visited** (stood in) (`docs/exploration.md` "the two memories"). Drives
the unexplored qualifier, the terrain overview's percent, and what a regenerated map already knows
(`explored.dat`, keyed by map seed+level+room-layout digest, `exploration.md` "On disk").

**Beacon (target lock, `src/beacon.cpp`/`beacon_service.cpp`):** L3 or Ctrl+B locks the last thing the
scanner/exit-review spoke; a 620 Hz ping (`beacon_audio.cpp`) repeats faster as a proved route shortens,
panned to the route's next leg (`lookAhead`=8 cells), BFS over the same collision `World`
(`searchRadius`=158, `docs/beacon.md` Parameters). Exploration mode restricts routing to visited/seen
rooms only (`docs/decisions.md` 2026-09-24).

**Level exits / doors / footsteps:** exits (warps/stairs/portals/borders) get a spatial `portal` or
`zone transition` loop (`docs/level-exits.md`); closed doors loop by material; a stalled-footstep filter
suppresses repeated HD footfalls at an unchanged position, restored by any fractional movement
(`docs/footsteps.md`). None of this depends on the radar engine itself — separate services layered on
the same collision `World`.

**Rooms / junction announcements — closest analogue to D4's "junctions, dead ends, big rooms" ask.**
`Rooms::Describe` (`src/rooms.h:56-60`) finds the streamed room containing the player and every
passable gap on its border (`Opening{centre, width, across}`); `room_service.cpp:118` `Announce()`
speaks it (`Ctrl+M` toggle, `Ctrl+V` reviews openings one at a time). Anti-spam is `Rooms::Hysteresis`
(`rooms.h:39-49`): `settleMs`=1000, `dwellMs`=400 — a room flip must hold `dwellMs` unless it's the
first room or entered more than `settleMs` after the last change. This — not the reactive tin/tock
radar — is the model to port for automatic junction/dead-end/room-size speech: D2R has no "big room" cue
because its maze rooms are one fixed size; D4 tiles are 32×32 with a group id joining multi-tile rooms
(`research/client-memory.md` §1, `a−36`), so "big room" = several tiles sharing that group id.

## 2. Controls

All discovered bindings (`README.md`, `AGENTS.md`, `docs/decisions.md`, `docs/*.md`):

| Input | Action |
|---|---|
| L3 (short click) | lock/retarget/release beacon on the last scanner/exit-review offer |
| Right stick up/down | scanner: change category |
| Right stick left/right | scanner: step through category, nearest first |
| Hold LT ~0.5s | read the LT skill bar (unrelated to radar) |
| Ctrl+R | toggle radar |
| Ctrl+T | radar status/dump (Ctrl+T "diagnostic read", not a controller binding) |
| Ctrl+B | lock beacon (keyboard equivalent of L3) |
| Ctrl+V | review room exits one at a time |
| Ctrl+M | toggle room-exit announcements |
| Ctrl+O | terrain overview |
| Ctrl+F | toggle stalled-footstep filter |
| Ctrl+P / Ctrl+L / Ctrl+I / Ctrl+K | exit loops / item loops / item speech / container loops |
| `access-radar on\|off\|status\|dump\|test\|diagonal\|cardinal` | console, dev/diagnostic |
| `access-beacon lock\|clear\|status\|exploration\|full` | console |

**R3 is deliberately left alone** — it opens the game's emote wheel; the beacon moved off it onto L3 for
exactly that reason (`docs/decisions.md` 2026-09-25). **No settings menu**: everything is a `radar.toml`
`[radar]`/`[navigation]`/`[entity_cues]` key read at startup, or a console/keyboard toggle for the
session (`README.md` "Build and install", `docs/entity-cues.md`).

**How input is read — the important part for reuse:** `src/input_direction.h` samples **XInput
directly**, dynamically loaded (`Shutdown()` frees the DLL), **read-only** — comment at
`input_direction.h` ("BACK... reads XInput... critical rule 3 forbids speaking over a real pad, not
listening to it"): the mod never blocks or consumes pad state from the game, only observes it in
parallel. Keyboard shortcuts (Ctrl+letter) go through the D2RLoader SDK's own key-binding service
(`D2RLPlugin/input.h`), which is D2RLoader-specific and does **not** port. The XInput polling in
`input_direction.cpp`/`.h` is engine-agnostic and copies almost verbatim into any Windows process,
injected or not.

## 3. Implementation architecture

- **Engine-agnostic core** (verified: these headers include only `radar.h`/each other, no SDK/native
  headers): `radar.h/.cpp`, `radar_passage.cpp/.h`, `radar_structure.cpp`, `radar_grid.cpp`,
  `navigation_grid.h/.cpp`, `rooms.h/.cpp`. All operate on a plain `World`/`RoomGrid`/`Settings` struct
  (`radar.h:94-119`) — a room-grid abstraction: dimensions, a `uint16_t` cell buffer, `visited` bool,
  plus player position, `walkingMask`, `exits`, `frontier` marks. This is a **general-purpose 2D
  passage/room classifier**, not a D2 reader.
- **D2R-specific glue** (does not port): `native_world.cpp` (reads the struct above out of D2R
  process memory), `radar_service.cpp` (lifecycle/dialogue/pause gating via D2RLoader SDK callbacks),
  `radar_config.cpp` (`radar.toml` under D2RLoader's config folder), `radar_ui.cpp`/`radar_readout.cpp`
  (Ctrl+T dump format).
- **Audio backend:** pure synthesis (sine tin/tock/knock tones + filtered-noise breath), no sample
  files, no third-party audio library — Windows' built-in `waveOut*` mixer (`src/wave_out.h`,
  `radar_audio.h:8` "Windows' built-in mixer; no new native library"). Panning is a simple stereo gain
  split (`ScreenAxes` → left/right ±0.9), not HRTF/3D audio. Item/ground-item loops use XAudio2 +
  X3DAudio instead (`item_audio.cpp`/`item_spatial.cpp`) — a **different, heavier** backend, not shared
  with the radar. **The radar's synthesis math (`radar_synth.cpp`) is the reusable part; `wave_out.h`
  (raw WinMM) is not** — a .NET port would replace only the output sink.
- **Speech path:** PRISM loaded **dynamically** (`LoadLibrary`/`GetProcAddress`, undecorated x64
  exports), `screen_reader.cpp`, one dedicated speech thread (PRISM/NVDA `speak()` blocks 300-800 ms —
  `AGENTS.md` "Hard-won facts"). Matches this project's own plan (Prismatoid or raw P/Invoke to
  `prism.dll`).
- **Dependencies/licences:** D2RLoader PluginSDK (git submodule, D2R-specific, MIT-ish per its own repo,
  irrelevant to D4) · PRISM (MPL-2.0, `third_party/prism/LICENSES`, dynamic `.dll`) · project code
  itself MIT (`README.md` "License"). No pathfinding library — `Beacon::FindRoute` is a hand-rolled BFS
  over the dilated collision field (`docs/beacon.md` "Route search").
- **Data model:** no persistent map/graph beyond `ExploredRooms::Memory` (seen/visited rings) and
  `explored.dat` (bounded binary archive, 4 MiB / 400 layout entries, keyed by seed+level+room digest).
  The live "world" is rebuilt from a fresh native snapshot every poll (50 ms) — no retained level graph
  except the room list used for beacon routing (rebuilt from explored rooms each update).

## 4. Reuse assessment for D4

**Copy near-verbatim (logic, not code-for-code, since the language differs):**
- `radar.h`/`radar.cpp` state machine (`Sensor`, `Frame`, confirm-by-travel, `Frontier`, cue-interval
  gating) — pure data-in/state-out, zero D2R coupling.
- `radar_passage.cpp` BFS-with-lateral-tolerance passage scan, and `radar_structure.cpp`'s clutter
  filter — both operate on the generic `RoomGrid` cell buffer. D4's 0.5-unit voxel walkability grid
  (`STATUS.md` "Tile walkability offline") slots directly into this abstraction; only the cell size
  (0.5 unit vs. one D2R subtile) and the exit-mask semantics (D4's `eTileType` N=1 S=2 E=4 W=8 vs. D2R's
  discovered `LevelExit` list) need adapting.
- `rooms.h`/`rooms.cpp` (`Describe`, `Opening`, `Hysteresis`) — this is the closest existing artifact to
  the requested "junctions/dead ends/big rooms" feature; D4's DRLG tile + group id is a ready-made `Room`
  equivalent.
- `radar_synth.cpp`'s synthesis formulas (tone/noise generation, panning, pitch-lowering) — trivial to
  reimplement in any language; the DSP, not the C++, is the asset.
- `input_direction.h`'s XInput-polling *pattern* (dynamic-load, read-only, edge-detected press state)
  — directly usable for D4 since D4 is likewise external and must read the pad without a hook.
- `explored_rooms.*` / `explored_store.*` design (two rings, keyed by a stable per-layout digest,
  bounded archive) — directly applicable to D4's per-dungeon seen/visited tracking.

**Tied to D2R internals, does not port:** `native_world.cpp`, `native_rooms.cpp`, `native_entities.cpp`,
`native_level_exits.cpp`, `radar_service.cpp`'s D2RLoader lifecycle hooks, `radar_config.cpp`'s
D2RLoader-relative TOML path, everything under `external/PluginSDK`, and the WinMM `wave_out.h` sink
(replace, don't port).

**C++ vs. C#/.NET 10 for the D4 companion.** **Recommend C#/.NET**, not C++:
- The *reusable* D2R material is algorithmic (state machines, BFS, DSP formulas), not a binary or a
  library with a foreign ABI — nothing here forces staying in C++. The amount of code that would compile
  unchanged is ~zero, because the "reuse" is a port of ideas over a different data model (voxel grid +
  32-unit DRLG tiles vs. D2R's subtile `RoomGrid`), not header inclusion.
  Estimate: the four core files (`radar.h/.cpp`, `radar_passage.cpp`, `radar_structure.cpp`,
  `rooms.h/.cpp`) total roughly 1,200–1,500 lines of pure logic worth porting; everything else in `src/`
  (~140 files) is D2R-specific and irrelevant.
- D4's own tooling (`tools/D4Data`, `tools/D4MemScan`) is **already C#** (`.csproj` throughout) and is
  the thing that supplies the `World`/`RoomGrid`-equivalent data (player position, tile exit masks,
  voxel walkability) — reusing that data path in-process (same solution, same types) beats a C++
  process reading output from, or P/Invoking into, a separate C# memory reader.
  Prismatoid's own .NET binding targets **.NET 10**, matching this project's chosen TFM (`STATUS.md`
  "Screen-reader transport"), so no P/Invoke shim is needed for PRISM either — a C++ build would still
  have to dynamic-load `prism.dll` manually as D2R does.
  A C++ companion would gain nothing except reusing WinMM/XInput headers, which are a handful of
  Win32 API calls, equally available from C# via P/Invoke.
- Build system: D2R's is CMake/Ninja/MSVC purely because it must produce a DLL the D2RLoader can inject.
  D4 Dungeon Assist has no injection target (`STATUS.md`: "no modding framework, no injection... external
  companion app") — `dotnet build` is simpler and already the project's chosen command.

## 5. Input mapping: D2R radar inputs → D4 data sources

| D2R radar input | D4 equivalent | Status |
|---|---|---|
| `World.player` (subtile position) | player position (`research/client-memory.md` §3, static `.data` record, RVA `0x3445004`, 4 Hz) | **have** |
| `World.rooms[].cells` (subtile collision grid) | per-tile 0.5-unit voxel walkability grid (`STATUS.md` "Tile walkability offline") | **have**, offline-proven; live scan not yet run against the game |
| Room rect / neighbours (`RoomEx` tile rect) | DRLG tile (32×32 units) + `eTileType` exit mask, group id for multi-tile rooms (`client-memory.md` §1) | **have**, only ~2 tiles loaded around the player at a time (narrower window than D2R's 152-subtile snapshot) |
| `World.exits` (warps/stairs/portals/borders) | dungeon transitions from the log file (world/subzone strings, `STATUS.md` "World / dungeon identity") | **partial** — identity yes, in-world position of the exit tile not yet derived |
| `World.walkingMask` / body footprint dilation | none yet — D4 player collision radius/footprint unmeasured | **missing** |
| Player heading (D2R: movement direction only, radar is screen-relative, not heading-relative) | actor quaternion → yaw (`client-memory.md` §3, `+0x6C`) | **have**, though the radar design itself doesn't need heading (screen-relative by design, matching the user's fixed isometric camera) |
| Screen ↔ world rotation (D2R: stick cardinals are world diagonals) | same 45°-rotated convention confirmed live (`client-memory.md` §3 table: stick up = −X−Y, tile N = screen up-right) | **have** — and it is the *same* geometry problem D2R solved with the `Diagonal` frame: D4's tile N/S/E/W exits will read as screen up-right/down-right/down-left/up-left, so the ported radar's world-axis-aligned "Diagonal" rays, not the raw stick cardinals, are what should drive D4's junction/opening detection |
| `Levels.txt DrlgType` (maze vs. outdoor vs. preset) | D4 dungeons are `eLayoutType` 2 (procedural) uniformly (`STATUS.md` "Dungeon worlds are procedural") — simpler, one room model instead of three | **have**, and simpler than D2R |
| `ExploredRooms::Memory` (seen/visited rings) | none yet — no persisted per-dungeon exploration store | **missing**, but the design ports directly once dungeon identity + tile coordinates are stable keys |
| Beacon target list (scanner-offered units/points) | no entity/object scanner yet (P4/P5 in `STATUS.md` are still `todo`) | **missing** — out of scope for the first "automatic announcement" feature anyway |

**Main gap:** D4's live loaded-tile window (~2 tiles around the player) is much narrower than D2R's
152-subtile snapshot, so `reachDepth`/`boundaryRange`-style lookahead (20 cells in D2R) will need
smaller defaults, or the radar will report "unknown" past 1-2 tiles far more often than D2R ever did.
