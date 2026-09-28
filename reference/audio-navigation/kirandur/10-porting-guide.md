# 10 — Porting Guide: taking Kirandur's semantic sonar to any game

Standalone guide for porting Kirandur's **continuous semantic sonar** — the fixed **6-axis probe** (4
cardinals + up/down, per-cell classification, elevation sampling) — to another game, **organized by game
type**. The algorithm itself (scan geometry, the 6-ray table, classification, constants, spatialization)
lives in [`sonar-navigation-spec.md`](sonar-navigation-spec.md); this document does **not** restate it —
it tells you *what to build per genre* and *where the seam is*, and points back to the spec for the
mechanism, so the two can't drift.

> **Confidence.** Everything about Kirandur's own behavior is verified against the NativeAOT decompile
> (see the spec's `FUN_xxxx` citations and the constants read from the binary). Everything marked
> **(recommendation)** is engineering judgment for adapting it to other genres/engines — the sonar is a
> clean, small algorithm, so most of the port is *wiring three world queries*, not rewriting logic.

> **"Wall" means *any impassable obstacle*, never just a rendered wall.** Kirandur classifies a blocked
> cell via the same collision the player's movement obeys (`IsBlocked`, spec §"World queries"). On a port
> the rays must hit rocks, fences, furniture, invisible/out-of-bounds barriers — the test is *"does this
> block movement?"*, never *"is it named wall?"*.

---

## 1. What is portable (the layered stack)

Only the **bottom layer is game-specific**. Everything above the adapter is copied almost verbatim.

```
   ┌──────────────────────────────────────────────────────────────┐
   │ OUTPUT   spatial cue sink (HRTF at hit pos) + Q spoken readout │  engine-agnostic
   ├──────────────────────────────────────────────────────────────┤
   │ GATING   0.1 s pulse · diff-gate (type changed) · 2-frame      │  engine-agnostic
   │          hysteresis · movement gate (dot ±1.5)                  │
   ├──────────────────────────────────────────────────────────────┤
   │ CLASSIFY per-ray raymarch → Open/Wall/Door/Portal/Ground/      │  engine-agnostic
   │          HarmlessDrop/HarmfulDrop + SlopeUp/Down (height-based) │
   ├──────────────────────────────────────────────────────────────┤
   │ SENSORS  the fixed 6-axis ray set (4 cardinals + up/down),      │  engine-agnostic
   │          each with baked volume/pan                             │  (written vs the adapter)
   ├──────────────────────────────────────────────────────────────┤
   │ ADAPTER  IsBlocked · SampleHeight · HasDoorInAabb · Listener    │  the ONLY seam
   ├══════════════════════════════════════════════════════════════┤
   │ QUERYABLE WORLD  physics / tilemap / navmesh / heightfield      │  GAME-SPECIFIC
   └──────────────────────────────────────────────────────────────┘
```

If you find yourself re-implementing the raymarch or the gating per game, the seam is in the wrong place —
push it down to the adapter.

---

## 2. The adapter contract

Write **all** sonar logic against this; never against the engine.

| Member | Type | Contract |
|---|---|---|
| `IsBlocked(p)` | `bool` | Does world point `p` block player movement? (same colliders/tiles movement uses). Kirandur: `FUN_1400174e0`. |
| `SampleHeight(p)` | `float` | Ground/terrain elevation at `p` on the up-axis. This is what powers ramps and drops. Kirandur: `FUN_140020380/310`. **Return the walkable surface height, not the collider top.** |
| `HasDoorInAabb(min,max)` | `bool` | Is there a door entity in the box around the hit? Distinguishes Door from Wall. Kirandur: `FUN_1403f7460`. Return `false` if your game has no doors → everything blocked becomes Wall. |
| `ListenerPosition` | `Vec3` | The player/camera position rays originate from. |
| `PlayCue(oggId, worldPos)` | — | Play cue at a 3D world position (positional HRTF). Fallback: stereo pan from the ray's baked `Volume`/`Pan`. |

**Axis convention (read this first).** Kirandur's ground plane is **XY**, up-axis is **Z** (cardinals use
X/Y, the 2 vertical rays use Z). Your engine may differ:

| Engine | Ground plane | Up-axis | Cardinal rays | Vertical rays |
|---|---|---|---|---|
| Kirandur (source) | XY | Z | ±X, ±Y | ±Z |
| Unity | XZ | Y | ±X, ±Z | ±Y |
| MonoGame 3D | XZ | Y | ±X, ±Z | ±Y |
| MonoGame 2D / top-down sprite | XY (screen) | (none / a data layer) | ±X, ±Y | — (see §3B) |

Map the spec's 6 direction vectors into your frame once, in the ray table, and everything downstream is
frame-independent.

---

## 3. Per-game-type recipes

The sonar is a **6-axis cross**: 4 rays in the movement plane + 2 vertical. How much of it you keep, and
what "vertical" means, depends on the genre.

### A. Top-down grid/tile explorer *with* an elevation layer — the native fit
*(Kirandur itself; also fits Core Keeper-like games that add height, roguelikes with pits/ramps.)*
- Keep **all 6 rays** exactly as the spec table.
- `IsBlocked` = tile solidity / collision layer. `SampleHeight` = your height layer (per-tile height,
  heightmap, or a downward raycast). `HasDoorInAabb` = door tag in the tile/AABB.
- The 4 cardinals march horizontally and detect ramps/drops from **height changing along the march**; the
  2 vertical rays give ceiling/floor above-below.
- This is the highest-value target: you get walls, doors, ground, ramps, and safe-vs-dangerous drops for
  free. **(This is the configuration the spec's pseudocode ships.)**

### B. Pure 2D top-down, *no* elevation (most audiogame tile maps, classic top-down sprites)
- **Drop the 2 vertical rays**; keep the **4 cardinals**.
- Stub `SampleHeight` to a constant → slope/drop detection disappears (never fires), which is correct:
  those beam types simply won't occur. Classification collapses to Open/Wall/Door/Portal/Ground.
- You keep the best part for 2D — 360° semantic cues with baked front/back volume and L/R pan — at almost
  zero extra cost over a plain wall radar. **(recommendation)**

### C. Isometric / 2.5D
- Same as **A**, but map the 4 cardinals onto your **iso movement axes** (the screen diagonals usually =
  world N/E/S/W). Keep vertical rays if the game has real height (stacked floors, cliffs). **(recommendation)**

### D. First-person / third-person 3D (RE7-class, FPS, action-adventure)
- Rays should be **relative to player facing**, not world-fixed: rotate the 4 horizontal directions by
  `HeadingDegrees` so ray 1 = forward, 2 = right, 3 = back, 4 = left (the baked volume/pan already assume
  front/back/L/R — keep them). **(recommendation)**
- 4 cardinals is coarse for 3D; **add the 4 diagonals** (8 horizontal rays) for corridor/room legibility.
  Give diagonals interpolated volume/pan.
- `IsBlocked` = `Physics.Raycast`/sphere-cast against the movement collision mask. `SampleHeight` = a
  downward raycast to the floor at `p` (for ledges/steps). The **up ray** finds low ceilings; the **down
  ray** becomes your **ledge/fall detector** — reuse Harmful/HarmlessDrop with a real fall height.
- Use true positional HRTF here (you already have a 3D listener); the stereo fallback is only for 2D. **(recommendation)**

### E. Side-scroller / platformer (2D, gravity matters)
- The movement plane is **XY on screen but vertical is real**. Use **2 horizontal** (left/right) + **2
  vertical** (up/down); ramps/drops and Harmful/Harmless-drop are the *headline* feature here (pits!).
  Map the down ray's drop-depth to real fall damage thresholds. Drop the back/forward distinction.
  **(recommendation)**

> **Rule of thumb.** Keep the 4 cardinals whenever the player moves in a plane; keep the 2 vertical rays
> only if the game has a meaningful up/down (elevation, ledges, ceilings). When in doubt, ship A/full and
> let `SampleHeight` stubbing neutralize what doesn't apply.

---

## 4. Classification wiring per engine

| Query | Unity (Mono/IL2CPP) | MonoGame | Native |
|---|---|---|---|
| `IsBlocked(p)` | `Physics.CheckSphere(p, r, moveMask)` or `Tilemap.HasTile(cell)` | grid cell solid flag | your collision fn |
| `SampleHeight(p)` | downward `Physics.Raycast` / `Terrain.SampleHeight` / tile height field | height layer lookup | terrain fn |
| `HasDoorInAabb` | `Physics.OverlapBox` filtered by door tag/layer | door list in AABB | entity query |

- On **IL2CPP**, cache component lookups; never `FindObjectOfType` per ray/frame (perf rule). Do the world
  queries against cached colliders/tilemaps.
- Keep the raymarch in managed code; only the three queries cross into the engine.

---

## 5. Spatialization

- **Positional HRTF (preferred, 3D games):** call `PlayCue(oggId, hitWorldPos)` and let your spatializer
  (Steam Audio / Resonance / Unity spatial / OpenAL-HRTF) do pan + distance attenuation. Kirandur uses a
  `SpatialPanner` (binaural + direct effects) — spec §"Spatialization".
- **Stereo fallback (2D games, no HRTF):** use each ray's **baked `Volume`/`Pan`** from the ray table
  (North 1.0/center, East 0.98/right +0.6, South 0.8/center, West 0.94/left −0.6, Up 1.1, Down 0.7). That
  table *is* the game's non-binaural presentation — copy it verbatim; scale distance to an extra
  attenuation on top.
- Reuse the **same type→localization-key map** for the Q spoken readout so voice and sound agree (spec §"SpeakSonar").

---

## 6. Tuning constants — scale to your world

All from the binary (spec §"Real constants"); **they are in Kirandur's world units — rescale**:

| Constant | Kirandur | How to scale |
|---|---|---|
| `StepSize` | 1.0 | set to ~1 tile / ~0.5–1 m; keep **~20 steps per ray** |
| `MaxRange` | 20.0 | `StepSize × ~20`; raise for open worlds, lower for tight interiors |
| `MinHeightDelta` | 2.0 | in your **up-axis** units — smallest step/ledge that should register |
| `MinGradient` | 0.2 | Δheight/step; unitless-ish, keep unless steps are much larger |
| `HarmfulDropDepth` | 6.0 | your fall height where damage/death starts |
| `ScanInterval` | 0.1 s | keep (10 Hz); raise if 6+ rays × world queries is heavy |
| `MoveGate ±` | ±1.5 | how far you must move along a ray before it re-chirps; scale with `StepSize` |
| `GroundZThreshold` | −0.5 | `dir.z ≤ −0.5` → Ground; keep (it's a direction-dot, unit-independent) |

Keep the **ratios** (`MaxRange ≈ 20·StepSize`, `HarmfulDropDepth ≈ 3·MinHeightDelta`), not the absolute
numbers.

---

## 7. Pitfalls

- **Wall ≠ rendered wall.** Radar against movement collision, or blind spots appear at invisible barriers
  (see the banner at the top).
- **`SampleHeight` returning collider tops instead of walkable surface** → phantom ramps/drops. Return the
  surface the player actually stands on.
- **World-fixed rays in a 3D game** feel wrong — rotate by facing (§3D). In top-down they should stay
  world-fixed (N is N), which is why Kirandur bakes them world-fixed.
- **Shipping vertical rays where there's no verticality** → they always read Open/Ground and add noise.
  Drop them (§3B) rather than letting them chatter.
- **Per-frame instead of per-pulse** → cue spam. Respect the 0.1 s accumulator + movement gate; that's
  what makes it "breathe as you walk" instead of buzzing.
- **Not scaling the height constants** → on a metric game a 2.0-unit `MinHeightDelta` may be 2 m and never
  fire, or on a fine grid it fires on every pixel. Rescale per §6.

---

## 8. Minimal port checklist

1. Implement the 5-member adapter (§2) against your world; get the axis mapping right.
2. Pick the ray set for your genre (§3): full 6, 4 cardinals, facing-relative 8, or 2+2.
3. Copy the spec's `Sonar.Tick/Emit/Classify` verbatim; only the adapter differs.
4. Wire `PlayCue` to your spatializer, or seed the stereo fallback from the baked volume/pan table.
5. Rescale the constants to your world units (§6), keeping the ratios.
6. Validate with a screen reader: walk a wall, a doorway, a ramp, and a pit; confirm each reads distinctly
   and stops chattering when you stand still.
