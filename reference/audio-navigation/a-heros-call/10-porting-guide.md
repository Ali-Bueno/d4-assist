# 10 — Porting Guide: taking Style-1 reactive radar to any engine

This is the document for porting A Hero's Call's (AHC) **whole navigation stack** to another game —
FPS (Resident Evil 7 class), third-person, top-down, or anything without a tile grid: the layered
architecture and adapter, the hidden assumptions that only hold on a 2D tile grid, per-genre recipes,
the announcement layer, and field-tested pitfalls. **The radar itself — spec and porting recipe — lives
in doc 02**; §5 below points there instead of restating it, so the two documents cannot drift.

> **Confidence.** Everything cited as `path:line` is verified against the decompiled source
> `D:\code\c#\a-heros-call\decompiled\src` (2026 re-derivation pass; see the sibling docs 02–09 and the
> findings they were reconciled against). Everything marked **(recommendation)** is our engineering
> judgment for other engines, cross-checked against **our RE7 port** (the first real continuous-3D port,
> used here as a running case study). When the two disagree, the AHC citation is the fact and the
> recommendation is how we adapt it.

> **"Wall" always means *any impassable obstacle*, never just a rendered wall.** AHC radars against
> `Wall || Impassable || Door` (`Sound/Ian/ReactiveRadar.cs:165` → `CollisionHelperCopy.cs:30-41`). On a
> port the beams must hit rocks, trees, fences, furniture, **invisible / out-of-bounds barriers** — the
> same colliders that stop the player's own movement. The test is *"does this block movement?"*, never
> *"is it named wall?"*.

---

## 1. The layered architecture (what is portable)

The whole system is a stack. **Only the bottom layer is game-specific.** Everything above the Queryable
World is engine-agnostic and should be copied almost verbatim across ports — that is the entire point of
the adapter (§2).

```
        ┌─────────────────────────────────────────────────────────────┐
        │  OUTPUT   Cue / Speech sink  (pan L/C/R, pitch bucket, TTS)  │  engine-agnostic
        ├─────────────────────────────────────────────────────────────┤
        │  DIFF-GATE / HYSTERESIS   one cue per eval; enter≠exit;      │  engine-agnostic
        │           post-cue cooldown; "only announce real changes"    │
        ├─────────────────────────────────────────────────────────────┤
        │  CLASSIFICATION   impassable / door / dynamic / hazard;      │  engine-agnostic
        │           open vs closed; near/mid/far bucket; region name    │
        ├─────────────────────────────────────────────────────────────┤
        │  SENSORS   reactive radar beams (L/F/R)                      │  engine-agnostic
        │            POI forward/side scan · bump · tile-quantized      │  (all written against
        │            events (region/door/ambient) · beacon             │   the adapter only)
        ├─────────────────────────────────────────────────────────────┤
        │  ADAPTER  PlayerPosition · HeadingDegrees · TravelDirection  │  the ONLY seam
        │           · QueryRay · CategoryOf · RegionAt · Clock         │
        ├═════════════════════════════════════════════════════════════┤
        │  QUERYABLE WORLD   the game itself: physics, navmesh, tile    │  GAME-SPECIFIC
        │           grid, transforms, scene graph                       │  (rewritten per game)
        └─────────────────────────────────────────────────────────────┘
```

In AHC the Queryable World is a resident 2D tile grid (`FPTile`/`FPTerrain`, doc 03 §1) and
the adapter reads `world.Player.Position / world.Facing / world.Velocity` after movement has run
(`RPG/Ian/RPGGameLoop.cs:83-89`). In a 3D game the Queryable World is physics + transforms; **you rewrite
only that layer and the adapter that wraps it.** The radar loop (§5), the diff-gating, the classification,
and the cue sink move over unchanged. If you find yourself re-implementing radar logic per game, the seam
is in the wrong place.

---

## 2. The adapter contract

This is the minimal interface the game must provide. Write **all** accessibility logic against this, never
against the engine (recommendation — this is the single discipline that made AHC's own logic portable, and
its absence is what broke our RE7 port).

| Member | Type | Contract |
|---|---|---|
| `PlayerPosition` | `Vec2` | Player position **projected onto the navigation plane** (drop the up-axis; §4e for verticality). AHC: `V2`, already 2D. |
| `HeadingDegrees` | `float` | Facing as a compass angle in **ONE canonical frame** — see below. AHC: `FPState.Player.FacingInDegrees` (`FPClientWorld.cs:49-59`). |
| `TravelDirection` | `Vec2?` | **Unit direction of actual motion, derived from POSITION DELTA** — not from input axes. `null`/zero when not moving. |
| `QueryRay(origin, dirDeg, maxDist)` | `{hitDistance, category}` | First movement-blocking hit along `dirDeg`. AHC: `CollisionHelperCopy.GetWallOrImpassableOrDoorCollision` (`ReactiveRadar.cs:165`). |
| `CategoryOf(hit)` | enum | `impassable / door / dynamic object / hazard`. AHC packs this into terrain flags `Wall/Impassable/Door` (`FPTerrain.cs:31-41,60-70`). |
| `RegionAt(position)` | `RegionId` | Named area at a position, for enter/leave announcements. AHC: `map.Tiles.Get(floor X, floor Y).Region` (`FPExploring.cs:169-191`). |
| `Clock` | monotonic seconds | Frame-independent time for cooldowns. AHC uses a `Stopwatch` for the radar (`ReactiveRadar.cs:93,117`) but `DateTime.Now` elsewhere — **use a monotonic clock everywhere** (recommendation). |

### One canonical heading frame — declare it once, convert in one helper

Define the compass **once** for the target engine and never re-derive it. Pick: **0° = North, increasing
clockwise**, mapped onto a stated axis pair. AHC's frame is `x = sin θ`, `y = −cos θ` on a Y-down plane
(`Core/Ian/MyMath.cs:35-43`), i.e. 0°=N=(0,−1), 90°=E=(1,0). Your engine's plane will differ (RE Engine is
Y-up, so the navigation plane is XZ and the vertical sign flips). **Write exactly one
`HeadingToVector(deg)` helper and one `VectorToHeading(vec)` helper; every service calls them.**

> **This is the mirroring bug.** AHC's `Left` property is built from `PerpendicularRight` and vice-versa
> (`MapAndPlayer.cs:19,23` → `V2.cs:59,61`) — one sign error inverts L/R for the whole mod. All
> mirrored-direction bugs come from *redundant re-derivations* of this math. In our RE7 port **five**
> separate services each re-derived the yaw→forward/right transform independently; they happened to agree,
> but any one tuning edit silently desyncs the rest. **Never let a second copy of the conversion exist.**

### Travel direction from position delta — why, not input axes

**Derive `TravelDirection` from `(position_this_frame − position_last_frame)`, projected to the nav plane
and normalized** (recommendation, and it is what AHC effectively does — its `Velocity` is
`travel * speed` applied to `Position` each frame, `FPMapLogic.cs:276-290`, and the radar reads that
post-movement `Velocity`, `ReactiveRadar.cs:120`).

Do **not** feed the beam role-split from raw input axes. In our RE7 port the travel signal was taken from
the engine field `inputMoveAngle`, whose own reference frame was documented **contradictorily** — one
comment called it "relative to facing" (camera-relative), another "world compass degrees" (world-absolute)
— and the code treated it as world-absolute. If it is in fact camera-relative, then every time the camera
yaw leaves 0° the travel vector is rotated by the wrong amount, corrupting the `dot(travelDir, beamDir)`
gate that **every** cue keys off. That one ambiguity produced widespread, direction-dependent flakiness.

Position delta sidesteps the entire class of bug: it is **frame-independent and unambiguous** — a world
displacement has no "relative to camera vs relative to body" question to get wrong. Whatever the input
system reports, where the player *actually went* last frame is a fact. Smooth it with a short EMA if the
per-frame delta is jittery, but never re-introduce input-axis angles as the source of truth.

---

## 3. Hidden assumptions of AHC and their replacements

Each of these holds *only* because AHC is an integer 2D tile grid with digital input. Every one breaks on a
continuous 3D game. The replacement column is the port (recommendation unless it restates AHC).

| # | AHC assumption (evidence) | Why it breaks elsewhere | Replacement |
|---|---|---|---|
| 1 | **Integer 1-unit tile grid** underpins events, regions, readouts, beacon arrival. Tile = 1 world unit, `Center=(i+0.5,j+0.5)`, player→tile = `(floor X, floor Y)` (`FPTile.cs:14-21`, `FPMapLogic.cs:31`). | Continuous engines have no cells; nothing to `floor`. | Define a **virtual cell size Q** (≈1 m, or scaled to player speed) and quantize with `floor(pos / Q)`. Fire the tile-keyed events (region/door/announce) on virtual-cell change. |
| 2 | **Role/axis matching by exact equality** — velocity `==` a direction vector, `V2.==` is exact float compare (`ReactiveRadar.cs:149-151` → `V2.cs:385-392`); AlmostEquals uses ε=1e-5 (`CoreExtensionMethods.cs:240-242`). Works only because digital input produces exactly-axis-pure motion. | Analog sticks / mouselook almost never produce an exactly axis-pure travel vector, so the test essentially never matches → the role split misfires → "inconsistent radar." **This is the single biggest source of porting inconsistency.** | **Angular sectors** via dot product, **with hysteresis**: a beam is "travel-aligned" when `dot(travelDir, beamDir) > cos(±22.5°..±45°)` and "perpendicular" when `|dot| < cos(~60°)`. Make the **enter and exit angles differ** so a beam near a boundary can't flap between roles. |
| 3 | **Y-down clockwise compass**, `x=sinθ, y=−cosθ` (`MyMath.cs:35-43`); Left/Right via cross with world-up (`FPClientState.cs:39-47`). | A different engine handedness/up-axis flips the perpendicular sign → mirrored L/R. | Declare the target frame **once** (§2) with one conversion helper. All mirroring bugs are redundant re-derivations — delete them. |
| 4 | **Collision evaluated only at cell crossings; walls axis-aligned; player is a dimensionless point.** `HandleUnitPointToWallCollision` returns with no check when the move stays in one tile, and slides by zeroing one axis (`FPMapLogic.cs:46-49,90-131`). | Sub-cell motion goes unchecked; arbitrary-angle 3D walls need normal projection, not axis-zeroing; a point player misses low/thin geometry. | Use the engine's **physics raycasts / capsule sweeps every frame**. **Do not replicate the axis-zero slide logic** — that is the game's job, not the accessibility layer's. You only *sense*, you never move the player. |
| 5 | **DDA grid rays.** Two-sweep grid traversal (`CollisionHelperCopy.cs:43-92`). | No grid to traverse. | **Physics raycast** to first blocking hit; return `hitDistance` + `category`. |
| 6 | **Wall-clock `DateTime` throttles at a fixed 60 fps tick** (`RPGGameLoop.cs:10,24`); several throttles use `DateTime.Now`. | Fixed-fps assumption + wall clock drift under variable frame rate. | **Monotonic time. Sense EVERY frame; apply a cooldown only *after* a cue fires.** AHC's real numbers: radar post-cue cooldown **0.1 s** (`ReactiveRadar.cs:141,154-157`), bump **0.6 s** (`FPMapLogic.cs:142-150`), beacon **2 s** (`FPExploring.cs:98`). The 0.1 s sits *inside* the play branch, so with nothing playing the beams re-scan every frame — it is a min-gap-between-cues, **not** a 10 Hz sample rate. |
| 7 | **Distance buckets = `floor(distance)` in tiles, at an effective 3.5 tiles/s speed.** Pitch fires at `rounded < 3` and picks 1 of 3 samples by `floor(dist)` (`ReactiveRadar.cs:177,187`). Speed: base 1.4 tiles/s (`MapScriptObject.cs:171-175`) **× an unconditional 2.5 "running" modifier** (`RPGExploring.cs:43-51` — applies every tick, run key or not; corrected 2026-07, Doc 04) ⇒ the only speed that exists while exploring is **3.5 tiles/s**, so the 3-tile reach ≈ **0.86 s of travel**. | "3 tiles" is unitless magic on another game's scale — a 3-unit reach feels wrong at a different movement speed. | Express thresholds in **time-to-contact** (seconds at current speed) — `ttc = dist / speed` — or in meters scaled by player speed, so cues feel identical across games regardless of unit size or pace. Bucket the TTC/scaled distance into the same 3 levels; AHC parity ⇒ reach ≈ 0.9 s TTC. |

Two more AHC facts that porters over-copy (both **recommendation: do NOT port**):

- **AHC has no dialogue/cutscene gate on the radar.** `RunRadar` never checks `AreWeInMenu`
  (`MapSoundHandler.cs:300`; doc 02 §4.2). It is silent in menus only *incidentally*, and **not because
  velocity is zero**: a zero velocity normalises to `NaN`, which makes the role gate return *false* and
  lets the open/closed path run. What actually keeps it quiet is that **position and facing freeze**, so
  the beam re-hits the same point and the `point == lastPoint` gate bites (doc 02 §4.2). An engine that
  keeps a physics velocity or a drifting camera alive while paused will chatter. Modern games also have
  real-time dialogue and cutscenes where the player *is* still positioned in the world; you must add an
  explicit gameplay gate (§4b). AHC gets away without one; you won't.
- **The beacon "guidance" is advisory audio only** — AHC never auto-walks the player;
  `PlaySimpleBeacon()` is even an empty method (`FPExploring.cs:332-334`; doc 07). Don't build
  auto-steering expecting parity. Parity is a positioned beep + on-demand distance/direction speech.

---

## 4. Per-game-type recipes (with workarounds)

### 4a. Top-down / native grid

Near-direct mapping — the closest case to AHC. The Queryable World is already a grid or a flat plane; the
radar, POI scan, and tile-events port almost verbatim.

Still adapt:
- **Beam layout — take 4 world-fixed beams, not AHC's 3 facing-relative ones** (full rationale and the
  cue-design consequences in doc 02 §5.3). A top-down player reads the map world-fixed and moves
  world-fixed, so beams should be **N/S/E/W (screen up/down/left/right)**, keeping the same
  velocity-driven role rule. This also answers "how do I sense behind the player?" — with a world-fixed
  set there is no rear beam as such; whichever direction is behind you rotates as you move, and it starts
  emitting proximity pitch the instant you reverse into it. Two consequences: **N and S both pan centre**,
  so separate the axes by **pitch** (N high, S low) or they are indistinguishable; and strafing makes two
  beams perpendicular at once, so keep the single global cooldown and a deliberate priority order.
  Do **not** expand to 8 beams for diagonal movement — reuse the fixed-order arbitration instead.
- **"What is behind me *right now*" is not a radar question.** The radar only speaks on change, so it is
  silent while you stand still. Pair it with the continuous
  [wall-sonification bed](../wall-sonification/) on the same four directions; that layer answers the
  standing-still question, the radar answers "what just changed".
- **Input frame.** Even a top-down game may have a rotating camera; derive `TravelDirection` from position
  delta (§2), not stick axes.
- **Audio backend.** AHC is FMOD; you likely have a different sink. Route pan L/C/R and the 3 pitch
  buckets through your `PlaySound` primitive (§5).
- **Cell size.** If the grid unit ≠ 1 m, set Q to the real cell size and keep thresholds in TTC/meters (§3 #7).

### 4b. Continuous 3D first-person (RE7-class)

The reference port. Everything in §3 applies. Concretely:

- **Rays: physics raycasts with correct layer masks.** Cast against movement-blocking geometry only —
  **exclude triggers, decals, water, and non-collidable volumes**. A ray that stops on a decal reports a
  phantom wall.
- **Dual-height rays.** Cast each beam at **two heights — chest and knee — and merge by min-distance**, so
  low obstacles (crates, railings, furniture the eye-ray flies over) are still caught. (recommendation;
  our RE7 port uses an eye ray plus a floor-hugging low ray.)
- **Heading source = camera yaw.** In first-person there is usually no separate body yaw; the camera *is*
  the facing. Use camera yaw for `HeadingDegrees`.
- **Travel from position delta projected to the ground plane (XZ)**, per §2.
- **Virtual-grid quantization** (Q, §3 #1) drives region-enter, door-announce, and position-readout events.
- **Angular-sector beam roles with hysteresis** (§3 #2) — never exact equality.
- **Sense per frame, cooldown only after a cue** (§3 #6).
- **Gameplay gate INCLUDING dialogue and cutscenes.** AHC omits this (§3); you must not. In our RE7 port
  the gameplay check covered only pause/inventory menus, so the radar kept firing wall/exit tones *under*
  in-scene dialogue and cutin popups — which reads to the player as pure randomness. Gate on a single
  `IsGameplay()` that also returns false during dialogue, interaction prompts, and cutscenes.
- **One frame-conversion helper** (§2) — our RE7 port's five copies are the anti-pattern.
- **Reset all service state on scene load / teleport.** Detect a large single-frame position jump *and*
  hook scene loads. In our RE7 port most services' `Reset()` methods existed but were **never called**, so
  stale positions and destroyed-object references leaked across transitions. Wire `Reset()` to real events,
  not just to a teleport-distance heuristic (§6).

### 4c. Third-person

As 4b, plus the camera-yaw ≠ character-yaw split:

- **Choose the heading deliberately.** Beams can be **character-relative** (use the character's facing, so
  "front" is where the body points) or **travel-relative** (roles derived purely from `TravelDirection`).
  Pick one and document it; do not mix. (recommendation)
- **Beware the over-the-shoulder camera offset.** The camera is displaced from the character, so a ray
  from the *camera* origin starts in the wrong place. **Cast beams from the character capsule origin, not
  the camera transform** — otherwise near-wall distances are off by the shoulder offset. (recommendation)

### 4d. No usable raycast API

When the engine won't give you an arbitrary-direction ray, build a passability oracle:

- **Virtual occupancy grid.** Sample walkability at a ring of virtual cells around the player from the
  navmesh / walkability probes; cache it, refresh only on virtual-cell change. Then "raycast" by marching
  the beam through the cached grid (this is exactly AHC's model, just fed from probes instead of tiles).
  (recommendation)
- **Short spherecasts** as a raycast substitute where available. (recommendation)
- **Pathfinding reachability as an oracle.** If the engine exposes an A*/navmesh query, "is this point
  reachable / how far along a straight path before it's blocked" answers open/closed and proximity without
  a raw ray. AHC's own beacon LOS clamp uses exactly this idea — walk path tiles until the straight
  segment is blocked (`FPExploring.cs:281`, doc 04). (recommendation)

### 4e. Verticality (stairs, ramps, multi-floor)

AHC is strictly 2D — height exists in data but is renderer-only, and `VectorHelpers.MyTransform` throws if
Z≠0 (doc 03 §1). So this is entirely **recommendation**:

- **Project per floor.** Keep the nav plane 2D, but bind it to the *current* floor's ground height.
- **Inclined probe rays for step/ramp detection.** Cast a short ray angled down-forward; a hit slightly
  above/below foot height that is still walkable is a step or ramp, not a wall.
- **Separate up/down cues.** Distinguish "stairs up" from "stairs down" with distinct sounds; don't fold
  them into the flat wall/open vocabulary.
- **Re-plane on floor change.** Detect a floor change by the ground height crossing into a new band and
  rebind the nav plane; **reset radar history on the transition** (a floor change is a teleport as far as
  open/closed history is concerned).
- **Elevators and ladders are POIs, not radar targets.** Announce them via the POI/interaction scan; they
  are destinations, not obstacles to sonify.

---

## 5. The radar itself: one spec, one place

The radar's verified spec **and** its porting recipe live in **doc 02** — the adapter surface and
per-tick core loop (02 §5.1–5.2), beam count and frame per genre incl. rear/world-fixed coverage
(02 §5.3), the open/closed classifier options for continuous engines and why the verbatim line fit
collapses (02 §5.4), constants derivation (02 §5.5), the symptom table (02 §6) and the acceptance test
(02 §7). Port from there directly — the §3 replacements above are already folded into it. Nothing is
restated here, so the two documents cannot drift apart.

One radar-adjacent fact that belongs to this guide rather than doc 02: **radar cues are flat discrete
pan, but AHC's world-anchored sounds ride a real HRTF spatializer** (verified 2026-07, doc 08). AHC
loads the **Oculus Spatializer** (+ Google VR audio) FMOD plugins and "Desktop Oculus" banks
(`FModSoundContext.cs:172-182,739`) — beacons, POI/landmark loops, NPC footsteps and ambience are
**binaurally** positioned, a large share of AHC's perceived spatial precision. Port rule: radar/scan
cues on the flat 2-D path (`spatialBlend = 0`, outside reverb/occlusion mixer groups — README "HARD
RULE"), and every *world-anchored* sound through the target engine's spatialized/HRTF path (Steam
Audio, Oculus/Meta Audio, etc.), never hand-rolled stereo pan — plain linear panning is why ported
beacons feel vaguer than AHC's. (recommendation)

---

## 5b. The announcement layer — portable spec (verified 2026-07, docs 04/05/06)

The radar is half the system; the other half is *what gets spoken, when*. AHC's spoken layer is quieter
and more rule-driven than our ports have been — most "our mod feels imprecise/spammy" complaints trace to
missing one of these rules:

- **Single FIFO speech sink, no priorities.** Every announcement funnels into one queue
  (`SpeakLowPriority` is byte-identical to `SpeakAsync`, `SpeechContext.cs:72,109`). "Interrupt" is not a
  flag on the utterance — it is an explicit **cancel command enqueued before the speak**
  (`CancelAllSpeech`, then say). Port exactly that: one sink, `Interrupt()` + `Say()` as two operations.
- **Interrupt only on context change.** Region enter/leave **interrupts** (`FPExploring.cs:179`); the
  manual forward scan **interrupts**; everything else **queues** — portal prompts, examine, interact,
  position/region/beacon readouts (`FPExploring.cs:201,605,609,747`). And when a region change and a
  forward-scan fire in the same frame, the scan **queues behind** the region line instead of killing it
  (`FPExploring.cs:419`). Result: "entering Market, stall 4, as far as I can see 12" reads as one
  sentence stream, never as speech fighting itself.
- **The auto forward scan is SUPPRESSED while travelling straight forward or backward**
  (`FPExploring.cs:411-421`): it speaks only on strafe, stand-and-turn, region change (which bypasses the
  throttle), or the manual scan key. This is AHC's real anti-spam mechanism — a player beelining down a
  corridor hears footsteps and radar, *not* a POI list re-read every few steps. Ports that re-announce the
  forward list on a distance timer while walking are the ones that feel like chatter. Port the rule as:
  *speak the forward picture when the picture can have changed (heading/lateral change or new region),
  not while it is merely getting closer.*
- **"Leaving X" only exists when walking into unnamed space** (`FPExploring.cs:180-186`). Named region A →
  named region B says only "entering B". Do not emit leave+enter pairs; they double speech volume for
  zero information.
- **Sides are audio-only.** The side scans/comparisons never speak (`FPExploring.cs:529-532`) — lateral
  awareness is entirely the radar's open/closed + door cues. Resist adding spoken side lists; AHC's
  legibility comes from reserving speech for the forward axis and names.
- **Zones ≠ regions.** Reverb zones switch FMOD snapshots silently (`MapSoundHandler.cs:328-336`);
  spoken names come only from regions. Keep the acoustic-environment channel non-verbal in ports (reverb
  presets per area do real orientation work for free).
- **The scan output format** (doc 05): march the forward ray, emit `{name} {rounded distance}` items
  joined by commas, terminated by `"As far as I can see {dist}"` — distance-ordered, near to far. The
  terminator matters: it tells the player how much open space is ahead even when nothing is in it.

---

## 6. Porting pitfalls checklist

Generalized from the RE7 audit. Every item is a real failure we hit or nearly hit.

- [ ] **One shared frame-conversion helper — never N copies.** Our RE7 port had the yaw→forward/right
      transform re-derived in *five* services. They agreed by luck; one tuning edit desyncs them. Centralize.
- [ ] **Travel direction from POSITION DELTA**, not input axes — kills the camera-relative vs world-absolute
      ambiguity that caused direction-dependent flakiness (§2).
- [ ] **Hysteresis at every threshold** — beam-role sectors, open/closed bands, pitch buckets. Enter angle ≠
      exit angle; enter distance ≠ exit distance. A single-threshold compare *will* flap on analog input.
- [ ] **Uniform monotonic throttling.** Pick one clock and one policy: sense every frame, cooldown after a
      cue. Do **not** mix wall-clock throttles with frame-counted throttles — in our RE7 port the radar was
      wall-clock (true 10 Hz) while the door and interactable scans counted frames (assuming 60 fps), so
      under frame-rate variance they drifted and cues disagreed about what was there.
- [ ] **`Reset()` actually wired to scene loads.** Defining `Reset()` is not enough — in our RE7 port most
      services' resets were dead code, called by nothing, so stale positions and destroyed-object references
      survived scene transitions. Hook real scene-load / teleport events.
- [ ] **Gameplay gate covers dialogue and cutscenes**, not just pause/inventory menus. Cues firing under
      dialogue read as random (§3, §4b). AHC omits this; you must add it.
- [ ] **Unit sanity — meters vs game units.** State the unit once ("1 unit = 1 metre") and keep every
      threshold in it, or express everything in TTC. Mixed units silently mis-scale reach and pitch.
- [ ] **Run the AHC ground-truth acceptance test before tuning** (doc 02 §7): straight 2-wide corridor →
      side walls silent; strafe into a wall → that side chirps 3-2-1; walk at a wall → center chirps
      down; back into a wall → silence (bump only); stop and resume toward a near wall → exactly one
      reminder chirp. A port that passes this cannot produce "random" proximity tones.
- [ ] **Verify L/R with a known landmark on day one — the "mirrored world" smoke test.** Put a wall on the
      player's known right, walk past it, confirm the cue pans right. Do this *before* tuning anything; a
      sign error in the one conversion helper mirrors the entire world and every later tuning fights it.
- [ ] **Radar cues on the flat 2D audio path — never spatialized.** No `spatialBlend = 1`, no HRTF, no
      distance attenuation, no reverb/occlusion on radar/scan/sonification cues; those effects belong
      only to world-anchored sounds (§5; README "HARD RULE"). If a port's radar feels vague
      or cues seem to come from behind, check this first.
- [ ] **Time-scale your turning; don't copy AHC's.** AHC turns ±1°/loop (keys) and `MouseXChange/8`/loop
      **without delta-time scaling** (`FPExploring.cs:345-358`) — frame-rate-dependent, a defect of the
      original, survivable only because its loop is fixed 60 Hz. In a port, turn rate must be °/second.
- [ ] **Speech verbosity rules from §5b in from day one** — suppression while beelining, interrupt only on
      context change, no leave+enter pairs, sides audio-only. Bolting them on after tuning the radar is
      how ports end up feeling noisy *and* imprecise at once.

---

## 7. Lessons from the RE7 port: four more traps (2026-07 audit)

Four additional, source-verified findings from cross-checking the actual RE7 port against a second, deeper
decompile pass of AHC. All extend or correct guidance elsewhere in this guide.

### 7a. Do not cap the ray

Porting with a short sensing cap (our RE7 port used 8 m) plus a synthetic "FAR" state destroys the emergent
open-field cues that fall naturally out of an uncapped ray (doc 02 §4.4: a beam that finds no
wall at all goes silent and wipes its line rather than reporting a synthetic far state) and clusters phantom
transitions at the cap boundary. AHC's own open/close ray is a hardcoded `1000f`
(`Sound/Ian/ReactiveRadar.cs:165`) — effectively unbounded, the only real limit being the map edge (the DDA
walk simply exits the tile array, `CollisionHelperCopy.cs:63,83`). The faithful port keeps the
effectively-unbounded ray and treats "no hit" exactly as AHC does: silence and a line wipe, never a
synthetic FAR state.

### 7b. The swallow class is amplified in continuous worlds

AHC's consume-on-fire behavior (`ReactiveRadar.cs:220` nulls `LastCollisionLine` after every open/close cue;
the next evaluation early-returns with no baseline, `:212-215`) is inherent to the line method, not a bug
specific to one port — see doc 02's under-firing finding. It gets **worse**, not better, in continuous
worlds: lower sensing rates, point-acceptance thresholds, and any added coalescing windows all widen the
blind spot, and a pair of opposite-type cues (open→wall or wall→open) can land closer together in eval-time
than the rebuilt line allows. Extra coalescing makes this worse, not better — do not add it hoping to reduce
cue noise.

### 7c. Far-audible doors are the POI scan, not the radar

The perceived "doors audible from far away" effect does not come from `ReactiveRadar` at all — it is
`FirstPerson/Ian/FPExploring.cs` + `POIHelpers.cs` (doc 05 §1-2): three rays (front/left/right) every 0.1 s,
stepping 0.1 tile out to `GameConfig.ScanDistance = 30` tiles (`GameEngine\Ian\GameConfig.cs:59`), stopping
at the **first** `Wall` or `Door` tile (`POIHelpers.cs:42-47`); when that first blocker is a door, a panned
door cue plays — hard-left/center/hard-right sounds initialised at `FPExploring.cs:139-141`, played at
`:461-485`. Occlusion is inherent to this scan because it stops at the first hit. There is a secondary
channel: doors also emit positioned enter/exit sounds with `HeardThroughDoors=true`
(`FModSoundContext.cs:587-607`, triggered from `FPMapLogic.cs:199-210`) that ignore door tiles in the
occlusion sum; the positioned-sound mute distance is `13` tiles (`FModEventWrapper.cs:96`). Do not port "far
door audibility" as a radar feature — it belongs with the POI scan system (doc 05).

### 7d. Open-vs-enclosed comes from non-radar systems

"This room feels open / this room feels enclosed" is not radar output. It comes from: per-zone
environmental-reverb FMOD snapshots swapped at zone boundaries (`MapSoundHandler.cs:242-249,320-337`, docs
06/08); the indoor/outdoor ambient loop swap (`FPMapLogic.cs:185-197`) plus per-sound FMOD params
`Obstruction`/`PlayerIndoors`/`SoundIndoors` (`FModEventWrapper.cs:194-200`, doc 08); and shared terrain-bed
loops repositioned each frame to the nearest, least-obstructed tile of each terrain type within ≈26 tiles
(`MapSoundHandler.cs:274-292,588-627`). Cross-reference docs 06 (zones) and 08 (FMOD) rather than
re-deriving this signal in the radar — the radar should stay a pure geometry sensor.

---

*Cross-refs: doc 02 (reactive radar internals + the continuous-engine open/closed fix), doc 03 (raycaster),
doc 04 (movement/turning), doc 05 (POI scan), doc 06 (regions), doc 07 (beacons), docs 08–09 (audio +
speech), doc 11 (ancillary systems: NPC audio presence, landmark loops, scripting hooks). The verified findings behind this rewrite live in docs 02/03/04/08 themselves, reconciled with our
RE7 port audit.*
