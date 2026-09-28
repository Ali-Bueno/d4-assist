# 02 — The Reactive Radar (A Hero's Call): verified spec + porting guide

**The one radar document.** §1–§3 tell you what the system is and does (enough to implement from);
§4 is the verified A Hero's Call implementation with `file:line` evidence (read when you need *why*);
§5–§7 are the porting recipe, the failure table and the acceptance test. Doc 10 (whole-stack porting)
points here for everything radar.

**Primary source:** `D:\code\c#\a-heros-call\decompiled\src\Sound\Ian\ReactiveRadar.cs` (233 lines — the
whole system), plus `CollisionHelperCopy.cs` (sensor), `Line.cs`/`V2.cs` (geometry), `MapAndPlayer.cs`
(inputs), `MapSoundHandler.cs` (driver), `FModSoundContext.cs` (sound), `FPExploring.cs` (movement,
turning, and the separate door chirp).

> **Verification.** Every claim is either read off the decompiled source (cited `file:line`) or a
> simulation result (marked *measured*; each is ~150 lines, rebuildable from the cited sources —
> re-run rather than trust if a number ever drives a decision). Last full pass: 2026-08-24, line-by-line
> against the source. Two corrections from older revisions are preserved as warnings so they don't creep
> back: the normals-only classifier (§5.4) and the "input is axis-pure" claim (§4.6).

Related: [01 coordinate math](01-coordinate-system-and-math.md) · [03 raycasting](03-raycasting-and-collision.md) ·
[04 movement](04-navigation-and-movement.md) · [05 POI scanning](05-scanning-pois-and-interactables.md) ·
[08 spatial audio](08-spatial-audio-fmod.md) · [10 porting guide](10-porting-guide.md)

---

## 1. The whole algorithm on one page

Three persistent beams — **Left, Front, Right**, no rear — re-aimed every tick from **facing**. What a
beam *does* is decided by **velocity**: each runs up to two independent detectors on the same single ray.

| Beam vs. your movement | Role | Question it answers |
|---|---|---|
| **Aligned** (moving that way, diagonals count) | proximity pitch only | "how long until I hit something?" |
| **Perpendicular** | open/closed only | "did the wall I'm sliding past just change?" |
| **Opposite** | idle | — |
| (diagonal travel ⇒ a beam can be aligned *and* non-parallel: both detectors, same tick) | | |

```
every frame:
    if a cue played < 0.1 s ago: return               # ONE global cooldown — freezes sensing too
    aim beams from FACING; v = unit travel direction (none when still)
    for beam in [Left, Front, Right]:                 # fixed priority
        hit = raycast(player, beam.dir, ∞)            # first thing that BLOCKS MOVEMENT (doors count)
        d   = floor(distance)                         # tiles
        # detector A — approach pitch (aligned beams)
        if aligned and d < 3 and d != beam.lastD: play WallApproach[d]   # 0 = nearest = highest pitch
        beam.lastD = aligned ? d : ∞                  # wipe ⇒ one reminder chirp on resume
        # detector B — open/closed (perpendicular beams)
        line = line through (hit point, previous hit point)   # the surface being tracked
        if line changed since last tick:              # an EDGE just crossed the beam
            play (new hit BEYOND the old line ? "Open space" : "Closed space")
        if anything played: cooldown 0.1 s; break     # one cue per tick, ≤ ~10 cues/s total
```

Detector B is the clever part: consecutive hits on the same flat wall are collinear **at any approach
angle**, so sliding along a wall — however obliquely — is silent, and the line changes only when the ray
jumps to a *different* surface: past a corner (orientation change) or through a gap to a farther wall
(offset change). Which side of the **old** line the new hit lands on tells you whether space **opened**
(hit receded: doorway, end of wall, room mouth) or **closed** (hit nearer: pillar, jut, narrowing).

The radar speaks only on **change**. Standing still it is silent by design; "is there a wall beside me
*right now*" is a state, answered by the [wall-sonification bed](../wall-sonification/), not by the radar.
The two layer cleanly.

## 2. Cue vocabulary — six sounds, TWO systems

All cues are **flat 2-D stereo**, pre-panned left/centre/right — never spatialised (hard rule,
[`../README.md`](../README.md)).

| Cue | FMOD event | Meaning | Fired by |
|---|---|---|---|
| Proximity 2 tiles | `Wall Approach 1` (lowest) | obstacle 2 units away, in your direction of travel | `ReactiveRadar` |
| Proximity 1 tile | `Wall Approach 2` | same, 1 unit | `ReactiveRadar` |
| Proximity 0 tiles | `Wall Approach 3` (highest) | about to touch it | `ReactiveRadar` |
| Space opened | `Open space` | the obstacle on that side receded (doorway / corner / room) | `ReactiveRadar` |
| Space closed | `Closed space` | something is nearer on that side than what was tracked | `ReactiveRadar` |
| Door | `door` | a door tile lies along that beam (no distance) | POI scanner (§4.8, doc 05) |

Two systems, one soundscape — **do not conflate them** (the most common porting confusion):

- **The geometry radar** (`ReactiveRadar`) senses *shape change only*. It has no idea what an obstacle
  is: wall, boulder, furniture and **door** are all just "the thing that stopped my ray".
- **The POI scanner** (`FPExploring`, §4.8) senses *identity* and contributes exactly one cue: `door`.

**The doorway rule that follows** (player-confirmed in game): a door tile **always blocks the radar
ray** — the game has no open/closed door state at all (`IsDoor` is a static terrain flag, §4.3). So at a
doorway *with a door* you hear the `door` chirp and **not** `Open space`; `Open space` fires only at
genuinely empty gaps. Same hole in the wall, different sound depending on whether a door tile fills it.

## 3. Ground truth — what the player hears

| Situation | Cue |
|---|---|
| Uniform corridor, walking straight | **silence** |
| Walking head-on at a wall | `Wall Approach 1` → `2` → `3`, centre, one per tile crossed |
| A wall ends on your left | `Open space`, panned left |
| Walking past a doorway (no door tile) on your right | `Open space` right, then `Closed space` right as the wall resumes |
| Walking past a doorway **with a door** | `door` chirp on that side — no open/close (§2) |
| Strafing into a wall on your right | `Wall Approach` chirps panned right |
| Turning on the spot | front-beam `Open`/`Closed` cues as the beam sweeps edges — see §4.7 for whether to port this |
| Stopping, then resuming toward a near wall | exactly **one** reminder chirp |
| Backing straight into a wall | silence (no rear beam; the bump sound covers it, doc 04) |

**Density:** hard cap ~10 cues/s (one global cooldown); in practice far less — *measured*: an 8-tile
corridor walk past a room opening, ending at a wall, produced **5 cues in 2.3 s**. Sparse is a feature.

---

## 4. The A Hero's Call implementation, verified

### 4.1 Inputs and conventions

`MapSoundHandler` hands the radar one `MapAndPlayer` snapshot per update (`MapSoundHandler.cs:299-300`):

| Field | Source | Notes |
|---|---|---|
| `PlayerPosition` | `world.Player.Position` | **tile units**; tile *(x,y)* spans `[x,x+1)×[y,y+1)` (`FPTile.cs:14-20`) |
| `Velocity` | `travel × speed` (`FPMapLogic.cs:280`) | **exactly `V2.Zero`** when no key held (`FPExploring.cs:657`) |
| `Front` | unit vector from compass degrees (`MyMath.cs:35-43`) | **facing**, continuous angle (mouse-look), not 8-dir |
| `Left`/`Right` | `Front.PerpendicularRight` / `Front.PerpendicularLeft` (`MapAndPlayer.cs:19,23`) | see trap below |
| `Map` | tile grid | tiles expose `IsWall / IsImpassable / IsDoor` (`FPTile.cs:60-66`) |

- **Compass:** `(sin d°, −cos d°)` — 0° = North, clockwise, **Y-down** (+X = East, +Y = South). Doc 01.
- **Mirroring trap:** `Left = PerpendicularRight = (Y, −X)`; `Right = PerpendicularLeft = (−Y, X)`
  (`V2.cs:59,61`). The property named *Left* is built from *PerpendicularRight*. One sign error mirrors
  beams *and* panning.
- **Speed:** base 1.4 tiles/s × an **unconditional** ×2.5 run modifier (`RPGExploring.cs:43-51`) ⇒
  **3.5 tiles/s always** ⇒ 0.058 tiles per 60 Hz evaluation, ~17 evaluations per tile — and the 3-tile
  pitch reach ≈ **0.86 s to contact**.

### 4.2 Driver, cooldown, priority

Called from the game loop every frame (`RPGGameLoop.cs:89` → sound thread → `MapSoundHandler` →
`RunRadar`). The radar itself (`ReactiveRadar.cs:134-160`):

```csharp
if (!Enabled) return;                                  // player toggle only
if (NextRadarTime >= now) return;                      // POST-CUE cooldown (own Stopwatch, real time)
V2 v = ent.Velocity.Normalized();
Front.Direction = ent.Front;  Right.Direction = ent.Right;  Left.Direction = ent.Left;
Front.TravellingThisDirection = ent.Front == v || ent.FrontLeft == v || ent.FrontRight == v;  // §4.6!
...                                                    // Left/Right likewise, with their diagonals
foreach (RadarBeam b in AllBeams)                      // order: Left, Front, Right
    if (PlayRadarBeamSound(ent, b, ent.PlayerPosition, v)) { NextRadarTime = now + 0.1; break; }
```

Load-bearing properties:

1. **Post-cue cooldown, not a scan rate.** With nothing playing, all three beams cast every frame.
2. **The cooldown freezes sensing too** — the early return sits before the beam loop, so history is
   frozen for ~0.1 s after any cue (a bounded blind spot).
3. **Strict priority Left > Front > Right; cues serialised, never simultaneous.** A deferred beam's
   history is untouched while it waits, so its cue still fires ~0.1 s later. The one same-instant overlap
   allowed: pitch *and* open/close from a **single** beam on diagonal travel.
4. **No menu/dialogue gate exists.** Menus are quiet only because position and facing freeze (the
   `point == lastPoint` gate, §4.4) — *not* because velocity is zero: zero velocity normalises to **NaN**
   (`V2.cs:88-100`), which makes `TravellingThisDirection` false but also defeats the open/closed role
   gate — the quirk behind turn noise (§4.7). **Ports add the gameplay gate explicitly.**

### 4.3 The sensor

One ray per beam per tick, shared by both detectors:
`GetWallOrImpassableOrDoorCollision(map, position, dir, 1000f)` (`ReactiveRadar.cs:165`) — a grid-DDA
that visits **grid-line crossings only**, sorts them by distance, and returns the first tile that is
`IsWall || IsImpassable || IsDoor` (`CollisionHelperCopy.cs:30-92`; detail in doc 03).

- **"Wall" = anything that blocks movement** — impassable water, pits, rocks, furniture, invisible
  boundary tiles (`CanPass = !Wall && !Impassable`, `FPTerrain.cs:60-69`). Port as "whatever stops the
  player's own movement", never a cosmetic "walls" layer.
- **Doors are always ray-solid.** `IsDoor` is a static terrain flag; there is no open/closed door state
  anywhere in the tree (terrain only changes via the generic script `SetTerrain`). Identity ("that's a
  door") is the POI scanner's job (§4.8).
- **The pin — the property everything rests on:** every hit point lies exactly on an axis-aligned tile
  face, its face-perpendicular coordinate an **exact integer** — zero numerical noise. This is what makes
  the line fit exact (§4.4) and what your engine almost certainly does *not* give you (§5.4).
- The 1000-tile length is effectively unbounded — the walk exits at the map edge. (It builds the full
  crossing list then sorts; a port should early-out at the first hit.)
- **Decompiler note:** `PitchRadarReach = 3`, `OpenCloseRadarReach = 1000`, `delay = 0.1`
  (`ReactiveRadar.cs:92-96`) look unreferenced only because the C# compiler **inlines consts** — they are
  the original source's names for the live literals at `:187`, `:165`, `:156`. Port the *names*.

### 4.4 The open/closed classifier

The core (`ReactiveRadar.cs:162-232`), annotated:

```csharp
// sense
V2 point = V2.Zero; double dist = MaxValue; int rounded = MaxValue;
if (hit.HasValue) { point = hit.Value.Point; dist = (position-point).Length; rounded = floor(dist); }

// shift history — for EVERY beam, before any role exclusion
V2 lastPoint = rb.LastCollisionPoint;  rb.LastCollisionPoint = point;
int lastRounded = rb.LastRoundedDistance;
rb.LastRoundedDistance = rb.TravellingThisDirection ? rounded : int.MaxValue;

// detector A — proximity pitch (§4.5)
if (rounded < 3 && rounded != lastRounded && rb.TravellingThisDirection) { PlayPitch(rounded); played = true; }

// role gate: open/closed only for beams NOT (anti)parallel to travel   (AlmostEquals, ε = 1e-5)
if (rb.Direction.AlmostEquals(travelHeading))      return played;
if (rb.Direction.AlmostEquals(travelHeading * -1)) return played;

// need two valid, DIFFERENT consecutive hits
if (point == V2.Zero || lastPoint == V2.Zero) { rb.LastCollisionLine = null; return played; }  // miss ⇒ wipe
if (point == lastPoint) return played;               // idle gate — only bites standing still

// detector B — the line through the last two hits IS the surface being tracked
Line line = Line.FromPoints(point, lastPoint, isSegment:false);   // 2-D points lifted to Z = 0
Line lastLine = rb.LastCollisionLine; rb.LastCollisionLine = line;
if (lastLine == null)          return played;        // silent re-seed
if (line.Equivalent(lastLine)) return played;        // same surface ⇒ silence

// surface changed ⇒ classify against the OLD line, then consume
rb.LastCollisionLine = null;
bool playerSide = lastLine.IsPointLeftOfLine(position);
bool hitSide    = lastLine.IsPointLeftOfLine(point);
if (playerSide == hitSide) PlayClose(); else PlayOpen();
return true;
```

Why it works, and its edges:

- **`Line.Equivalent` compares only the slope** (sign-agnostic, ε = 1e-5 — `Line.cs:109-116`), yet is a
  *full* same-line test here, for two stacked reasons: hits on the same face are exactly collinear (the
  pin, §4.3), and **consecutive fits share a hit point** — pair *(vₙ, vₙ₋₁)* then *(vₙ₊₁, vₙ)* — so
  "parallel" already implies "identical". No offset check needed *in AHC*; a port that loses the shared
  point must compare offset too (§5.4).
- The line changes for exactly two reasons, **both genuine events**: the beam enters a
  differently-oriented face (corner), or a parallel face at a different offset (the hit jumped through a
  doorway to a farther wall — the transition pair is oblique, so `Equivalent` fails). In plane terms:
  **the supporting plane of the hit changed — in normal *or* offset**. Normal-only ports silently drop
  the doorway half (§5.4).
- **The side test is a signed cross-product** (`IsPointLeftOfLine`, strict `> 0`, `Line.cs:102-107`),
  taken against the **previous** line for both the player and the new hit. New hit on the far side ⇒ the
  obstacle receded ⇒ `Open`. Same side as the player ⇒ something nearer ⇒ `Closed`. Walking past a bare
  doorway therefore yields `Open` at the leading jamb and `Closed` at the trailing jamb.
- **Consume-on-fire:** the tracked line is nulled after a cue, forcing a silent 2-sample re-seed —
  no double-fire per corner, at the price of a ≈0.12 s / ~0.4-tile under-fire window (cooldown + re-seed)
  in which a second transition is lost. Fine at tile granularity; dense geometry should re-track instead
  (§5.4).
- **Caveats:** `point == lastPoint` is the *idle* gate (while moving, the hit moves every tick) — don't
  port it as a change filter. `V2.Zero` doubles as "no hit", so a genuine origin hit would be dropped —
  use a real nullable. A beam **grazing a convex corner** at a shallow angle can alternate faces and
  false-fire; AHC has no guard (§5.4 adds one).

### 4.5 The proximity pitch

Gate: `floor(dist) < 3 && floor(dist) != lastRounded && TravellingThisDirection` — nothing else. `dist`
is Euclidean player→hit (the player is a dimensionless point).

| `floor(dist)` | Field | Event | Pitch |
|---|---|---|---|
| 0 | `Pitch5` | `Wall Approach 3` | highest |
| 1 | `Pitch3` | `Wall Approach 2` | mid |
| 2 | `Pitch1` | `Wall Approach 1` | lowest |

(Field names are musical intervals; the event numbers run the other way — easy to invert when porting.)

- **Edge-triggered on any bucket change, up or down** — diagonal travel can re-cross a boundary. Ports:
  fire only on *decrease* (§5.2); safe here only because AHC's distances are noise-free.
- **Travelling sets are narrow:** Front ∈ {F, FL, FR}; Left ∈ {L, FL, BL}; Right ∈ {R, FR, BR}. Straight
  reverse matches nothing (no rear pitch — the bump sound covers it).
- **Re-chirp on resume, by design:** every non-travelling tick wipes `LastRoundedDistance` to
  `int.MaxValue`, so the first travelling tick toward a near wall re-announces it. Keep this — it
  re-anchors the player after stopping or turning. (Resets never touch it.)
- **Reach is a time, not a distance:** 3 tiles at 3.5 tiles/s ≈ 0.86 s to contact. Port the time (§5.5).

### 4.6 The travel gate — defect, do not port

`TravellingThisDirection` uses **exact float equality** (`V2.operator==` → `Equals`, `V2.cs:356`),
while the role gate uses `AlmostEquals` (ε = 1e-5). The vectors compared went through different rounding
chains (`normalize(sin θ, −cos θ)` vs `normalize(normalize(Σ inputs) × speed)`), which disagree by ~1e-7
— under the tolerance, over exactness. *Measured* over 3600 headings: the exact `==` succeeds **26.8 %**
of ticks walking forward, 31.6 % strafing, 24.4 % diagonal — but **100 % at the four cardinal facings**,
which snap-turns set exactly. (An older revision claimed input is "axis-pure so `==` always holds" —
false; facing is continuous under mouse-look.)

**The compounding that makes it audible** (2026-08 finding): each failed tick also **wipes
`LastRoundedDistance` to `MaxValue`** (§4.5) — so near a wall at a non-cardinal heading, every *successful*
tick re-fires the chirp (the memory was just erased), throttled only by the 0.1 s cooldown: **machine-gun
re-chirping at up to ~10/s** instead of a clean 2→1→0. The radar feels solid with snap-turns and noisy
under mouse-look — same code, different headings. Port the *intent* with a dot-product cone
(`dot(beamDir, v) > cos 15°` passes 100 % of the same headings) and both symptoms vanish.

### 4.7 Resets and turning — a design decision, not just a bug

`ResetRadar()` / `ResetAllButFrontRadar()` clear `LastCollisionPoint` + `LastCollisionLine` only
(`ReactiveRadar.cs:113-132`):

| Trigger | Call | Source |
|---|---|---|
| Continuous (mouse/key) turn, any tick it happens | `ResetAllButFrontRadar` | `FPExploring.cs:363-366` |
| 90° snap turn | `ResetRadar` | `FPExploring.cs:796-824` |
| Scripted teleport | `ResetRadar` | `FPScriptAPI.cs:213-216` |
| Map change | `ResetRadar` | `MapSoundHandler.cs:98-106` |

Purpose: a deliberate facing change must not manufacture fake transitions out of stale geometry.

**The front exemption.** Turning in place is *not* silent in AHC: with zero velocity the role gate
compares against a NaN heading and lets the open/closed path run (§4.2), the side beams are wiped every
turning tick, but the **front beam keeps its history and sweeps** — firing `Open`/`Closed` as it crosses
real edges. *Measured*: a 360° turn produced 8 cues next to a room opening, 7 in a plain corridor.
Read it honestly: the exemption is **deliberate code** (a method named `ResetAllButFrontRadar` exists
precisely to spare Front) riding an **accidental mechanism** (the NaN path), and the behaviour is a
**rotational edge scan** — turning in place sonifies each edge as your facing crosses it, which is a real
way to *find a doorway by turning*, at the cost of cues for distant geometry you didn't ask about.

**Porting decision, make it explicitly:** default to **silent turning** (reset all beams on any turn —
predictable, no chatter); optionally offer **turn-scan** as a deliberate feature (keep the front beam
tracking while rotating, with explicit zero-velocity handling — never via NaN). Don't ship the ambiguous
middle by accident.

### 4.8 The door chirp companion (POI scanner — full detail in doc 05)

Same three directions, completely separate code (`FPExploring.cs`): `RunForwardComparison` (`:391-439`),
`RunSideComparison` (`:441-459`), `PlayInteractibleInDistanceSound` (`:461-517`); door sounds built at
`:139-141` as `GetRadarSound("door", 0f/1f/2f)`. Verified first-hand 2026-08-24:

- Sensor is a **0.1-tile marching ray to 30 tiles** (`GameConfig.ScanDistance`), stopping at the first
  `Wall || Door` tile — not the radar's DDA.
- If any collected POI is a door tile (`p.Tile.Terrain.Door`, `:467`), the flat pre-panned `door` chirp
  plays for that side — **no distance, no pitch**; door outranks per-object radar sounds.
- Same role idea, tolerant version: suppressed when `LastTravel` is (anti)parallel to the beam
  (`AlmostEquals`, `:411-416`, `:451-455`).
- Throttled ~0.1 s by **two independent `DateTime.Now` timers** (forward `:393`, side `:443`) — so unlike
  the radar, a forward and a side `door` cue *can* land on the same tick.
- Re-fire gate is a **text diff** of the rendered POI list, which embeds `Math.Round(distance)` — sub-unit
  jitter absorbed, integer-distance changes re-fire. A cheap, robust idea worth stealing.
- Same player toggle (`SC.RadarEnabled`).

### 4.9 Sound layer and constants

- `GetRadarSound(name, pan)` sets the FMOD event's `"Panning"` parameter **once at construction**
  (`FModSoundContext.cs:790-803`); beams use `Left = 0f, Front = 1f, Right = 2f`. All 15 radar event
  instances (5 cues × 3 pans) are created once per session — first map load only; later map changes just
  `ResetRadar()` (`MapSoundHandler.cs:98-106`).
- **No 3-D positioning, reverb or occlusion** on radar cues; volume rides the FMOD snapshot
  `"Snapshot Radar and Beacon MASTER"` (default 85). The actual pan curve and the Wall-Approach timbres
  live in the FMOD banks (not source-verifiable).

| Value | Meaning | Where |
|---|---|---|
| `3` (`PitchRadarReach`) | pitch reach, tiles | `ReactiveRadar.cs:92,187` (const inlined, §4.3) |
| `1000f` (`OpenCloseRadarReach`) | ray length, tiles — "to the map edge" | `:94,165` |
| `0.1` (`delay`) | post-cue cooldown, seconds, real time | `:96,156` |
| `0f / 1f / 2f` | pan left / centre / right | `:105-107` |
| `1e-5` | the only live epsilon (`AlmostEquals`, `CoreExtensionMethods.cs:240-243`) | via `SoundExtensionMethods.cs:31-38` |
| `1.4 × 2.5 = 3.5` | tiles/s, always | `MapScriptObject.cs:171-175`, `RPGExploring.cs:43-51` |
| `30` | POI scan distance, tiles | `GameConfig.cs:59` |

True dead weight (don't port): `LastOpenOrCloseSoundPlayed` (written, never read), `mLastTravel` (never
touched), the ignored `position` args of `PlayOpen/PlayClose/PlayPitch`, `CollisionHelperCopy.EPSILON`.

---

## 5. Porting it

### 5.1 The adapter surface

```csharp
public interface IRadarWorld {
    Vector2 Position         { get; }  // navigation-plane position, world units
    Vector2 Facing           { get; }  // unit, YAW ONLY, projected to the nav plane — never camera pitch
    Vector2 TravelDir        { get; }  // unit; Vector2.zero when not moving — from the POSITION DELTA
    float   Speed            { get; }  // world units/s (magnitude of position delta / dt)
    bool    InActiveGameplay { get; }  // false in menus, dialogue, cutscenes, loading, death
    bool TryCastBlocking(Vector2 origin, Vector2 dir, float maxDist, out RadarHit hit);
}
public struct RadarHit {
    public float   Distance;           // required
    public Vector2 Point;              // origin + dir*Distance
    public Vector2 Normal;  public bool HasNormal;   // if the engine gives one (§5.4)
    public int     SurfaceId;          // collider/instance id — the best "same surface" test there is
}
```

Rules, each one a real bug class:

1. **`TravelDir` from the position delta, never input axes** — input lives in a different frame
   (camera-relative vs world) and any mismatch rotates the role split. Treat tiny deltas as standing still.
2. **`Facing` is yaw-only and the ray origin is the body/waist, not the camera** — camera-forward rays hit
   the floor when the player looks down; over-the-shoulder cameras start rays in the wrong place.
3. **`TryCastBlocking` hits exactly what stops the player's movement** — including closed doors and
   invisible barriers — and never triggers, decals, foliage, water. Reuse the movement collision mask.
4. **Gate on `InActiveGameplay` explicitly** — AHC survives without one only because position and facing
   freeze in menus (§4.2).

**What to hunt for in the target game's code first** — the adapter is filled from the game's own data
(PRINCIPLES: no invented values), so a non-tile port stays equivalent to AHC by construction — same
questions, that game's own answers:

1. **The movement collision query** the player controller actually uses (its layer mask / collision
   flags) — that exact mask is what `TryCastBlocking` casts against. Find it in the movement code, don't
   guess a "walls" layer.
2. **The walk/run speed source** — a movement component field or config value; if none is readable,
   measure the position delta in game. Feeds `Speed` and the 0.86 s reach (§5.5).
3. **The yaw source** — body transform vs camera; where the game separates them, take the body.
4. **The gameplay-state flags** the game itself checks for pause / menu / dialogue / cutscene;
   `InActiveGameplay` is their AND.
5. **What the raycast API returns** (normal? collider id? distance only?) — this alone picks your
   open/closed classifier row in §5.4.
6. **The world unit scale** (1 unit = ? metres) and the typical corridor width — measure the width in
   game with the radar's own left+right hit distances (§5.5).

### 5.2 The per-tick core

```csharp
sealed class Beam {
    public Vector2 Dir; public bool Aligned, Lateral;
    public float   Pan;                // -1 / 0 / +1, fixed at construction
    public Plane?  Plane;              // tracked surface (§5.4)
    public int     LastBucket = -1;    // last proximity bucket announced
}

sealed class ReactiveRadar {
    readonly Beam Left  = new(){ Pan = -1f },
                  Front = new(){ Pan =  0f },
                  Right = new(){ Pan = +1f };
    Beam[] Order => new[]{ Left, Front, Right };     // fixed priority — side beams outrank Front (§4.2)
    float nextCueTime;                               // ONE global cooldown

    public void Tick(IRadarWorld w, float now) {
        if (!Enabled || !w.InActiveGameplay) { ResetAll(); return; }   // the gate AHC lacks
        if (now < nextCueTime) return;                                 // freezes sensing too — deliberate

        Vector2 f = w.Facing;
        Front.Dir = f;
        Left .Dir = new Vector2( f.y, -f.x);         // AHC "Left" = PerpendicularRight — VERIFY the sign
        Right.Dir = new Vector2(-f.y,  f.x);         // in your engine's handedness (§4.1 trap)

        Vector2 v = w.TravelDir;
        bool moving = v != Vector2.zero;
        float reach = 0.86f * w.Speed;               // reach is a TIME — §5.5
        float bucketSize = reach / 3f;

        foreach (Beam b in Order) {
            float align = moving ? Vector2.Dot(b.Dir, v) : 0f;
            // cone with hysteresis replaces AHC's broken exact == (§4.6)
            b.Aligned = moving && (b.Aligned ? align > AlignExit : align > AlignEnter);
            b.Lateral = moving && MathF.Abs(align) < PerpMax;          // role set by VELOCITY (§1)
        }

        foreach (Beam b in Order) {
            if (!w.TryCastBlocking(w.Position, b.Dir, MaxRayLength, out RadarHit hit)) {
                b.Plane = null; b.LastBucket = -1; continue;           // miss ⇒ silent re-seed (§4.4)
            }
            bool emitted = false;

            if (b.Aligned && hit.Distance < reach) {                   // detector A (§4.5)
                int bucket = Math.Min((int)(hit.Distance / bucketSize), 2);
                if (bucket < b.LastBucket || b.LastBucket < 0) {       // fire only when CLOSER
                    Play(Cue.Approach(bucket), b.Pan); emitted = true;
                }
                b.LastBucket = bucket;
            } else {
                b.LastBucket = -1;                                     // re-arm ⇒ one reminder on resume
            }

            if (b.Lateral && Classify(b, hit, w.Position) is Cue c)    // detector B (§5.4)
                { Play(c, b.Pan); emitted = true; }

            if (emitted) { nextCueTime = now + 0.1f; break; }          // one beam per evaluation
        }
    }

    // §4.7 — on continuous turn: sides always; Front too unless you deliberately ship turn-scan
    public void ResetSides() { Left.Plane = Right.Plane = null; }
    public void ResetAll()   { foreach (var b in Order) b.Plane = null; }
}
```

Deviations from AHC, all deliberate: the gameplay gate, the alignment cone, proximity firing only on
*decrease* (AHC fires on any change — safe only with noise-free distances), and re-tracking instead of
consume-and-null inside `Classify` (§5.4).

### 5.3 Beam layout: how many, in which frame

Two decisions hide inside AHC's "3 facing-relative beams", and they only resolve that way for
first-person:

| Camera / genre | Frame | Beams | Notes |
|---|---|---|---|
| First-person (AHC, RE7) | facing-relative | 3 (L/F/R) | AHC parity; reversing covered by a bump sound |
| Top-down / isometric / twin-stick | **world-fixed** | **4 (N/S/E/W)** | see below |
| Third-person, free camera | relative to the **body**, not the camera | 3 or 4 | free strafe/reverse ⇒ take 4 |
| Aiming games | world-fixed for navigation + a separate aim ray | 4 + 1 | don't overload one beam set with both jobs |

- **Top-down goes world-fixed** because the player reads the map and the controls world-fixed ("an
  opening to the east" must sound the same whatever the sprite faces). Same velocity-driven role rule;
  whichever beam is behind you rotates as you move.
- **"A ray behind the player"** falls out for free from a world-fixed set — and that's the right framing
  of the instinct. A rear beam in a *facing-relative* set is idle while walking forward (antiparallel),
  and earns its keep only while reversing (pitch — the cue AHC lacks) or strafing (open/closed behind).
  Worth it where reversing is common (top-down, twin-stick); marginal in first-person.
- **Front/back pan collides** in a 4-beam set: N and S both want centre. Disambiguate by **pitch**
  (N high, S low, E/W panned at mid pitch) so direction survives in mono. Do **not** go to 8 beams —
  diagonals make two beams aligned at once; the fixed-priority + one-cue arbitration (§4.2) already
  handles it, and 8 beams double the cue rate.
- **What the radar cannot answer:** "is there a wall behind me *right now*" is a state; pair the radar
  with the [wall-sonification bed](../wall-sonification/) on the same directions (§1).

### 5.4 The open/closed classifier for your engine

AHC's line fit is exact only because of the pin (§4.3). In a continuous engine the hit point carries the
sensor's noise, and the fit's baseline is tiny (~0.06 units), so *millimetres* of noise swamp the wall
direction. *Measured* (AHC-faithful sim, corridor with one opening — ground truth **2** events):

| Classifier | σ=0 | σ=1e-4 | σ=1e-3 | σ=1e-2 |
|---|---|---|---|---|
| AHC line fit verbatim | **2** ✓ | 67 | 67 | 67 |
| Normal-only compare (20°) | 0 ✗ | 0 ✗ | 0 ✗ | 0 ✗ |
| **Plane tracker (below)** | **2** ✓ | **2** ✓ | **2** ✓ | **2** ✓ |
| Distance hysteresis (fallback) | 2 ✓ | 2 ✓ | 2 ✓ | 2 ✓ |

(The line fit collapses at the *first* nonzero noise; field corroboration from our RE7 port: 27 flapping
events vs 7 clean. The normal-only row fails differently — **blind**: past a doorway the near and far
walls are parallel, the normal never changes, only the offset does. An older revision recommended
normals-only; it drops the most common cue. Always test offset too.)

**The portable formulation** — this *is* AHC's algorithm (track the supporting plane; fire when it
changes in normal *or* offset; classify by side of the old plane):

```csharp
// per beam, only while Lateral and moving
if (noHit) { beam.Plane = null; return null; }                      // lost surface ⇒ silent re-seed
Plane cur = PlaneFrom(hit);                                         // point + normal (sources below)
if (beam.Plane == null) { beam.Plane = cur; return null; }          // SILENT seed, never announce

float denom = Dot(beamDir, beam.Plane.Normal);
if (Abs(denom) < 0.2f) { beam.Plane = cur; return null; }           // grazing guard (AHC has none)
float predicted = Dot(beam.Plane.Point - origin, beam.Plane.Normal) / denom;   // old plane, this tick

bool changed = predicted <= 0f
            || Abs(hit.Distance - predicted) > PlaneEps             // offset changed (doorway!)
            || (hit.HasNormal && Dot(hit.Normal, beam.Plane.Normal) < COS20);  // orientation changed
Cue? cue = changed ? (hit.Distance > predicted ? Cue.Open : Cue.Close) : null;
beam.Plane = cur;                                                   // re-track EVERY tick (no blind window)
return cue;
```

Predicting the old plane's distance (instead of comparing raw distances) cancels out the player's own
oblique motion — that's what keeps it quiet along walls at any angle. *Measured*: robust to combined
±0.08 rad normal jitter + 0.05-unit distance noise, no flapping at 45°/70° beam-wall angles.

Where the normal comes from:

| Your raycast returns | Use |
|---|---|
| Tile/voxel face hits | AHC's line fit verbatim (§4.4) — hits are exact, nothing to tune |
| Physics normal (Unity `RaycastHit.normal`, Unreal `ImpactNormal`) | plane tracker above |
| Collider/instance id | add `SurfaceId != lastId ⇒ changed`; keep the offset check (long walls = one collider) |
| Distance only | second parallel probe ~0.3 u offset ⇒ `n ≈ normalize(perp(hitB − hitA))`; or the fallback below |

**Fallback (no normals at all)** — absolute-distance hysteresis; slightly later cues,
speed-independent, four constants:

```
if      dist >= OpenEnter:                ns = OPEN     // ≈ 2.5 × corridorWidth
else if dist <= WallEnter:                ns = WALL     // ≈ 1.0 × corridorWidth
else if prev == OPEN && dist < OpenExit:  ns = NEUTRAL  // enter ≠ exit: dead-band ≈ 0.5 × width
else if prev == WALL && dist > WallExit:  ns = NEUTRAL
seed silently on (re)start; announce only on state change to OPEN/WALL
```

Never use a per-tick jump threshold (`|dist − lastDist| > k`): speed-dependent — slow walks miss
openings. Field-validated failure.

### 5.5 Constants: derive, don't copy

Express everything against two values read from the game itself: `walkSpeed` (units/s, measured from the
position delta) and `corridorWidth` (stand in a corridor, read left+right hit distances off the radar).

| Constant | Derivation | AHC value |
|---|---|---|
| `Reach` | `0.86 s × walkSpeed` (time-to-contact, not distance); recompute per tick if speed varies | 3 tiles |
| Bucket size | `Reach / 3` | 1 tile |
| Cue cooldown | 0.1 s — perceptual, copy as-is | 0.1 s |
| Sense rate | every frame; cap cues, not sensing | ~60 Hz |
| `MaxRayLength` | biggest room that matters; ≥ 30 × corridorWidth ≈ AHC's "unbounded" | 1000 tiles |
| `PlaneEps` | `0.25 × corridorWidth`, floored at 3× raycast noise σ | n/a (exact) |
| `AlignEnter / AlignExit` | `cos 15° / cos 25°` — enter narrow, leave wide | exact `==` (broken, §4.6) |
| `PerpMax` | `\|dot\| < 0.5` ⇒ lateral role | `AlmostEquals` ε=1e-5 |
| Grazing guard | `\|dot(beamDir, normal)\| < 0.2` ⇒ silent re-seed | none |

**Do not port:** the exact-equality gate (§4.6) · `V2.Zero` as no-hit sentinel · consume-and-null (§4.4)
· the NaN-dependent turn behaviour (§4.7) · 3-D spatialisation of the cues (flat pan only — README hard
rule) · a rear beam in a facing-relative first-person set (bump sound covers it).

---

## 6. Symptom → cause → fix

| Symptom | Cause | Fix |
|---|---|---|
| Open/closed flip-flops constantly | line fit on a continuous raycast (§5.4) | plane tracker |
| Doorways never announce, corners do | normal-only compare (§5.4) | add the plane-**offset** test |
| Slow walks miss openings | per-tick jump threshold | hysteresis or plane tracker (§5.4) |
| Machine-gun chirps near walls | exact-equality gate flicker + bucket-memory wipe (§4.6) | alignment cone |
| Almost no pitch cues at all | exact-equality gate copied verbatim (§4.6) | `dot > cos 15°` |
| Side tones while walking straight | role gate too loose, or travel from input axes | cone ±15°, travel from position delta |
| Chirps with no wall there | ray hits triggers/decals/water, or camera-forward beam hits the floor | movement mask; yaw-only beams from the body |
| Repeated chirps at one distance | `floor()` flapping on noisy distance | fire only on bucket **decrease**; re-arm on leaving reach |
| Cues feel late / bunched | sensing throttled to the cue rate | sense every tick, cooldown only emission (§4.2) |
| Two cues at once, muddy | per-beam cooldowns | one global cooldown + first-beam-wins (§4.2) |
| Cue storm on turn / teleport / load | missing resets (§4.7) | reset all beams on those events |
| Cues while turning in place | shipped AHC's accidental turn-scan (§4.7) | decide: silent turns (reset all) or deliberate turn-scan |
| Radar chatters in menus / dialogue | no gameplay gate (AHC has none, §4.2) | gate on `InActiveGameplay` |
| Left/right mirrored | `PerpendicularRight/Left` naming trap (§4.1) | verify: "facing East, my left points North" |
| Doorways with doors say nothing | correct — doors are ray-solid (§2); identity is the door chirp's job | port a POI/door scan too (§4.8, doc 05) |
| Silence in open terrain | correct — no hit ⇒ silent by design (§4.4) | if needed, add a *separate* "in the open" cue |

## 7. Acceptance test for a port

Screen reader off, only the radar audible:

1. Straight 2-wide corridor, walk forward → **silence** from the side walls.
2. Walk head-on at a wall → three chirps, rising pitch, centre, ending as you touch it.
3. Walk past a bare doorway on the right → exactly one `Open` right, then one `Close` right.
4. Repeat #3 at half and double speed → same two cues, same places.
5. Walk past a doorway **with a closed door** → `door` chirp, no open/close (needs the §4.8 companion).
6. Strafe right into a wall → chirps panned right, none centre.
7. Back straight into a wall → silence (bump only) — unless you shipped a rear/world-fixed beam (§5.3).
8. Turn 360° on the spot → per your §4.7 decision: silence (default) or edge cues *only* at real edges
   (turn-scan mode); never a burst when you start walking again.
9. Stop next to a near wall, resume toward it → exactly **one** reminder chirp.
10. Walk a long, gently curved wall → **silence** (the noise test — any flapping fails here).
11. Load a level / teleport → no cue in the first 0.5 s.
