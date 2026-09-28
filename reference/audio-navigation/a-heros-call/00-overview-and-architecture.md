# A Hero's Call — Audio Navigation System: Technical Reference

> Reverse-engineered from the decompiled C# source of **A Hero's Call** (Out of Sight Games,
> MonoGame / .NET Framework 4.0). Source recovered to `D:\code\c#\a-heros-call\decompiled\src`.
> This document set is written so the techniques can be re-implemented in accessibility mods for
> arbitrary engines (Street Fighter 6 / RE Engine, 7 Days to Die / Unity, Xenoverse 2 / native,
> Resident Evil 7 / RE Engine).

## Document index

| File | Subsystem |
|------|-----------|
| `00-overview-and-architecture.md` | This file — big picture, coordinate system, frame loop, porting strategy |
| `01-coordinate-system-and-math.md` | V2/V3, compass-angle math, the 8 direction vectors, `Line` geometry |
| `02-reactive-radar.md` | The wall/open-space "reactive radar" (the centerpiece) |
| `03-raycasting-and-collision.md` | Grid raycaster (`GetLineToTileCollision`), wall/door occlusion depth |
| `04-navigation-and-movement.md` | First-person movement, turning, snapping, cardinal announcements |
| `05-scanning-pois-and-interactables.md` | Forward/side POI scan, examine, interact, focus |
| `06-zones-regions-and-announcements.md` | Region enter/leave announcements, zone reverb |
| `07-beacons-and-pathfinding.md` | Beacon menu, A*-guided "advanced beacon", direction/distance readout |
| `08-spatial-audio-fmod.md` | FMOD listener, stereo panning, distance attenuation, obstruction filtering |
| `09-speech-output-layer.md` | SpeechContext, Tolk/SAPI/PRISM routing, TranslateSO |
| `10-porting-guide.md` | Concrete mapping of every technique to SF6 / 7DTD / XV2 / RE7 |
| `11-ancillary-systems.md` | OS-level accessibility (FilterKeys), NPC navigation/audio behavior, scripting API surface, and other systems adjacent to navigation |

---

## 1. What the game actually is (mechanically)

A Hero's Call is a **grid-based first-person dungeon RPG** played entirely by ear. The player
occupies a continuous 2D position on a tile grid and faces a continuous compass heading. There is
**no third dimension for navigation** — "first person" here means a 2D top-down world presented from
the player's heading. All spatial information reaches the player through three channels:

1. **The reactive radar** — short looping cues that tell you, *as you move*, whether walls are
   closing in or opening up to your front/left/right, plus a rising pitch when a wall is within 3
   tiles ahead. (Doc 02.)
2. **Positional audio** — every object, terrain tile, and ambient emitter is a 3D FMOD sound, panned
   and attenuated relative to the player's position and heading, and **muffled/muted through walls
   and doors**. (Doc 08.)
3. **Screen-reader / TTS speech** — discrete announcements: region names on crossing boundaries,
   the result of a directional "scan", examine/interact text, beacon distance+direction. (Docs 05–09.)

The genius of the design — and the reason it ports so well — is that **the world model is trivially
simple (a tile grid with per-tile flags) and every cue is derived by raycasting that grid**. If you
can produce, in your target game, (a) the player's position+heading and (b) a way to ask "what is at
world point P / along ray R", you can reproduce the entire system.

> **Two independent ray systems, not one.** The radar, audio occlusion, beacon line-of-sight and
> object/script rays all use a grid **DDA** (`CollisionHelperCopy`/`CH`, Doc 03). The forward/side
> **POI scans** — the primary spoken "what's ahead" readout — use a completely separate fixed
> **0.1-tile point-march** (`POIHelpers.GetLineCollisionWithPointsOfInterest`, Doc 05). Both need
> porting; don't assume one implementation covers both (`POIHelpers.cs:12-84` vs
> `Sound/Ian/CollisionHelperCopy.cs:43`).

---

## 2. The five assemblies that matter

| Assembly | Namespace | Role for navigation |
|----------|-----------|---------------------|
| `Core` | `Ian` | `V2`, `V3`, `Line` math; `MyMath`, `VectorHelpers`, `VectorExtensions`; `DiagSO` base |
| `Sound` | `Ian` | `ReactiveRadar`, `CollisionHelperCopy` (raycaster), `ObstructionHelpers`, `MapSoundHandler` (per-frame audio driver), the `I2D/I3DSoundSource` + `IAudible*` interfaces |
| `FirstPerson` | `Ian` | `FPExploring` (the gameplay state / input loop), `FPMapLogic` (movement+collision), `POIHelpers` (scan), `DirectionHelper` (compass binning), `FPBeaconMenu`, `FPMapOverview` |
| `GameEngine` | `Ian` | `SpeechContext` (TTS/Tolk), `GameConfig` (tunables) |
| `Translate` | `Ian` | `TranslateSO` / `TranslateAPI` — localized speech strings (`TSay`) |

---

## 3. The coordinate system (read this before anything else)

This single section prevents 90% of porting bugs.

- Positions are `V2` (`float X, float Y`). The world is a tile grid; **tile (i,j) spans `[i, i+1) × [j, j+1)`** and its **center is `(i+0.5, j+0.5)`**. Integer player tile = `(floor(X), floor(Y))`.
- **`+X` is East, `+Y` is South.** Y increases *downward* (screen/tile convention), not upward. This is why the cardinal vectors are `North=(0,-1)`, `South=(0,1)`, `East=(1,0)`, `West=(-1,0)` (see `FPExploring`).
- **Heading is a compass angle in degrees, clockwise, 0° = North.** `0=N, 90=E, 180=S, 270=W`.
- Heading → unit vector (the authoritative conversion, `MyMath.GetUnitVectorFromCompassDegrees`):

  ```csharp
  // degrees: compass, 0=N, clockwise. Returns a unit V2 in (+X=East, +Y=South) space.
  float r = degrees * PI/180;
  V2 front = new V2( sin(r), -cos(r) ).Normalized();
  //   0° -> ( 0, -1) North      90° -> ( 1, 0) East
  // 180° -> ( 0,  1) South     270° -> (-1, 0) West
  ```

- The perpendiculars (from `V2`): `PerpendicularRight = (Y, -X)`, `PerpendicularLeft = (-Y, X)`.
  In this Y-down frame, **`Left = Front.PerpendicularRight`** and **`Right = Front.PerpendicularLeft`**
  (see `MapAndPlayer`). Do not "fix" this to look right-handed; it is internally consistent with Y-down.

Full detail and the inverse (vector → compass angle) are in Doc 01.

---

## 4. The per-frame data flow

Everything spatial is driven once per frame from **`MapSoundHandler.UpdateMapSoundss(...)`**
(`Sound/Ian/MapSoundHandler.cs:208`). The gameplay state `FPExploring.Process()` updates the player
position/heading and calls into this. The essential pipeline:

```
FPExploring.Process()                       // FirstPerson: input, movement, region check, scans
  └─ player Position (V2), Facing (deg), Velocity (V2) updated
MapSoundHandler.UpdateMapSoundss(map, actors, playerPos, facingDeg, velocity, inMenu)
  ├─ build MapAndPlayer 'ent'               // bundles pos, Front/Left/Right…, map, velocity, inMenu
  ├─ set FMOD 3D listener (pos + forward + up)
  ├─ mRadar.RunRadar(ent)                   // <-- the reactive radar (Doc 02)
  ├─ for each positioned/terrain/object sound:
  │     ObstructionHelpers.PositionSoundForObstruction(ent, srcPos, sound, …)
  │       └─ depth = CollisionHelperCopy.GetWallAndDoorDepth(map, listener, src)   // raycast (Doc 03)
  │       └─ mute if depth>=1 ; set obstruction/indoor status ; sound.Update(srcPos)
  └─ zone change? -> swap FMOD reverb snapshot (Doc 06)
```

Independently, **`FPExploring`** handles the discrete/speech side each frame:
`HandleRegion()` (enter/leave announcements), `RunForwardComparison()` (auto forward POI scan),
side-radar comparisons, beacons, and key handling for manual scans / examine / interact.

---

## 5. The master list of tunable constants

Pulled from the actual source. Use these as sane defaults when porting.

| Constant | Value | Where | Meaning |
|----------|-------|-------|---------|
| Radar tick delay | `0.1 s` | `delay` const, inlined by the compiler to the literal at `ReactiveRadar.cs:156` (Doc 02 §4.3) | Min real-time gap between radar cues (~10 Hz max, **all beams combined** — one global cooldown, Doc 02 §4.2) |
| Radar pitch reach | `3` tiles | `PitchRadarReach` const, inlined to the `< 3` literal at `ReactiveRadar.cs:187` | Distance under which an approaching wall pitches |
| Radar open/close reach | `1000` | `OpenCloseRadarReach` const, inlined to the `1000f` literal at `ReactiveRadar.cs:165` | Max length of the **single** radar ray; it feeds *both* pitch and open/close — there is no separate open/close ray (Doc 02 §4.3) |
| Float epsilon | `1e-5` | `CoreExtensionMethods.AlmostEquals` | Vector/heading equality tolerance |
| Obstruction mute threshold | `depth >= 1.0` | `ObstructionHelpers` | Wall+door thickness (tiles) that fully mutes a source |
| Cardinal announce threshold | `67°` | `FPExploring.AnnounceDirection` | Play a cardinal cue when heading within 67° of it |
| Turn rate | mouse `÷8`, keys `±1°` | `FPExploring` turning (`:355-366`) | Degrees **per loop, frame-rate-dependent (NOT delta-scaled)**: mouse-X delta ÷ 8, held turn keys ±1°/loop. Contrast movement, which *is* time-scaled |
| Snap targets | `0/90/180/270°` | `FPExploring.SnapLeft/Right` | Snap heading to nearest cardinal |
| Forward scan rate | `0.1 s` | `FPExploring.RunForwardComparison` | Auto forward-scan recompare interval |
| Step ray increment | `0.1` tiles | `POIHelpers` | Raycast granularity for POI scans |
| Auto-focus distance | `2.5` tiles | `POIHelpers` | Object within this range becomes "focused" (interactable) |
| Interact/examine range | `5` tiles | `FPExploring.AttemptInteract/Examine` | Max scan length for the action |
| Default scan distance | `GameConfig.ScanDistance` | `GameConfig` | Forward/side scan ray length |
| Bump-sound throttle | `0.6 s` | `FPMapLogic.PlayBumpSound` | Min interval between wall-bump sounds |
| Run footstep threshold | `velocity.Length > 2` | `FPMapLogic.PlayStepSound` | Above this speed, use "running" footstep |
| Effective travel speed | ≈`3.5` tiles/s (**constant**) | `1.4` base × unconditional `2.5` (`RPGExploring.cs:43-51`) × terrain `MovementSpeedAddPercent` | **No walk/run gear**: `speedMod *= 2.5f` is applied *always*; the run key only adds an extra Front-vector bias. This is the speed feeding every radar timing intuition |
| Advanced-beacon interval | `2.0 s` | `FPExploring` advanced beacon | Min time between beacon path beeps |
| Advanced-beacon near switch | `< 5` tiles | `FPExploring` advanced beacon | Switch to simple beacon when this close |
| Advanced-beacon path lookahead | `10` tiles | `FPExploring` advanced beacon | Max path tiles scanned for an unobstructed beep source |

---

## 6. Porting strategy in one paragraph

Build an **engine adapter** exposing exactly: `PlayerPosition (Vector2 in tile units)`,
`HeadingDegrees (compass)`, and `Tile QueryTile(int x,int y)` / `RaycastFirstBlocking(origin, dir, maxDist)`.
Behind that adapter, copy the `Ian` math (`V2`, compass conversions), **both ray systems** — the DDA
raycaster (`CollisionHelperCopy`/`CH`) and the POI scan's separate 0.1-tile point-march
(`POIHelpers.GetLineCollisionWithPointsOfInterest`) — and the radar (`ReactiveRadar`) almost verbatim —
they have **no engine dependencies** beyond the adapter and a "play this short sound with this stereo
pan" primitive. For games that are not tile-based (SF6, RE7, XV2 are continuous 3D), you replace
`QueryTile` with a physics raycast and quantize positions to a virtual grid only where the algorithm
needs integers (the pitch-distance rounding). The full mapping is in Doc 10.
