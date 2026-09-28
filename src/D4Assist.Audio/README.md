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
equal-power pan law as D2R's cues (`radar_synth.cpp:73`), a master volume, and a hard clip
(`radar_synth.cpp:100-101`). D2R itself plays one cue at a time through a priority queue
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
