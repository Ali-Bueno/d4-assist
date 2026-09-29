# D4Assist.Navigation

Pure navigation/announcement logic for the Diablo IV Dungeon Assist. No game or
process access, no dependencies beyond the BCL — everything here is a function
of `TileInput`/`PlayerInput` snapshots the host app feeds in.

## Behaviour

1. `DungeonMap` accumulates every DRLG tile ever seen (loaded window is ~1 Hz,
   ~2 tiles around the player); tiles that scroll out of the window stay in memory
   until `Reset()` (dungeon change).
2. `RoomModel` groups tiles sharing a `NodeId` into a `Room`, requiring grid
   adjacency too (a shared id alone doesn't merge unrelated tiles — guards
   against a default/zero id). Its openings come from `OpeningFinder`, not the
   tile edge's midpoint (see "Real openings" below); each is tagged `Unexplored`
   when its neighbouring cell has never been loaded.
3. `OpeningFinder` scans each exit-bit side's tile-local walkable grid
   (`ITileWalkability`, host-supplied) for contiguous walkable runs at least
   `MinOpeningWidthUnits` wide, one per real passage. No grid, no run, or a
   non-axis-aligned tile yaw (`TileRotation`) falls back to the old side-midpoint
   placement, flagged (`RoomOpening.UsedFallback`). Openings that resolve to the
   same neighbour cell are de-duplicated.
4. `RoomWalkGrid` combines every member tile's local grid (rotated by its yaw)
   into one world-space grid; `RoomPathfinder` runs an 8-connected,
   no-corner-cutting BFS over it. `RoomRouteCache` resolves each opening's
   spoken direction from a look-ahead point on that path (falling back to a
   straight line, flagged, when no path is found) and caches routes per room
   until the player moves past `RecomputeThresholdUnits`.
5. `ScreenDirections` bins a world offset into one of 8 controller-stick
   directions, using the live calibration in `research/client-memory.md` §3.
6. `RoomClassifier` labels a room DeadEnd/Corridor/Junction by opening count, or
   BigRoom if it spans more than one tile (BigRoom takes priority).
7. `AnnouncementPolicy` decides what to say: junctions, dead ends and big rooms
   speak; corridors are silent unless `IncludeCorridors` is set. Diff-gated —
   identical text is never repeated. Also returns each opening's `OpeningRoute`
   (position, width, path length, fallback flags) for `--debug` diagnostics.
8. `NavigationPhrases` holds every spoken string (`Spanish` / `English` instances; `LanguageSetting` is the live switch).
9. `NavigationEngine` is the entry point: `OnTiles`, `OnPlayer`, `Reset`, and an
   `Announce(text, interrupt)` event. `Interrupt` defaults to `false` so automatic
   announcements never cut the game's own NVDA speech. Takes an optional
   `ITileWalkability` in its constructor; without one, every opening uses the
   old side-midpoint/straight-line behaviour.

## Real openings and path-based directions (2026-09-28 fix)

Live testing found the room announcer pointing the player into walls: `RoomModel`
used to place every opening at the tile edge's geometric midpoint, and speak the
straight-line direction to it. On large tiles (64x64+) the real passage sits in
one part of the edge, not the middle, and even on 32x32 tiles a straight line to
a correctly-placed opening can cross a wall on an irregular room. Fixed by:

- Finding the opening from the tile's own walkable grid instead of assuming the
  edge midpoint (`OpeningFinder`, `WalkGrid`, `ITileWalkability`).
- Speaking a BFS path look-ahead direction instead of the straight line
  (`RoomWalkGrid`, `RoomPathfinder`, `RoomRouteCache`).

`ITileWalkability` keeps this project BCL-only: the host (`src/D4Assist`,
`CascTileWalkability`) implements it from D4Data's offline `SceneInfo.Walkability.Map`
(CASC read, no live process access), cached per scene SNO. `WalkGrid`'s
row=X/column=Y convention is pinned against real data in `WalkGridOrientationTests`
(the exact tile from the 2026-09-28 bug report, `TT_Step_City_to_Protodun_Jail_NSEW_01`).

## Exploration guide

`DungeonMap` also records explored tiles: ones the player stood in (`MarkVisited`, fed by `NavigationEngine.OnPlayer`)
or saw from a doorway (`MarkSeen`, see below); cleared with `Reset`. `ExplorationGuide` (`engine.Guide.Step(player)`) leads to the nearest unexplored part:
a loaded tile never stood in, or a frontier (an exit opening whose far side never loaded). `TileGraph` links
tiles through their real openings (`OpeningFinder`); `TileRoutes` runs Dijkstra from the player's tile with
straight legs between crossings. The waypoint is `RoomRouteCache.Trace` (BFS path + farthest visible point)
toward the first opening that leaves the player's room. Seen, not stood-in: a tile directly joined to the player's by a real opening (graph edge, never across a wall)
is explored once the player is within `SeenRadiusUnits` (20) of its centre, so dead ends are learnt from
their doorway (big tiles still need entering). Hysteresis: the current target stays unless another
is `SwitchMarginUnits` (one tile) shorter, and is never dropped while its route is under `CommitDistanceUnits`
(one tile) unless it stops being a candidate. A frontier that still hasn't loaded when the player stands within
`FrontierArrivalUnits` is a dead exit and is dropped. Nothing left reachable gives `AllExplored`; off the
known grid gives `Unknown`. Guess: exit bits are symmetric (only the side carrying the bit gets an edge).
Touching tiles of one room are linked even without exit bits (preset rooms' live masks omit internal sides).
Re-check mode: `Step(player, exitMissing: true)` with nothing left to explore returns `Rechecking` (`GuideStep.Round`) toward the known tiles whose scene `isExitCandidate` accepts, nearest first with the same commitment rules;
a candidate is re-checked once the player is within `SeenRadiusUnits` of its centre (an arrival), and after all are done a new round starts (the one under the player re-arms only after leaving). No known candidate keeps `AllExplored`.
`Arrivals` counts targets reached (explored, or a dead exit stood at), not targets that merely loaded.

## D2R correspondence

Ported from `d2r-access/src/rooms.h/.cpp` (see `research/d2r-radar-port.md` §1, §4):

- `RoomHysteresis` is `Rooms::Hysteresis` line-for-line: `settleMs`=1000,
  `dwellMs`=400, unchanged — this gates announcement timing, not radar range
  (the only value the narrower D4 tile window affects).
- `RoomModel`/`OpeningFinder` openings mirror `Rooms::Describe`/`Openings`: D2R's
  gap-width field is now `RoomOpening.Width`, since D4 openings are also found by
  scanning the walkable boundary (`OpeningFinder`), not just a per-side boolean.
- **Deliberate difference:** D2R has no "big room" case (its maze rooms are one
  fixed preset size); D4's multi-tile `NodeId` groups become `RoomKind.BigRoom`.

## Conventions

- Engine axes: X = south, Y = east. Exit bits: N=1 (-X), S=2 (+X), E=4 (+Y), W=8 (-Y).
- Stick frame is 45 degrees off the world axes (D2R's `Diagonal` frame problem):
  tile N reads as screen up-right, E as down-right, S as down-left, W as up-left.
- Openings are ordered clockwise from up in every announcement.
