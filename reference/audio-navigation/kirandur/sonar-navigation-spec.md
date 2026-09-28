# Kirandur — "Sonar" radar: reconstructed algorithm and porting guide

> Reverse-engineering of Kirandur's sonar system (top-down audiogame, MonoGame/XNA in C#
> **compiled with NativeAOT** → native x64; there is no IL — it was decompiled with Ghidra). This
> document reconstructs the continuous audio-radar algorithm plus the spoken readout (Q key) so it can
> be ported to other accessibility mods. Every claim cites the Ghidra `FUN_xxxx` it comes from; the
> constants were read **directly from the binary** (no invented magic numbers).

## What it is (summary)

A **continuous sonar**: every 0.1 s it emits, in several directions at once around the player (top-down
→ 360°, including behind), a ray that **raymarches while sampling terrain height**. It classifies each
direction (wall / open / door / portal / ground / harmless drop / harmful drop / slope up / slope down)
and plays a `.ogg` cue **spatialized via HRTF (Steam Audio/Phonon)** at the hit position. The **Q** key
(`SpeakSonar`) speaks the same state, with distance and direction.

What makes it better than A Hero's Call's reactive radar: it **samples elevation** (ramps and drops,
with severity), has **rich semantic classification**, **360° simultaneous** coverage, and **real
binaural spatialization** instead of pan-by-angle.

## Real constants (read from the `.exe`, `.data` section)

| Constant (Ghidra symbol) | Value | Use |
|---|---|---|
| `DAT_140c1ee78` ScanInterval | **0.1 s** | interval between pulses (`dt` accumulator) |
| `DAT_140c1eed8` StepSize | **1.0** | raymarch step (world units ≈ 1 tile) |
| `DAT_140c1eed0` MinHeightDelta | **2.0** | minimum absolute height delta to consider ramp/drop |
| `DAT_140c1eed4` MinGradient | **0.2** | minimum gradient (Δheight/distance) to mark a ramp |
| `DAT_140c1ef10` HarmfulDropDepth | **6.0** | depth separating harmful drop (≥6) from harmless (<6) |
| `DAT_140c1ef30` GroundZThreshold | **-0.5** | if `dir.z ≤ -0.5` (ray points down) → classify "ground" |
| `DAT_140c1e588` SpeakMinDist | **-1.0** | sentinel: distance is always read (`> -1` comparison) |
| `DAT_140c1ee8c` / `DAT_140c1ee90` MoveGate | **+1.5 / -1.5** | threshold of `dot(Δhit_position, direction)` to pick the cue variant (approaching/receding) |

> `MaxRange` is the scanner field `scan+0x24`, read fresh each tick and passed to the sampler as
> `param_5` (`FUN_1403f5600:3359`). Its **default is 20.0** world units, written literally in the scanner
> constructor `FUN_1403f52c0:340` (`*(scan+0x24) = 0x41a00000`). The field is re-read every tick, so it
> can be changed at runtime, but 20.0 is the built-in default.

## Scan geometry — a fixed 6-axis probe (not a polar sweep)

The scanner constructor is `FUN_1403f52c0`. It builds **two persistent, fixed ray lists** — primary at
`scan+0x10` (a 4-element array), secondary at `scan+0x18` (a 2-element array) — from a **static table of
direction vectors** in `.data` (no `cos`/`sin`; the directions are literal constants). The pulse
iterates **all 6 rays** every tick (`FUN_1403f5600:3351-3426`). Each ray is built by the ray constructor
`FUN_1403f5d60`, which **normalizes** the direction into `ray+0x2c` and stores two per-ray scalars:
`ray+0x10` (volume) and `ray+0x14` (stereo pan).

So it is **not** a fine polar scan: it's the **6 axis directions** — the 4 cardinals in the top-down
plane (XY) plus straight up/down (Z) for elevation. Recovered directly from `FUN_1403f52c0` +
`FUN_1403f5d60` + the `.data` constants (`DAT_140c1ee10..70`), all **high confidence**:

| # | List | Direction (x,y,z) | Volume `+0x10` | Pan `+0x14` | Meaning |
|---|---|---|---|---|---|
| 1 | primary | (0, 1, 0) | 1.0 | 0.0 | North / forward |
| 2 | primary | (1, 0, 0) | 0.98 | +0.6 | East / right |
| 3 | primary | (0, -1, 0) | 0.8 | 0.0 | South / back |
| 4 | primary | (-1, 0, 0) | 0.94 | -0.6 | West / left |
| 5 | secondary | (0, 0, 1) | 1.1 | 0.0 | Up (Z+) |
| 6 | secondary | (0, 0, -1) | 0.7 | 0.0 | Down (Z-) |

- The **4 primary rays** are the horizontal cardinals (90° apart → 360° in the movement plane). The
  per-ray **volume** encodes a front/back bias (front 1.0 louder than back 0.8) and the **pan** a
  left/right bias (East right +0.6, West left -0.6, North/South centered). This is a built-in stereo
  presentation baked into the ray table.
- The **2 secondary rays** are purely **vertical** (straight up / straight down), centered pan, up
  slightly boosted (1.1) vs down (0.7) — they probe ceiling/above and floor/below (elevation). The
  secondary pass runs only when the `scan+0x29` flag is set (indoor/occlusion state, `FUN_1403f54c0`).
- Per-ray raymarch (`FUN_1403f6480`, or `FUN_1403f6fd0` without occlusion): advances `p += dir*StepSize`
  while `dist < MaxRange` (20.0), sampling terrain height at each step. For the 4 horizontal rays, the
  ramp/drop detection comes from the **height (Z) changing along the horizontal march**; the 2 vertical
  rays give direct above/below readings.

## Per-ray classification (beam-type enum, result field `+0x10`)

| Value | Type | Rule | Source |
|---|---|---|---|
| 0 | **Open** | raymarch exhausts `MaxRange` with no block | `FUN_1403f6480:5115-5120` |
| 1 | **Wall** | blocked and **no** door in the AABB | `FUN_1403f6480:4967`, `FUN_1403f6fd0:5263` |
| 2 | **Portal** | (doubtful per-ray; seems announced as a separate event) | `FUN_1403f5600:3335-3341` |
| 3 | **HarmlessDrop** | drop edge with depth `< 6.0` | `FUN_1403f6480:5058` |
| 4 | **HarmfulDrop** | drop edge with depth `≥ 6.0` (`HarmfulDropDepth`) | `FUN_1403f6480:5012,5044-5048` |
| 5 | **Door** | blocked and `FUN_1403f7460(AABB)` finds a door | `FUN_1403f6480:4966` |
| 6 | **Ground** | `dir.z ≤ -0.5` (`GroundZThreshold`) → grazing/downward ray | `FUN_1403f6fd0:5230-5247`, bank `ground.ogg`=idx6 `FUN_1403f4f00:2524` |

**World queries:** cell collision `FUN_1400174e0(point)` (blocked?), terrain height
`FUN_140020380/140020310(point)`, door-in-AABB `FUN_1403f7460`, material `FUN_1401d93c0`. Sampling the
**height (Z)** along the ray is what enables ramps and drops (this is not a flat 2D tilemap).

**Ramps** (`FUN_1403f6480:5090-5103`): with `dh = currentHeight - previousHeight`, a ramp is marked only
if `|dh| ≥ 2.0` (MinHeightDelta) **and** `|dh|/step ≥ 0.2` (MinGradient). Direction: `dh ≤ 0 → Down (2)`,
else `Up (1)`. Cues: `incline_up.ogg`=slope idx1 (`FUN_1403f51c0:2865`), `incline_down.ogg`=idx2.

**Drops** (downward loop `LAB_1403f6b7d`, `FUN_1403f6480:5000-5063`): on a downward edge, it probes the
depth; `≥ 6.0 → HarmfulDrop(4)`, `< 6.0 → HarmlessDrop(3)`.

## Continuous emission (the radar)

- **Cadence** (`FUN_1403f5600:3291-3296`): accumulator `timer += dt`; if `timer < 0.1` return; otherwise
  `timer = 0` and emit a pulse. → **10 pulses/second**, all directions in the same pulse.
- **Diff-gating + movement gate** (`FUN_1403f5de0`): re-triggers a cue only if the type changed, or
  within a 2-frame hysteresis, or if you move along that direction: it computes
  `dot(Δhit_position, dir)`; `> +1.5` → sound variant idx2, `< -1.5` → idx1 (`FUN_1403f5de0:4429-4457`).
  Standing still with no change → suppressed (that's why it "sounds as you walk").
- **Type→.ogg map** (`FUN_1403f4f00`): two `Dictionary<int,Sound>` (variants A/B by the `scan+0x29` flag,
  special surface). Confirmed by string: `ground.ogg`=idx6; the rest by the enum above. Ramps have their
  own dict pair (`FUN_1403f51c0`).

## Spatialization

**Real binaural HRTF, not manual pan.** The sonar uses a `SpatialPanner` (`FUN_14007e6f0`) that creates a
**binaural effect** + mono/stereo **direct effects** from a Steam Audio/Phonon-style middleware (strings
`"Failed to create binaural effect"` `:2423`, `"...mono direct effect"` `:2403`). Each cue is placed at
the ray's **world hit position** (result `+0x04` XY, `+0x0c` Z) and routed to the `sonar` bus
(`FUN_1403f63d0:3130-3136`); the L/R pan and distance attenuation are computed by the binaural renderer
from emitter vs. listener. There is a `StereoPanner` (`FUN_140071e60`, scalar pan) as a simple non-binaural
fallback.

> **Porting implication:** to replicate it faithfully, feed a 3D positional HRTF panner (Steam Audio /
> Resonance / OpenAL-HRTF) with the **world hit position**, rather than a `pan = f(angle)`.

## SpeakSonar (Q key) — on-demand spoken readout

`FUN_1404048d0` registers the input action; the text is composed by `FUN_1403d0900`:
1. No data → speaks the `"No sonar"` key and returns (`:1851-1860`).
2. Iterates the primary list (capped at 4 directions, `:1896`): per ray composes
   `<type-key> + distance(if > -1) + <direction-name>` using the **same type→key dict** as the radar
   (`FUN_1403f4d00`), keeping voice↔sound consistent.
3. If the ray has a ramp → appends `<slope-key> + distance` (`:1900-1920`).
4. If the second-pass flag is set → iterates the secondary list (capped at 2, `:1950`).
5. Speaks the composed string (`FUN_1403b55a0`, voice sink, `:1961`).

Localization keys (`.ftl`): `sonar-beam-{wall,open,door,ground,portal,harmless-drop,harmful-drop}`,
`sonar-slope-{up,down}`, `No sonar`.

## Portable C# pseudocode (continuous radar, engine-agnostic)

```csharp
// Adapter to implement per engine (Unity/MonoGame/…)
interface ISonarWorld {
    bool    IsBlocked(Vector3 p);                    // FUN_1400174e0
    float   SampleHeight(Vector3 p);                 // FUN_140020380 / 140020310
    bool    HasDoorInAabb(Vector3 min, Vector3 max); // FUN_1403f7460
    Vector3 ListenerPosition { get; }
}
interface ISonarAudio {
    void PlayCue(string oggId, Vector3 worldPos);    // SpatialPanner (positional HRTF)
}

enum Beam { Open=0, Wall=1, Portal=2, HarmlessDrop=3, HarmfulDrop=4, Door=5, Ground=6 }
enum Slope { None=0, Up=1, Down=2 }

// REAL constants read from the binary (not guessed)
static class SonarConst {
    public const float ScanInterval     = 0.1f;   // DAT_140c1ee78
    public const float StepSize         = 1.0f;   // DAT_140c1eed8
    public const float MinHeightDelta   = 2.0f;   // DAT_140c1eed0
    public const float MinGradient      = 0.2f;   // DAT_140c1eed4
    public const float HarmfulDropDepth = 6.0f;   // DAT_140c1ef10
    public const float GroundZThreshold = -0.5f;  // DAT_140c1ef30
    public const float MoveGatePos      = 1.5f;   // DAT_140c1ee8c
    public const float MoveGateNeg      = -1.5f;  // DAT_140c1ee90
    public const float MaxRange         = 20.0f;  // FUN_1403f52c0 (scan+0x24 = 0x41a00000); re-read each tick
}

struct Hit { public bool Valid; public Vector3 Pos; public Beam Type; public Slope Slope; public float Dist; }

sealed class Ray {
    public Vector3 Dir;      // normalized in the ctor (FUN_1403f5d60)
    public float Volume;     // ray+0x10 — front/back gain preset
    public float Pan;        // ray+0x14 — left/right stereo bias preset
    public Beam LastType; public Vector3 LastHitPos; public int FramesSince;
    public Ray(Vector3 dir, float vol, float pan) { Dir = Vector3.Normalize(dir); Volume = vol; Pan = pan; }
}

sealed class Sonar {
    ISonarWorld world; ISonarAudio audio;

    // Fixed 6-axis probe recovered from FUN_1403f52c0 + the .data table (DAT_140c1ee10..70).
    // Primary = 4 cardinals in the top-down plane; secondary = 2 vertical (elevation).
    readonly Ray[] primary = {
        new Ray(new Vector3( 0,  1, 0), 1.00f,  0.0f), // North / forward
        new Ray(new Vector3( 1,  0, 0), 0.98f, +0.6f), // East / right
        new Ray(new Vector3( 0, -1, 0), 0.80f,  0.0f), // South / back
        new Ray(new Vector3(-1,  0, 0), 0.94f, -0.6f), // West / left
    };
    readonly Ray[] secondary = {
        new Ray(new Vector3(0, 0,  1), 1.10f, 0.0f),   // Up
        new Ray(new Vector3(0, 0, -1), 0.70f, 0.0f),   // Down
    };
    float timer;

    static readonly Dictionary<Beam,string> Cues = new() {
        [Beam.Open]="open", [Beam.Wall]="wall.ogg", [Beam.Portal]="portal.ogg",
        [Beam.HarmlessDrop]="harmless_drop.ogg", [Beam.HarmfulDrop]="harmful_drop.ogg",
        [Beam.Door]="door.ogg", [Beam.Ground]="ground.ogg",
    };
    static readonly Dictionary<Slope,string> SlopeCues = new() {
        [Slope.Up]="incline_up.ogg", [Slope.Down]="incline_down.ogg",
    };

    public void Tick(float dt) {                          // FUN_1403f5600
        timer += dt;
        if (timer < SonarConst.ScanInterval) return;
        timer = 0f;
        var origin = world.ListenerPosition;
        foreach (var r in primary)   Emit(origin, r);
        foreach (var r in secondary) Emit(origin, r);
    }

    void Emit(Vector3 origin, Ray r) {                    // FUN_1403f5de0
        Hit h = Classify(origin, Vector3.Normalize(r.Dir));
        bool typeChanged = h.Type != r.LastType;
        r.FramesSince = h.Valid ? 0 : r.FramesSince + 1;
        float moveAlong = Vector3.Dot(h.Pos - r.LastHitPos, r.Dir);
        bool retrigger = typeChanged || r.FramesSince < 2
                         || moveAlong > SonarConst.MoveGatePos || moveAlong < SonarConst.MoveGateNeg;
        if (h.Valid && retrigger) {
            audio.PlayCue(Cues[h.Type], h.Pos);
            if (h.Slope != Slope.None) audio.PlayCue(SlopeCues[h.Slope], h.Pos);
        }
        r.LastType = h.Type; r.LastHitPos = h.Pos;
    }

    Hit Classify(Vector3 origin, Vector3 dir) {           // FUN_1403f6480 / FUN_1403f6fd0
        float dist = 0f; var p = origin; float prevH = world.SampleHeight(origin);
        var slope = Slope.None;
        while (dist < SonarConst.MaxRange) {
            dist += SonarConst.StepSize; p += dir * SonarConst.StepSize;
            if (world.IsBlocked(p)) {
                var min = p - Vector3.One*SonarConst.StepSize; var max = p + Vector3.One*SonarConst.StepSize;
                Beam t = world.HasDoorInAabb(min, max) ? Beam.Door : Beam.Wall;
                if (dir.Z <= SonarConst.GroundZThreshold) t = Beam.Ground;
                return new Hit{ Valid=true, Pos=p, Type=t, Slope=slope, Dist=dist };
            }
            float h = world.SampleHeight(p), dh = h - prevH;
            if (slope == Slope.None && MathF.Abs(dh) >= SonarConst.MinHeightDelta
                && MathF.Abs(dh)/SonarConst.StepSize >= SonarConst.MinGradient)
                slope = dh <= 0 ? Slope.Down : Slope.Up;
            if (dh <= -SonarConst.MinHeightDelta) {
                float depth = ProbeDropDepth(p, dir);     // LAB_1403f6b7d
                Beam t = depth >= SonarConst.HarmfulDropDepth ? Beam.HarmfulDrop : Beam.HarmlessDrop;
                return new Hit{ Valid=true, Pos=p, Type=t, Slope=slope, Dist=dist };
            }
            prevH = h;
        }
        return new Hit{ Valid=false, Pos=p, Type=Beam.Open, Slope=slope, Dist=dist };
    }
}
```

## How to port it to your mods (fit with the playbook)

> For a full, genre-by-genre porting recipe (adapter contract, per-game-type ray sets, engine wiring,
> constant scaling, pitfalls) see [`10-porting-guide.md`](10-porting-guide.md). The essentials:

- It is a blend of **Style 2 (polar scan radar)** + **Style 3 (wall sonification)** from
  `reference/audio-navigation`: a per-direction scan (polar) but **continuous** and with a semantic cue
  per direction instead of a proximity tone. The improvement over AHC (Style 1, reactive) is the
  **elevation sampling**.
- Implement `ISonarWorld` against the target engine:
  - **Unity**: `IsBlocked` → `Physics.CheckSphere`/tilemap; `SampleHeight` → vertical raycast or
    `Terrain.SampleHeight`/tile height; `HasDoorInAabb` → `Physics.OverlapBox` filtered by door tag/layer.
  - **MonoGame/tilemap**: direct grid query + a height layer.
- **Ray pattern is fixed and known**: the 4 cardinals in your movement plane (±X, ±Y) + 2 vertical
  (±Z). Copy the table above verbatim, including the per-ray volume (front/back bias) and pan
  (left/right bias). If your game has no meaningful vertical axis, you can drop the 2 secondary rays and
  keep the 4 cardinals; if you want finer resolution you can add the 4 diagonals, but the original
  ships a clean 6-axis cross on purpose.
- **Spatialization**: use a 3D positional HRTF panner fed by the hit position. If your stack has no HRTF,
  fall back to the `StereoPanner` seeded with each ray's baked `Volume`/`Pan` (that table is exactly the
  stereo presentation the game uses when binaural isn't available).
- Respect the **diff-gating + movement gate** (0.1 s pulse, 2-frame hysteresis, dot threshold ±1.5): it's
  what avoids spam and gives the "breathes as you walk" feel.
- Reuse the **same type→localization-key map** for the spoken readout (Q) so voice and sound stay
  consistent.

## Open items (minor)

Ray geometry and `MaxRange` are now **fully resolved** (see "Scan geometry"): 6 fixed rays (4 cardinals +
2 vertical), `MaxRange = 20.0`, from `FUN_1403f52c0` / `FUN_1403f5d60` / the `.data` table. Remaining
low-impact unknowns:

1. **Full idx→.ogg map**: only `ground.ogg`=idx6, `incline_down`=slope idx2, `incline_up`=slope idx1 are
   string-confirmed; the rest is assigned by the enum (medium confidence).
2. **Type 2 (portal) per-ray**: doubtful; seems announced as a separate event.
3. **Drop sub-sampling step**: the downward probe (`LAB_1403f6b7d`) uses the same `StepSize`/`MinHeightDelta`
   family; not separately parameterized.

## Raw reverse-engineering artifacts

This doc is the **reference copy** (the playbook is docs-only). The raw decompilation lives outside the
playbook at **`D:\code\decompiled games\Kirandur\`**:

- `ghidra/` — Ghidra project with `kirandur.exe` already analyzed (browsable; "the decompiled game").
- `sonar-decompiled/sonar-cluster-full.c.txt` — 290 functions of the cluster (anchors + xrefs).
- `sonar-decompiled/sonar-cluster-focused.c.txt` — trimmed to the sonar core (64 functions).
- `SONAR-RADAR-porting-spec.md` — the original of this document.
