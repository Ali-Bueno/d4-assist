# D4Assist.Radar

Engine-agnostic port of the user's Diablo II Resurrected reactive radar (`d2r-access` `src/radar.h/.cpp`,
`radar_passage.cpp`, `radar_structure.cpp`; study in `research/d2r-radar-port.md`). BCL + `D4Assist.Audio` only
(for `RadarRay`/`RadarFrame`/`RadarCueKind`); no game or process access. The host adapter is
`src/D4Assist/Game/DungeonRadarWorld.cs`, the wiring `Game/RadarBridge.cs`.

## Pipeline (one poll, `RadarEngine.Tick`)

`IRadarWorld` (walkable / blocked / unknown per 1-unit cell, stable room ids, visited rooms, typical doorway
width) → `LocalGrid` window around the player → optional `ClutterFilter` → per ray `PassageScanner` (BFS, one
cell of lateral slack) → `RadarEngine.Qualify` (unexplored) → `RadarSensor.Observe` (confirm, wall memory,
way-back, cue interval) → `RadarCue`s for `RadarCuePlayer`.

## Rays and axes

`RadarDirectionModel` (Audio) holds the only ray table, as screen vectors. `RadarRays` maps each one into engine
axes (X = south, Y = east) through the camera measured live on 2026-09-28: screen right = (−1,+1)/√2, screen
down = (+1,+1)/√2 (STATUS.md "Screen ↔ world"). Result: Cardinal Up/Right/Down/Left = (−1,−1), (−1,+1), (+1,+1),
(+1,−1) (normalized), stepping diagonally in the engine grid like D2R's cardinals; Diagonal Up/Left/Right/Down
= −Y, −X, +X, +Y (screen up-left, up-right, down-left, down-right). D2R's own world vectors are **mirrored** in
left/right relative to D4 (D2R Left = (−,+), D4 Left = (+,−)), so its table is not copied. Pinned in
`RadarRaysTests`.

## Readings in D4

| Reading | Rule |
|---|---|
| Blocked | proved route ≤ closing depth (7) |
| Open | route ≥ opening depth (10), no room left, no unknown reached; inside the band (7, 10) the committed reading stands |
| Exit | the route arrives within a doorway of the player's room: centre ahead within depth + doorway width and within half its width of the ray (D2R's level-exit rule, fed with RoomModel doorways), **or** it enters another room and clears its border by the doorway width (D2R maze rule), **or** it steps into unknown cells (beyond the loaded tiles) |
| Door | unused: D4 doors are not read yet |

Unexplored rides on Exit only: the room beyond was never stood in, or the route reaches unknown cells.

## Constants (`RadarSettings`)

| Value | D2R source | D2R | D4 default |
|---|---|---|---|
| `Frame` | radar.h:75, decisions.md 2026-09-25 | Cardinal | Cardinal |
| `PollMs` | radar.h:62 | 50 ms | 100 ms (`GameSession.DefaultPlayerPollIntervalMs`) |
| `OpenDepth` | radar.h:78 | 10 | 10 |
| `BlockedDepth` | radar.h:79 | 3 | 3 |
| `BodySpan` (threshold term) | radar_passage.cpp:31, collision-semantics.md:42 | 3 (player cross) | 3 |
| `CloseDepth` | radar.cpp:18-20 | 7 | 7 |
| `ReachDepth` | radar.h:93 | 20 | 20 |
| `BoundaryRange` | radar.h:77 | 20 | 20 |
| `TeleportDistance` | radar.h:80 | 20 | 20 |
| `ConfirmDistance` | radar.h:85 | 1 | 1 |
| `StationaryConfirmPolls` | radar.h:86 | 2 polls (100 ms) | 2 polls (200 ms) |
| `MaxGapPolls` | radar.h:81 | 250 ms (5 polls) | 5 polls (500 ms) |
| `StoppedMs` | radar.h:82 | 200 | 200 |
| `CueIntervalMs` | radar.h:87-90 | 500 | 500 |
| `LateralTolerance` | radar_passage.cpp:46-48 | 1 | 1 |
| `ClutterSpan` | radar_structure.cpp:11-15 | 5 | 5 |
| `StructuralRadius` / `PassageRadius` | radar_structure.cpp:71-74 / radar_passage.cpp:32-34 | 28 / 24 | 28 / 24 |
| `WayThrough` | radar.cpp:21-27, rooms.cpp:86-99 | median opening, clamped [3, 10] | same, from RoomModel openings |
| retreat margin | radar.cpp:119 | (open − close)/2 = 1.5 | 1.5 |
| `RemoveClutter` | radar.h:121-123 (`World::slides`) | off (wall-slide prevention on) | off |

## Deviations from D2R

- **Unknown cells are an unexplored Exit** at any depth in the window; D2R made the passage unavailable when
  unknown lay within the opening depth. A passage is unavailable only when the player's own cell is unknown.
- **Point footprint.** D4's walk map is already per-position walkability, so cells are not dilated by a body;
  `BodySpan` keeps D2R's 3 only in the threshold formulas. With no footprint, the BFS refuses a diagonal step
  whose two orthogonal cells are both non-open (a zero-width gap D2R's cross body could not take either).
- **Doorways are D2R's level exits.** `IRadarWorld.Doorways` holds every room's RoomModel openings (not the
  fallback ones), each with the room beyond, and the scan's level-exit branch (radar_passage.cpp:73-84) tests the
  player's room's doorways, using each doorway's own half-width as the side tolerance. The maze rule alone almost never
  fired: a diagonal ray through a 14-unit doorway meets the far corridor's side wall before it clears the border by
  10. In the 2026-09-30 log, Exit came only 1.5 to 4.5 cells from the door. A doorway leading into unloaded tiles is unexplored.
- **Always a maze level:** D4 dungeons only; rooms are RoomModel rooms under ids that survive rebuilds.
- **Poll-relative timings** follow D4's 100 ms poll (stationary confirm, maximum gap); wall-clock ones are kept.
- **No collision kinds** in the clutter filter: every small blocked island is a candidate.
- **A committed wall gives way to floor only when the floor holds one lane to either side.** The band is scanned again
  shifted one lane (`PassageScanner.LaneSpacing`) left and right; if either shift still meets an obstacle, the wall
  stands, as it does inside the depth band. Walls and exits are never held back. The band is anchored to the player's
  cell, so a wall grazing its edge flipped the reading with every cell walked sideways (2026-09-30 log: five Left cues
  in 20 s along one diagonal wall). Replayed over that session's path: 128 cues down to 87, the same 7 exits.
- **The wall memory follows the wall.** The retreat gate's frontier is refreshed on every poll while the confirmed
  wall is within closing depth, and a silent-baseline wall sets it too; D2R set it only when a wall was announced.
  Backing away from a wall the player started beside, or walked along for more than `BoundaryRange`, no longer reads
  Open: nothing new opened (user, 2026-09-30).

## Not ported

D2R-native readers (`native_*.cpp`), `radar.toml`, the beacon, the scanner, item/door/exit loops,
`explored.dat` persistence, keyboard/console bindings, level-exit positions (`LevelExit`; the branch is fed doorways instead), the open-ground
frontier (`World::frontier`), velocity smoothing (only fed the Ctrl+T readout), and the audio revision counter.
