# D4MemScan

Read-only diagnostic memory scanner: locates a revealed dungeon's tile layout and, from that, the player's
position, in the running Diablo IV process. Built to test the accessibility mod's "where is the player in the
generated dungeon" problem before wiring it into the mod itself — see `research/dungeon-data.md` §9 and
`CLAUDE.md` §2 for why this can't be solved from offline data alone (dungeon layouts are server-generated).

The read-only process layer and record matchers now live in `src/D4Memory/` (a class library shared with the
`D4Assist` app) — D4MemScan itself keeps only its CLI commands and the diagnostics-only logic (pose
narrowing, the ASCII map, JSON report models). See `src/D4Memory`'s own doc comments for that half.

## Safety constraints (non-negotiable)

- Opens the process with `PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ` only — see
  `src/D4Memory/Safety/NativeMethods.cs` / `ProcessReader.cs`. No `SeDebugPrivilege`, no write/inject/debug
  APIs, no handle duplication, no anti-cheat evasion. Neither project declares such an API anywhere.
- Enumerates memory with `VirtualQueryEx` and reads only `MEM_COMMIT` regions whose protection is
  `PAGE_READWRITE` or `PAGE_READONLY`; `PAGE_GUARD`, `PAGE_NOACCESS` and every executable protection are
  skipped. Reads happen in bounded, overlapping windows; a failed chunk is silently skipped. The
  module-scoped enumeration used by `ModuleLocator` (`ProcessReader.ImageRegions`) additionally allows
  `PAGE_WRITECOPY` — an unmodified module data page reads as copy-on-write until first write — but only for
  that image-scoped scan; the heap scans above keep their original two-protection filter unchanged.
- The process handle is opened for the duration of one command and closed immediately after (a `using` scope
  in each `Commands/*Command.cs`); the tool never keeps it open in the background.
- The process is found by name `Diablo IV`; if it isn't running, the tool fails with a clear message.

## Commands

```
D4MemScan world --defs <definitions.json> [--install <dir>]
D4MemScan scenes --defs <definitions.json> [--install <dir>] [--tiles-only] [--focus <name-regex>] [--replay <capture.focus.json>] [--dump-module <file.focus.json>] [--out <file.json>]
D4MemScan pose-snapshot --scenes <report.json> --defs <definitions.json> [--install <dir>] [--out <file.json>]
D4MemScan pose-filter --in <file.json> --mode changed|unchanged [--out <file.json>]
D4MemScan pose-context --in <file.json> [--out <file.focus.json>]
D4MemScan player --defs <definitions.json> [--install <dir>] [--samples N] [--interval-ms M] [--dump-module <file.focus.json>] [--replay-module <file.focus.json>] [--snapshot-pointers <file> [--snapshot-count N --snapshot-every-ms M]]
D4MemScan peek --address <hex>[,<hex>...] [--seconds N] [--interval-ms M]
D4MemScan screens [--install <dir>] [--watch <seconds>] [--interval-ms M]
D4MemScan conversations --defs <definitions.json> [--install <dir>] [--watch <seconds>]
D4MemScan quest --defs <definitions.json> --sno <quest> [--install <dir>] [--callbacks] [--group <gbid>] [--actors <regex>] [--world-actors <regex>] [--find] [--watch <seconds>]
D4MemScan gizmos --defs <definitions.json> (--type <eActorGizmoType> | --match <name-regex> [--type N]) [--install <dir>]
D4MemScan scene-route --defs <definitions.json> [--install <dir>] [--compare]
D4MemScan actors --defs <definitions.json> [--install <dir>] [--range <units>] [--watch <seconds>] [--match <name-regex>] [--interval-ms <ms>] [--words <hex,hex>]
D4MemScan acd --defs <definitions.json> [--install <dir>] [--range <units>] [--match <name-regex>] [--watch <seconds>] [--interval-ms <ms>] [--names <attribute-regex>] [--slot-words <hex,hex>]
D4MemScan layout --defs <definitions.json> [--install <dir>] [--shift <hex>]
D4MemScan waypoints --defs <definitions.json> [--install <dir>] [--locale deDE] [--live] [--scan [--min N] [--saved] [--watch <seconds>]]
```

`--install` defaults to `D:\games\battlenet\Diablo IV`. `--out`/default output paths land under `%TEMP%`, never
the repo.

1. **`world`** — offline only, no process access. Parses the last `[Game] Client entered world` line of
   `<install>\FenrisDebug.txt` and resolves it to a World SNO via CoreTOC.
2. **`scenes`** — opens Diablo IV (must be running) and scans readable memory for the real in-memory scene
   layout (see `research/client-memory.md` §1–2), validated field-by-field against each candidate's offline
   scene data rather than by value coincidence:
   - **CScene tile records**: matched by a live record whose quaternion is a unit rotation, whose local AABB
     equals the candidate's offline AABB, whose world AABB center equals local center + world position, whose
     has-DRLG-info flag is 1, and whose exit mask and tile hash agree with both the offline data and their own
     in-record copies (`CSceneRecordMatcher`). The candidate set is every Scene SNO in CoreTOC by default
     (`--tiles-only` restricts it to non-zero exit masks).
   - **Revealed-scene list entries**: 96-byte "A" records naming every revealed scene of the dungeon, with no
     position (`RevealedListMatcher`).
   Writes a JSON report (`--out`) with the distinct tile hits, a vtable-value histogram, the union of world
   AABBs, an exit-consistency check over single-grid-cell tiles (multi-cell tiles are reported as skipped, not
   guessed), the revealed-scene list, and a top-down ASCII map. Console summary is at most 25 lines.
3. **`pose-snapshot`** / **`pose-filter`** — classic memory-narrowing for the player's position. `pose-snapshot`
   takes every 4-aligned float triple inside the `scenes` report's world-AABB bounds (plus a Z margin),
   dropping any candidate that falls on a known-unwalkable voxel of its tile (walkability is re-read offline
   per tile SNO, not embedded in the report). `pose-filter` re-reads a snapshot's addresses later and keeps
   the ones that moved past a minimum distance (`--mode changed`) or stayed put (`--mode unchanged`).
4. **`player`** — locates the local player's static position record directly, instead of narrowing candidates:
   resolves the current World SNO from the log, builds the full Scene SNO set from CoreTOC and the player
   actor SNO set (`Scan/PlayerActorSnos.cs`; see provenance note below), finds the main module without
   `EnumProcessModules`/Toolhelp (`Safety/ModuleLocator.cs`: match every `MEM_IMAGE` region's in-memory PE
   header against the on-disk exe's `TimeDateStamp`/`SizeOfImage`), then scans only that module's writable
   pages for the record (`Scan/PlayerRecordMatcher.cs`; see `research/client-memory.md` §3). Prints the
   module base, the record's address and RVA, and the match count (expect exactly 1). Then samples the
   32-byte window around the position `--samples` times every `--interval-ms` (defaults: 10, 250ms),
   printing elapsed time, position, scene SNO/name, delta, and the movement bearing (compass, engine axes
   X=south/Y=east). `--dump-module` saves every main-module writable page as a `--focus`-format capture (a
   `module-base` sentinel entry plus one `module` entry per page) for later offline replay with
   `--replay-module`, which runs the same matcher/sampling logic against the capture instead of the process.
   `scenes --dump-module` does the same module dump right after its heap scan, so a CScene report and a
   module dump come from the same moment (for later pointer-chain analysis).
5. **`scene-route`** — reads the same CScene tiles as `scenes`, but via the static pointer-chain route
   (`research/client-memory.md` §1: module → game object → scene container → chunk tables → slots) instead
   of scanning all of readable memory (`D4Memory.SceneRoute.SceneRouteReader`, offsets from
   `D4Memory.SceneRoute.GameBuildOffsetsTable`, keyed by the exe's PE `TimeDateStamp`; an unknown build makes
   the route unavailable, and the caller should fall back to `scenes`). Every read is validated (container
   bookkeeping + the CScene vtable); an inconsistency skips that slot rather than throwing. `--compare` also
   runs the full `scenes` heap scan and reports how many route tiles the scan confirms, and how many scan
   tiles the route missed — this is the live validation step STATUS.md calls out as pending.

6. **`actors`** — reads the RActor pool via the static route (`research/client-memory.md` §4; offline RE, **not yet
   validated live**; `D4Memory.Actors.RActorReader` over the shared `D4Memory.Pools.PoolContainer`). Finds the
   player's static record like `player`, then prints PASS/FAIL checks: (a) exactly one placed RActor with the player
   actor SNO within 1 unit of the record, (b) its scene id resolves in the scene pool to `snoScene == p-16`; and (c)
   prints its world id next to `p-12` and the scene slot's u32 at +0x18. Then lists placed actors within `--range`
   (default 60 units) by distance with the stick direction, actor SNO/name and ActorDefinition `eType`/`eActorGizmoType`
   (read by field name from `definitions.json`); at most 40 lines on the console, the full list as JSON under `%TEMP%`.
   `--watch N` picks the nearest gizmo/prisoner actor (or, with `--match`, the nearest actor whose name matches) and, every `--interval-ms` (default 250) for N seconds, prints the
   4-byte words of its slot that changed (and slot free/reuse), to find the "used" signal while you free a prisoner or
   pull a lever.
   `--words 0x148,0x1F4` prints those slot words too, next to the use words of every gizmo in the listing and JSON, to compare used and untouched gizmos in one snapshot.

7. **`player --snapshot-pointers`** — depth-1 heap snapshot for UI-state searches (`Commands/PointerSnapshotWriter.cs`):
   - It keeps every word in the live module's writable data that points into readable memory, plus 0x400 bytes at each target.
   - A series (`--snapshot-count`) numbers the files and logs each capture time.
   - Diffing snapshots taken with a panel closed and open finds `[base+rva]+offset` routes. That is how the
     conversation state was found ([client-memory](../../research/client-memory.md) §5).
8. **`peek`** — prints every change of a few u32 words at absolute addresses, to confirm a candidate live.
9. **`acd`** — reads the ACD pool (the actors' game records, route `[[G+0xAB0]]`, `research/client-memory.md` §4;
   `D4Memory.Actors.AcdReader`) for the placed RActors within `--range` (default 30 units) that have an ACD id: every
   gizmo, or with `--match` the actors whose name matches.
   - A PASS line checks that each ACD slot holds the same RActor id and actor SNO as its RActor.
   - One line per actor lists its attributes by name (`AcdAttributeReader`, names from the game's own table via
     `AttributeNameTable`). `Name[0x..]` is a parameterized attribute; `@1`..`@3` marks entries of the container's
     other maps. The JSON under `%TEMP%` has the full list.
   - `--watch N` follows the nearest listed ACD for N seconds (every `--interval-ms`, default 250). It prints each
     attribute added, changed or removed, and the slot words that changed. It stops when the ACD leaves the pool.
   - Example, the "used" test on a corpse: `acd --defs data/definitions.json --match Corpse --range 5 --watch 60`, then
     use the corpse. `Gizmo_Has_Been_Operated: (absent) -> 1` is the expected line.
   - `--names <regex>` lists the game's attribute names (with ids) matching the pattern instead, e.g. `--names Gizmo`.
   - `--slot-words 518,258` adds those u32 words of each ACD slot to its line (`+0x518=…`).

10. **`screens`** — finds every `screen_*` name in the game's memory (330 distinct in 2026-09-30), locates each one's
    record in the heap screen registry (`D4Memory.Ui.ScreenRegistryReader`, `research/client-memory.md` §5), prints the
    open ones, then every open/close with its time for `--watch` seconds (default 120, every 200 ms). About 35 s to
    start. Use it to see which screens a game state opens, e.g. a cutscene or a new panel.

12. **`layout`** — checks the structure layout against the live game (`D4Memory.SceneRoute.LayoutDiscovery`,
    `research/client-memory.md` §6) twice: with the known layout (each field "as known" on a build it fits; after a game
    update it names any field that moved and where to), then with every field shifted by `--shift` (default 0x10), which
    must read MOVED back to the real offsets. Live 2026-10-01 (town, 239 RActors, 121 ACDs): all seven fields found back
    in 56 ms. `layout --defs data/definitions.json`.

11. **`conversations`** — searches the module data for the playing-conversation list and the dialogue menu
    (`D4Memory.Ui.ConversationDiscovery`) every 2 s until both are found, then prints their contents on each change. Talk
    to an NPC while it runs: each part is only found while live.

12. **`quest --sno N`** — the quest's phases (`dwUID`, array order) and the objects each phase names (offline); `--callbacks` adds each callback's event, indicator, tokens and script parameters; `--group`, `--actors` and `--world-actors` look up an actor group's markers, actors by name (team, NPC flag, family) and the actors a marker set places. `--find`
    prints the {quest SNO, phase} pair the app reads in the module data; `--watch S` watches every phase UID within
    ±0x100 of the quest SNO anywhere in memory for S seconds, to see where the phase lives when the quest advances.

13. **`gizmos --type N`** — every actor whose `eActorGizmoType` is N, by name (offline census), e.g. `--type 29` →
    only `POI_Look_At`. `--match <regex>` lists the actors whose name matches instead, with eType, gizmo type, default
    team and fIsNPC (e.g. `--match Prisoner`).

14. **`waypoints`** — every waypoint of the game (`WaypointCatalog`: LevelArea, name in `--locale`, world, spot).
    `--live` prints what the app reads: the local player's activated waypoints and the world map's center and zoom.
    `--scan` finds runs of waypoint LevelArea SNOs anywhere in memory (`--saved`: only those that fill a
    `PlayerSavedData.arWaypointsActivated`, with seconds played; `--watch S`: print each run that changes), the way the
    player slots were found ([client-memory](../../research/client-memory.md) §4).

Every live command finds the build's addresses itself (`Offsets.cs` → `BuildOffsetsDiscovery`) and prints them, e.g.
`Module 0x7FF7B55F0000: game object at RVA 0x3670918, attribute names at RVA 0x36CF540.`

## Layout

- `src/D4Memory/Safety/` — the process-access policy described above; nothing outside this folder touches
  the game process. Also holds `ModuleLocator`/`PeHeaderReader`, which find the main module's base by
  PE-header signature matching rather than `EnumProcessModules`/Toolhelp.
- `src/D4Memory/Scan/` — the two record matchers (`CSceneRecordMatcher`, `PlayerRecordMatcher`),
  `SceneScanner`, `RevealedListMatcher`, `CandidateIndex`/`CandidateScene`/`CandidateSceneBuilder`,
  `PlayerActorSnos`, `PlayerSampleWindow`, `RegionWindows` and `IMemoryReader`/`MemoryRegion` — shared with
  `D4Assist`. Reads memory only through `IMemoryReader`, so tests use an in-memory fake.
- `src/D4Memory/SceneRoute/` — the static scene route (`SceneRouteReader`, `GameBuildOffsets`/`GameBuildOffsetsTable`).
- `src/D4Memory/Pools/` — `PoolContainer`, the chunked pool walk shared by the scene, RActor and ACD routes; `src/D4Memory/Actors/` — `RActorReader`, `AcdReader`,
  `AcdAttributeReader`, `AttributeNameTable`.
- `Scan/` (this project) — diagnostics-only logic that doesn't belong in the shipped app: exit-consistency
  check, ASCII map, pose scan/filter/walkability, JSON report models, `CaptureMemoryReader` (`--replay`).
- `Commands/` — CLI glue per subcommand.
- CASC/`definitions.json` reading is shared with `D4DungeonDump` via the `D4Data` library
  (`src/D4Data/`) — see its own scope for that part.

## Tests

`tests/D4Memory.Tests` (xUnit) covers the two record matchers (valid hit, each single-field corruption,
window edges), the static scene route (chunk-table/stride math, free slots, bad-vtable rejection, an
unreadable/implausible container), PE-header parsing, and `ModuleLocator`'s base-matching/writable-page
filtering — all against synthetic buffers/fakes, no process access.

`tools/D4MemScan.Tests` (xUnit) covers the exit-consistency check (including a multi-cell tile), the ASCII
map builder, the pose filter, the bearing formatter, the world-log parser, and JSON round-tripping of the
`scenes` report — reusing `D4Memory.Tests`' `FakeMemoryReader` where needed, no process access.
