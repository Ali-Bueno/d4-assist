# D4Assist.Audio

Audio side of the ported Diablo II Resurrected radar (`research/d2r-radar-port.md`). BCL only, WinMM
`waveOut*` via P/Invoke — no NuGet or native audio dependency. No game or process access.

## Cues (exact port of `d2r-access/src/radar_synth.cpp`)

| Kind | Sound | Recipe | Provenance |
|---|---|---|---|
| `Exit` | bright "tin", ~1100 Hz | decaying sine + 2.7x harmonic, 45 ms decay | `radar_synth.cpp:50-52,75-77` |
| `Blocked` | dull "tock", ~260 Hz | decaying sine + 1.5x harmonic, 25 ms decay | `radar_synth.cpp:50-52,75-77` |
| `Door` | two 180 Hz knocks | the same knock struck twice, 60 ms apart, 12 ms decay | `radar_synth.cpp:52,94-98` |
| `Open` | soft filtered-noise "breath" | **D4 change:** band-passed noise sweeping up (900→2400 Hz) for up rays, down (600→220 Hz) for down rays, steady 1200 Hz left/right; 180 ms; same seed + swell | `radar_synth.cpp:80-92` (base) |
| unexplored tail | two ~900 Hz pips, 30 ms gap | rides after any passable (Open/Exit) cue, 30 ms gap first | `radar_synth.cpp:58-59,104-118` |

`RadarCueSynth.Synthesize` returns mono `float[]` samples — deterministic (fixed noise seed) and
device-free, so it is directly unit-testable. Pan and the low-pitch flag are *not* baked in here; they
come from `RadarDirectionModel` so a single recipe serves every direction/frame.

## Direction model

Ported from the D2R radar's ray table (`radar.h:28-55`, `Axes`/`ScreenAxes`/`Rays`): each
`(RadarFrame, RadarRay)` maps to a screen vector. Pan = `0.9 * screenX` (flat stereo pan only — see
`reference/audio-navigation/README.md`'s hard rule against 3D-positioning radar cues). A ray whose
screen Y points down is pitched down (`DownPitch` = 0.86 for tones, the breath instead rises/falls — user
feedback 2026-09-28: D2R's darker-breath variant was indistinguishable) so Up and Down stay distinguishable even when both pan center. Cardinal
frame: Left/Right pan, Up/Down pitch. Diagonal frame: all four rays get both a pan and a pitch component,
per the same table. `ScreenVector` exposes that table so `D4Assist.Radar.RadarRays` derives its engine rays from
it instead of keeping a second one.

## Mixer

`AudioMixer` sums concurrent mono voices into interleaved stereo PCM16: per-voice gain, the same
equal-power pan law as D2R's cues (`radar_synth.cpp:73`), a master volume, and a soft limiter (linear up
to 0.8, then easing to full scale) instead of D2R's hard clip (`radar_synth.cpp:100-101`).

**Loudness (D4 change, 2026-09-28):** the user found the cues drowned out by D4 combat at D2R's level, so
`RadarCuePlayer.LoudnessBoost` = 4 (+12 dB) scales every radar cue. `RadarVolume` 1 means that boosted level.
A single cue still peaks below the limiter knee, so only overlapping cues are ever compressed. D2R itself plays one cue at a time through a priority queue
(`radar_audio.cpp` `CueQueue`, urgency-ordered: obstacle > exit > floor); this mixer generalizes that to
real polyphony (`MaxVoices` = 8) so overlapping directions are never dropped, but keeps the same
urgency order for voice stealing once full.

## Output

`WaveOutAudioOut` streams through `winmm.dll` (P/Invoke), matching D2R's own transport
(`wave_out.h`/`radar_audio.cpp`): 48 kHz stereo (`radar_audio.h:9`), triple-buffered on a dedicated
background thread so playing a cue never blocks the caller, and it retries opening the device every 2 s
if none is present (`radar_audio.cpp:30`) instead of throwing.

## Diagnostics

`D4Assist --sound-test` (`src/D4Assist/SoundTestMode.cs`) speaks each cue's Spanish name, then plays it
once per direction (direction announced first) through the app's own PRISM `ISpeechSink`, so a blind
user can learn the sounds without the game running.

## Beacon loops

Looping positional sources, ported from D2R's ground-item loops (`ground_items.h:34,44,53`, `ground_items.cpp:38-41`,
`item_audio.cpp:24`, `cmake/GroundItems.cmake:9-31`). `SpatialLoopPlayer.Update` takes `LoopSource`s (key, sound,
offset in screen right/up, `LoopProfile`) and starts/retargets/stops `AudioMixer` loop voices by key. Gain =
`NearGain * (1 - d/Reach)^Falloff` (silent at reach), flat equal-power pan from the screen-right component, 0.94
pitch when the source is screen-down, 80 ms smoothing. Loops have their own `MaxLoopVoices` = 8 budget, separate
from radar cues. Profiles: `Quest` (D2R values) and `Beacon` (user request: louder, reach 60 vs 27).
`Sounds/quest-item.wav` (embedded in the assembly, loaded by `SpatialLoopPlayer.LoadSounds`) is D2R's quest loop (Diablo IV audio, reuse approved), trimmed to 700 ms like D2R:
`ffmpeg -i "quest item.ogg" -map_metadata -1 -vn -af "apad=whole_dur=0.7,atrim=duration=0.7,afade=t=in:d=0.005,afade=t=out:st=0.68:d=0.02" -ac 1 -ar 48000 -c:a pcm_s16le quest-item.wav`

The other WAVs are D2R's loops converted by its own recipe (`d2r-access/cmake/GroundItems.cmake`):
`ffmpeg -i <ogg> -map_metadata -1 -vn [filter] -ac 1 -ar 48000 -c:a pcm_s16le -fflags +bitexact -flags:a +bitexact <wav>`.
- `corpse.wav`: filter `-af aformat=sample_fmts=flt,volume=42dB,alimiter=limit=0.9:level=false`.
- `scroll.wav`: the same filter at `volume=24dB`.
- `chest.wav`, `wooden-door.wav`, `metal-door.wav`, `stone-door.wav`: no filter.

Sources: `loot/{corpse,scroll,chest}.ogg` and `exploration/{wooden,metal,stone} door.ogg`. The `Container` profile
(doors, chests, readables) is D2R's `AudibleRadius` 30 subtiles with a squared fade (`ground_items.cpp:39`).
`LoopSource.Priority` decides who gets the loop budget first. `AudioMixer.Paused` silences everything: it drops
one-shot cues and holds loops, and is used during cutscenes.

## Guide ping

The exploration guide's sound: D2R's target-beacon ping (`d2r-access/src/beacon_audio.cpp:105-141`, `docs/beacon.md`):
a 620 Hz, 80 ms blip (`GuidePingSynth`) repeating every 160 ms (route end) to 1200 ms (>= 40 subtiles left)
linearly (`beacon.h:39-40`, `beacon.cpp:95-100`), panned and rear-pitched (0.94) from the waypoint's screen offset
with `SpatialLoopPlayer.Pan/Pitch`. Subtiles convert to world units with `D2rScale.D4UnitsPerSubtile` (0.6, the
same guess as the quest loop reach), so the slowest cadence starts at 24 units. `GuidePingPlayer` schedules it
on a 25 ms tick and plays through `AudioMixer.PlayGuide`, a 2-slot budget separate from radar voices and loops
(neither steals from the other). Level 0.15 like D2R, times `RadarCuePlayer.LoudnessBoost` (same reason as the radar).
`PlayArrived` is D2R's arrival confirmation (`beacon_audio.cpp:117`): the ping rising a fifth then an octave,
60 ms gaps, centred; the next ping waits until it ends.

## Friendly-NPC cue

D2R's entity cue for friendly NPCs (`d2r-access/src/entity_cue_synth.cpp:12-18,62-94`, `entity_cues.cpp:41-43`,
`entity_cues.h:14`): an 84 ms wooden tap (850 Hz partials + low-passed noise, `NpcCueSynth`) repeating every 450 ms
at the range edge down to 150 ms at contact, linearly, silent beyond `RangeUnits` (default 15, D2R's 15 subtiles
taken as D4 units). Pan is the unit screen `right` (D2R `entity_cue_synth.cpp:73`); screen-down lowers the pitch
to 0.80 (`RearPitch`, line 13). `NpcCuePlayer.Aim(right, up, distance)` / `Silence()` mirror the guide ping and
play through `AudioMixer.PlayEntity` (own 2-slot budget, silenced by `Paused`). Enemy swoops are not ported.

## Settings knobs (per-cue volume, repeat speed, preview)

- `RadarCuePlayer.GainOf(kind, ray)` replaces the old single volume; a cue with gain <= 0 is skipped before it reserves a scheduler slot.
- `GuidePingPlayer.Volume` / `ArrivalVolume` / `Rate` and `NpcCuePlayer.Volume` / `Rate`: `Rate` divides the repeat interval; volume 0 keeps the schedule but plays nothing.
- `LoopSource.Rate` / `LoopVoice.Set(..., rate)`: repetitions per natural cycle (1 = back to back, 0.5 = equal silence between, 2 = overlapping); implemented with overlap-added play heads, clamped to `LoopVoice.MinRate`..`MaxRate`.
- `AudioMixer.PlayPreview` / `StopPreview`: one dedicated voice that also plays while `Paused`; `CueRepeat.Render` builds a repeating-cue preview buffer.
