# Dialogue (Response System) & the Director — detecting speech, panic & waves

> Two questions this answers: **(1)** can we tell when a character is speaking, so we can pause audio cues
> during dialogue (playbook §9)? **(2)** how do we detect panic events / hordes / Tank / finale stages to
> announce them? Read [vscript.md](vscript.md) first.
>
> Sources: [Response System](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Response_System),
> [Director_base.nut](https://developer.valvesoftware.com/wiki/Director_base.nut),
> [Director Scripts](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Director_Scripts),
> [L4D2 events (AlliedModders)](https://wiki.alliedmods.net/Left_4_Dead_2_Events).

## Dialogue / Response System (the "talker")

Rules are `concept → criteria → response` (Squirrel tables). A rule = `{name, criteria, responses}`:
- **criteria:** static (`["concept","TLK_HELP_ME"]`, `["distance",0,500]`) or a function receiving a
  `query` table of world facts (`incombat`, `intensity`, `health`, `healthfrac`, positions/distances…).
- **responses:** scene (`{scenename="scenes/Coach/MoveOn01.vcd"}`) or script (`{func=Fn}` → `(speaker,query)`).
- Functions: `rr_ProcessRules()`, `rr_GetResponseTargets()`, `RThen(target,concept,contexts,delay)`.

### ⚠ There is NO native hook for dialogue start/end
Confirmed absent on the Response System page: no `OnSpeechFinished`, no `player_speak`, no
`speakresponseconcept`, no `EstimateSpeechDuration`. The scene response's `onFinish` parameter is
**documented as "currently not implemented"** — Valve left the speech-finished hook unimplemented. The
only near event, `player_talking_state(player,istalking)`, is **human VOIP**, not AI character dialogue.

**Implication for pausing cues during dialogue (playbook §9):** we cannot key off a clean "speaking now"
signal from VScript. Options, in order of preference — **all still to be validated**, this is an open
question, not a settled solution:
1. **Native-side (our plugin):** watch the scene/choreo entity (`scripted_scene` / `CSceneEntity` /
   `CBaseFlex` flex weights) or query whether a survivor is currently emitting a `.vcd` — the plugin is
   in-process and can read this even though VScript can't. *Most promising; needs RE confirmation.*
2. **Closed-caption tap:** the caption system knows what's being said; if the plugin can read the active
   caption token it doubles as both "is speaking" and the text. (Off-screen captions aren't
   screen-reader-visible, but the plugin reading the caption entity/queue is a different path — unverified.)
3. **Heuristic:** if we can read the response line's string, estimate duration from length and suppress
   cues for that window. Crude fallback.

Do **not** build cue-pausing on the Response System's own callbacks — they don't fire on speech end.

## The Director — panic, hordes, Tank, finale

### `director_base.nut` (the shipped base script — short)
Defines the default `DirectorOptions <- { finaleStageList=[], OnChangeFinaleMusic="", MaxSpecials=2 }`.
Key mechanism: **`GetDirectorOptions()` walks a delegation chain**
`DirectorScript → MapScript → LocalScript → ChallengeScript`, so an addon overrides options at a more
specific level (via `delegate`) and inherits the rest — this is how `director_base_addon.nut` layers on
without replacing the base. `OnChangeFinaleStage(from,to)` updates music per finale-stage index.

### Director callbacks (script-side)
- **`OnBeginCustomFinaleStage(int num, int type)`** (LocalScript) — called on **each scripted panic event
  or finale-stage change**; `type` ∈ `PANIC`(0)/`TANK`(1)/`DELAY`(2)/`SCRIPTED`(3). **The main script hook
  for announcing Director state changes.**
- `OnChangeFinaleStage(finaletype, stagetype)`, `GetCustomScriptedStageProgress(defvalue)` (0–1 during
  `FINALE_CUSTOM_SCRIPTED`), `GetNextStage()` (ChallengeScript), `EndScriptedMode()` (round ending).

### `Director` singleton methods (callable from VScript)
`Director.ResetMobTimer()`, **`Director.PlayMegaMobWarningSounds()`** (explicit "horde incoming" signal —
usable to anticipate a wave), `Director.ForceNextStage()`.

### `DirectorOptions` tempo fields (config, not live telemetry)
`PanicForever`, `PanicWavePauseMin/Max`, `MobSpawnMinTime/MaxTime`, `MobMinSize/MaxSize`, `MegaMobSize`,
`SustainPeakMinTime/MaxTime`, `RelaxMinInterval/MaxInterval`, `IntensityRelaxThreshold`, `TankLimit`,
`PreTankMobMax`. **Note:** the wiki states there is **no readable "current Director state" table/convar** —
only these config options and the callbacks above. So live tempo must come from **events**, not polling.

### The robust way to detect panic/wave/Tank/finale: game events
More reliable than owning the Director script (events fire regardless):
- **`create_panic_event(userid)`** — a panic event was created (start of a panic horde).
- **`panic_event_finished`** — panic over.
- **`tank_spawn(userid, tankid)`** / `tank_killed` / `tank_frustrated`.
- Finale sequence: `finale_start` → `finale_rush`/`finale_escape_start` → `finale_vehicle_incoming` →
  `finale_vehicle_ready` → `finale_vehicle_leaving` → `finale_win`/`mission_lost` (see
  [scripting-hooks-and-events.md](scripting-hooks-and-events.md)).

## Recommendation

- **Panic / waves / Tank / finale:** hook the game events (`create_panic_event`, `panic_event_finished`,
  `tank_spawn`, the finale sequence); optionally corroborate with `OnBeginCustomFinaleStage` and
  `Director.PlayMegaMobWarningSounds()`. This is solid and needs no Director-script ownership.
- **Pausing cues during character dialogue:** unresolved from VScript — resolve it **natively** in the
  plugin (scene/flex entity or caption tap). Tracked as an open question in `STATUS.md`.
