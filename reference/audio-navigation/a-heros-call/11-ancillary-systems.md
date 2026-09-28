# 11 — Ancillary systems adjacent to navigation

Systems that shape a blind player's experience but sit outside the core radar/announcement/beacon
pipeline documented in Docs 02–09. (Passive terrain enter/exit sounds and the indoor/outdoor ambient
crossfade are covered in Doc 04, not repeated here.)

---

## 1. OS-level accessibility — FilterKeys

`FirstPerson/Ian/FilterKeysChanger.cs` + `FilterKeys.cs`. On entering exploration
(`FPExploring.Enter`, `FPExploring.cs:112`) the game calls `FilterKeysChanger.Enable()`, which invokes
Win32 `SystemParametersInfo` (`user32.dll`, `SPI_SETFILTERKEYS = 51`) to force-enable Windows FilterKeys
with `dwFlags = 3`, `iBounceMSec = 1`, all other delays zeroed (`FilterKeysChanger.cs:51-58`). Before
overwriting, it reads the player's **current** OS FilterKeys via `SPI_GETFILTERKEYS = 50` and persists
it to a safety file (`SaveFilterKeys`, `FilterKeysChanger.cs:44-50,61-70`).

- **Revert paths**: `FilterKeysChanger.Revert()` on losing window focus while in the explore state
  (`FPLogic.HandleOnLoseFocus`, `FPLogic.cs:204-211`) and re-`Enable()` on regaining it
  (`FPLogic.HandleOnGainFocus`, `FPLogic.cs:213-220`); `RevertAndRemoveSafetyFile()` on clean shutdown
  (`FPLogic.HandleShutdown`, `FPLogic.cs:222-230`, and `RPGGameLoop.cs:53`).
- **Crash recovery**: `FilterKeysChanger.RestoreFilterKeysFromFileIfExists()` runs at startup, before the
  adventure is even loaded (`RPGStartup.cs:61`), reading back the safety file and reverting if one is
  found from a previous session that didn't shut down cleanly. `FilterKeysChanger.FilePath` is set to
  `<DataPath>/Settings/OriginalFilterKeys.dat` (`RPGStartup.cs:248`).
- `G.GameConfig.SkipFilterKeys` (set from a command-line flag OR'd in at `RPGStartup.cs:57`) short-circuits
  every one of the above methods to a no-op.

**Modding warning:** a mod that alters input handling, or that crashes without going through
`HandleShutdown`/`RestoreFilterKeysFromFileIfExists`, can leave the player's OS-level keyboard
FilterKeys settings altered. Any mod that hooks the input pipeline or the process lifetime should
either preserve this save/restore pair or replicate it.

---

## 2. NPC navigation & incidental audio presence

NPCs run on a completely separate movement stack from the player — useful as passive orientation cues,
not to be confused with the player's own beacon A* (Doc 07).

- **Brains**: `FPBrain` (`FirstPerson/Ian/FPBrain.cs`) is an abstract `GoalMachine`
  (`GoalMachine.cs`) driving a command list (`Commands: List<ICommand>`,
  `StartNextCommand`/`StartCurrentCommand`, `FPBrain.cs:19-51`); `RPGBrain`
  (`RPG/Ian/RPGBrain.cs:3-26`) is the concrete implementation used by map agents, owning a
  `SteeringManager` and starting in `IdleGoal.Singleton` (`GoalBase`-derived, `IdleGoal.cs`).
- **Commands**: `ICommand : IGoal` (`ICommand.cs`) plus `CommandBase` (`CommandBase.cs`) give
  `m_agent`/`m_world` access to concrete commands — `SeekPositionCommand.cs`, `WaitCommand.cs`,
  `ScriptCommand.cs` — each a `GoalBase` state entered/exited by the brain.
- **Pathing**: `SeekPositionCommand.CalcPath` (`SeekPositionCommand.cs:80-100`) calls the same
  `GraphSearch.AStar(map, from, to, includeDiagonals:false)` used by the beacon system (Doc 07 §3), then
  wraps the result in a `SteeringPath` followed by `SteeringManager.FollowPath` — fully independent of
  the player's beacon/beeps.
- **Audible presence while moving** — `SeekPositionCommand.Process` (`SeekPositionCommand.cs:35-78`):
  - Terrain **enter/exit sounds** fire when the NPC's tile terrain changes (`SeekPositionCommand.cs:58-68`),
    the same mechanism as the player's own terrain sounds (Doc 04).
  - Positional **footsteps** play on a throttled timer via `m_world.SC.Play3DFootstep(...,
    m_agent.Position, ...)` (`SeekPositionCommand.cs:75`).

Both are 3D-positioned at the NPC's own location, so a moving NPC is audible to the player as an
orientation cue (footsteps approaching/receding, doors opening) independent of any accessibility feature.

---

## 3. NPC/object landmark sound sequences and the dialogue pause policy

`Sound/Ian/SoundSequence.cs` implements the "find the blacksmith by ear" mechanic: a positional,
**looping** sequence of sounds cycling through `IAudibleObject.SequenceSoundStrings`
(`IAudibleObject.cs:17`), each play scheduled after `IAudibleObject.CalcSequenceDelay()`
(`IAudibleObject.cs:21`, implemented at `MapScriptObject.cs:192`) via `GlobalEvents.SubscribeTimer`
(`SoundSequence.cs:133-153`). `GetNextSound`/`PlayNextInLoop` round-robin the configured sound list
(`SoundSequence.cs:61-73,133-145`).

**The pause/resume policy is NOT in `Sound/Ian` — it lives in the RPG goal state machine**, driven by
dialogue focus messages:

- `RPG/Ian/TalkingWithPlayerGoal.cs`: on `Enter`, stops the agent's sequence sounds
  (`m_brain.World.SC.StopSequenceSounds(m_brain.Agent)`, line 14); on receiving
  `FPMessages.PlayerStoppedTalkingToYou` transitions to `JustFinishedTalkingWithPlayerGoal` (line 21).
- `RPG/Ian/WaitForAMomentAfterGettingFocusGoal.cs`: also stops sequence sounds on `Enter` (line 22); once
  `m_secondsToWait` elapses, restarts them (`StartSequenceSounds`, line 30) and resumes the command queue.
- `RPG/Ian/WaitForPlayerToLoseFocusGoal.cs`: stops sequence sounds on `Enter` (line 16); on
  `FPMessages.LostFocus` restarts them (line 23).
- `RPG/Ian/JustFinishedTalkingWithPlayerGoal.cs`: a **hard-coded 6-second cooldown**
  (`c_secondsToWait = 6.0`, line 11) after dialogue ends before sequence sounds resume (line 26-29).

So landmark loops mute the instant a dialogue/focus interaction starts and only resume after these
goal-specific delays — not immediately on dialogue close.

---

## 4. Scripting API surface relevant to navigation aids

Map authors build custom navigation/orientation aids through the JS scripting API, not just through
Portals/AreaTriggers (Doc 06 §2):

- `FirstPerson/Ian/FPScriptAPI.cs`:
  - `Face(float degrees)` (`FPScriptAPI.cs:193`) — snaps the player's facing to an absolute compass
    bearing.
  - `SetPosition(x, y)` (`:206`, tile-centered, adds `+0.5f` to both) and `SetExactPosition(x, y)`
    (`:211`, raw coordinates) — script-driven teleport, resets the radar afterward.
  - `PlaySoundAt(string sound, float x, float y, bool heardThroughWalls)` (`:425`) — a one-shot,
    arbitrarily-positioned 3D sound, optionally audible through walls.
- `RPG/Ian/RPGScriptAPI.cs`:
  - `LoadMap(string mapName, float x, float y)` (`:377`) — a scripted map jump distinct from `Portal`
    (Doc 06 §2): switches the client map and places the player at `(x+0.5, y+0.5)` directly, no portal
    rectangle/interact prompt involved.
  - `StartConstantSound(MapScriptObject mso, string name)` / `StopConstantSound(MapScriptObject mso)`
    (`:333`, `:338`) — starts/stops a looping sound bound to any map object, a scripting-level equivalent
    of the built-in landmark sequence (§3) that a map author can attach to arbitrary objects.

These are the primitives map authors have for custom nav aids; `AreaTrigger` JS callbacks
(`OnEnter`/`OnExit`/`OnProcess`, Doc 06 §2) are the trigger side that typically invokes them.

---

## 5. Cinematics gap

`FirstPerson/Ian/PlayingCinematic.cs`: plays a single 2D voice clip (`mSoundObject`, `Enter`,
lines 21-32). `GetCaption()` returns `TString.Empty` (line 18) — **no text caption is ever shown for a
cinematic**. It is skippable via cancel or activate key (`GHM.IsCancelKey`/`IsActivateKey`, line 38) and
auto-advances (`mWorld.Scene.Up()`) if the sound asset fails to load (line 24-27, `mSoundObject == null`).

**Accessibility note**: there is no text fallback if the audio fails to load or the TTS/audio pipeline is
unavailable — a cinematic with a missing sound asset silently skips itself with zero player-facing
information (relies on `mSoundObject == null` → `Scene.Up()`, not an announcement).

---

## 6. Notable absences (verified by sweep)

- **No quest waypoint/arrow/objective-pointer system.** `RPG/Ian/Quest.cs` and `QuestStage.cs` are
  purely textual/state bookkeeping (`StageNumber`, `Description`, `Success`/`Failure` flags parsed from
  data, `QuestStage.cs:22-33`) — no position, no direction, no distance field anywhere on either class.
  All spatial guidance in the game is **author-placed** Beacons (Doc 07) or POIs (Doc 05); a "guide me to
  my current objective" mod needs new plumbing linking quest state to a beacon/POI, since none exists.
  (There is a purely **visual** on-screen compass needle in `Platform/Ian/MGRender.cs:1152-1168`
  (`DrawHud`, `compassBack`/`compassNeedle` textures) that rotates with `facingInDegrees` — it shows
  player orientation only, has no accessible/speech output, and has no notion of a quest objective.)
- **No day/night cycle, no weather system, no water/drowning/swimming system** anywhere in the
  decompiled source (confirmed by a project-wide sweep for `TimeOfDay`, `Rain`, `Storm`, `Underwater`,
  `IsSwimming`, `Weather` — zero matches). Games or ports that assume these systems exist must add them
  from scratch; nothing to hook or read from this codebase.

---

## 7. Speech/caption commit loop

`GameEngine/Ian/GameLoopLogic.cs`, `RunLoops` (lines 13-51). App-wide backstop, not specific to first-person
exploration: after running the fixed-timestep simulation loop(s), it commits the pending speech/caption
title:

```csharp
// GameLoopLogic.cs:47-49
if (ent.GLS.ForceTitleCommit || pendingMessages == ent.Speech.PendingMessages || ent.Speech.PendingMessages > 10)
{
    ent.Speech.CommitTitleChange(ent);
}
```

`pendingMessages` was captured before the loop(s) ran (line 23); the commit fires once the pending-message
count **stabilizes** (no new messages queued this tick) or **exceeds 10**, whichever comes first. This is
the app-wide speech/caption timing backstop that also governs the CustomNVDA title/braille mode (Doc 09).

---

## 8. Visual accessibility toggles (peripheral)

`FirstPerson/Ian/FPLogic.HandleCommonKeys` (`FPLogic.cs:160-172`):

```csharp
if (input.IsPressed(FPInput.ToggleColorMode))       // config key "toggle_color_mode"
{
    ent.Window.CurrentColorMode.MoveNext();
    TranslateSO.TSay(ent.Window.CurrentColorMode.Name);
}
if (input.IsPressed(FPInput.ToggleViewMode))        // config key "toggle_view_mode"
{
    ent.Window.CurrentViewMode.MoveNext();
    TranslateSO.TSay(ent.Window.CurrentViewMode.Name);
}
```

Both cycle to the next enum value (`Window.CurrentColorMode`/`CurrentViewMode`) and **speak the new
mode's name** — a low-vision accommodation (color mode) and a display accommodation (view mode), each
self-announcing so a screen-reader user gets feedback identical to a low-vision user.

---

## 9. Exploration command table

Command → key config name mapping is registered in `FirstPerson/Ian/FPKeys.cs:12-42` (`AddInput("name",
FPInput.Value)`); handlers live in `FPExploring.HandleKeys` (`FPExploring.cs:544-659`) and
`FPLogic.HandleCommonKeys` (`FPLogic.cs:160-` , §8 above). One line per command, first-person exploration
only unless noted:

| Config key | Handler behavior |
|---|---|
| `fp_announce_position` | Speaks floored `"X, Y"` player position (`FPExploring.cs:607-610`). |
| `fp_announce_region` | Speaks the current region's friendly name (`:603-606`). |
| `fp_announce_direction` | `AnnounceDirection()` — plays cardinal **direction SOUNDS only** (N/E/S/W positional cues), no speech in the default path (`:827-846`). |
| `fp_snap_left` / `fp_snap_right` | Rounds facing to the nearest 90° in that rotational sense, replays the direction sound, resets the radar (`SnapLeft`/`SnapRight`, `:775-825`). |
| `fp_move_forward/backward/left/right` | Held keys; accumulate a `travel` vector from `FPState.Front/Back/Left/Right` (`:621-636`). |
| `fp_turn_left` / `fp_turn_right` | Held; continuous rotation (`:349-354`). |
| `fp_scan_forward` | `RunForwardScan` → `AnnouncePOIs`, a **spoken** POI list, cancels prior speech by default (`:757-761`, `:534-542`). |
| `fp_scan_left` / `fp_scan_right` | `RunLeftScan`/`RunRightScan` → `AnnounceSidePOIs`, which **only cancels speech — no text is spoken** (`:763-773`, `:529-532`); passive side audio cues (radar/positional sounds) are unaffected. |
| `overhead_map` | Opens `FPMapOverview` scene (`:595-598`). |
| `beacon_menu` | Opens `FPBeaconMenu` scene (`:571-574`). |
| `announce_beacon` | Speaks distance + 8-point direction to the active beacon (Doc 07 §2, `:734-748`). |
| `disable_beacon` | Clears the active beacon (`:575-578`). |
| `toggle_radar_enabled` | Toggles the reactive radar system (Doc 02) (`:563-566`). |
| `toggle_auto_forward_scan_enabled` | Toggles automatic forward scanning on movement (`:567-570`). |
| `fp_interact` | `AttemptInteract` — interacts with the focused POI object, or steps through the current portal if no POI is focused (`:678-713`). |
| `fp_examine` | `AttemptExamine` — speaks the focused object's `Description`, or its name if no description is authored (`:715-732`). |
| `save_game` / `load_game` | Opens save/load menu (`:555-561`). |
| `translation_menu` / `toggle_translation` / `debug_language` | Localization/dev keys, not accessibility-relevant. |
| `editor_zone_menu` / `editor_create_zone` | Editor-only, dev build. |

RPG-mode extra (not in the base `FPInput` enum — registered separately in `RPG/Ian/RPGKeys.cs:9` as
`RPGInput.FPRunForward`):

| Config key | Handler behavior |
|---|---|
| `fp_run_forward` | Held; `RPGExploring.HandleRunning` (`RPGExploring.cs:43-51`) adds `FPState.Front` to `travel` **and** unconditionally multiplies `speedMod *= 2.5f` on line 50 — the 2.5× speed multiplier applies whether or not the key is held (see Doc 04 for the full analysis of this bug). |

**`grid_announce_tile` is a different system entirely** — it belongs to the turn-based combat grid mode
(`GameEngine/Ian/GeneralKeys.cs:86`, `GameInput.GridAnnounceTile`), not first-person exploration. Do not
confuse it with the `fp_*` table above when porting.
