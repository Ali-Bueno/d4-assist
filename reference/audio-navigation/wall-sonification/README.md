# Wall Sonification — Continuous Directional Wall Tones

The **third navigation style**. Instead of event cues fired as you move (reactive radar) or an on-demand
sweep (polar scan), wall sonification keeps a **continuous ambient "audio map"**: one always-playing
tone per direction whose **volume tracks how close the nearest wall is on that side**. An open
direction stays silent, so openings reveal themselves by their absence of sound. It gives the player a
constant, passive sense of "the box I'm standing in" without pressing anything.

> **"Wall" is shorthand for *anything the player cannot pass through*.** The tones must sonify every
> movement-blocking obstacle, not just literal walls: impassable rocks, trees the player can't walk
> through, fences, ore veins, big roots, **invisible walls** / out-of-bounds barriers, etc. The
> detection test is *"does this block movement?"* (blocking tile set / collision layer mask), never
> *"is this named or rendered as a wall?"* — invisible barriers especially have no visual mesh but
> must still sound. Core Keeper's blocking set (§4) is an example: it includes ore, roots and crystals
> alongside walls.

> Synthesized from **our own mods** (source available on disk), not a decompile:
> - **Core Keeper** — top-down grid, Unity **Mono**, BepInEx 6, Unity `AudioSource` + procedural wind clips.
>   `…/Core Keeper Access/ckAccess/Patches/Player/WallSonarPatch.cs`
> - **Deep Rock Galactic** — 3D first-person voxel caves, Unity **IL2CPP**, BepInEx 6, **NAudio** synth.
>   `…/drg access/drgAccess/Components/WallNavigationAudio.cs`
> - **Megabonk** — 3D action, Unity **IL2CPP**, BepInEx 6, **NAudio** synth (+ collision & vertical cues).
>   `…/megabonk access/MegabonkAccess/Components/WallNavigationAudio.cs`

---

## 1. The model (identical across all three mods)

```
Per direction d in {Forward/N, Back/S, Left/W, Right/E}:
    one CONTINUOUS looping tone channel, created once, NEVER stopped.
    channel d has a FIXED frequency and a FIXED stereo pan.
Every tick:
    hit = cast a ray / step the grid in direction d, up to maxRange
    if hit within range:  channel.volume = baseVol * falloff(hit.distance)   // closer = louder
    else:                 channel.volume = 0                                  // open = silent
    (volume changes are SMOOTHED so there are no clicks)
```

Four design invariants that make it work — copy all four:

1. **Continuous, not one-shot.** Channels loop forever; silence = volume 0. No `PlayOneShot`, no
   start/stop clicks, no scheduling. Volume is the only thing that moves.
2. **One distinct frequency per direction**, so the player identifies direction *by pitch* and doesn't
   have to rely on panning alone (panning is unreliable, especially front/back). Convention that tested
   well: **Forward = high, Back = low, Sides = mid**.
3. **Fixed pan per channel**: sides hard-panned (`Left = -1`, `Right = +1`), Front/Back centered (`0`).
   Pitch disambiguates front from back; pan disambiguates left from right.
4. **Smooth every change.** Lerp volume toward its target each frame (or smooth per audio sample) so a
   wall sliding past doesn't crackle.

> **Why this is "always-on" and quiet:** the bed sits at a low base volume (0.12–0.22) so it can play
> constantly without fatigue, under any speech/event cues. It answers *"what's around me right now"*;
> the radar styles answer *"what just changed"* / *"what's down this hallway"*.

---

## 2. The three implementations side by side

| Aspect | Core Keeper (grid, Mono) | Deep Rock Galactic (3D, IL2CPP) | Megabonk (3D, IL2CPP) |
|--------|--------------------------|----------------------------------|------------------------|
| Direction frame | **World-fixed** N/S/E/W (top-down) | **Camera-relative** F/B/L/R | Camera-relative F/B/L/R |
| Detection | Grid step scan, tile-type test | `Physics.Raycast` ×4 from waist (`+1.0` up) | `Physics.Raycast` ×4 from waist |
| Range | `MAX_RANGE = 6` tiles | `5–25 m`, default **12 m** | **12 m** |
| Falloff | **linear** `1 − (d−1)/6` | **quadratic** `1 − norm²` | quadratic `1 − norm²` |
| Frequencies | sides **A3 220**, ahead **G4 392**, behind **D3 147** Hz | fwd **500**, back **180**, sides **300** Hz | fwd 500, back 180, sides 300 Hz |
| Pan | sides ±1, F/B center | sides ±1, F/B center (NAudio `PanningSampleProvider`) | sides ±1, F/B center |
| Base volume | per-dir `0.20 / 0.13 / 0.22` | `0.12`, min-vol dist `0.5 m` | `0.15`, min-vol dist `0.5 m` |
| Audio backend | Unity `AudioSource` ×4 (looping) + **procedural wind clip** (band-pass noise + tone) | **NAudio** `WaveOutEvent` + `ISampleProvider` (70% triangle + 30% sine) | NAudio, same 70/30 synth |
| Scan rate | tile scan **10 Hz**, volume lerp every frame (`MoveTowards`, speed 6/s) | every frame, per-sample smoothing (`freq 0.05`, `vol 0.02`) | every frame, per-sample smoothing |
| Extra info in tone | — | **frequency ×1.6 if indestructible** (diggable vs solid) | — |
| Companion cues | proximity audio for objects / enemies / ore (separate patches) | hazard warning (separate) | **collision thump** + **vertical-space cues** (gap/tunnel/platform/drop) |
| Enable | always-on, no key; watchdog silences off-gameplay | config file + keybind (F1/Y), 0–100% slider, 5–25 m range | runtime setters; silenced in menus |
| Coupling | tile-type list is CK-specific; synth + channels reusable | `MineableBlock` check + game-state enum specific; synth reusable | menu trackers specific; synth reusable |

**Convergent core** (the part to lift verbatim): 4 continuous channels, freq-per-direction, pan
sides ±1, proximity→volume with smoothing, silence when open, gate by game state.

**Divergences that are deliberate choices, not accidents:**
- **World-fixed vs camera-relative directions.** Top-down grid games (Core Keeper) want *world* N/S/E/W
  so "wall to the east" always sounds east regardless of facing. First/third-person 3D games (DRG,
  Megabonk) want *camera-relative* F/B/L/R so "ahead" means where you're looking. Pick by camera model.
- **Linear vs quadratic falloff.** Quadratic ("`1 − norm²`") concentrates loudness near the wall and is
  generally the better feel in continuous 3D; linear is fine on a coarse tile grid. Default to quadratic.
- **Encoding tile type in pitch** (DRG's destructible ×1.6) is the cheapest way to add a second data
  dimension to the same channel — reuse the idea for any binary tile distinction (wall vs door, etc.).

---

## 3. Audio synthesis — no sound files needed

All three generate the tones procedurally; none ship wall WAVs.

- **Unity Mono (Core Keeper):** four looping `AudioSource`s on a `DontDestroyOnLoad` GameObject, each
  fed a procedurally built `AudioClip` — resonant band-pass-filtered white noise + a pure tone + slow
  "gust" amplitude modulation, with a crossfaded seam for a clickless loop. Sounds like *airy wind at a
  pitch* rather than a synthetic beep. Volume driven by `src.volume`; pan by `src.panStereo`.
- **Unity IL2CPP (DRG, Megabonk):** Unity's audio path is awkward under IL2CPP, so they run **NAudio**
  directly: a custom `ISampleProvider` (`SineWaveGenerator`) generating **70% triangle + 30% sine**
  (softer than pure sine, more present than pure triangle), → `PanningSampleProvider` → mixer →
  `WaveOutEvent`. Frequency and volume are smoothed *per sample* (`+= (target−current)*k`) so changes
  are seamless. One output device, all channels mixed.

> **Porting rule:** Mono games → Unity `AudioSource` + a generated clip (or 3 pre-pitched WAVs) is
> simplest. IL2CPP or engines where the audio API is hostile → **NAudio with your own output device**
> sidesteps the engine entirely (this is the proven path for both IL2CPP mods).

---

## 4. Detection — the only engine-specific part

- **Grid game:** step tile-by-tile in each direction from the player tile; the first tile whose type is
  in your "blocking" set is the wall; distance = step count. The blocking set = **every tile type the
  player cannot walk onto** — walls, impassable rocks, trees, ore, roots — not just wall-named tiles.
  (Core Keeper blocking set: `wall`, `greatWall`, `thinWall`, `bigRoot`, `ore`, `ancientCrystal`.)
- **3D game:** `Physics.Raycast(origin, dir, out hit, maxDist, layerMask)` once per direction. Origin =
  player position **+ ~1 m up** (waist height — avoids floor/ceiling bias). Direction = camera forward
  projected to the horizontal plane, plus its perpendicular for left/right:
  ```csharp
  Vector3 fwd = cam.forward; fwd.y = 0; fwd.Normalize();
  Vector3 right = new Vector3(fwd.z, 0, -fwd.x);   // perpendicular on the ground plane
  // directions: fwd, -fwd, -right, right
  ```
  Use a layer mask covering **all movement-blocking colliders** (walls, terrain, rocks, trees,
  invisible/out-of-bounds barriers) — the same layers the player's own collider collides with, not
  just a "walls" layer. Classify the hit (e.g. `GetComponentInParent<…>()`) only if you want to encode
  type in pitch.

Everything downstream (channel volume, pan, smoothing, gating) is engine-free.

---

## 5. Performance & gating (all three follow this)

- **Cache the player; don't `FindObjectOfType` per frame.** Re-search every ~2 s only.
- **Cheap rays:** exactly one ray/scan per direction (4 total), not a fan. Optionally throttle the scan
  to ~10 Hz (Core Keeper) while still lerping volume every frame.
- **Gate by game state:** silence all channels (volume 0) when paused / in menus / loading / not in
  gameplay / on a dead or remote player. A watchdog or per-frame state check enforces it.
- **Lock** around the channel collection if the audio runs on its own thread (NAudio does).
- **Throttle logs** (every N frames), never log per frame.

---

## 6. Optional companions (seen in these mods, reuse as needed)

- **Collision thump** (Megabonk): raycast in the *movement* direction; when within ~1.5 u and actually
  moving, play a short descending square-wave "thump" (100→50 Hz, 60 ms, 0.3 s cooldown). Tells you when
  you *walk into* a wall, distinct from the proximity bed.
- **Vertical-space cues** (Megabonk): extra raycasts at different heights detect **gap** (duck-under-clear),
  **tunnel** (crouch), **platform** (climbable, normal.y > 0.7, 0.5–2.5 m up), **drop** (no ground 2.5 m
  ahead / >3 m fall) — each a distinct short sweep with a multi-second cooldown. Essential for 3D
  platforming; irrelevant on a flat grid.
- **Object/enemy/resource proximity** (Core Keeper): same proximity→pitch/pan idea but as *intermittent*
  positioned one-shots keyed off footsteps, with different base frequencies per category (e.g. ore 360 Hz
  vs interactables 600 Hz) so types are told apart by ear. This is a separate layer from the wall bed.

---

## 7. Porting checklist (engine adapter surface)

Expose to the engine-free core:

```csharp
Vector3 PlayerPosition { get; }
Vector3 CameraForwardOnPlane { get; }        // or, for grid games, just use world N/S/E/W
bool RaycastWall(Vector3 origin, Vector3 dir, float maxDist, out Hit hit);  // or grid step-scan
bool IsInActiveGameplay();                    // for gating
// audio primitive: a continuous tone channel you can set { frequency, volume, pan } on, smoothly
```

Then copy verbatim: the 4-channel setup, freq/pan-per-direction table, `proximity→volume` mapping with
smoothing, the "open = silent" rule, and state gating. Tune `maxRange`, `baseVolume`, and the three
frequencies to the game's scale by ear.

See [`../README.md`](../README.md) for **when to use this style alone vs. combined** with the reactive
radar (A Hero's Call) and polar scan (Swamp) styles.
