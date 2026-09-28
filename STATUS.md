# STATUS — Diablo IV Dungeon Assist

**Last updated:** 2026-09-28 — `tools/D4MemScan` scanner switched to the validated CScene record signature (30 unit tests, offline only, not yet run against the game).

## Identity

- **Engine / framework:** Blizzard proprietary ("Fenris"), always-online. **No modding framework, no injection** — this is an *external companion app*; the game install and process stay untouched.
- **Screen-reader transport:** .NET companion app → PRISM (`prism.dll`, Prismatoid binding if it loads on the chosen TFM).
- **Build command:** `dotnet build`
- **Game install:** `D:\games\battlenet\Diablo IV` (build 3.2.1.73552 on 2026-09-16)
- **Run / test:** run the companion app, then play D4 with the screen reader on.
- **Brief:** [`diablo4-dungeon-assist-research-for-claude.md`](diablo4-dungeon-assist-research-for-claude.md)

## Section status

| Section / feature | Status | Notes |
|---|---|---|
| P0 Offline proof (CASC → World/Scene) | done | [dungeon-data](research/dungeon-data.md); CASC/definitions reading factored into `tools/D4Data`, used by `tools/D4DungeonDump` and `tools/D4MemScan` |
| P1 DRLG / layout graph | blocked | layouts are server-generated; instance tile list only in client memory; scanner ready, see P3 |
| P2 Game TTS capture | todo | route chosen: NVDA add-on ([runtime-signals](research/runtime-signals.md) §1) |
| P1b Live layout (CScene records) | done | static route `[[base+0x368D188]+0xC18]` validated live, used by the app every 1 s. Preset rooms are split into 96×96 pieces whose live exit mask is a subset of the offline one (`Triad_B_ThroneRoom_N02_E03_DGN`: offline 15, live 7) — matcher accepts subsets since 2026-09-28. Free slots (`id == -1`) keep stale records: never trust a full-scan hit without the route |
| P3 Runtime anchor | done | `D4MemScan player`: static record (RVA 0x3445004, live `MEM_MAPPED` view) found in ms, updates live at ≥4 Hz; verified in 2 dungeons |
| P4 Auto room announcements (D2R `Rooms::Describe` port) | wip | live test 2 (2026-09-28): user finished the dungeon faster but directions felt imprecise. Fixed: bearing to the farthest line-of-sight path point (was a 3-unit look-ahead → diagonal bias), widest run wins per neighbour (a 1-cell noise run hid the real 15-wide exit), start-cell snap, 4-unit doorway stickiness + silent rekey, entry opening not spoken ("Sin otras salidas."), first room = "Entrada.", repeated directions grouped. Needs live test 3 |
| P5 Procedural dungeon / exploration tracking | todo | |
| P5b D2R radar (port) | wip | `src/D4Assist.Radar` ([README](src/D4Assist.Radar/README.md)) + `DungeonRadarWorld`/`RadarBridge`; Cardinal frame; cues queued like D2R (`CueScheduler`). **Live test 3 (2026-09-28): works, user impressed**, incl. a preset throne room. Depths (10/7) not tuned yet. No beacon/XInput yet |
| P6 UX (cues, TTS, anti-spam, controller) | todo | |
| Adapter + Speech (`Speak("test")` via PRISM) | done | Prismatoid 0.3.0, backend NVDA; `src/D4Assist` companion app |

## Derived facts

| Fact | Value | Source |
|---|---|---|
| Game speaks via Tolk; NVDA outranks the SA shim | Tolk driver order JAWS → WE → NVDA → SA | [runtime-signals](research/runtime-signals.md) §1 |
| World / dungeon identity | `Client entered world \| world: <World>` and `[WarpManager] Intro portal to dungeon Subzone '<name>'` | `FenrisDebug.txt`, [runtime-signals](research/runtime-signals.md) §2 |
| Dungeon worlds are procedural | World `eLayoutType` 2 for all `DGN_`/`CSD_`/`QST_`/`PIT_`/`TWR_` (802 worlds), empty scene list | [dungeon-data](research/dungeon-data.md) §3 |
| Tile walkability offline | 0.5-unit voxel grid (64×64 × 72 levels) in scene payload | [dungeon-data](research/dungeon-data.md) §4 |
| Tile exits offline | scene exit bitmask N=1 S=2 E=4 W=8 | [dungeon-data](research/dungeon-data.md) §4 |
| WalkableMap orientation confirmed | row = X (north-south, row 0 = N edge), column = Y (west-east, col 0 = W edge); N/S/E/W walkable runs land exactly on their exit sides | `D4DungeonDump --grid` on `TT_Step_City_to_Protodun_Jail_NSEW_01` (SNO 715062, 2026-09-28); pinned in `WalkGridOrientationTests` |
| Player collision radius (min opening width) | ≈0.38 world units, unmeasured/task-supplied — not found in any research doc; still listed as missing in [d2r-radar-port](research/d2r-radar-port.md) §5 | `OpeningFinder.PlayerCollisionRadius` |
| Axes | X = south, Y = east, Z = up | [dungeon-data](research/dungeon-data.md) §5 |
| Instance layout reaches the client as | `RevealSceneMessage` (scene SNO + transform), progressively | [dungeon-data](research/dungeon-data.md) §6; exe log strings |
| Exe code encrypted on disk | `.text` entropy 8.00 (146/147 blocks); decrypted at runtime by `diablo_iv_loader.dll` → no static code RE | PE triage 2026-09-26 |
| Game RTTI stripped | only third-party (ICU) `.?AV` names remain | exe `.rdata` scan |
| Live scan cost | 16 GB of RW memory scanned in ~2 min (parallel, array-indexed candidates, 2381 DRLG tiles) | live run 2026-09-28, `DGN_Step_GuulrahnSlums` |
| Naive signature fails | "SNO → unit quat → grid-aligned vec3" within 256 B yields noise (mostly wp = 0) | `scenes2.json` (scratchpad) |
| Live CScene record found | `snoScene` + transform @+80 + local AABB @+136 (== offline) + exit mask @−44; only ~2 tiles around the player loaded | [client-memory](research/client-memory.md) §1 |
| Revealed list at entry | all scene ids + SNOs known at entry, no positions | [client-memory](research/client-memory.md) §2 |
| Player position (static) | record in main-module `.data`: `p−16` scene SNO, `p−12` world SNO, `p−8` actor SNO, `p` vec3; 1 copy, stable RVA per build | [client-memory](research/client-memory.md) §3 |
| Player heading | actor record (heap, stride 0x680) `+0x6C` quaternion, yaw from +X to +Y | [client-memory](research/client-memory.md) §3 |
| Screen ↔ world (camera) | stick up = −X−Y, right = −X+Y, down = +X+Y, left = +X−Y (tile exit N = screen up-right) | 4 live moves 2026-09-28, [client-memory](research/client-memory.md) §3 |
| Scene record signature | `RevealSceneMessage`: `snoScene` @344, `transform` PRTransform (quat + vec3) @356 | d4data `definitions.json` (build 3.2.1.73552) |
| No verbose/debug log switch for players | `DebugLoggingTypesEnabled` is a server/debug attribute, not a pref | exe strings + `LocalPrefs.txt` |

## Next step

User's next ask (2026-09-28): audio cues for quest objects and things to interact with to progress (e.g. prisoners). Needs a live actor/gizmo list (only the player record is known so far) — find it the same way as the scene pool (static route from `.data`). Then: L3 beacon via read-only XInput (D2R design).

## Known issues / open questions

- Preset rooms (96×96 pieces): the walkable area doesn't touch the tile edge, so `OpeningFinder` finds no runs and falls back to edge midpoints (path fails → straight line). Scan a band of a few cells inward instead of the boundary row. The radar is unaffected.
- The first room after the app starts is called "Entrada." even when the app was started mid-dungeon.
- The app loads walkability for all ~4000 scenes (≈650 MB); load lazily.

- Player position and the instance tile list exist only in client memory. Minimap CV was rejected by the user.
- Target setup: controller + NVDA via the in-game third-party SR option — TTS capture must not silence NVDA.
- UX decisions (2026-09-28): speech in Spanish; first feature = automatic announcements (junctions, dead ends, big rooms) in stick directions. Controls: copy the user's D2R radar (`D:\code\c++\diablo 2 access project\d2r-access`), see [d2r-radar-port](research/d2r-radar-port.md). Language: C# (.NET 10).
