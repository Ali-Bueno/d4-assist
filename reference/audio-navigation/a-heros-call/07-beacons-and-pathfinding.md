# 07 — Beacons & Pathfinding-Guided Navigation

Source: `FirstPerson/Ian/FPBeaconMenu.cs`, `Beacon.cs`, beacon logic in `FPExploring.cs`,
`FirstPerson/Ian/GraphSearch.cs` (which wraps **QuickGraph's** `AStarShortestPathAlgorithm` — see §3),
`DirectionHelper` (Doc 04 §4). Also covers the **map overview** review-cursor mode (§4).

Beacons are the game's **waypoint guidance**: pick a named destination, then get repeated audio/speech
telling you which way to go. Two modes: a simple distance+direction readout, and an "advanced beacon"
that uses A* pathfinding to place a directional beep on the next reachable step toward the goal.

---

## 1. Beacon data & selection

```csharp
// Beacon.cs (Beacon.cs:9-13)
public sealed class Beacon {
    public string  IndexedName;     // internal id
    public TString FriendlyName;    // spoken name
    public float X, Y;              // authored position, NOT tile-centered
    public V2 Position => new V2(X, Y);
}
```

> **Not tile-centered.** `Position` is the raw authored `(X, Y)` — no `+0.5` offset is applied. The
> `+0.5` tile-center convention exists elsewhere in the codebase (portals, `tile.Center`) but not for
> beacons (Beacon.cs:9-13).

Beacons are **authored map data** — `Beacon` is a serialized object (`X`, `Y`, `IndexedName`,
`FriendlyName`) baked into the map, not something the game generates at runtime.

`FPBeaconMenu` (caption `"Choose a beacon"`, `FPBeaconMenu.cs:13-16`) lists `mWorld.Map.Beacons` by
`FriendlyName` as menu items; selecting one sets `map.ActiveBeacon` (`FPBeaconMenu.cs:22-44`):

```csharp
foreach (Beacon b in mWorld.Map.Beacons) menu.Items.Add(new MyMenuItem(b.FriendlyName, HandleClick, null, b));
…
void HandleClick(MyMenuItem mi){ mWorld.Map.ActiveBeacon = (Beacon)mi.Tag; mWorld.Scene.Up(); }
```

Bound keys (all in `FPExploring.HandleKeys`): `BeaconMenu` (open list, `FPExploring.cs:571-574`),
`AnnounceBeacon` (speak distance+direction now, `:579-582`), `DisableBeacon` (clears `ActiveBeacon`,
`:575-578` → `:661-664`).

---

## 2. Simple readout — distance + 8-point direction

`FPExploring.AnnounceDistanceAndDirectionToBeacon`:

```csharp
Beacon b = mWorld.Map.ActiveBeacon;
if (b == null) { DiagSO.Say("No active beacon"); return; }
V2 toBeacon = mWorld.Position - b.Position;             // vector FROM beacon TO player
int dist = (int)Math.Round(toBeacon.Length);
RoughDirection rd = GetRoughDirection(ref toBeacon);
DiagSO.Say(dist + " " + Enum.GetName(typeof(RoughDirection), rd));   // e.g. "45 NorthEast"

static RoughDirection GetRoughDirection(ref V2 toBeacon) {
    double a = Math.Atan2(toBeacon.X, -toBeacon.Y);     // compass bearing (Doc 01 §2)
    float dir = MyMath.RadiansToDegrees((float)a) + 180f;
    return DirectionHelper.GetRoughDirectionEnum(dir);  // 45°-wide bins
}
```

> The `atan2(X, -Y) + 180°` converts the player↔beacon delta into a compass bearing and then the
> `+180` flips it from "direction to player" into "direction to beacon" before binning into the
> 8-point `RoughDirection`. (If you instead compute `beacon - player`, drop the `+180`.) `GetRoughDirection`
> (`FPExploring.cs:750-754`) is the **same shared helper** the advanced beacon uses (§3).

> **The spoken direction is the raw, untranslated enum name.** `Enum.GetName(typeof(RoughDirection), rd)`
> (`FPExploring.cs:746-747`) yields the PascalCase identifier verbatim — `"NorthEast"`, `"SouthWest"`, … —
> so this readout is **not localized** (unlike the region/POI strings that go through `TranslateSO`). Port
> it through your own direction-name table if you need translation.

`DirectionHelper.GetRoughDirectionEnum` bins (Doc 04 §4): N 337.5–22.5, NE 22.5–67.5, E 67.5–112.5,
SE 112.5–157.5, S 157.5–202.5, SW 202.5–247.5, W 247.5–292.5, NW 292.5–337.5.

---

## 3. Advanced beacon — A*-guided directional beeps

When `GameConfig.UseAdvancedBeacon`, instead of a raw bearing the game pathfinds and plays a
directional beacon sound positioned on the **next reachable tile along the path**, so you're guided
around walls rather than into them.

```csharp
float dist = (mWorld.Position - b.Position).Length;
if (dist < 5f) { PlaySimpleBeacon(); }                 // see note below: this is a no-op, execution falls through

SinceLastAdvancedBeaconSound += GameTimeSinceLastTick;
if (SinceLastAdvancedBeaconSound < 2.0) return;        // throttle: one beep / 2 s
SinceLastAdvancedBeaconSound = 0.0;

FPTile playerTile = FSO.FPML.GetPlayerTile(mWorld);
FPTile goalTile   = FSO.FPML.GetTile(mWorld, b.Position);
if (playerTile == goalTile) {                          // arrived
    mWorld.SC.PlayPlain2DIfExists("arrived at beacon"); DisableBeacon(); return;
}

// A* on the tile graph, NON-diagonal moves.
SEquatableEdge<FPTile>[] path = GraphSearch.AStar(mWorld.Map, playerTile, goalTile, includeDiagonals:false);
if (path == null) { DiagSO.Say("You can't get there from here"); return; }

// Walk up to 10 path tiles; keep the farthest one with clear line-of-sight from the player.
FPTile beepTile = null; int n = 0;
foreach (var edge in path) {
    if (++n > 10) break;
    FPTile t = edge.Target;
    float len = (t.Center - mWorld.Position).Length;
    V2 slope = (t.Center - mWorld.Position); slope.Normalize();
    if (CH.GetWallOrImpassableCollision(mWorld.Map, mWorld.Position, slope, len) != null) break; // blocked -> stop
    beepTile = t;
}
if (beepTile == null) { DiagSO.Say("Could not find a tile to play the sound from"); return; }

// Direction from that tile back to player -> choose one of 8 pre-positioned directional sounds.
V2 toBeacon = mWorld.Position - beepTile.Center;
RoughDirection rd = GetRoughDirection(ref toBeacon);
I3DSoundSource se = rd switch {
    North=>mABNorthSE, South=>mABSouthSE, East=>mABEastSE, West=>mABWestSE,
    NorthEast=>mABNorthEastSE, NorthWest=>mABNorthWestSE, SouthEast=>mABSouthEastSE, SouthWest=>mABSouthWestSE };
se.Update(beepTile.Center.AsV3());                     // position at the path tile
se.Play();
```

> **`PlaySimpleBeacon()` is an empty method body** (`FPExploring.cs:332-334`) and the `dist < 5f` branch
> that calls it has **no `return`** (`FPExploring.cs:241-244`) — so there is no "fall back to simple cue
> when close" behavior in the original game. Worse, `HandleActiveBeacon` routes to `PlaySimpleBeacon`
> **whenever `UseAdvancedBeacon` is off** (`FPExploring.cs:228-231`), so with the advanced option disabled
> an active beacon gives **zero passive feedback**; the only readout is pressing `AnnounceBeacon`
> (`AnnounceDistanceAndDirectionToBeacon`, §2). With advanced beacon on, being under 5 tiles simply does
> nothing extra and execution continues into the throttle/A*/LOS logic below exactly as if the check
> weren't there.
>
> **Dead beacon-timing fields.** `SinceLastBeacon`, `MaxBeaconDelay = 2.0`, `MinBeaconDelay = 0.05` and
> `BeaconDelayPerDistance = 0.1` are declared (`FPExploring.cs:73-79`) but **never read** — they look like
> the remains of an intended distance-scaled beep cadence that was replaced by the flat 2-second throttle.
> Don't infer a distance-varying beep rate from them; the shipped rate is constant.
>
> **Known bug — SouthEast plays the wrong sound.** The `RoughDirection` → sound switch above maps
> `SouthEast` to `mABSouthSE` (the *South* sound), not `mABSouthEastSE` (`FPExploring.cs:320-323`). The
> correct `mABSouthEastSE` source is both **declared** (`:39`) and **loaded** (`Init`, `:137` — `"ab
> southeast"`), it is simply **never played**. This is a bug in the original game; document it as such and
> do **not** replicate it when porting.

The eight `mAB*SE` sounds are preloaded with effectively infinite min/max distance and no reverb so
they always play clearly:

```csharp
mABNorthSE = mWorld.SC.Get3DSound("ab north", "map", 10000f, 10000f, useEnvironmentalReverb:false);
// … ab south / east / west / northeast / northwest / southeast / southwest
```

### Why this design is good (and worth copying)

- **A* keeps guidance honest** — it routes you around obstacles; a raw bearing would point you at walls.
- **Line-of-sight clamp (the 10-tile walk)** places the beep as far ahead as you can actually hear in a
  straight line, so the cue sits "down the corridor" at the next turn, not on an unreachable diagonal.
- **2-second throttle** prevents nagging.
- **Auto-disable on arrival** (tile equality with the goal tile).
- The `dist < 5f` "close range" branch is intended as a degrade-to-simple-cue but is broken in the
  shipped game (see note above) — don't rely on it as designed behavior when porting; either fix it
  (add the `return`) or intentionally drop the distinction.

`GraphSearch.AStar(map, from, to, includeDiagonals)` returns an array of `SEquatableEdge<FPTile>`
(graph edges); `edge.Target` is the next tile.

> **Which A* actually runs (correction).** The beacon's `GraphSearch.AStar` is a thin wrapper around
> **QuickGraph's `AStarShortestPathAlgorithm`** (`FirstPerson/Ian/GraphSearch.cs:82-92`), configured with a
> **uniform edge cost of `1.0`** (`:85`), a **Manhattan-distance heuristic** (`:86-87,123-128`), and edge
> generation that sets **`canCrossWalls:false, canCrossImpassables:false`** (`:82`) — so the path never
> routes through walls or impassable terrain. The **hand-rolled** `GraphSearch/Ian/AStar.cs` is **not** on
> this path: it is used only by a dead comparison test harness (`GraphSearch.cs:43-70`, `TestIansGraphSearches`)
> and by NPC pathing — never by the beacon. If a doc or comment implies the beacon uses the custom A*, it's
> wrong. Portable as-is, or replace with your engine's nav-mesh path.

The advanced-beacon control flow (all in `PlayAdvancedBeacon`, `FPExploring.cs:235-330`) also emits three
spoken diagnostics worth preserving: **arrival** — only reachable in advanced mode, on a throttle tick,
when `playerTile == goalTile` → 2D `"arrived at beacon"` cue + `DisableBeacon` (`:253-258`); **no route**
— `"You can't get there from here"` when A* returns null (`:262`); and **no audible vantage** — `"Could
not find a tile to play the sound from"` when every candidate path tile is LOS-blocked (`:290`). The
played bearing is computed from `player − chosenTile.Center` (`:294`) run through the same `+180°`
`GetRoughDirection` helper, so the beep points **player → tile** (down the corridor), matching §2's manual
readout.

### Player guidance is audio-only — never auto-walk

Both beacon modes are **advisory only**: the game never moves, steers, or snaps the player onto the
path. The A*/steering machinery visible elsewhere in the codebase (`SteeringPath.Create`,
`SteeringManager.FollowPath`, `SeekPositionCommand`) is used exclusively by **NPC agents**
(`SeekPositionCommand` is an AI `CommandBase` driving `m_agent`/`m_brain`, not the player;
SeekPositionCommand.cs). `SteeringManager` (SteeringManager.cs:57-82) is a boid-style path follower with
`MAX_FORCE=100`, waypoint arrival radius **0.2 tiles intermediate / 0.01 tiles final**. None of this
touches the player — the blind player always walks manually and is guided only by the 3D-positioned
beacon beep or the on-demand distance/direction speech. When porting, don't build an auto-walk-to-beacon
feature expecting parity; parity is the beep + speech only.

---

## 4. Map overview (review-cursor mode) — `FPMapOverview`

A non-moving "review cursor" the player drives over the grid to survey surroundings without walking
(summary in Doc 04 §5; the full audio/speech breakdown lives here as a navigation-guidance surface). It
extends `FPMapGridBase` but overrides the movement handler with its own.

### Movement is single-step only

`FPMapOverview.HandleMoveReviewWithinWalls` (`FPMapOverview.cs:49-75`) handles the four arrow keys as
**one-tile steps plus a re-announce key** — and that is the *entire* control set. The base class's
jump-by-10 / jump-to-edge machinery (`FPMapGridBase.cs:21-80`) is **unused here** (its `isJump` path is
never reached), and there is **no** jump-to-POI, jump-to-beacon, or go-to-coordinate feature. Two blocked
cases:

- **Impassable target tile** (`!Terrain.CanPass`): the cursor does **not** move — it plays that tile's
  terrain `BumpSound` and speaks its terrain `FriendlyName` (`FPMapOverview.cs:86-91`).
- **Off the map**: speaks `"That is the edge of the map."` (`:80-84`).

(`AnnounceTile`'s `playSound` parameter is dead — `FPMapGridBase.cs:114-120` never acts on it.)

### Wall-proximity audio on each successful step (`:92-204`)

Cues are **staggered in time** via `GlobalEvents.SubscribeTimer` on an accumulator that grows by ~`0.13 s`
per vertical-axis (top/bottom) cue and ~`0.1 s` per horizontal-axis (left/right) cue, so they read in
order rather than on top of each other. Only one axis fires per step (arrows are single-axis):

1. **Perpendicular side cues — gap change, not absolute distance.** `"open space"` = the gap on that side
   **widened**, `"closed space"` = it **narrowed** (it compares `GetX/YDistanceToWall` at the *old* cursor
   tile vs the *new* one). On a **horizontal** move the two perpendicular sides are **top** (pitch `1.0`)
   and **bottom** (pitch `0.7`) via `PlayWithAdjustedPitch` (`:101,116`); on a **vertical** move they are
   **left** / **right** via `PlayOnLeft` / `PlayOnRight` (`:149-175`).
2. **Center cue — wall directly ahead** in the direction of the step: `0` tiles remaining →
   `"Wall Approach 3"`, `1` → `"Wall Approach 2"`, `2` → `"Wall Approach 1"` (`:184-204`); `≥3` tiles is
   silent. (`GetXDistanceToWall` / `GetYDistanceToWall` step outward counting passable tiles until
   `!CanPass`.)

> **Correction:** this is a *widened/narrowed gap* + *tiles-to-wall-ahead* scheme, **not** a
> "pitch encodes distance, pan encodes axis" one. Pitch here only distinguishes the top (`1.0`) from the
> bottom (`0.7`) side on horizontal moves; pan (left/right) is used only on vertical moves.

### Per-tile speech (`GetTileAnnounceText`, `FPMapOverview.cs:242-276`)

Built as a space-joined list, in this fixed order:

`"You are here,"` (**only** when the cursor sits on the player's own floored tile — this is the sole
own-position indicator) → each **visible** object's `FriendlyName` → the terrain `FriendlyName` →
`"in {Region}"` (when the tile has a named region) → the tile `X` → the tile `Y`. There are **no POIs and
no beacon indicator** in the overview readout.

---

## 5. Porting notes

- **Simple beacon** is trivial anywhere: `bearing = atan2(dx, -dy) deg`, bin to 8 points, speak
  `"{round(distance)} {dir}"`. Great for "where's the objective/exit/enemy" in any game.
- **Advanced beacon**: use your engine's pathfinder (Unity `NavMesh.CalculatePath` for 7DTD; in
  RE-Engine games without exposed nav, fall back to simple bearing or a coarse waypoint graph you
  build). Keep the **line-of-sight clamp** and **throttle**. Position the beep in 3D at the chosen
  waypoint so spatial audio does the pointing; speak the 8-point direction as a fallback for users who
  prefer words.
- Beacon positions are authored, not tile-centered (`(X+0.5, Y+0.5)` is a `tile.Center`/portal
  convention, not a `Beacon.Position` one, see §1); in continuous games just use the world positions
  directly.
- Player guidance is audio cue only — no auto-walk; if a "walk me there" feature is wanted, it would be
  new functionality, not a port of anything in the original game (see above).
