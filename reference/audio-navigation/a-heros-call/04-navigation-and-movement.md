# 04 — First-Person Movement, Turning & Cardinal Cues

Source: `FirstPerson/Ian/FPExploring.cs` (state + input loop), `FPMapLogic.cs` (movement + collision),
`FPInput.cs` / `FPKeys.cs` (bindings), `FPMapOverview.cs` (look-around mode). `mWorld.Position`,
`mWorld.Facing`, `mWorld.Velocity` are convenience passthrough properties on `FPClientWorld`
(FPClientWorld.cs:35-59) that read/write the real backing state on `FPState.Player.FacingInDegrees` /
`FPState.Position` / `FPState.Velocity` (FPClientState.cs:37) — same values, just accessed through a
different object.

---

## 1. State representation

| State | Type | Notes |
|-------|------|-------|
| Position | `V2` | continuous (not grid-locked); grid tile = `(floor X, floor Y)` |
| Facing | `float` degrees | compass, 0=N CW; `mWorld.LastFacing` holds previous frame for crossing detection |
| Velocity | `V2` | set by movement; `= travel * CalcMovementSpeed * speedMod` |
| Front | `V2` | `FPMapLogic.UpdateFrontVector` → `GetUnitVectorFromCompassDegrees(Facing)`; recomputed on every facing change |
| LastTravel | `V2` | last frame's normalized travel direction (used to suppress scans while strafing) |

Heading is always clamped to `[0,360)` after any change:

```csharp
if (mWorld.Facing < 0f) mWorld.Facing += 3600f;   // +10 turns so a single subtract can't go negative
mWorld.Facing %= 360f;
```

---

## 2. Movement (continuous, per-tick, collision-aware)

`FPExploring.HandleKeys()` accumulates a travel vector from held movement keys, in the player's basis:

```csharp
V2 travel = V2.Zero;
if (input.IsHeld(FPMoveForward))  travel += FPState.Front;
if (input.IsHeld(FPMoveBackward)) travel += FPState.Back;
if (input.IsHeld(FPMoveLeft))     travel += FPState.Left;
if (input.IsHeld(FPMoveRight))    travel += FPState.Right;

float speedMod = 1f;
AffectTravelFunc?.Invoke(ref travel, ref speedMod);   // hook for slows/hastes
IsTravelling = travel.Length != 0f;
if (IsTravelling) {
    travel.Normalize();
    mWorld.Player.Travel = travel;
    FSO.FPML.MoveUnitBasedOnTravelVector(mWorld, mWorld.Player, travel, speedMod);
    FSO.FPML.AfterUpdatePosition(mWorld, travel);
    mWorld.FPState.LastTravel = travel;
} else { mWorld.Player.Travel = V2.Zero; mWorld.Velocity = V2.Zero; }
```

Speed is not free-form: `MapScriptObject.CalcMovementSpeed` (MapScriptObject.cs:171-175) =
**1.4 tiles/s × (1 + terrain.MovementSpeedAddPercent)** — a base of 1.4 tiles/sec on the tile the entity
currently stands on, modulated by that tile's terrain.

> **Effective speed is 3.5 tiles/s, not 1.4 (2026-07 deep pass, re-verified).** The only `AffectTravelFunc`
> ever assigned is `RPGExploring.HandleRunning` (`RPGExploring.cs:43-51`), and its `speedMod *= 2.5f` is
> **unconditional** (`RPGExploring.cs:50`) — it applies every tick whether or not the run key is held. The
> run key (`FPRunForward`) only **adds a `Front` vector** to `travel` (`RPGExploring.cs:46-48`), which
> merely biases direction when you're also strafing; it never touches `speedMod`. And because `travel` is
> **normalized** before the move (`FPExploring.cs:645`), that extra `Front` can't grow the magnitude
> either. So there is **no "walk" gear at all**: the player's real exploring speed on 0%-modifier terrain
> is `1.4 × 2.5 = 3.5 tiles/s`, modulated per tile by `Terrain.MovementSpeedAddPercent`
> (`MapScriptObject.cs:174`). At the 60 Hz fixed tick that is ≈0.0583 tiles/tick, ≈17 ticks to cross one
> tile. Use **3.5 tiles/s** whenever a port derives thresholds (time-to-contact, radar reach) from AHC's
> pacing.

`FPMapLogic.MoveUnitBasedOnTravelVector`:

```csharp
ent.Velocity = travel * u.CalcMovementSpeed(ent) * speedMod;
V2 change = ent.Velocity * (float)ent.GameTimeSinceLastTick;          // dt-scaled
FPLBumpResult bump = HandleUnitPointToWallCollision(ent, u, ref change);  // may zero an axis
if (bump.BumpOccured) PlayBumpSoundIfPlayer(ent, u, bump.BumpSound);
u.Position += change;
ent.FPState.DistanceTravelled += change.Length;
```

### Collision / wall blocking (`HandleUnitPointToWallCollision`, FPMapLogic.cs:39-132)

- Compute target tile from `Position + change`. **If it's the same tile as now, the function returns
  immediately with no collision check at all** (FPMapLogic.cs:46-49) — sub-tile motion is never
  evaluated; blocking is only checked on tile-boundary crossings. This is a key porting assumption for a
  continuous 3D game, which needs per-frame collision instead.
- Inspect the 4 orthogonal neighbors (N `y-1`, S `y+1`, W `x-1`, E `x+1`). If the entered neighbor is
  the target and **`Terrain.CanPass == false`**, zero the offending axis of `change` (slide along the
  wall — this assumes axis-aligned walls) and return an `FPLBumpResult` carrying that tile's `BumpSound`
  (`FPLBumpResult` is just `bool BumpOccured` + `string BumpSound`, no vectors/normals/positions).
- For diagonal/skipped-tile moves, it raycasts (`CH.GetLineToTileCollision`) and tries X-then-Y / Y-then-X
  axis-zero sliding (FPMapLogic.cs:90-131).
- **Bump sound throttle**: `PlayBumpSound` (FPMapLogic.cs:142-150) ignores calls within `0.6 s` of the
  last bump; when it does play, it `StopPlain2D`s the previous bump sound then `PlayRandom`s a variant of
  the terrain's `BumpSound` (the terrain default is `"default bump.wav"`, `FPTerrain.cs:11`). Dedup is
  **time-only**, not per-direction/per-tile. There is no speech on bump, ever — purely a sound cue.

### Footsteps (`PlayStepSound`, FPMapLogic.cs:152-167)

- Interval between steps = `BaseStepSoundSpeed / Velocity.LengthFast` (default `BaseStepSoundSpeed = 1f`,
  FPClientWorld.cs:77) → **faster movement = more frequent steps** (inverse relationship). Uses
  `LengthFast` (fast inverse-sqrt approximation), not `Length`.
- "Running" footstep variant chosen when `Velocity.LengthFast > 2f`. **Because real speed is a
  near-constant ≈3.5 tiles/s** (see the speed note above), that threshold is essentially **always** met —
  the "running" variant plays almost all the time and the step interval sits around `1 / 3.5 ≈ 0.29 s`;
  there is no distinct "walking" cadence.
- Sound comes from the current tile's `Terrain.StepSound` (per-terrain footsteps).

### Other passive world cues (not footsteps/bumps)

- **Terrain-change one-shots**: whenever the tile's `Terrain.IndexedName` changes under a moving entity,
  the leaving tile's `ExitSound` and the entered tile's `EnterSound` play (`FPMapLogic.cs:199-211`) — for
  the player and for NPCs alike (NPC path following routes through the same call, `SeekPositionCommand.cs:58-68`).
- **Indoor/outdoor ambient beds**: on a change of the tile's `Terrain.InDoors` flag, the indoor and
  outdoor ambient loops crossfade via `PlaySmoothRepeating` / `StopSmoothRepeating`
  (`FPMapLogic.cs:185-197`). This is driven by the terrain flag, not by Zone (Doc 06 §3).

---

## 3. Turning

`FPExploring.HandleMouseTurning` blends mouse and key turning into a continuous heading delta:

```csharp
float m = mWorld.RawInput.MouseXChange / 8f;     // mouse sensitivity: raw delta / 8
float k = 0f;
if (input.IsHeld(FPTurnLeft))  k += -1f;          // key turn: ±1 degree/frame, accumulative
if (input.IsHeld(FPTurnRight)) k += +1f;
mWorld.LastFacing = mWorld.Facing;
mWorld.Facing += m + k;
ClampPlayerDirectionBetween0And360();
PlayCardinalDirectionSoundIfCrossed();
FSO.FPML.UpdateFrontVector(mWorld);               // recompute Front immediately
mTurnedWithMouseThisLoop = (m != 0f || k != 0f);
if (mTurnedWithMouseThisLoop) mWorld.SC.ResetAllButFrontRadar();  // clear stale open/close history
```

> **Radar interaction:** any intentional turn calls `ResetAllButFrontRadar()` (mouse/key,
> `FPExploring.cs:365`) or `ResetRadar()` (snaps/teleports) so the open/closed transition logic
> (Doc 02 §4.4) doesn't fire from the heading change itself — **but only for the two side beams.**
> `ResetAllButFrontRadar` deliberately spares the Front beam (`ReactiveRadar.cs:113-123`), and with the
> player stationary the role gate compares against a NaN travel heading and therefore does *not* suppress
> the open/closed path, so the front beam keeps sweeping and firing: a simulated 360° turn in place
> produced ~7 cues even in a featureless corridor. In effect a **rotational edge scan** riding an
> accidental NaN path — see Doc 02 §4.7; a port must choose explicitly: silent turns (reset **all three**
> beams, the default) or a deliberate turn-scan mode (never via NaN).

> **Turning is frame-rate dependent (unlike movement).** The heading delta `MouseXChange/8 °` and the
> `±1 °` key step are applied **once per `Process` loop with no delta-time scaling** (`FPExploring.cs:345-358`),
> so faster frame rates turn faster. Movement, by contrast, **is** time-scaled (`change = Velocity * dt`,
> `FPMapLogic.cs:281`). Turning is continuous while the key/mouse is held, with **no acceleration ramp**.
> When a turn crosses a quadrant boundary (or an exact cardinal) it fires a cardinal sound via
> `PlayCardinalDirectionSoundIfCrossed` (`FPExploring.cs:370-389`, gated on
> `EnableCardinalDirectionAnnouncementsForMouseTurns`). Port with a fixed per-second turn rate scaled by
> `dt` to remove the frame-rate coupling.

### Snapping to cardinals

`SnapRight()` / `SnapLeft()` (FPExploring.cs:775-825) zero `LastTravel`, jump `Facing` to the
next/previous cardinal (`0/90/180/270`), update Front, call `AnnounceDirection(suppressTTS:true)`, and
**`ResetRadar()`**. Boundaries are biased so a snap always moves you to the *next* cardinal in that
rotational direction. Bound to `FPSnapLeft` / `FPSnapRight`.

> **No speech on snap.** `AnnounceDirection` never speaks (see §4) — it only plays positional N/S/E/W
> cues, so "announce the new direction" here means a sound cue, not a spoken direction name. The
> `suppressTTS` parameter it's called with is unused dead code; there is no TTS branch anywhere in
> `AnnounceDirection` to suppress (FPExploring.cs:827-863).

---

## 4. Cardinal-direction audio cues — `AnnounceDirection`

This plays up to four short **positional** cues (north/south/east/west sound emitters placed 1 unit
from the player) for whichever cardinals your heading is within `67°` of. It's the "which way am I
roughly facing" feedback, distinct from the radar. **`AnnounceDirection` never speaks** — despite taking
a `suppressTTS` parameter, there is no TTS/speech branch anywhere in the method (FPExploring.cs:827-863);
the parameter is dead code and every caller (turn-crossing, snap) gets sound only, no spoken direction
name.

```csharp
ClampPlayerDirectionBetween0And360();
float f = mWorld.Facing;
float dN = (f > 270f) ? (360f - f) : f;     // angular distance to North(0)
float dE = Math.Abs(90f  - f);
float dS = Math.Abs(180f - f);
float dW = Math.Abs(270f - f);
int thresh = 67;
if (!NoSoundsOrMusic && SC.RadarEnabled) {
    if (dN < thresh) { mNorthSE.Update((North + Position).AsV3()); mNorthSE.Play(); }
    if (dS < thresh) { mSouthSE.Update((South + Position).AsV3()); mSouthSE.Play(); }
    if (dW < thresh) { mWestSE .Update((West  + Position).AsV3()); mWestSE .Play(); }
    if (dE < thresh) { mEastSE .Update((East  + Position).AsV3()); mEastSE .Play(); }
}
// cardinal vectors (Y-down): North(0,-1) South(0,1) East(1,0) West(-1,0)
```

The threshold is the **integer `67`** (`FPExploring.cs:839`) — note it is *not* the `67.5°`
`RoughDirection` bin edge (§4 "Direction binning"), just a nearby round number. Because `67 > 45`, near
an intercardinal you'll hear **two** cardinal cues (e.g. facing NE plays both North and East), which
encodes the diagonal. The whole block is gated on `!NoSoundsOrMusic && SC.RadarEnabled`
(`FPExploring.cs:840`). Mouse-turn crossings additionally trigger
`PlayCardinalDirectionSoundIfCrossed()` which uses `DirectionHelper.GetQuadrantDirectionEnum` to fire a
cue only when you cross between quadrants/exact cardinals (avoids continuous spam while sweeping).

### Direction binning (`DirectionHelper`)

Two granularities (both consume a compass angle in degrees):

- **`GetQuadrantDirectionEnum`** → `ExactNorth/East/South/West` (at 0/90/180/270 precisely) or the four
  quadrants in between. Used for "did I cross a cardinal while turning".
- **`GetRoughDirectionEnum`** → 8-point compass (N, NE, E, SE, S, SW, W, NW), each a 45°-wide bin
  centered on the compass point (N = 337.5–22.5°, NE = 22.5–67.5°, …). Used for beacon/"which way"
  readouts (Doc 07).

---

## 5. Map overview / look-around mode (`FPMapOverview`)

A non-moving "review cursor" the player drives over the grid to survey surroundings without walking.

- Enter at the player's current tile; announce it (`AnnounceTile`, `FPMapOverview.cs:27-33`).
- Arrow movement steps the review cursor **one tile at a time — that is the *only* motion**
  (`HandleMoveReviewWithinWalls`, `FPMapOverview.cs:49-75`). The jump-by-10 / jump-to-edge machinery in
  the base class (`FPMapGridBase.cs`) is **unused here**, and there is **no** jump-to-POI / jump-to-beacon /
  go-to feature.
- Blocked tiles (`!Terrain.CanPass`) stop the cursor, play the terrain `BumpSound`, and speak the
  terrain's `FriendlyName` (`FPMapOverview.cs:86-91`); map edges speak "That is the edge of the map."
  (`:80-84`).
- On each successful step it sonifies space: staggered "Open space" / "Closed space" side cues (open =
  the side gap **widened**, closed = **narrowed**) plus a center `Wall Approach 3/2/1` cue for the wall
  directly ahead in the move direction.

> **Full breakdown moved to [Doc 07 §4](07-beacons-and-pathfinding.md).** The exact pitch/pan/timing of
> the side and center cues, and the per-tile speech string, are documented there alongside the beacon
> navigation aids (both are "navigation guidance" surfaces) — this is the summary.

---

## 6. Full input map (`FPInput` enum, registered in `FPKeys`)

```
FPAnnouncePosition  FPAnnounceRegion  FPAnnounceDirection
FPSnapLeft  FPSnapRight
FPMoveForward FPMoveBackward FPMoveLeft FPMoveRight
FPTurnLeft FPTurnRight
FPScanForward FPScanLeft FPScanRight
OverheadMap (look-around)  BeaconMenu AnnounceBeacon DisableBeacon
ToggleRadarEnabled  ToggleAutoForwardScanEnabled  ToggleViewMode
FPInteract  FPExamine   SaveGame LoadGame  …
```

Registered like `AddInput("fp_move_forward", FPInput.FPMoveForward)` — string action ids map to enum
members, so rebinding is data-driven.

The two "announce my state" keys speak a fixed format (both handled in `HandleKeys`):

| Key | Output | Source |
|-----|--------|--------|
| `FPAnnounceRegion` | `TSay(mCurrentRegion.FriendlyName)` — the current region name, localized | `FPExploring.cs:605` |
| `FPAnnouncePosition` | `Say("{floor(X)}, {floor(Y)}")` — the integer tile coordinates | `FPExploring.cs:609` |

`FPAnnounceDirection` speaks **nothing** — it only plays the positional cardinal cues (§4).

---

## 7. Region enter/leave (speech) — `HandleRegion`

Checked every `Process()`; **regions are a navigation concept (named areas), distinct from audio
"zones"** (reverb volumes, Doc 06).

### Per-frame order inside `FPExploring.Process` (`FPExploring.cs:149-167`)

The whole loop runs in this fixed order every frame; announcement sequencing (below) depends on it:

1. `HandleMouseTurning` (mouse/key turn + cardinal-cross cue)
2. capture the current player tile
3. `HandleKeys` (all key actions + movement `MoveUnitBasedOnTravelVector` / `AfterUpdatePosition`)
4. `RunProcessAreaTriggers`
5. `HandleActiveBeacon`
6. `HandleRegion` — returns `announcedRegion`
7. `HandlePortalAnnouncement`
8. `RunForwardComparison(announcedRegion)` (auto forward scan, Doc 05 §2)
9. `RunSideComparison` ×2 (left, then right — audio cue only, Doc 05)

```csharp
int x = floor(Position.X), y = floor(Position.Y);
FPRegion region = map.Tiles.Get(x,y).Region;
if (region != mCurrentRegion) {
    mWorld.Speech.CancelAllSpeech();
    if (region.IsNone() && mCurrentRegion != null) TranslateSO.TSay("leaving {0}",  mCurrentRegion.FriendlyName);
    else                                           TranslateSO.TSay("entering {0}", region.FriendlyName);
    mCurrentRegion = region;
    return true;   // signals "region changed" -> forces a fresh forward POI scan this frame
}
```

- **`CancelAllSpeech` first (`FPExploring.cs:179`) → region announcements INTERRUPT.** The newest area is
  what matters, so any queued speech is dropped before "entering/leaving" is spoken.
- **"leaving X" fires only into *unnamed* space** (`:180-183`): the branch requires the *new* region to
  be `IsNone()`. A named region A → named region B transition therefore says **only "entering B"**, never
  "leaving A" (see Doc 06 §1).
- **The `return true` bypasses the auto-scan throttle.** `RunForwardComparison` skips its `0.1 s` gate
  whenever `announcedRegion` is set (`FPExploring.cs:393`), so the new area is re-scanned and described
  the same frame it's entered (Doc 05 §2).

Portals (`GetPortal` / `HandlePortalAnnouncement`, `FPExploring.cs:193-217`): stepping onto a portal
rect speaks the exact string **`"Press the enter key to enter {0}"`** and plays a `PlayPlain2D("Portal")`
cue (`:201-202`). This announcement is **queued, not interrupting** (no `CancelAllSpeech`). On `FPInteract`
over a portal, the game switches `IClientMap` and snaps the player to the destination tile *center*
`(DestX + 0.5, DestY + 0.5)` (`:701`); if the portal carries a `DestFacing`, it sets facing and calls
`AnnounceDirection()` (`:702-706`) — which is **sound only, no spoken direction name** (§4).

---

## 8. Porting checklist

- Maintain your own `position (Vector2 tile-units)`, `headingDegrees (compass)`, `velocity`.
- On any heading change: recompute `Front`, clamp to `[0,360)`, and **reset the radar's open/close
  history**.
- Movement: scale by `dt`, run a collision check that can zero one axis (slide), throttle bump sounds.
- Cardinal cues: the `67°` window with positional N/S/E/W emitters is a cheap, effective "facing"
  readout; or fall back to speaking the 8-point bin from `GetRoughDirectionEnum`.
- Add a "review cursor" mode (overview) if your game has a surveyable space; it reuses the radar logic.
