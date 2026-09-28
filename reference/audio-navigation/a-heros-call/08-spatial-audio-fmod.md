# 08 — Spatial Audio (FMOD): Panning, Attenuation, Occlusion

Source: `Sound/Ian/FModSoundContext.cs`, `FModEventWrapper.cs`, `I2DSoundSource.cs`,
`I3DSoundSource.cs`, `IMovableSound.cs`, `SoundConfig.cs`, `MapSoundHandler.cs` (driver, Doc 00 §4).

Two parallel audio paths: **flat stereo pan** (radar cues — discrete L/C/R) and **true 3D positioned**
(everything in the world — objects, terrain, ambient), the latter with distance attenuation, wall/door
occlusion, and indoor/outdoor colouring. Both are FMOD Studio *events* whose DSP/mix behaviour is
authored in the FMOD project and *parameterized from C#*.

---

## 1. Stereo panning for radar (discrete) — the `"Panning"` parameter

Radar cues don't use 3D position; they set a single FMOD event parameter named **`"Panning"`** to a
discrete value:

```csharp
// FModSoundContext.GetRadarSound
public I2DSoundSource GetRadarSound(string name, float panningValue) {
    FModEventWrapper ev = InnerGetEvent(name);
    if (ev == null) { DiagSO.ESay("unable to find radar sound " + name); return null; }
    ev.mEventInstance.setParameterValue("Panning", panningValue);   // 0=L, 1=C, 2=R
    return ev;
}
```

`ReactiveRadar` builds its beams with `0f / 1f / 2f` (Left/Front/Right; Doc 02). The actual L↔R balance
curve for those three values is authored inside the FMOD event on the `"Panning"` parameter. So in
FMOD-land, "panning" is just an automation parameter you map to a panner; in a non-FMOD port you'd map
`{0,1,2}` to stereo balance `{-1, 0, +1}` (or use 3 mono sounds routed L/C/R).

---

## 2. 3D positioning — listener & sources

### Listener (once per frame, from player pose) — `MapSoundHandler`

```csharp
V2 fwd = MyMath.GetUnitVectorFromCompassDegrees(facingDeg);   // heading -> unit vector
V3 pos = VectorHelpers.MyTransform(new V3(0,-0.0001f,0), facingDeg, playerPos.AsV3()); // ~player pos
var attr = new FMOD.Studio._3D_ATTRIBUTES {
    forward  = { x = fwd.X, y = fwd.Y },
    up       = { x = 0, y = 0, z = 1 },     // mFPUpVector = (0,0,1)
    position = { x = pos.X, y = pos.Y },
};
mSC.mFModStudio.setListenerAttributes(0, attr);
```

The world is 2D (XY); `up` is `+Z`. (The tiny `-0.0001` Y offset avoids a degenerate zero vector in
`MyTransform`.)

> **MAJOR: the actual spatializer is Oculus/Resonance, not FMOD's built-in panner.** The
> `FModSoundContext` constructor loads two FMOD low-level plugins — **`OculusSpatializerFMOD.dll`** and
> **`gvraudio.dll`** (Google VR / Resonance Audio) — and sets `UseOculus = true` **by default**
> (`FModSoundContext.cs:172-182`), then loads banks from the `...\Banks\Desktop Oculus\` variant
> (`FModSoundContext.cs:739`; see §5). So the **binaural HRTF panning and distance attenuation for every
> 3D world sound** is performed by the **Oculus spatializer effect placed on the events inside the FMOD
> Studio project** — driven by the listener/source `_3D_ATTRIBUTES` set here — **not** by FMOD's stock
> 3D panner and **not** by any C# math. Practical port note: to reproduce AHC's actual sound field you
> need an HRTF spatializer (Unity: Oculus Audio / Steam Audio / Resonance; not the default linear panner),
> fed the same listener pose. **The one exception** is the reactive radar, which bypasses all of this and
> uses a flat 2-D manual `"Panning"` parameter (`0/1/2`, §1 / Doc 02) — it is deliberately *not*
> spatialized.

### Source position — `FModEventWrapper.Update`

```csharp
public void Update(V3 position) {
    var attr = new _3D_ATTRIBUTES { position = position.AsFModV3() };
    mEventInstance.set3DAttributes(attr);
}
```

`_3D_ATTRIBUTES { VECTOR position, velocity, forward, up }`. Sources set only `position`. Positions are
tile-units; tile emitters sit at tile centers `(X+0.5, Y+0.5)`.

> **There is NO Doppler anywhere.** The **listener** attributes set only forward/up/position, never a
> velocity (`MapSoundHandler.cs:217-235`); world emitters set only position; even the combat-TTS 3D
> sound passes an explicit **zero velocity** (Doc 09 §3, `CombatEvent.cs:299-302`). `SoundConfig`
> exposes a `ListenerDopplerFactor` but it is **never applied** to FMOD. Don't port a Doppler model —
> the game deliberately has none.

---

## 3. Distance attenuation

> **CORRECTION (verified against the decompiled source):** an earlier
> pass presented `SoundMinDistance`/`SoundMaxDistance`/`SoundRolloffFactor` as the values driving
> FMOD's real 3D min/max distance for events. **They are not.** Grepping the whole decompiled tree for
> `set3DMinMaxDistance`/`Set3DMinMaxDistance` finds exactly **one** real call site, and it isn't wired
> to this config at all.

`Sound/Ian/SoundConfig.cs` fields (`SoundMinDistance=1f, SoundMaxDistance=15f, SoundRolloffFactor=1f,
ListenerRolloffFactor=1f, MuteGain=0`) are exposed as `GameConfig` properties
(`GameEngine/Ian/GameConfig.cs:77-147`), populated from a config file (`GameConfigParser.cs:59-68`), but
they are **never applied to any FMOD event**:

- **`SoundMinDistance` and `SoundRolloffFactor`** are read in `RPG/Ian/RPGStartup.cs:256-263`,
  `RunTest2()`, which computes `SoundLogic.GetMuteDistanceBasedOnMuteGain` (`Sound/Ian/SoundLogic.cs:13-16`)
  and (only when `G.Debug`) *speaks* `"The default mute distance is {x}"`. **Correction to the earlier
  pass:** `RunTest2` is **not** an uncalled dead function — it **is** invoked at startup
  (`RPGStartup.cs:151`); it's just that its output is a `G.Debug`-gated spoken line and it **never wires
  the value into any sound's attenuation**. So the *values* are consumed (to print a number), but nothing
  downstream applies them to FMOD.
- **`SoundMaxDistance` has NO reader at all** — not even `RunTest2`. It's populated from config and then
  referenced nowhere.
- The **only real** `set3DMinMaxDistance` call in the entire decompiled source is
  `Sound/Ian/CombatEvent.cs:303` — `lowLevelSound.Channel.set3DMinMaxDistance(1000f, 1000f)` — hardcoded
  to a huge distance so the **combat TTS voice-over channel is explicitly non-attenuating** (§8 below).
  This has nothing to do with `SoundConfig`.
- `FModSoundContext.Get3DSound(name, lifetime, minDistance=-1f, maxDistance=-1f, ...)`
  (`FModSoundContext.cs:477-483`) accepts per-cue min/max distance and reverb-flag parameters in its
  **signature**, but the **body ignores every optional parameter** and just returns `InnerGetEvent(name)`
  — vestigial/unused knobs in this build.
- **Conclusion**: real attenuation comes entirely from the **rolloff curve authored in the FMOD Studio
  project** per event (bank data). It is **not recoverable from the C# source** — do not port
  "min/max ≈ 1/15" as if it were an active clamp; it's a config value that exists on disk but is dead
  code for events.

| Parameter | Value | Status |
|-----------|-------|--------|
| `SoundMinDistance` | `1f` | read by `RunTest2` (startup, `G.Debug`-gated print only); never applied to any FMOD event |
| `SoundMaxDistance` | `15f` | **no reader at all** — populated from config, referenced nowhere |
| `SoundRolloffFactor` | `1f` | read by `RunTest2` (same print); never applied to any FMOD event |
| `MuteDistance` (`FModEventWrapper.cs:96`) | `13f` | **live, but unrelated** — see below |

`FModEventWrapper.MuteDistance` is a **hardcoded literal getter** (`public float MuteDistance => 13f;`),
not derived from `SoundConfig`, and — despite the name — it is **not a "mute beyond this distance"
cutoff**. Its **only** uses are `MapSoundHandler.cs:420,441` as `MuteDistance * 2f` (≈ **26 tiles**): the
**tile-search radius for SHARED-TERRAIN sounds only** — the code searches out to that radius for the
nearest tile whose terrain matches the sound, and mutes the sound (`mEventInstance.setVolume(0)`) when
**no matching-terrain tile is found within the radius**. It has nothing to do with the *listener*-to-
*source* distance. **Positioned / object sounds are NOT muted this way** — they are muted by
**obstruction** instead: `ObstructionHelpers` sets `Mute` C#-side when occlusion depth `>= 1`
(`ObstructionHelpers.cs:23-27`; §4). `SoundSequence.MuteDistance` (`SoundSequence.cs:51`) just forwards
`GetCurrentSound()?.MuteDistance ?? 0f`, inheriting the same hardcoded `13`. Do not present `MuteDistance`
in the same breath as `SoundMinDistance`/`SoundMaxDistance` — it's an unrelated, unused-by-FMOD constant.
`SoundLogic.GetMuteDistanceBasedOnMuteGain` (formula below) is only ever called by the dead `RunTest2`:

```csharp
// SoundLogic.GetMuteDistanceBasedOnMuteGain
return (minDistance / muteGain - minDistance) / rolloffFactor + minDistance;
```

---

## 4. Occlusion (wall/door muffling) — the navigation-critical part

The obstruction *amount* is the raycast thickness from Doc 03 (`GetWallAndDoorDepth`). It is applied in
two steps by `ObstructionHelpers.PositionSoundForObstruction` (Doc 03 §4):

1. **Hard mute** if `depth >= 1f` (≥1 tile of wall/door → silent).
2. Otherwise push `depth` (0..1) plus indoor flags into the event:

```csharp
// FModEventWrapper.SetObstructionAndIndoorStatus
mEventInstance.setParameterValue("Obstruction",   obstruction);          // 0..1 -> low-pass muffle
mEventInstance.setParameterValue("PlayerIndoors",  playerIndoors ? 1 : 0);
mEventInstance.setParameterValue("SoundIndoors",   soundIndoors  ? 1 : 0);
```

The FMOD project's DSP chain reads `"Obstruction"` to drive a low-pass filter (and likely volume), and
the two `*Indoors` booleans to pick reverb/EQ.

> **CORRECTION:** `IMovableSound.SetDirectLowPass(gainHF, gain)` / `RemoveDirectLowPass()`
> (`FModEventWrapper.cs:176-182`) are **empty no-op stubs** in this decompiled build — they are not a
> real lower-level LPF path, contrary to what the earlier pass implied. The `"Obstruction"` FMOD
> parameter is the **only** active occlusion mechanism; there is no separate direct-LPF fallback to
> port.

So the **occlusion model is: raycast tile thickness → {mute if ≥1 tile, else 0..1 muffle amount} → FMOD
`"Obstruction"` parameter.** Simple, cheap, and reproduces "muffled through the wall, silent through two
walls." (Whether `"Obstruction"` drives a low-pass filter, a volume dip, or both is authored in the FMOD
DSP chain — not visible in C#.)

---

## 5. Volume-category snapshots, VR bank selection & ducking (missing from earlier pass)

- **4 permanent volume-category snapshots** — `Snapshot Music/SFX/Radar and Beacon/Dialogue MASTER`
  (`FModSoundContext.cs:191-202`), each **created and `Play()`ed once at init** and exposing a
  `"User Audio Setting"` FMOD parameter (0–100) read/written by `MusicVolume`/`SoundVolume`/`RadarVolume`/
  `DialogueVolume` properties (`FModSoundContext.cs:95-141,985-1001`), defaulted to `85`. This — not
  distance attenuation — is the real user-facing volume-category system, and it's unrelated to §3 above.
  **The four sliders set the snapshot *parameter*; they do NOT call `setVolume` on any bus.** The
  snapshots drive the buses **inside the FMOD Studio project**; the C# side never touches bus volumes.
  In fact the **only literal `getBus(...)` reference anywhere in C#** is
  `getBus("bus:/Radar and Beacon MASTER/Accessibility Sounds/Combat TTS")`
  (`FModSoundContext.cs:547`), used solely to route the combat-TTS channel group (Doc 09 §3) — not for
  volume mixing. Zone reverb is likewise snapshot-driven: a per-zone reverb snapshot is started and
  released via a **`StopSnapshot`** mechanism (`MapSoundHandler.cs:328-336`; the main-zone equivalent is
  `RunMainSnapshot`, `FModSoundContext.cs:778-785`).
- **`UseOculus` is hardcoded `true` in the constructor** (`FModSoundContext.cs:182`), regardless of
  actual VR hardware. It selects the bank folder — `Data for RPG\Adventures\A Heros Call\FMOD
  Studio\Banks\Desktop\` vs `...\Desktop Oculus\` (`FModSoundContext.cs:739`) — and is only later
  overridden by a debug hotkey (`AHM.cs:257`) or a settings file (`AHM.cs:302`) / menu
  (`AudioSettingsMenu.cs:64`). Practical effect: **by default the game loads the Oculus bank variant even
  on a plain desktop run.** `OculusSpatializerFMOD.dll` and `gvraudio.dll` (Google VR audio) are loaded
  unconditionally as FMOD low-level plugins at construction (`FModSoundContext.cs:172-181`), present even
  outside VR.
- **No automatic ducking during speech/menus.** Verified: nothing in the engine dips the ambient/radar
  bed volume when TTS or dialogue speaks. The only automatic silencing is the per-object
  `IAudibleObject.SilentInMenus` flag (`IAudibleObject.cs:9`), applied in
  `ObstructionHelpers.PositionSoundForObstruction(..., silentInMenus, ...)` only when the caller passes
  `true` **and** `MapAndPlayer.AreWeInMenu` is true — `AreWeInMenu` is `inMenu` computed at
  `RPGGameLoop.cs:88`: `!(world.Scene.Current is RPGExploring)`, true for *any* non-exploring scene
  (menu, dialogue, shop, etc.). The radar itself has no automatic mute-in-menu of its own —
  `RadarEnabled` is a manual player toggle only (`FPExploring.cs:674`, F-key). Playbook §9's "pause cues
  during dialogues" rule is a **design recommendation for new mods**, not something AHC itself already
  does uniformly — implement it explicitly rather than assuming the engine does it for you.

---

## 6. Interfaces (contracts to re-implement)

```csharp
// I2DSoundSource (radar/UI) : ISound, IDisposable
bool Mute; bool IsLooping; bool IsDisposed; float Panning; float Pitch;
void Play(); void Stop(); void PlayAndDispose(); void PlayAndCallback(Action cb);
bool IsCompleted(); double GetPercentCompleted();

// IMovableSound (anything 3D-positioned & occludable)
string OriginalFile; bool Mute; float MuteDistance;
void Update(V3 position);
void SetDirectLowPass(float gainHF, float gain = 1f); void RemoveDirectLowPass();
void SetObstructionAndIndoorStatus(float obstruction, bool playerIndoors, bool soundIndoors);

// I3DSoundSource : IMovableSound (+ IsLooping, Volume, SecondarySound, …) — world emitters
```

`IAudibleObject` (Doc 05 §3) is the world-object side: `RenderPosition`, `HeardThroughWalls`,
`SilentInMenus`, and the sound ids `FocusSound / RadarSound / ConstantSound / ConstantEvent /
SequenceSoundStrings`.

---

## 7. FMOD playback API surface used

```csharp
FMOD.Studio.System.create(out mFModStudio);
mFModStudio.initialize(1024, INITFLAGS.LIVEUPDATE, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
mFModStudio.getEvent(path, out EventDescription ed);
ed.createInstance(out EventInstance inst);
inst.start();  inst.stop(STOP_MODE.ALLOWFADEOUT);
inst.set3DAttributes(attr);  inst.setParameterValue(name, value);
inst.setVolume(v); inst.setPitch(p);
mFModStudio.setListenerAttributes(0, listenerAttr);
mFModStudio.update();                  // pumped on the sound thread every 10 ms
```

### Threading model (important for mods)

- Game thread enqueues sound commands into a `BlockingCollection<Action>`.
- A dedicated **sound thread** ticks every **10 ms**: drains commands under `lock(mLockObject)` then
  calls `mFModStudio.update()`. All FMOD calls happen on this thread / under the lock.
- Copy this pattern: never call FMOD from arbitrary threads; marshal onto one audio thread.
- More precisely: a **single serialized audio thread** drains the `BlockingCollection` command queue,
  and its `OnTick` runs bookkeeping and then exactly **one** `mFModStudio.update()` per tick
  (`FModSoundContext.cs:207-313`).

### Sound object lifecycle (what gets created, reused, or recreated)

Not every sound is made the same way — porting the mix faithfully means matching these lifetimes:

- **2-D one-shots** (`PlayPlain2D`, `FModSoundContext.cs:340-358`): cached **per name** in
  `m_currentlyPlaying2D`; on replay the cached instance is **reused or recreated**, then
  `PlayAndDispose`d.
- **Footsteps** (`:402-428`): **recreated per call** (not cached).
- **Looping terrain / object beds**: created **once, muted** (`StartConstantSoundMuted`, `:674-690`; init
  block `:258-292`), then **muted/unmuted and repositioned every frame** rather than started/stopped —
  cheaper than re-triggering a loop each frame.
- **Radar instances**: created **once per session**, not per map. The `ReactiveRadar` constructor builds
  all fifteen event instances (5 cues × 3 pans) via `GetRadarSound`, and `MapSoundHandler.MapChanged`
  only constructs it `if (mRadar == null)` (`MapSoundHandler.cs:98-106`); every later map change just
  calls `ResetRadar()`, which clears beam history and touches no sound instance.

### Shared-terrain dual crossfade

For a terrain type audible from **two directions at once**, `MapSoundHandler.cs:431-510` finds the **two
nearest matching tiles** and **crossfades their two emitter volumes by inverse distance** — a **manual
volume law applied on top of** FMOD's own 3D positioning, so the bed doesn't pop as the player moves
between two patches of the same terrain. (This is the same shared-terrain search whose radius is
`MuteDistance * 2` from §3.) If you port terrain beds, replicate this two-source inverse-distance
crossfade or the ambient bed will jump discontinuously.

---

## 8. Porting notes (non-FMOD engines)

- **Radar pan**: 3 mono variants per cue routed L/C/R, or one sound + a stereo-balance set to
  `{-1,0,+1}`. You do not need FMOD for the radar.
- **3D world sounds**: use the engine's spatial audio (Unity `AudioSource.spatialBlend=1` +
  `rolloffMode`). Do **not** carry over AHC's `SoundConfig` `1`/`15`/`13` numbers as if they were the
  live curve — per §3 they're dead config in this build; author your own rolloff curve (or reuse them
  only as an untested starting guess) and cull using your own mute/culling policy.
- **Occlusion**: compute `depth` with your raycast (Doc 03 port), then `if depth≥1 mute; else set a
  low-pass cutoff scaled by (1-depth)`. Unity: `AudioLowPassFilter.cutoffFrequency`. This single rule
  is what sells "behind a wall" — no separate direct-LPF fallback is needed (§4).
- Feed an `indoors` boolean to pick a reverb preset per listener/source if available.
- Consider a volume-category snapshot system (Music/SFX/Radar/Dialogue, §5) for user-facing volume
  sliders, separate from any distance attenuation.
- Keep all audio mutation on one thread.
