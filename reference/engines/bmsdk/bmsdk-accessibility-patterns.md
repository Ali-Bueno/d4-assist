# BmSDK — accessibility patterns for Arkham City / Arkham Knight

> The applied doc: how a screen-reader + audio-cue mod is actually built on this platform. It assumes
> [architecture](bmsdk-architecture.md), [mod authoring](bmsdk-mod-authoring.md) and
> [performance](bmsdk-performance.md). Behaviour specs (what to say, when to interrupt) stay in
> [`ui-accessibility/`](../../ui-accessibility/) — this doc is *where the data lives and how to reach it*
> on these two games.

---

## 1. Speech transport — the blocking decision (READ FIRST)

| | Arkham City | Arkham Knight |
|---|---|---|
| Process bitness | **x86 (32-bit)** | **x64** |
| PRISM prebuilt available? | **No** — the release ships x64 + arm64 only | Yes |
| Transport | **unresolved — see below** | `[DllImport("prism.dll")]` from the mod script, PRISM next to the SDK |

Arkham Knight is a solved problem: the process is x64, `[DllImport]` is available to mod scripts with no
ceremony (the SDK P/Invokes Detours itself, and `global using System.Runtime.InteropServices;` is applied
assembly-wide), so the standard playbook path applies —
[prism-prebuilt.md](../../screen-reader-integration/prism-prebuilt.md), undecorated x64 exports, ship only
`prism.dll`.

**Arkham City needs a decision before any speech code is written.** Options, best first:

1. **Compile PRISM for x86 from source.** Keeps one API, one adapter, one mental model across both games
   and the rest of the playbook. PRISM is C++23/CMake; an x86 MSVC configuration is plausible but
   unverified — this is the thing to spike first. Cost: a build investigation, once.
2. **Out-of-process bridge.** A small x64 helper process hosting PRISM, driven over a named pipe from the
   x86 game. Robust and reuses the prebuilt DLL, but adds a second process to ship, a lifetime problem
   (start/stop with the game), and latency on every utterance — bad for a mod where interrupt timing
   matters.
3. **Tolk x86.** Prebuilt, 32-bit, and exactly what [`source-l4d2`](../source-l4d2/) already does for the
   same reason. It contradicts the global "PRISM for everything, no Tolk fallback" rule, so it is the
   user's call, not ours — but it is the only option that is known-working today.

> **Constraint that shapes all three:** mod scripts are Roslyn-compiled against a **fixed reference set**
> — `Basic.Reference.Assemblies.Net100` + `BmSDK` + `MoreLinq` + `Tomlyn`. **You cannot add a NuGet
> package to a mod script.** Any speech library must therefore be reached by **P/Invoke** (which needs no
> reference), or be vendored into `BmSDK.dll` itself (a fork). This rules out managed wrappers such as
> `TolkDotNet` or `System.Speech` unless we patch the SDK.

**Pipeline smoke test that needs none of the above:** SAPI is reachable with BCL-only reflection
(`Type.GetTypeFromProgID("SAPI.SpVoice")` + `InvokeMember("Speak", …)`). It is not a screen reader and
must not ship, but it lets us validate hooks, diff-gating and menu reading on AC on day one while the
transport question is settled.

## 2. Architecture for the mod

Standard playbook layering, mapped onto this SDK:

```
Scripts/ArkhamAccess/
  Boot.cs                 [Script]  lifecycle only: init, teardown, hotkeys
  Adapter/IGameAdapter.cs           Speak / PlayCue / PlayerPose / Raycast / ReadFocusedUi
  Adapter/AcAdapter.cs              #if AC   — AC class names, AC Trace signature
  Adapter/AkAdapter.cs              #if AK   — AK class names, AK Trace signature
  Services/SpeechService.cs         PRISM P/Invoke + queue + interrupt policy + diff-gate
  Services/AudioCueService.cs       AkWwise.StartCustomAudioEvent wrapper
  Services/UiReader/*.cs            widget archetypes (see §3)
  Services/Navigation/*.cs          wall sonification / radar (see §5)
  State/AccessibleState.cs          centralized state: menu, region, combat, dialogue
```

Rules that are non-negotiable **on this platform specifically**:

- **Every entry point is individually try/catch'd.** An escaping exception calls `Environment.Exit(1)`
  and closes the game (architecture §7). One broken menu reader must not be a crash.
- **No hardcoded offsets, ever.** BmSDK already carries every fragile RVA. Everything we need comes
  through the generated API. When BmSDK is re-pinned to a new game build, our mod should need zero edits.
- **Per-game code lives only in the adapter.** The API is source-compatible but not binary-compatible
  (offsets differ; `Trace`'s signature differs; AC/AK dialogue and HUD stacks differ) — see §8.

## 3. Menus and UI — Scaleform (`GFxUI`), the biggest chunk of work

Both games render their UI in **Scaleform GFx**. There is **no universal "focused item" or "caption"
property at framework level** — confirmed absent in both games. That is the central design constraint:
we cannot write one reader that covers every screen for free, so we follow the playbook's
[generic UI strategy](../../ui-accessibility/generic-strategy.md) — classify **widgets**, not screens —
and build the archetype readers on the primitives below.

### 3.1 Reading text out of a movie

| API | Use |
|---|---|
| `GFxUI.GFxMoviePlayer.GetVariableString(path)` / `GetVariable(path) -> FASValue` | **primary** text read by ActionScript path, e.g. `_root.menu.item0.label` |
| `GFxUI.GFxObject.GetText()` / `GetString/GetFloat/GetBool/Get(member)` | read a clip you already hold |
| `GFxUI.GFxObject.GetElementString(i)` / `GetElementMemberString(i, member)` | walk list/menu items **by index** — the backbone of a list reader |
| `GFxUI.GFxObject.Invoke(member, args)` / `ActionScriptString/Int/Void(method)` | call arbitrary ActionScript when reading isn't enough |
| `GFxUI.GFxObject.TranslateString(str, ctx)` (static) | resolve a localization key that came back raw |
| `GFxUI.GFxInteraction.GetFocusMovie(controllerId)` | **"which movie is active right now"** — the entry point for the whole reader |
| `GFxUI.GFxMoviePlayer.SetFocus/HasFocus/OnFocusGained/OnFocusLost` | movie-level focus only (coarse) |

`FASValue` is a tagged union (`.Type`, `.B`, `.N`, `.S`); `.S` is the string to speak.

AK additionally exposes int-typed variants (`GetVariableInt`/`SetVariableInt`) and byte-array helpers.

### 3.2 Game-side widgets that already carry text (use these before path-walking)

- `BmGame.RGFxMovieUI.OnFocus(int Id)` / `OnLostFocus(int Id)` / `OnClicked(int Id)` — **per-widget focus
  events with an Id**. This is the closest thing to a focus-change notification in the game; redirect
  these as read-only taps and you get "selection changed" for free on the screens that use them.
- `BmGame.RGFxMovieUI.GetGridButtonContext(int ButtonId)`, `AddGridItem(int id, FString name, …)` —
  grid/list item text (character/skin select).
- `BmGame.RGFxMovie.SetToolTip(FString)`, `FrontMost_SetPrompt(int id, FString tag, int brightness)` —
  tooltips and button prompts, as setters we can tap.
- `BmGame.RGFxMovieHudExtendable` (**AC**) — `SetMainPrompt` / `SetPrompt(id, label, alpha, …)`,
  `SetInfoText(FString, justify, int)`.
- `BmGame.RGFxMovieModularHud(Base)` (**AK**) — the modular HUD hub: `CreateHudExtension`,
  `RegisterModuleClass`, `IsModuleVisible`, `SetPrompt`, `SetInfoText`, orchestrating ~18 independent
  `RHudModule*` movies (Batmobile, Radar, Targets, Objectives, Scanner, DetectiveMode…).
- AK-only screens with explicit text + index, all worth dedicated readers:
  `RGFxMovieMissionWheel.UC_SetGoal(int goalId, FString goalStr)` (Most Wanted wheel),
  `RGFxMovieRadialGadgetSelect.CheckGadgetWheel() -> int` (gadget wheel),
  `RGFxMovieWorldMap3D.GetSelectedItemIndex() -> int` (**one of the very few native "selected index"
  accessors in either game**), `MGFxWidgetOverlayMain/Screen.NavUp/Down/Left/Right()` (WBID overlay).
- `BmGame.RDynamicMenu` — a debug menu whose rows are explicit `RenderItem(FString Label, …)` /
  `ItemB/I/F/L(FString Label, out value, …)`. Dev-only, but **the most accessible-shaped API in the SDK**
  and an excellent reference for what a good reader looks like.

### 3.3 Non-Scaleform fallback

`Engine.HUD.DrawText(FString, Vector2 pos, Font, Vector2 scale, FColor)` is a catch-all: redirecting it
gives every string the game draws through Canvas, with a screen position to sort by. Noisy, but it is the
safety net for anything the GFx readers miss. `Engine.HUD.AddLocalizedMessage/Message/AddConsoleMessage`
carry explicit strings too.

### 3.4 Recommended reader strategy

1. On `OnEnterMenu()`, and on each `RGFxMovieUI.OnFocus` tap, ask `GFxInteraction.GetFocusMovie(0)` which
   movie is live and map it to an **archetype** (list, grid, slider row, tab bar, dialog, prompt strip).
2. Read the focused row via the game-side accessor if one exists (§3.2), else by path/index through
   `GetElementMemberString` (§3.1).
3. Diff-gate against the last spoken value; **context change → `interrupt = true`**, incremental value
   change → speak only what changed (playbook §8).
4. Anything unmatched falls through to a generic movie tree-walk, and failures are caught per-widget so
   one bad screen degrades locally.

## 4. Text, subtitles and objectives

- **Localization**: `GameObject.Localize(FString section, FString key, FString package)` — a static on
  every UObject wrapper, the only true localization primitive. AK adds
  `ParseLocalizedPropertyPath(FString)`.
- **Subtitles (shared)**: `BmGame.RSubtitle` (`Duration`, `SubtitleCharacterName`,
  `TArray<FSubtitleCue> Subtitles`), `Engine.EngineTypes.FSubtitleCue { FString Text; float Time }`,
  `BmGame.RSubtitleLookup`. `Engine.PlayerController.IsShowingSubtitles()` / `SetShowSubtitles(bool)` —
  **force subtitles on so the text exists to capture.**
- **Dialogue pipeline is a full swap between the games** — branch in the adapter:
  - **AC**: `BmGame.RDialogueManager.GFXSetSubtitle(FString)` / `GFXClearSubtitle()` push subtitle text
    straight to Scaleform; `Engine.RDialogueEvent.LocalizedSubtitles`. Redirect `GFXSetSubtitle` and you
    have every line of dialogue.
  - **AK**: Wwise-based. `Engine.AkDialogue.GetDialogueSubtitle(line, event, actor, out float dur)`,
    `AkDialogueEvent.GetSubtitle()`, `AkDialogueVoice.GetSubtitle(langExt)` + `.VoicePortrait`,
    `AkDialogueComponent.IsSpeaking()/IsSpeakingLine()`.
  - Shared: `BmGame.RBMSpeechManager` (`PlaySpeech/PlayBark/PlayEmote`, `CurrentVocal` with
    `bSpeechIsValid`, `DialogueLine`, `bSubtitles`, `bCinematic`) — the hook for "is an NPC talking",
    which is also the signal to duck our audio cues (playbook §9).
- **Objectives**:
  - **AK** has real runtime state: `RGameInfo.CurrentObjectiveString`, `RGameInfo.CheckObjectiveActive(FString)`,
    `RPlayerController.GetCurrentObjectiveMissionName()`, and
    `RHudObjectivesContainer.QueueObjectiveMessage(time, title, desc, orgDesc, arrowType, …)` with a live
    `QueuedObjectivesArray` — the best pollable "what am I supposed to do" source in either game.
  - **AC has none of that.** `RHudExtensionObjectives` exposes only `AddObjective`/`FlushObjective` with
    no readable state, and objective text flows one-way through Kismet. On AC we must **tap the setters**
    and maintain our own objective model. `RSeqAct_Objective.GetObjectiveName()/GetSubObjectiveName()`
    exist but are design-time Kismet, not pollable.
- **Item/gadget descriptions do not exist as data** in either game — no `ItemDescription`/`FlavorText`/
  `TooltipText` anywhere; that text is baked into Flash assets. `RInventoryGadget.TutorialText` is the
  only adjacent field (AK adds `HUDDescription` on highlight labels). Expect to read gadget names from
  the `EBatmanGadgetList` enum and get descriptions only via the GFx text of the screen showing them.

## 5. Player state, world queries and navigation

### 5.1 Player state (all property reads — cheap, poll every tick)

- Position/facing: `Engine.PlayerController.GetPlayerViewPoint(out Vector3, out Rotator)` is the single
  best call (`RPlayerController` doesn't redeclare `Location`/`Rotation`); `Engine.Actor.Location` is a
  `ref System.Numerics.Vector3` and `Rotation` a `ref Rotator` on the pawn. Camera:
  `Engine.Camera.GetCameraViewPoint(out Vector3, out Rotator)`, `GetFOVAngle()`.
- **State machine**: `BmGame.RPlayerController.KismetState : KismetBatmanStates` — a byte enum, 18 values
  on AC (`KBS_Walking` … `KBS_Dead`), 21 on AK (adds `KBS_Batmobile`, `KBS_RadioMode`,
  `KBS_HangingOffGargoyle`). **This is the mod's primary context signal**: diff it every tick and
  announce transitions.
- `RPlayerController.GetSelectedMovementState() -> GroundMovementState`
  (`GMS_Walking/RunningMove/StealthMove/LedgePerch/BatarangAim/SilentPredator/Investigating/ChargeStrike`).
- Combat: `RPlayerControllerCombat.IsInCombat(bool)`, `IsInCombatForDialogue()`, plus combo counters
  (`ComboSize`, `CurrMaxComboSize`, `CombosStarted`, `CombosAchieved`).
- Gliding: `RPawnPlayer.bGliding`, `bCanCapeGlide`, `PlayerGlideSpeed`,
  `RPlayerController.CalculateCapeGlideAltitude()`.
- Grapple: `BmGame.RGrappleGun` — `CanAimGrapple()`, `GetGrappleTargetPosition() -> Vector3`,
  `GetNumAvailableGrapplePoints()`, `QueueGrapple(float)`; AK adds `GetAimedGrappleTargetPoint()`,
  `GetGrappleEndLocation()`. **This is the traversal-accessibility goldmine** — a blind player can be
  told how many grapple points are available and where the aimed one is.
- Gadgets: `RPlayerController.GetCurrentGadget() -> RInventoryGadget`, names from `EBatmanGadgetList`.
- Health: `Engine.Pawn.Health` / `HealthMax`.
- **Detective mode has no on/off accessor** — only `IsDetectiveModeJammed()` /
  `SetDetectiveModeJammed(bool,bool)`. Infer the mode from `KismetState` / `GMS_Investigating`, or tap
  whatever toggles it.

### 5.2 World queries (for wall sonification and radar)

- `Engine.Actor.FastTrace(end, start, boxExtent, bTraceBullet) -> bool` — **identical in both games**,
  the cheap occlusion check. Use it for the sonification bed.
- `Engine.Actor.Trace(out hitLoc, out hitNormal, end, start, bTraceActors, extent, out FTraceHitInfo, int extraFlags) -> Actor`
  — full hit info. **The signature differs**: AK replaces `bool bTraceActors` with
  `Component.ECollisionFilter TraceFilter` (`COF_NoCollision/COF_Level/COF_LevelGeometry/…`), and
  `FTraceHitInfo` grows 36 → 40 bytes. Adapter-level branch.
  - There are **no named `TRACE_*` constants** in the SDK; `ExtraTraceFlags` is a raw int bitmask. If we
    ever need it, it must be a named constant with a documented UDK source (PRINCIPLES §4) — not a
    bare literal.
- `GetBoundingCylinder(out radius, out height)`, `GetComponentsBoundingBox(out FBox)` — obstacle extents
  for distance falloff.
- **There is no batch trace and no actor-radius query.** `AllActors`, `CollidingActors`,
  `VisibleCollidingActors`, `ActorsInRadius` are UnrealScript **VM iterator opcodes, not `UFunction`s**,
  so no generated SDK can ever expose them. An N-ray fan is N calls — budget accordingly
  ([performance](bmsdk-performance.md) R6). This pushes us naturally toward the playbook's
  [wall-sonification](../../audio-navigation/wall-sonification/) style rather than actor enumeration.
- **Navmesh is fully exposed** and is the interesting alternative: `Engine.NavigationHandle` —
  `ActorReachable(Actor)`, `PointReachable(Vector3, …)`, `FindPath(…)`, `GetNextMoveLocation(out, float)`,
  static `GetPylonFromPos(Vector3)`, and
  **`GetAllPolyCentersWithinBounds(pos, extent, out TArray<Vector3>)`** — the closest thing to
  "walkable points near me" in either game, and a far better basis for a *path* radar than raycasting.
  `GetCurrentEdgeType() -> Pylon.ENavMeshEdgeType` classifies traversal (walk/jump/climb/ladder), which
  is directly speakable. Batman-specific nodes: `RNavigationHandle`, `RNavigationManager`,
  `RBMPathNode_128Step/FenceJump/GapJump/Ladder/SpecialMove` (AK adds `RBMPathNode_WallClimb`,
  `RVehicleNavVolume`).

### 5.3 Objective markers and threat awareness

- `BmGame.RObjectiveMarker` — `SetMarkerPosition(Vector3)`, `SetHudTargetVisible(bool)`,
  `CheckWaypointLogic()`, plus `Priority`, `IsMarkerActive`, `AppearOnCompass`, `AppearAsBatsymbol`.
  Tapping this gives us a spoken/audio waypoint beacon.
- **AC**: `RHudExtensionTargets` — `SetCompassAngle(…)` and parallel arrays `abMarkersVisible`,
  `abMarkerAngles`, `avMarkerWorldPositions` — a ready-made compass model to sonify.
- **AK**: `RGFxMovieWorldMap3D` — `CalcRoute(…)`, `SetUserMarker(…)`, full parallel marker arrays
  (`Icon_TypeName/InstanceName/X/Y/Z/Rotation/Flags`), map camera state, `SatNavRoute`. AC has **no**
  minimap/world-map class at all.
- **Neither game has fast travel** (confirmed absent) — traversal is grapple/Batmobile only, which raises
  the bar for navigation assistance.
- Interaction prompts: `BmGame.RInteractInterface.GetPrompt()/GetUpperPrompt()/IsButtonPrompt()/IsActive()`,
  `RInteractableItem.GetPrompt()` + `Prompt`/`Range`. And the canonical one:
  **`BmGame.RHUDPrompt`** — `SetInstructions(FString, bool)`, `SetHighPriorityInstructions(FString)`,
  `AddLine(FString text, EControlIcon icon, bool main, …)`, struct `FHelpLine { FString Line; byte Icon; int Alpha }`,
  and `enum EControlIcon` (~38 values: `CI_UI_A/B/X/Y`, `CI_Interact`, …). That icon enum maps cleanly to
  speakable button names — this is the "press X to…" source, and it's near-identical in both games.
- Detective/forensics targets: `BmGame.REvidence` (`EvidenceName`, `ScannedInfoData`,
  `ScannedInfoDataTitle`), `RScanSolution.GetObjectName()/CheckVisibility()` (Riddler puzzles),
  `RForensicsInvestigator.Items/ExtraItems` (AC). AK adds `RHudModuleDetectiveMode.SetEvidenceDetail/SetNPCDetail`,
  `RHudModuleScanner.FindDeepScanEvidence/StartScanning`, `RPhysicalEvidenceBase/CrimeScene/Trail`.
- Enemies: `BmGame.RBMAIManager.GlobalControllerInfoList : TArray<FControllerInfo>` (global AI registry)
  and `RBMCombatManager` (`CombatantsJoining`, `GetNumCombatants()`, `NumCombatantsWithGuns/WithKnives`).
  There is no `GetAllEnemies()`, but the registry plus `FastTrace` line-of-sight gives us a predator-mode
  threat radar — arguably the single most valuable feature for these games.

## 6. Audio cues — Wwise, not UE3 SoundCue

Both games use **Audiokinetic Wwise**. The stock `Actor.PlaySound(SoundCue, …)` /
`CreateAudioComponent(…)` exist but are vestigial — the game content doesn't use them and they need a
cooked SoundCue asset. **Use the Wwise path.**

| API | Use |
|---|---|
| **`Engine.AkWwise.StartCustomAudioEvent(...)` (static)** — AC: `(Actor parent, FString eventName)`; AK: `(FString eventName, EGlobalAudioSourceID)` | **plays a Wwise event by name** — the mechanism for our accessibility cues. Paired with `StopCustomAudioEvent(out FAkSoundHandle)` |
| `Engine.AkWwise.SetCustomGlobalParameter(FString, float)` / `SetCustomSourceParameter(Actor, FString, float)` / `SetCustomGlobalState/Switch` | by-name RTPC / state / switch control |
| `Engine.AkComponent.StartAudioEvent(AkEvent, …) -> FAkSoundHandle` | 3D event attached to the owner |
| `Engine.AkComponent.StartAuxAudioEvent(AkEvent, Vector3 position, …)` | event at an explicit **world position** — spatial cues |
| `AkComponent.SetSourceSpatial(Vector3 pos, Rotator orient, EAkComponentUpdate)` | move a playing cue in 3D |
| `AkComponent.SetSourceParameter(AkParameterName, float)` | per-instance RTPC — Wwise's volume/pitch equivalent (**named parameters, not raw floats**) |
| `AkComponent.EnableFalloffEnhancement(float radius, bool easeIn)`, `SetPeakAudibilityRadius(float)` | attenuation shaping for proximity cues |
| `Engine.Actor.AttachNewAkComponent() -> AkComponent`, `StartAudioEvent`, `StopAudioEvent(out handle, bool quickStop)` | same surface mirrored on Actor |

**The catch:** `StartCustomAudioEvent` plays events that exist **in a loaded soundbank**. Brand-new audio
of our own would have to be packaged into a Wwise bank (`AkBank` / `SeqAct_AkBankLoader`), which is a
content pipeline, not a code problem. Two practical routes:

1. **Reuse existing game events** for cues — a large, well-attenuated, already-spatialized library, and
   it keeps the mod's audio in the game's own voice. Enumerate candidates by grepping `AkEvent` assets.
2. If we need synthetic tones (the wall-sonification bed genuinely wants a continuous, pitch-mapped
   tone), consider generating them **outside** Wwise entirely — the mod is .NET and can drive its own
   audio output — at the cost of losing the game's spatialization and ducking. Decide per feature.

**Ducking during dialogue/menus** (playbook §9) — AK has a purpose-built API:
`Engine.AkDialogue.EnableForceDucking(bool fullyDuck, FString dlgTag)` / `DisableForceDucking(…)` /
`IsForciblyDucked()`. AC has none; drive it manually via `AkWwise.SetMusicParameter` /
`SetGlobalAudioState` and our own state tracker. User-facing volume sliders in both:
`BmGame.RPersistentOptions.SetVolumeMusic/SFX/Dialogue(float)` — the template for adding an
"accessibility cue volume" option.

## 7. Input

- **Use `Script.OnKeyDown(Keys)`.** It is SDK infrastructure, not game API: raw Win32 `WM_KEYDOWN`
  captured by a native detour of `FWindowsViewport::ProcessDeferredMessage`, edge-detected once per tick,
  and **byte-identical across AC and AK**. `Keys` mirrors `System.Windows.Forms.Keys`.
- Game-side alternatives, if we ever need to intercept *game* binds rather than add our own:
  `Engine.GameViewportClient.HandleInputKey(controllerId, FName key, EInputEvent, float, bool)` is the
  classic UE3 chokepoint (delegate-backed, redirectable);
  `Engine.Interaction.OnReceivedNativeInputKey(…)` is the base for `GFxInteraction`.
  `BmGame.RPlayerInput.IsMouseButtonPressed(bool right)` (AK adds shift/ctrl/thumb queries).
- `Engine.PlayerController.ConsoleCommand(FString, bool)` executes console commands — the way to reach
  `exec` functions, since **the `exec` qualifier is not preserved** in the generated C#.
- Nothing in the SDK provides a console-command registration system; if we want debug commands, build
  them on hotkeys or parse from a config file.

## 8. AC vs AK — what actually differs for us

| Area | Arkham City | Arkham Knight | Impact |
|---|---|---|---|
| Bitness / speech | x86, **PRISM unavailable prebuilt** | x64, PRISM fine | **blocking**, §1 |
| HUD | one monolithic movie + `RHudExtension*` | modular hub `RGFxMovieModularHud` + ~18 `RHudModule*` movies | AK reader must enumerate several concurrent movies |
| Dialogue/subtitles | `RDialogueManager.GFXSetSubtitle` | Wwise `AkDialogue*` | full adapter branch |
| Objectives | no readable runtime state (tap setters) | `CurrentObjectiveString`, `QueuedObjectivesArray` | AC needs our own model |
| Map | none | `RGFxMovieWorldMap3D` (+ SatNav) | AK gets a real map reader; AC gets compass only |
| `Trace` | `bool bTraceActors` | `ECollisionFilter TraceFilter` | adapter branch |
| Vehicle | — | Batmobile stack (`RVehicleBatmobileBase`, `RHudModuleBatmobile`, `KBS_Batmobile`) | a whole second state machine to accessibilize |
| Ducking | manual | `AkDialogue.EnableForceDucking` | AC needs manual ducking |
| Extra AK UI | — | Mission Wheel, Radial Gadget Select, Photo Mode, WBID overlay | more screens, but several expose a real selected-index |
| Stores | Steam/Epic/GOG (compat patch off-Epic) | Steam only | support burden |

**Which game first?** Arkham Knight: the transport is solved, objectives and the map are readable at
runtime, ducking is built in, and several AK screens expose a genuine selected index. Arkham City is the
harder target on every axis, so it benefits from the reader archetypes AK forces us to build.

## 9. Suggested build order

Straight application of the playbook's §1 flow, with this platform's specifics:

1. **Boot + speech smoke test.** `[Script]` with `Main()` → speak "ready". On AK via PRISM P/Invoke; on
   AC via the SAPI reflection stopgap while §1 is decided. Confirms host, CLR, hot reload and speech.
2. **State readout on a hotkey.** `OnKeyDown` → speak `GetPlayerViewPoint` position/heading +
   `KismetState`. Confirms the adapter and the whole pipeline end-to-end.
3. **Menus, as one fixed-cost phase** (playbook §8 — *never screen by screen*): `GetFocusMovie` →
   archetype classification → `GetElementMemberString` readers → generic tree-walk fallback. Validate on
   2–3 screens, then sweep the critical path (main menu → load → pause → options → map/objectives).
4. **HUD/prompt layer**: tap `RHUDPrompt.AddLine`/`SetInstructions` and `RInteractInterface.GetPrompt` —
   this gives "press X to…" and tutorial text almost for free, and it is what makes the world navigable.
5. **Dialogue/subtitles**: AC `GFXSetSubtitle` redirect / AK `AkDialogue`, plus `RBMSpeechManager` to
   know when to duck cues.
6. **Navigation**: `FastTrace` fan for wall sonification, then `NavigationHandle` for a path-aware radar,
   then `RObjectiveMarker`/compass beacons.
7. **Combat & predator**: `RBMCombatManager` counts, `RBMAIManager` registry + line-of-sight — the
   feature that decides whether these games are actually playable blind.
8. **Grapple/traversal**: `RGrappleGun.GetNumAvailableGrapplePoints()` +
   `GetGrappleTargetPosition()` as a spoken/sonified aiming aid.

Track it in the mod's own `STATUS.md` (playbook §11).

## 10. Open questions to resolve before starting

1. **PRISM on x86** — can it be built? (§1) This gates Arkham City entirely.
2. **Custom audio** — is reusing existing Wwise events enough for the cue vocabulary, or do we need our
   own bank / our own audio output? (§6)
3. **Detective mode** — is there a reliable state signal, or must we tap the toggle? (§5.1)
4. **AC objective text** — which setter carries the authoritative current objective? (§4)
5. **Game build pinning** — which exact executable build do the current BmSDK offsets target, and what is
   the practical instruction for a blind user installing this (Epic vs Steam + compat patch)? (see
   [install & build](bmsdk-install-and-build.md))
