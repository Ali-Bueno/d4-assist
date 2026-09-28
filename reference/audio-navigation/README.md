# Audio Navigation — Cross-Game Reference

Engine-agnostic playbook for building **screen-reader / audio navigation** into a game accessibility
mod. It documents **three navigation styles** distilled from real games (two decompiled, one from our
own mods) plus the shared model underneath them — so for any new game you can pick **one style, a
combination of two, or all three**.

---

## The three navigation styles (pick / combine per game)

| # | Style | One-line | Feedback shape | Reference |
|---|-------|----------|----------------|-----------|
| 1 | **Reactive radar** | Cues fire *as you move* when an **impassable obstacle** (wall, rock, tree, invisible barrier…) closes in or space opens/closes | Sparse, event-driven (one cue per tick) — tells you *what changed* | [`a-heros-call/02-reactive-radar.md`](a-heros-call/02-reactive-radar.md) · 3D variant: [`united-minecraft/`](united-minecraft/nav-radar-spec.md) |
| 2 | **Polar scan radar** | On-command sweep of N rays around you, each ray's first wall sounded | On-demand burst — tells you *what's all around, in detail* | [`swamp/navigation-spec.md`](swamp/navigation-spec.md) §3 |
| 3 | **Wall sonification** | One continuous tone per direction, volume = proximity of the nearest **impassable obstacle** (walls, rocks, trees, invisible barriers…) | Always-on ambient bed — tells you *the box you're in, right now* | [`wall-sonification/`](wall-sonification/) |

They are **complementary, not competing** — each owns a different audio "lane":

- **Wall sonification** = quiet continuous bed answering *"what's around me?"* passively.
- **Reactive radar** = sparse louder events answering *"what just changed?"* (corners, doorways, openings).
- **Polar / POI scan** = user-triggered detail answering *"what's down there?"* (often with speech).

Layer their volumes (ambient bed quietest, events louder, speech interrupts) and they don't turn to mush.

### Which to use — decision guide

| Game shape | Recommended | Why |
|------------|-------------|-----|
| **Top-down grid explorer** (Core Keeper, A Hero's Call, Swamp) | **All three.** Wall sonification with **world-fixed** N/S/E/W as the bed + reactive radar for openings + on-demand scan/speech for detail | Coarse grid + clear cardinal walls make every style cheap and legible |
| **3D first-person explorer** (RE7, DRG, 7 Days to Die) | **Wall sonification (camera-relative) as the backbone** + reactive radar for doorways/openings + simple beacon to objectives | Constant layout sense matters most when you can't see the corridor |
| **3D action / arena / fighting** (SF6, Xenoverse 2, Megabonk) | **Wall sonification → corner / ring-out proximity** + opponent-as-beacon; skip the polar scan | "Navigation" becomes *spacing*: distance to edge + to opponent |
| **Fast platforming / roguelite** (Megabonk) | Wall sonification + **collision thump** + **vertical-space / drop cues** | Movement is quick and 3D; you need drop/gap warnings, not a slow scan |
| **Minimal / proof-of-concept** | **Simple beacon only** (bearing + distance to target) | Cheapest high-value cue; validates the speech+audio pipeline first |

> Rule of thumb: **start with the cheapest cue that proves the pipeline** (a simple beacon or the wall
> bed), then add styles. Wall sonification is usually the highest value-per-effort backbone in 3D;
> reactive radar is the standout in tile corridors.

---

## Studies behind the styles

| Study | Folder | Engine / language | How recovered | Teaches |
|-------|--------|-------------------|---------------|---------|
| **A Hero's Call** | [`a-heros-call/`](a-heros-call/) | MonoGame / .NET 4.0 | Full managed decompile | Reactive radar (style 1) + scanning, beacons, zones, spatial audio, speech — the complete suite |
| **Swamp** | [`swamp/`](swamp/navigation-spec.md) | Native VB6 (Ghidra) | Decompile + map format | Polar scan radar (style 2) + collision sounds + zones, from a totally different engine |
| **Wall sonification** | [`wall-sonification/`](wall-sonification/) | Unity Mono **and** IL2CPP (BepInEx) | **Our own mods** (Core Keeper, DRG, Megabonk) | Continuous directional wall tones (style 3) + collision/vertical cues + IL2CPP NAudio synth |
| **Kirandur** | [`kirandur/`](kirandur/sonar-navigation-spec.md) | MonoGame **NativeAOT** (Ghidra, native x64 — no IL) | Native decompile | Continuous **semantic** sonar: a fixed **6-axis probe** (4 cardinals + up/down) with per-cell classification (wall/door/drop/ramp/…) and **elevation sampling** — a richer take blending styles 2+3 |
| **United Minecraft** | [`united-minecraft/`](united-minecraft/nav-radar-spec.md) | Minecraft Java / Fabric (JVM) | **Real GPL-3.0 source** (no RE) | Reactive radar in a **3D voxel world**: classification by **traversability** (walk/jump/turn) from obstacle *height* vs the player's own step & jump reach, cue **timbre taken from the hit material's own sound**, state keyed by **absolute compass bearing**, and a 3-layer anti-spam + cue-arbitration model |

> A Hero's Call is the **primary reference** for the shared subsystems (its code was fully recovered);
> Swamp **corroborates** the model from native VB6; wall-sonification is **battle-tested in our own
> shipped mods** across both Unity backends; **Kirandur** adds a continuous, semantically-classified
> sonar with real elevation detection (ramps/drops), reversed from a NativeAOT binary; **United
> Minecraft** is the only study read from **published source rather than a decompile**, and is the one
> to copy when the radar must work in a **3D world with a vertical axis** — it answers *"can I walk
> there?"* instead of *"is something there?"*.

---

## The shared model (every style sits on this — learn it first)

Everything reduces to **a world you can query + raycasting it + turning hits into panned/pitched sound**:

1. **World** = tile grid (2D games) or physics colliders / navmesh (3D games). Walls/obstacles are
   queryable.
2. **Player state** = position + a **heading / camera forward**.
3. **Every cue is a ray query turned into sound**:
   - *Reactive radar* — short panned cues as you move (wall ahead / space opening/closing).
   - *Polar scan* — N rays around you on command; first wall per ray → panned wall/open sound.
   - *Wall sonification* — one ray per direction every tick; proximity → continuous tone volume.
   - *Collision* — step blocked → positioned bump/thump, **no speech**.
   - *Scanning / POI* — on-demand ray → spoken list of what's ahead, with distances.
   - *Zones/regions* — announce on crossing a boundary; swap reverb on acoustic zones.
   - *Beacons* — bearing + distance to a target (optionally A\*-routed).
   - *Spatial audio* — 3D pan/attenuation + occlusion = raycast wall thickness → muffle/mute.
   - *Speech* — one sink, many backends, interrupt on context change.

> **Porting kernel:** expose `PlayerPosition`, `Heading / CameraForward`, and
> `Raycast(origin, dir, maxDist) -> firstBlockingHit` (tile step-scan or `Physics.Raycast`). Everything
> else is engine-free logic copied on top. Quantize to integers only where an algorithm needs it (the
> radar pitch bucket).

---

## HARD RULE — radar cues are FLAT PAN, never 3D audio

**Never spatialize radar/sonification cues as 3D world sounds.** This applies to every style: the
reactive radar's chirps (style 1), the polar scan's per-ray hits (style 2), and the wall-sonification
bed (style 3). These cues are **egocentric and categorical** — they mean "left / front / right", not
"an object exists at point (x,y,z)" — so render them as classic **flat stereo pan** (discrete L/C/R for
event cues; per-channel volume for the bed) on a 2D, non-spatialized audio path.

Why 3D-positioning them breaks the radar (every one of these has bitten our ports):

- **Distance attenuation double-encodes distance.** The cue already encodes proximity in its *pitch
  bucket* (radar) or *volume law* (sonification). Letting the engine also attenuate it by distance
  makes near-wall cues loud and far cues inaudible on top of that — two conflicting distance channels.
- **HRTF front/back confusion.** A binaural "front" cue is routinely heard as "behind" (the classic
  cone-of-confusion); a categorical front cue must never be ambiguous.
- **Reverb / occlusion smear.** World audio paths pick up zone reverb and occlusion muffling; a radar
  chirp heard through the wall reverb of a cathedral no longer sounds like the same cue.
- **Legibility = identical every time.** A categorical cue must sound *exactly* the same on every
  fire, differing only in pan (and the deliberate pitch bucket). Any engine-side processing that varies
  with player position destroys instant recognition.

This is exactly what A Hero's Call ships: radar events carry a manual FMOD `"Panning"` parameter
(0=L, 1=C, 2=R) baked at creation, on a 2D path with **no** spatializer, **no** reverb, **no**
occlusion (`ReactiveRadar.cs:105-110`, `FModSoundContext.cs:790-803`) — while **world-anchored**
sounds (beacons, landmark loops, NPC footsteps, positional TTS, ambience) go through the full
**Oculus/Resonance HRTF** path ([doc 08](a-heros-call/08-spatial-audio-fmod.md)). Keep both lanes,
never swap them:

| Sound | Rendering path |
|---|---|
| Radar chirps, open/closed cues, polar-scan hits, wall-sonification bed, UI/menu feedback | **Flat 2D**: discrete pan / per-channel volume; no spatializer, no distance attenuation, no reverb, no occlusion |
| Beacons, landmark/object loops, NPC footsteps, positional TTS, ambient beds, world one-shots | **Engine 3D/HRTF**: full spatializer + attenuation + occlusion (this is where the spatial precision lives) |

In Unity that means radar cues play on an `AudioSource` with `spatialBlend = 0` (and outside any
reverb-zone/occlusion mixer group), never `spatialBlend = 1`; in FMOD/Wwise, a dedicated 2D bus/event.

> **The one narrow exception** — an engine whose *entire* spatial path is a simple stereo panner with
> plain distance attenuation and **no HRTF, reverb or occlusion** (Minecraft's OpenAL mixer is the
> worked example). There, positioning the cue is safe and buys you distance for free. United Minecraft
> does exactly this, and [`united-minecraft/nav-radar-spec.md`](united-minecraft/nav-radar-spec.md) §7
> spells out why it works there and why it must **not** be carried into Unity / Unreal / RE Engine.

---

## Subsystem map — read this doc for that mod section

Build a navigation mod **section by section**; for each section, the authoritative reference:

| Mod subsystem | A Hero's Call doc | Swamp | Wall-sonification | Notes |
|---------------|-------------------|-------|-------------------|-------|
| Coordinate system & angle math | [01](a-heros-call/01-coordinate-system-and-math.md) | §0, §4–5 | §4 | **Read first.** Compass convention, deg↔vector, bearings, dir vectors |
| Grid raycaster / collision primitive | [03](a-heros-call/03-raycasting-and-collision.md) | §0, §1 | §4 | The one primitive everything stands on |
| Movement, turning, cardinal cues | [04](a-heros-call/04-navigation-and-movement.md) | §1, §4 | §6 (collision thump) | Continuous move + slide collision + heading clamp |
| **Reactive radar** (style 1) | [02](a-heros-call/02-reactive-radar.md) | §3 | — | 3-beam open/closed + pitch. In **3D**, add [`united-minecraft/`](united-minecraft/nav-radar-spec.md) §3 (height→walk/jump/turn) |
| **Polar scan radar** (style 2) | — | §3 | — | 16-ray sweep, modes select dirs/range |
| **Wall sonification** (style 3) | — | — | [whole doc](wall-sonification/) | Continuous per-direction tones, volume = proximity |
| POI scanning / examine / interact | [05](a-heros-call/05-scanning-pois-and-interactables.md) | — | §6 | Diff-gated auto-scan; 2.5 focus / 5 act |
| Zones, regions & announcements | [06](a-heros-call/06-zones-regions-and-announcements.md) | §2 | §5 (state gating) | Spoken *region* vs acoustic *zone* |
| Beacons & pathfinding | [07](a-heros-call/07-beacons-and-pathfinding.md) | §5 | — | Simple bearing+distance, or A\*-routed |
| Spatial audio / occlusion / synthesis | [08](a-heros-call/08-spatial-audio-fmod.md) | §1, §3 | §3 | Pan + attenuation + occlusion; procedural tone synth (Unity/NAudio) |
| Speech / screen-reader output | [09](a-heros-call/09-speech-output-layer.md) | §2, §4 | — | One sink, many backends; map Tolk→**PRISM** |
| Big picture & frame loop | [00](a-heros-call/00-overview-and-architecture.md) | §6 | §1, §5 | Architecture, per-frame data flow, constants |
| Porting to real target games | [10](a-heros-call/10-porting-guide.md) | §6 | §7 | Concrete SF6 / 7DTD / XV2 / RE7 mappings + adapter surface |

---

## Convention differences to watch (they cause mirrored-direction bugs)

| | A Hero's Call | Swamp | Wall sonification | United Minecraft |
|--|---------------|-------|-------------------|------------------|
| Heading zero / rotation | **0° = North**, clockwise | **0° = East**, clockwise | n/a (uses dir vectors / camera) | **0° = North**, clockwise (converted from Minecraft's **yaw 0 = South**) |
| Direction frame | player-relative beams | player-relative rays | **world-fixed** (grid) **or camera-relative** (3D) | **world-fixed**: facing snapped to the nearest cardinal |
| Axes | +X East, **+Y South** (Y-down) | +X East, +Y South (Y-down) | engine-native (Unity Y-up, raycast on XZ plane) | **Y-up**; +X East, **+Z South** |
| Radar shape | 3 beams (L/F/R), open/closed + 3-pitch | 16 rays, 22.5° apart, first hit | 4 channels (F/B/L/R), continuous volume | 3 rays (F/R/L), **open/jumpable/blocked** by obstacle height |
| Cue timing | event on transition | on command | continuous, every tick | event on transition + per-bearing cooldown + global stagger |
| Spoken distance | rounded tiles | **feet = tiles × 24** | n/a (non-verbal bed) | n/a for the radar (non-verbal); blocks elsewhere |

> Pin your convention **once at the engine-adapter boundary** and verify with a worked example
> ("facing East, my left points North"; "wall on my right pans right") before trusting any output.

---

## Recommended build order (any target game)

1. **Adapter + Speech** — get `Speak("test", interrupt)` through PRISM and read player position/heading
   on a key. Validates the whole pipeline.
2. **Simple beacon / bearing** — cheapest high-value cue (`atan2` + 8-bin + distance).
3. **Wall sonification** (3D) or **reactive radar** (tile corridors) — the navigation backbone. Wire
   `Raycast` + a panned/continuous-tone primitive; tune range, base volume, and per-direction
   frequencies to the game's scale.
4. **POI scan** (exploration) or **spacing/corner radar** (action), plus **collision / drop cues** if 3D.
5. **Occlusion**, **zone reverb**, **polar scan** last (polish / detail-on-demand).

See [`a-heros-call/10-porting-guide.md`](a-heros-call/10-porting-guide.md) and
[`wall-sonification/README.md`](wall-sonification/README.md) for per-engine mappings (RE Engine, Unity
Mono/IL2CPP, native/XV Patcher) and the adapter surface each style needs.
