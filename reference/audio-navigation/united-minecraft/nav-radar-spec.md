# United Minecraft — Navigation Radar: 3-ray reactive radar with material-timbre cues

> **Style 1 (reactive radar), studied from real source.** Unlike A Hero's Call (decompiled) or Swamp
> (Ghidra), this one is a **published, GPL-3.0 accessibility mod with readable source** — so every
> constant and branch below is *confirmed*, not inferred.
>
> Read [`../README.md`](../README.md) first for the three styles and the shared model. This doc covers
> what United Minecraft's radar does **differently** from A Hero's Call, and the ideas worth stealing
> wholesale.

---

## 0. Identification & tooling

| Field | Value |
|-------|-------|
| Game / build | Minecraft Java Edition **26.2** |
| Mod studied | **United Minecraft** by NibbleNerds / blindgoofball — GPL-3.0 |
| Engine | JVM (Java 25), voxel world |
| Modding framework | **Fabric Loader 0.19.3** + Fabric API `0.152.2+26.2` — **client-side only**, works on vanilla servers |
| Mappings | Mojang official (`net.minecraft.*` names as shipped in the repo) |
| RE tool used | **None — real source**, cloned from GitHub |
| Speech path | **NVDA controller client DLL via Java FFM** (`java.lang.foreign`), falling back to Minecraft's own narrator — see §9 |
| Source location | `D:\code\decompiled games\united-Minecraft` (rev `730e470`, 2026-08-24) |
| Radar file | `src/client/java/com/nibblenerds/unitedminecraft/client/NavRadarController.java` (190 lines) |

The mod ships **six** distinct spatial-cue systems (Navigation Radar, Hostile Radar, Mining Radar, Fall
Warning, Movement Assist, Surroundings Scanner). This doc is about the **Navigation Radar** — the one
that maps onto style 1. The siblings, and the *arbitration* between them, are in §10.

> **Paths below are relative to `src/client/java/com/nibblenerds/unitedminecraft/client/`.**

---

## 1. Core data model

- **World** = voxel grid, but the radar never reads the grid directly. It queries **real collision
  shapes** via `Level.clip(ClipContext)` — Minecraft's own voxel raycast, the same primitive the
  crosshair uses. `ClipContext.Block.COLLIDER` means *"what the player physically can't walk through"*,
  and `ClipContext.Fluid.NONE` makes water and lava **transparent to the radar** (they're not walls;
  other systems handle them).
- **Player state** = `getX()/getY()/getZ()` (feet position, Y-up) + `getYRot()` (yaw degrees).
- **Per-hit data that drives the classification** = the hit block's **collision-shape top**
  (`hitPos.getY() + hitState.getCollisionShape(...).bounds().maxY`) and its **`SoundType`**.

> The whole feature stands on one primitive:
> `raycast(origin, dir, maxDist) -> { hit?, point, distance, topOfObstacle, material }`.
> Everything else is engine-free logic. Note the extra field versus our other studies:
> **`topOfObstacle`** — the height of the thing you hit, not just the distance to it. That single extra
> value is what turns a wall detector into a **traversability** detector (§3).

> Confidence: **High** — read from source, `NavRadarController.java:128-156`.

---

## 2. Coordinate system & conventions

| Thing | Value |
|-------|-------|
| Axes | **Y-up**; **+X = East**, **+Z = South** (so **−Z = North**) |
| Yaw zero | Minecraft's `getYRot()` is **0 = facing South**, increasing toward West (confirmed at `CameraUtil.java:19-20`, `AccessibilityTickHandler.java:524-525`) |
| Mod's bearing | `bearing = floorMod(round(yRot) + 180, 360)` → **0 = North, 90 = East, 180 = South, 270 = West**, clockwise (`NavRadarController.java:93`) |
| bearing → direction vector | `dirX = sin(rad)`, `dirZ = −cos(rad)` (`:129-131`, `:159-161`) |
| Scan axes | facing **snapped to the nearest 90°**: `facing = floorMod(round(bearing / 90) * 90, 360)` (`:94`) |
| Beams | offsets `{0, 90, 270}` = **front, right, left**. **Behind is deliberately never scanned** (`:43`) |

**Worked example.** With `yRot = -95` → `bearing = floorMod(-95 + 180, 360) = 85` → snapped
`facing = 90` (East). Beams fire at bearings **90 (front / East)**, **180 (right / South)** and
**0 (left / North)**. Bearing 90 → `dirX = sin 90° = 1`, `dirZ = −cos 90° = 0` → **+X = East**. ✔
Facing East, your right points South, and the "right" beam correctly probes +Z.

> The mod converts Minecraft's south-zero yaw into a **standard compass bearing** at the boundary and
> then works purely in compass space — exactly the "pin your convention once at the adapter boundary"
> rule from [`../README.md`](../README.md). Beware: the formula is **duplicated inline** in three places
> (`NavRadarController.java:93`, `AccessibilityTickHandler.java:526`, and a hardcoded yaw variant in
> `BuildModeController.java:318-339`) rather than living in one helper. In our mods, put it in the
> adapter once.

---

## 3. The algorithm

Runs on `ClientTickEvents.END_CLIENT_TICK` (`AccessibilityTickHandler.java:76`), i.e. **20 tps → 1 tick
= 50 ms**, when the radar is toggled on (**N** by default, `ClientKeyBindings.java:78-79`):

```
tick():
  bearing  = floorMod(round(yaw) + 180, 360)          # south-zero yaw -> compass
  facing   = floorMod(round(bearing / 90) * 90, 360)  # SNAP to cardinal
  for offset in {0, 90, 270}:                         # front, right, left
      b      = floorMod(facing + offset, 360)
      result = probe(b)
      if lastState[b] == result.state: continue       # (1) diff gate, keyed by BEARING
      lastState[b] = result.state
      if ticks - lastPlayedTick[b] < 10: continue     # (2) per-bearing cooldown, 500 ms
      pending.removeIf(cue -> cue.center == b)        #     one pending cue per bearing, newest wins
      pending.add(cue(b, result))

  if pending nonempty and ticks - lastGlobalPlayTick >= 3:      # (3) global stagger, 150 ms
      cue = first pending whose state == OPEN, else pending[0]  # (4) OPEN has priority
      play(cue); lastPlayedTick[cue.center] = ticks; lastGlobalPlayTick = ticks
```

### 3.1 `probe(bearing)` — one ray, classified by **height**, not distance

```
range = config.navRadarRange                       # default 8 blocks (slider 4..16)
rayY  = player.y + 0.1                             # JUST ABOVE THE FEET, not eye height
hit   = clip(from = (x, rayY, z),
             to   = (x + dirX * range, rayY, z + dirZ * range),
             blocks = COLLIDER, fluids = NONE, ignoreEntity = player)

if hit == MISS: return OPEN at full range, no material

top      = hitBlockPos.y + hitState.collisionShape.bounds.maxY   # top of what we hit
rise     = top - player.y                                        # how far UP it is, from the feet
distance = |hitPoint - from|

state = rise <= player.maxUpStep() ? OPEN       # auto step-up: you walk straight over it
      : rise <= 1.25              ? JUMPABLE    # one jump clears it
      :                             BLOCKED     # turn around
```

Three things here are the substance of the whole study:

1. **The probe is at the feet (+0.1), not the eyes.** A fence, a slab, a one-block ledge — everything
   that actually stops your movement — lives below eye height; an eye-height ray sails straight over the
   exact obstacles a navigation radar exists to report. The source says so in as many words (`:133-134`).
2. **The classification is `rise`, not `distance`.** The radar answers *"can I keep walking this way?"*
   with three **actionable** answers (walk / jump / turn), not *"is there geometry over there?"*.
   Distance is still carried, but it drives **loudness**, not the category (§4).
3. **The thresholds come from the player, not from a table.** `player.maxUpStep()` is the *live vanilla
   value* for auto step-up, so the OPEN/JUMPABLE boundary is by construction exactly the height the
   player's own movement code will step over — it can never drift out of sync with the game. (The 1.25
   jump constant is *not* derived — see §8.)

### 3.2 State is keyed by **absolute compass bearing**, never by beam index

The single best idea in this file, and one that will bite anyone who doesn't copy it
(`NavRadarController.java:35-40`, `:97-103`):

> Track `lastState` in a `Map<bearingDegrees, State>`, **not** an array indexed by front/left/right. The
> index→bearing mapping *rotates with the player*, so on a 90° turn every beam index suddenly points
> somewhere else and **every direction looks like it changed** — a diff-gated radar would fire three
> cues on every turn, forever.

Keying by world bearing means turning in place is silent (the same walls are still where they were), and
cues fire only when the **world** changed relative to you. This is also the argument for **snapping the
scan axes to cardinals**: a snapped facing yields only 4 possible bearing sets, so the state map stays
meaningful across turns instead of being invalidated by every degree of mouse movement.

### 3.3 Three layers of anti-spam, plus a priority rule

| Layer | Constant | Real time | Purpose |
|---|---|---|---|
| Diff gate | — | — | Fire only on a **state transition** for that bearing (`:100-103`) |
| Per-bearing cooldown | `BEARING_COOLDOWN_TICKS = 10` | 500 ms | A direction flickering across a classification boundary (walking along a diagonal edge) can't machine-gun (`:106-108`) |
| Global stagger | `PLAY_INTERVAL_TICKS = 3` | 150 ms | Several simultaneous changes **trickle out one at a time** instead of overlapping into mush (`:113-125`) |

Plus **replace-don't-queue**: a new cue for a bearing removes the pending one for that same bearing
(`:109`) — you always hear the *current* truth, never a stale one from 400 ms ago.

And **OPEN-first priority**: when several cues are pending, an OPEN cue is dequeued first (`:114-120`).
Rationale: an opening is rarer and more actionable than yet another wall — *"there's a way out to your
left"* beats *"there's still a wall in front"*.

> Confidence: **High** — all read from source.

---

## 4. Cue design — the timbre comes from the world itself

| Case | Sound | Volume | Pitch | Position |
|---|---|---|---|---|
| **OPEN** | `SoundEvents.EXPERIENCE_ORB_PICKUP` (short chime) | `0.6` | `1.0` | **1 block** from the player in that direction |
| **JUMPABLE** | the hit block's **own `SoundType.getPlaceSound()`** | `1.0` | `2.0` | at the **real hit distance** in that direction |
| **BLOCKED** | the hit block's **own `SoundType.getPlaceSound()`** | `1.0` | `1.5` | at the **real hit distance** in that direction |

**The idea to steal:** the wall cue is *the sound the game already makes when you place that block*
(`:172`). A stone wall and an oak wall already sound different — for free, with **zero custom audio
assets**, covering **every block in the game including modded ones**, and staying correct forever
because it is the game's own data. It is the PRINCIPLES rule *"never a magic constant, derive it from the
game's real data"* applied to **audio design**: the material palette belongs to the game, not the mod.
(The author's note on `:29-31` explains choosing the *place* sound over the *hit* sound: it reads as more
solid and distinct.)

Distance and category ride on **separate channels**, which is why they don't collide:

- **category** → pitch (2.0 jumpable / 1.5 blocked) **and timbre** (which block)
- **distance** → the engine's own attenuation, because the cue is positioned at the true hit point
- **direction** → the engine's own stereo pan

Note the asymmetry: the **OPEN chime is placed at a fixed 1-block offset**, so it always plays at
constant loudness and encodes *only* direction — there is no "distance to nothing" to report. Wall cues
are placed at the real (range-clamped) distance so proximity is audible. That's a deliberate,
well-chosen split, and it's quieter (0.6) than the wall cues (1.0) so "clear" never dominates the mix.

---

## 5. What's new versus our existing style-1 reference (A Hero's Call)

| | A Hero's Call (doc 02) | **United Minecraft** |
|---|---|---|
| Beams | 3, **player-relative** (L/F/R) | 3, **snapped to cardinals** (front/right/left of a cardinal facing) |
| Rear | not scanned | not scanned |
| Ray height | 2D tile grid (no height) | **feet + 0.1**, one horizontal ray |
| Categories | open / closed | **open / jumpable / blocked** — traversability, not presence |
| Category source | tile passability | **obstacle height vs the player's own step & jump reach** |
| Distance encoding | **pitch bucket** (3 steps) | **engine attenuation** (cue placed at the hit) |
| Pitch used for | distance | **category** |
| Timbre | one fixed radar chirp | **the hit material's own place sound** |
| Diff key | beam state | **absolute compass bearing** |
| Anti-spam | transition only | transition + per-bearing cooldown + global stagger + OPEN-first priority |
| Rendering | **flat 2D pan**, no spatializer | **engine-positioned** sound (see §7) |

Four transferable ideas, in order of value:

1. **Classify by traversability (walk / jump / turn), not by presence.** Actionable beats descriptive.
2. **Key radar state by world bearing, not beam index** (§3.2) — otherwise turning spams.
3. **Derive the cue's timbre from the game's own material data** (§4) — free, complete, maintenance-proof.
4. **Probe at the feet, not the eyes** (§3.1).

---

## 6. Tunable constants (all confirmed from source)

| Constant | Value | Where | Meaning |
|---|---|---|---|
| `DIRECTION_OFFSETS` | `{0, 90, 270}` | `NavRadarController.java:43` | Front / right / left, relative to the snapped cardinal facing |
| `PROBE_HEIGHT` | `0.1` blocks | `:44` | Ray height above the player's feet |
| `JUMP_HEIGHT` | `1.25` blocks | `:45` | Upper bound for JUMPABLE (vanilla jump apex) — **hardcoded, see §8** |
| step-up bound | `player.maxUpStep()` | `:152` | Upper bound for OPEN — **read live from the player** |
| `PLAY_INTERVAL_TICKS` | `3` (150 ms) | `:47` | Global minimum gap between any two cues |
| `BEARING_COOLDOWN_TICKS` | `10` (500 ms) | `:48` | Per-bearing re-cue cooldown |
| `JUMPABLE_PITCH` | `2.0` | `:50` | Pitch for a jumpable obstruction |
| `BLOCKED_PITCH` | `1.5` | `:51` | Pitch for a blocking obstruction |
| OPEN cue volume | `0.6` | `:168` | Quieter than wall cues — "nothing here" shouldn't dominate |
| wall cue volume | `1.0` | `:178` | |
| `navRadarRange` | **8** blocks, slider **4–16** step 1 | `UnitedMinecraftConfig.java:41`, `SettingsScreen.java:54` | Ray length, user-tunable and persisted to `united_minecraft.json` |
| tick rate | 20 tps | `ClientTickEvents.END_CLIENT_TICK` | Converts every tick constant above into real time |

---

## 7. The spatialization question — how this squares with our HARD RULE

[`../README.md`](../README.md) says radar cues must be **flat 2D pan, never 3D-positioned**. United
Minecraft **positions** its radar cues in the world (`SimpleSoundInstance` with explicit x/y/z at
`:167-168`, `:177-178` — and in fact *every* cue in the mod is positioned; there is no 2D path
anywhere). That is a real deviation, and it works here for reasons that **do not automatically
transfer**:

- Minecraft's sound engine is **OpenAL with plain distance attenuation and stereo panning — no HRTF by
  default**. There is no front/back binaural confusion to fall into, because there is no binaural
  rendering; a "front" cue simply plays centred.
- The cue's **category never varies with position** (pitch and timbre are fixed per category), so
  attenuation carries distance only — it never smears the category. Distance is *deliberately* encoded
  by attenuation instead of by a pitch bucket, which frees pitch for the category. Coherent design, not
  an accident.
- The OPEN cue sidesteps the double-encoding trap entirely by sitting at a **constant 1-block offset**
  (§4).

**When porting to an engine with HRTF / reverb / occlusion** (Unity spatial blend, FMOD/Wwise 3D busses,
Resonance/Oculus), **keep our rule**: render radar cues **flat** (`spatialBlend = 0`, dedicated 2D bus,
outside reverb/occlusion groups) and move the distance information onto an explicit channel you control
— per-cue volume computed by *your* law, or a pitch bucket like A Hero's Call. Otherwise you inherit
front/back confusion, zone reverb on your chirps, and occlusion muffling of a cue whose entire job is to
announce the wall that's doing the occluding.

> Rule of thumb: **positioned radar audio is only safe when the engine's simple 2D panner *is* the whole
> spatial path.** In Minecraft it is; in Unity / Unreal / RE Engine it is not.

---

## 8. Weaknesses to fix when porting (don't copy these)

- **`JUMP_HEIGHT = 1.25` is a magic number** (`:45`). It's the vanilla jump apex, but it's typed in, not
  derived — so it silently goes wrong under Jump Boost or anywhere the movement attributes differ. Per
  PRINCIPLES, derive it from the movement attributes (jump strength + gravity), or at minimum name a
  constant that documents where the value comes from. The file is inconsistent with itself here: the
  OPEN bound right next to it *is* read live from `player.maxUpStep()`. (Repo-wide, `NavRadarController`
  is the only file touching either value.)
- **One horizontal ray per direction — no width.** A doorway edge you'd clip with your hitbox, or a
  half-block gap, is invisible. A capsule/box sweep, or a 3-ray fan at ±half the player width, is the fix.
- **One height per direction.** An overhang at chest height with clear space at the feet reads as OPEN,
  and a hole in the floor is invisible to the radar entirely. (The mod covers that gap with a *separate*
  Fall Warning system — that separation of concerns is itself worth copying, §10.)
- **The rear is never scanned.** Fine while walking forward; if your target game makes backing up or
  strafing a primary movement, add the fourth bearing.
- **Fluids are transparent** (`ClipContext.Fluid.NONE`). Correct for Minecraft (you can swim); wrong
  anywhere a liquid is lethal or impassable — decide per game.
- **No shared cue/sound layer.** Every controller builds its own `SimpleSoundInstance` inline and the
  bearing formula is copy-pasted in three files. Our playbook requires the opposite (§6 of the root
  playbook): one `AudioCueService`, one adapter.

---

## 9. Porting notes (→ our mods)

### Adapter surface

```
Vec3   PlayerPosition()                // feet
float  HeadingDegrees()                // normalized to compass: 0 = North, clockwise
float  StepUpHeight()                  // live, from the character controller
float  JumpApexHeight()                // derived from jump velocity & gravity, NOT hardcoded
Hit    Raycast(origin, dir, maxDist)   // -> { hit, point, distance, topY, materialId }
void   PlayCue(soundId, volume, pitch, pan)
void   Speak(text, interrupt)
```

Everything in §3 is then **engine-free logic copied verbatim**.

### Getting `rise` (obstacle top) outside a voxel game

In Minecraft the hit block's collision box gives the top for free. In a continuous 3D world you rarely
get "the top of the thing I hit" from one raycast, so **replace the single ray with a short vertical
ladder** of rays at the same bearing:

| Ray | Height above feet | If it MISSES |
|---|---|---|
| A | `+0.1` | **OPEN** (nothing at all) |
| B | `stepUpHeight + ε` | **OPEN** (you'll auto-step over it) |
| C | `jumpApexHeight + ε` | **JUMPABLE** |
| — | all three hit | **BLOCKED** |

Three rays × three directions = **9 raycasts per tick**, which is nothing — and ray A missing lets you
early-out. This preserves the exact semantics on any engine.

- **Unity (BepInEx)** — `Physics.Raycast` on the walkable layer mask; step/jump from
  `CharacterController.stepOffset` and the controller's jump speed; material via the hit collider's
  `PhysicMaterial` or renderer material name → your own timbre table (Unity has no free "place sound"
  equivalent, so this is the one place you *do* need a small sound set). Cues on an `AudioSource` with
  `spatialBlend = 0` (§7).
- **RE Engine (REFramework)** / **Unreal (UE4SS)** — same ladder using the engine's raycast / line
  trace; material from the physics material / surface type, which both expose and which maps cleanly
  onto a timbre table.
- **Voxel / tile games** — you already have the top height: copy §3.1 as-is.

### Speech transport — a bonus finding worth reusing

United Minecraft reaches NVDA from **pure Java, with no JNA**, using the **FFM API**
(`java.lang.foreign`, Java 22+): `nvdaControllerClient.dll` ships as a jar resource, is extracted to
`%TEMP%\united-minecraft-nvda\` on first use, then bound with `SymbolLookup.libraryLookup` +
`Linker.nativeLinker().downcallHandle(...)` for `nvdaController_testIfRunning` / `speakText` /
`cancelSpeech`, with UTF-16LE wide strings allocated in an `Arena`
(`speech/NvdaController.java:63-78`, `:85-97`, `:100-141`).

**The same three-call shape is the PRISM contract.** Point that lookup at `prism.dll` and the identical
pattern gives us PRISM from any modern-Java host — cleaner than the JNA route used in our Project
Zomboid work. Worth folding into [`../../screen-reader-integration/`](../../screen-reader-integration/)
as the Java option.

---

## 10. Sibling cue systems — the **arbitration** is the lesson

The Navigation Radar is one of six spatial systems. They coexist without talking over each other for two
structural reasons worth copying:

**(a) Each system owns exactly one question**, so their cues are never redundant:

| System | Question it answers | Query | Gating | Cue |
|---|---|---|---|---|
| **Navigation Radar** (this doc) | "Can I keep walking front / right / left?" | 3 raycasts, feet height | transition + 500 ms/bearing + 150 ms global | material place-sound, pitched by category |
| **Hostile Radar** `HostileRadarController.java` | "Is something dangerous near *and able to see me*?" | AABB entity query (16 blocks) + per-entity eye→eye LOS raycast | per-entity `lastAlertTick`, re-alert after 100 ticks; queue drained 1 per 5 ticks | `NOTE_BLOCK_BELL` 0.7 + narration (name, distance, direction) |
| — its **melee alert** | "Is it in reach *right now*?" | same, distance ≤ `entityInteractionRange()` | plain 10-tick repeat, resets when clear | `NOTE_BLOCK_HAT` 0.6 pitch 1.4, **no narration** (deliberate) |
| **Fall Warning** `FallWarningController.java` | "Is the floor about to vanish where I'm *moving*?" | point projected along **velocity** (lookahead `clamp(speed×20, 3, 8)`), AABB fit test, then downward column scan | per-`(x,z)` column + 40-tick cooldown, cleared when you stop | `ANVIL_LAND` (will hurt) vs `NOTE_BLOCK_PLING` (safe) + narration |
| **Mining Radar** `MiningRadarController.java` | "Did valuable ore just become visible?" | cube scan (radius 8) every 10 ticks + `OreDetection.isExposed` | **alert-once-ever** `Set<BlockPos>` | `PLAYER_LEVELUP` 0.6 pitch 1.6 + narration |
| **Movement Assist** `MovementAssistController.java` | "I'm stuck — on what, and which way around?" | no raycast: AABB occupancy at the block ahead, at Y and Y+1, plus a downward scan; route side from the pathfinder | once per stuck episode (15 ticks under speed threshold) | **narration only, no sound** |
| **Surroundings Scanner** `SurroundingsScanner.java` | "What's directly in front of me?" (on demand) | eye raycast (6 blocks) + entity cone ±35° | none — request/response | **narration only, no sound** |

**(b) One owner per tick.** `AccessibilityTickHandler.java:188-225` is a single `if/else` chain: nothing
ticks while a screen is open, and Auto-Walk / Water Exit / Trail / Scanner lock-on / Combat Mode / Build
Mode each *take over* the tick when active — the Navigation Radar and Fall Warning run only in the final
`else`, i.e. **only while you are walking around manually** (`:222`, `:224`). A null player resets every
controller (`:99`). That's the playbook's "centralized accessible state, cues paused when a mode owns
the context" rule implemented as plain control flow.

Two more patterns worth lifting:

- **Cue vs narration is a deliberate split.** Fast, repeating, "is it still true right now" information
  (melee range) is a **sound with no words**, because speech would compete with the fight. Rare,
  identity-carrying information (which mob, which ore, how far) is **narrated**. Never both for the same
  fact.
- **"No X-ray" visibility** (`OreDetection.java:43-65`): a block counts as visible only if one of its 6
  faces has an air/empty-fluid neighbour **and** a `COLLIDER` raycast from the player's eye to that
  face's midpoint either misses everything or stops on the target block itself. Cheap, and it's the
  general answer to *"don't announce what the player couldn't possibly perceive"* — a rule our scanners
  should adopt wherever a game would otherwise let us cheat.

**Neither a polar sweep nor a continuous tone appears anywhere in this mod.** Every cue is a discrete
one-shot; there is no wall-sonification-style sustained bed (style 3) and no N-ray scan (style 2). It is
a pure style-1 mod.

---

## 11. Not recovered / open questions

- Nothing in the radar itself is unrecovered — it's 190 lines of readable source and every branch is
  covered above.
- Not read line-by-line here: the sibling systems' full internals (§10 is their algorithm summary, from
  a delegated read) and the mixin-based menu/narration layer.
- Untested by us: how the three anti-spam constants *feel* in play. They're the first thing to retune
  for a game with different movement speed — a faster game wants a shorter cooldown, a slower one a
  longer stagger.
