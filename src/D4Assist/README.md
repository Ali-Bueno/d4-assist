# D4Assist

The Diablo IV Dungeon Assist companion app: a standalone, read-only .NET app, living in the notification area, that speaks dungeon
entry/exit and (once wired up further) tile/position state through a screen reader while you play. It never
injects into or writes to the game process — see `src/D4Memory`'s safety policy, which this app reuses as-is.

## What it does today

- Tails `<install>\FenrisDebug.txt` for world-transition lines (`WorldLogWatcher`) — no memory access at all
  in menus and loading screens (`WorldClassifier.MenuWorldPrefixes`), nor anywhere outside dungeons when
  `OutsideDungeonsEnabled` is off.
- Classifies the current world offline against the CASC `definitions.json` dump (`WorldClassifier`): is it a
  DRLG dungeon (`research/dungeon-data.md` §3), and what's its World SNO.
- On entering a dungeon, opens the game process read-only, locates the static player-position record and the
  main module (`LiveDungeonSessionReader`, built on `D4Memory`), then:
  - polls the player position every 100 ms,
  - polls tiles every 1000 ms via the static scene route (`D4Memory.SceneRoute`), falling back to one full
    CScene heap scan per dungeon entry (background thread) when the route is unavailable for the installed
    build, with further rescans only on demand (`GameSession.RescanTiles`).
- Speaks "Asistente de mazmorras activo" at startup and dungeon-enter/leave announcements through PRISM, in
  the game's language (`Strings/AppStrings`, Spanish or English; see `Language` below). The NVDA voice itself is your choice.
- `--debug` logs changes only (tile set, spoken announcements with their openings, radar readings); `--no-speech`
  swaps PRISM for a console sink so you can run it without a screen reader.
- Everything printed also goes to `d4assist-log.txt` next to the exe, timestamped (`Logging/LogFile`; the previous
  run's log becomes `d4assist-log.old.txt`). Always logged, even without `--debug`: why memory can't be read
  (`Memory: cannot read the game yet, retrying: …`, once per reason; the open is retried every second, since the
  log names a world before the player is placed in it), the player actor and game build once reading, and every
  `CUES muted/unmuted` reason.
- The game's memory addresses change with every game update; the app finds them at session start (`Memory: … game
  object at RVA …`). The conversation addresses can only be found while someone talks (`TALK playing list found at RVA
  …`); they are saved per game build in `d4assist-addresses.json` next to the exe (research/client-memory.md §6).
- Plays the D2R-ported reactive radar (`src/D4Assist.Radar`) while in a dungeon: four fixed stick directions,
  exit/floor/wall cues on confirmed changes only (`Game/RadarBridge`, world from `Game/DungeonRadarWorld`).
- Loops positional beacons at every portal in a dungeon and at configured actors such as the Undercity exit pad (`Game/ActorLoopBridge`, see Beacons and objective sounds).
- Outside dungeons (user-approved 2026-09-29) opens the same read-only session for the player position and the actor
  list. In the open world (`Sanctuary_Eastern_Continent`: towns, roads) that is all: no tiles, radar or announcements
  (`WorldKind.Overworld`); the game's own navigation assist covers it (user, 2026-09-30). Every other non-DRLG world is
  a story area (the Darkened Way, Tristram, the hells, the Mercenary den) and gets the radar too (`WorldKind.RadarOnly`,
  see `RadarOnlyWorlds`). Beacons, objective sounds and the quest routes run in both.
- Leads through known campaign routes with the guide ping, e.g. shelter to shelter in the desert sandstorm quest with
  Meshif (`Game/QuestRouteBridge`, see Quest routes).

Controller input and the rest of the announcement content are future work — see `STATUS.md` P4-P6.

## Requirements

- **PRISM**: this app uses the official **Prismatoid** .NET binding (NuGet, targets net10.0 — loads fine in
  a standalone app; see `reference/screen-reader-integration/prism-prebuilt.md`). Its NuGet package bundles
  the matching native `prism.dll` per platform (`runtimes/win-x64/native/`), copied to the output
  automatically by the SDK — nothing to download or ship by hand.
- **A screen reader**: NVDA (the project's target), JAWS or SAPI — whichever PRISM picks as the best
  available backend at startup (logged to the console).
- **Diablo IV accessibility options**: not required for this app itself (it doesn't touch the game's own TTS
  route), but the user's overall setup expects **Use Screen Reader** + **Use 3rd-Party Screen Reader** on,
  per `research/runtime-signals.md` §1.
- **`definitions.json`**: the CASC/CoreTOC dump `D4Data` reads offline (see `tools/D4DungeonDump`/`D4MemScan`
  for how it's produced). Path is set in the config file below.

## Configuration — `d4assist.json`

Read from next to the built exe. A missing file uses the defaults; a present-but-invalid one is a hard
error. No key needs a machine-specific path.

| Field | Default | Meaning |
|---|---|---|
| `InstallPath` | *(empty: found through Battle.net)* | Game install dir (`FenrisDebug.txt`, the exe for module identification). Set it only if detection fails. |
| `DefinitionsPath` | *(empty: the copy embedded in the exe)* | d4data `definitions.json`. The build embeds `data/definitions.json` in the exe when it exists; set a path only to use another file (relative paths resolve against the exe's folder). |
| `SpeechEnabled` | `true` | `false` behaves like `--no-speech` (console sink instead of PRISM). |
| `AutoAnnouncementsEnabled` | `true` | Speak the dungeon enter/leave lines automatically. |
| `Language` | `"auto"` | `"auto"` follows the game's text locale (`Current Text Locale` in FenrisDebug.txt: `es*` = Spanish, anything else = English; Spanish until a locale is seen); `"es"` / `"en"` force one. Anything else is a startup error. `--debug` logs the choice and changes. |
| `InterruptOnContextChange` | `true` | `interrupt=true` on those announcements (CLAUDE.md §9: interrupt only on context change). |
| `RadarFrame` | `"Cardinal"` | `"Cardinal"` (stick up/right/down/left) or `"Diagonal"` (along the dungeon's axes). |
| `ExplorationGuideEnabled` | `true` | Automatic ping that leads to the nearest unexplored part of the dungeon (see Exploration guide). |
| `ExplorationGuideInDungeons` | `false` | Guide default in dungeons other than the Undercity and the Pit (`X1_Undercity_*`, `PIT_*` always start on). `ExplorationGuideEnabled=false` removes the guide and the toggle entirely. |
| `RadarOnlyWorlds` | `[]` | Exact (case-sensitive) CoreTOC names of extra non-DRLG worlds that run **radar and actor loops only**: no room announcements, no exploration guide, no enter/leave speech. Story areas (every non-DRLG world but the open world, menus and loading screens) get this mode without being listed when `OutsideDungeonsEnabled` is on, so the list only matters for the open world or with that switch off. Tiles come offline from the world's placed scenes (`Game/StaticWorldTiles`), not the live scene route; walkability from the scenes' compressed nav data (`D4Data.Readers.CellBytesDecoder`, research/dungeon-data.md §4.1). `--debug` logs the world kind. |
| `OutsideDungeonsEnabled` | `true` | Read the player and nearby actors outside dungeons too (every non-DRLG world except menus, loading screens, cinematics and test maps), for the beacons, objective sounds and quest routes there, plus the radar in story areas outside the open world. No announcements outside dungeons. `false` restores "memory only in a dungeon". |
| `QuestRoutesEnabled` | `true` | Lead through the known campaign routes (see Quest routes). Needs `OutsideDungeonsEnabled`. Uses the guide ping's sound settings. |
| `PauseCuesInCutscenes` | `true` | Silence every cue while a cutscene plays (see Cutscenes). |
| `PauseCuesInBackground` | `true` | Silence every cue while the game window is not in the foreground, like D2R. The sound examples still play. |
| `PauseCuesInConversations` | `true` | Silence every cue while the dialogue menu is open, while the conversation picked from it plays, while a story conversation plays and while an NPC talks to you without a menu. Your own character's lines never mute. |
| `PauseCuesInMenus` | `true` | Silence every cue while a game menu is open: inventory, skills, map, pause, signs, Horadric shipments, vendor, artisans, stash and every other panel or full menu (`MenuScreens.Watched`, 85 screens). |
| `RadarClosedDoorsEnabled` | `true` | A closed door sounds as a door in the radar (two knocks) instead of open floor. |
| `BeaconActorNames` | `["X1_Undercity_WarpPad"]` | CoreTOC actor names that get a beacon; unknown names are logged once and ignored. |
| `Sound` | *(everything on, 100)* | The settings window's sound tabs (see Settings window): `MasterVolume`, `Preview`, and per group (`Groups`: `Radar`, `Objects`, `Quest`, `Guide`, `Characters`) and per cue (`Cues`: the `Config/SoundCue` names) an `Enabled` switch, a `Volume` (0–100) and a repeat `Speed` (25–300 %, repeating cues only). Missing or unknown entries are ignored in favour of the defaults. |

Files from before 2026-09-30 carry flat sound fields (`RadarEnabled`, `RadarVolume`, `BeaconVolume`, `DoorSoundsEnabled`…); they are no longer read, so those sounds start at their defaults.

## Notification area and settings window

The app has no console window: it puts an icon ("D4") in the notification area (`Ui/TrayApp`, its own STA thread). Enter,
a click or the context-menu key on the icon opens its menu: **Settings…**, **Restart assistant**, **Exit** (speaks
"Asistente de mazmorras cerrado"). **Ctrl+Alt+Shift+C** opens the settings from anywhere. A second copy refuses to start
(`SingleInstance`, a session mutex) and says where the running one is; a restart starts a new copy with `--restarted`, which
waits for the old one to exit. Startup errors (no install, bad `d4assist.json`) show a message box as well as the log.

The settings window (`Ui/SettingsForm`, standard Win32 controls, so NVDA reads every tab, check box, slider and group box)
has one tab per sound group plus **Sound** (master volume, preview switch) and **General**:
- Each group and each cue has a check box (on/off), a volume slider and, for the repeating cues (object and quest loops, the
  guide ping, the NPC tap), a speed slider. A cue's gain is group × cue, each volume squared (`Config/SoundMix`: 50 sounds
  about half as loud); speeds multiply. The radar tab also has one volume per direction (front = screen up), which scales
  every radar sound from that side. Changes play live (`Config/LiveSoundMix`) and are saved by OK/Apply; Cancel or closing
  restores the saved sound.
- Sliders (`Ui/CueSlider`, user request 2026-09-30): Up/Down ±1, Right/Left ±5, Page Up/Down ±10, Home 100 (the top),
  End 0 (the bottom). Screen readers get a trackbar's value from the MSAA proxy (WinForms' UIA value pattern included), which
  reports the position as a percentage of the range it queries (`TBM_GETRANGEMIN/MAX`): a 25–300 speed at 100 read "27".
  `CueSlider` answers that query as 0–100 (WinForms never sends it), so the proxy reads the number itself ([companion-app-winforms](../../reference/ui-accessibility/companion-app-winforms.md)).
- With **Preview** on, the cue plays at its new level 150 ms after the last change (`Ui/CuePreview`, the mixer's own preview
  voice, audible although the game is in the background); a repeating cue plays two to six times at its speed.
- Speed changes the time between repetitions, never the pitch: the object and quest loops overlap or space out their copies
  (`D4Assist.Audio.LoopVoice` rate), the guide ping and NPC tap divide their interval.
- **General** holds the other options (language, radar orientation, speech, announcements, guide, routes, outside dungeons,
  closed doors, cue pauses). They take effect on restart; saving a change there offers to restart now.

## Beacons

While in a dungeon the RActor pool (`D4Memory.Actors`) is read every 500 ms (`GameSession.PollActorsOnce`);
`ActorLoopBridge` (with `LoopTargetSelector`) picks the actors named in `BeaconActorNames` (resolved to SNOs through CoreTOC at startup) and,
inside dungeons, every portal (`Game/PortalClassifier`: portal gizmos, warp pads and portal switches, never town portals;
[dungeon-objectives](../../research/dungeon-objectives.md) §7) and,
on every player update, loops the quest-item sound (embedded in `D4Assist.Audio`) at each one, panned and faded by its offset in screen axes
(`ScreenDirections.ToScreen`). The loops share the radar's mixer/output and stop on any world change. If the
actor pool is unavailable the app logs it once and the beacons stay silent. `--debug` logs a `BEACON` line when
a target appears or disappears (`LOOP started/stopped`).

`Game/PortalMemory` keeps every portal seen in the dungeon instance (also across warps inside it) and marks the ones the
player appeared or vanished within 15 units of as used: they lead back, the others lead on. `--debug` logs `PORTAL seen`
(with its distance and whether it was arrived by) and `PORTAL went through / arrived by`.

## Objective sounds

The same bridge loops the quest sound (never spoken, positional, D2R quest profile at full near gain) at the dungeon's
objectives: actors the world's DPO quest names (`D4Data` `DungeonObjectiveReader`: world SNO, Global `zone_progress`,
`DPO_*` quest callbacks) minus monsters, plus every carryable (on the ground; the copy in the player's hands, with no
ACD, stays silent), carryable receptacle (a quest-locked one only while the player holds a carryable it takes; other quest steps
wait meanwhile), quest
switch and quest chest, switches and notes named for a quest (`QST_`, `Quest`, `Prologue`), and
free-able prisoners (name contains `Prisoner`, not `Dead`, monster or switch actor). Rules and data:
[dungeon-objectives](../../research/dungeon-objectives.md). The exit pads keep the beacon profile and are the only loops that
mute the exploration guide. A loop stops when its actor leaves the placed list, or when the gizmo was used
(`Game/GizmoUsedSignal`: the game's own mark, ACD attribute `Gizmo_Has_Been_Operated` (corpses, herbs, signs, doors;
research/client-memory.md §4); else a chest's glow effect gone while the player is within 10 units, else the RActor
state word; remembered by spot for the world visit, so a re-created actor stays silent). Chest kinds seen glowing are saved next to
the exe in `d4assist-chests.json`, so a chest first seen already open is judged too. `--debug` logs one `LOOP started/stopped` line
per loop with its reason (`dpo`, `gizmo`, `prisoner`, `beacon`). Prisoners loop D2R's corpse sound, the others the quest sound.
Quest gizmos (ACD `Gizmo_Is_Quest_Restricted`, research/client-memory.md §4): one the game shows on the minimap
(`MinimapActive`), or one the tracked quest's scripts name (the player's `ManuallyTrackedQuestSNO`, resolved offline by
`D4Data.Readers.QuestScriptMarkerReader`), is the quest's current step and gets the quest loop whatever it is
(`ActorState.QuestStep`, reason `quest`). Any other quest gizmo, or one with `Gizmo_Disabled_By_Script`, isn't usable yet
(`ActorState.Unusable`) and stays silent. `--debug` logs `Quest tracked: <sno>, N object(s) named.` on each change and
`LOOP silent … cause=unusable` once per gizmo.

## Doors, chests and readables

`Game/InteractableClassifier` gives every other gizmo a D2R loop by ActorDefinition gizmo type
([dungeon-objectives](../../research/dungeon-objectives.md) §6):
- doors (`DoorGizmoData`, not `*StartOpen*`) → wooden, metal or stone door, picked from the name;
- chests (`ChestGizmoData`, reward and locked chests) → chest;
- readables (`SignGizmoData`, `*Lorebook*` chests) → scroll;
- the "look" spot (`POI_Look_At`, the only gizmo of type 29) → scroll, and it keeps sounding after use (it can be looked at again);
- a chest or readable named `*Corpse*`, `*Remains*` or `*Body*` → corpse, like the prisoners (user request: a body on the ground shouldn't sound like a chest).

They use D2R's door/container profile: reach 18 units (30 subtiles), squared fade. `ActorLoopBridge` plays only the
2 nearest doors and the 3 nearest chests/readables (D2R `MaxDoorVoices`/`MaxContainerVoices`). When the 8-loop
budget is full, the order is beacon, objectives, doors, containers. Like objectives, they stop only when the actor
leaves the list: the live open/used state is not known yet.

Doorways are baked walkable in the scene data, so an open door already counts as an exit for the radar and the
room announcements. A closed door does not register as an obstacle yet.

## Cue pauses

`Game/CueGate` is the one switch on `AudioMixer.Paused`. The cues are silent while any reason holds:
- **cutscene** (below);
- **game in background**: `Input/GameWindowFocus` checks at the player-poll rate whether the foreground window
  belongs to the Diablo IV process;
- **conversation**: `ConversationMute`, fed by `GameSession.ConversationRead`
  ([client-memory](../../research/client-memory.md) §5), mutes while the dialogue menu is open and while a
  conversation picked from it, any standard story conversation (`eConvType` 3) or any type 1 conversation with an NPC line
  plays. Barks, taunts and the hero's own lines never mute;
- **menus**: `MenuMute`, fed by `GameSession.MenusRead`, mutes while any of the 85 watched screens (`MenuScreens.Watched`) is open:
  character panel, main menu, signs, Horadric shipments, vendor, artisans, stash (seen live), every other side panel and the
  full menus opened on purpose. `--debug` logs `MENU open: <screen>`. `MenuScreenTracker` locates the screen
  records by a background scan and retries after a miss. After the NPC interaction menu (`screen_InteractPopup`) closes,
  the mute holds 3 s more: a scene picked there can open with music 1.7 s before its conversation shows up (live
  2026-09-30). The conversation mute takes over if one starts.

A sound example lifts the pause while it plays.

## Closed doors in the radar

`Game/DoorObstacles` lays every closed door on the radar grid. A door is closed when it is a door gizmo, not
`*StartOpen*`, not used (`StateChanged`) and on the player's floor. The footprint is its `aabbBounds` box, turned by the
RActor yaw, at least half a cell diagonal thick and one cell wider on each side. `DungeonRadarWorld` turns open floor
under it into `RadarCell.Door`, so `PassageScanner` reads the ray as `Door` (D2R's two knocks) when the door stops it
within the closing depth. A quest-restricted door stays a door: nothing in memory says when the quest unlocks it
(research/client-memory.md §4). `--debug` logs `RADAR door … ends=A/B` once per door; both ends should be `Blocked`, which
means the yaw lays the door across its doorway.

## Cutscenes

`Game/CutscenePause` sets `AudioMixer.Paused` on the log's `ptCutscene->m_eCutsceneState: PLAYBACK` line. A paused
mixer renders silence, drops radar cues and guide pings, and holds its loops. The game never logs a cutscene's end
([runtime-signals](../../research/runtime-signals.md) §2), so the pause lifts on the next world change, or once the
player has walked one map cell. `--debug` logs `CUTSCENE state …` and `CUES paused/resumed (cause)`.

In-engine cinematics log nothing and can open with music long before their conversation shows up (live 2026-09-30:
the Inarius door scene, ~16 s). The player's ACD attribute `RTC Playing` (real-time cinematic) is 1 while one plays:
`LiveDungeonSessionReader.ReadCinematic` reads it at the actor-poll rate (500 ms), and `GameSession.CinematicRead` sets
the `Cinematic` mute (`CUES muted: Cinematic`). A world change lifts it. Also under `PauseCuesInCutscenes`.

## NPC sound

`Game/NpcCueBridge` plays D2R's friendly-NPC tap (`Audio/NpcCuePlayer`, a port of d2r-access `entity_cue_synth.cpp`) at
the nearest NPC within 15 units, on every player poll. It repeats faster as the NPC gets closer: 450 ms at the edge,
150 ms at contact. It is panned by screen side and pitched lower when the NPC is below the player.
- **Which NPCs:** `Game/NpcClassifier` takes the fIsNPC monsters off the hostile team, including quest followers and
  escorts. It leaves out hired mercenaries, cinematic stand-ins and prisoners, which have their own loop
  ([dungeon-objectives](../../research/dungeon-objectives.md) §9).
- **Followers go quiet:** a quest companion following the player carries the ACD attribute `Follower_Quest`
  (`ActorState.QuestFollower`, research/client-memory.md §4) and never taps (user, 2026-09-30). `--debug` logs
  `NPC follows the player, silent`.
- **No line of sight:** the open world loads no walk grid, so an NPC behind a wall taps too.
- **Muting:** the cue gate mutes it like every cue.
- **Log:** `--debug` logs `NPC tap: <name>` per target change.

## Exploration guide

`Game/ExplorationGuideBridge` asks the navigation engine's `ExplorationGuide` for a waypoint on every player poll
and tile update and aims `GuidePingPlayer` at it (no buttons). Outside the endgame it first leads to an objective heard
earlier and not done: `Game/ObjectiveMemory` remembers every objective loop (`ActorLoopBridge.ObjectivesLooping`), the
tracked quest's step before the dungeon's own objectives (prisoners, altars, levers, carryables). An objective that falls
silent within 20 units of the player is done (used, locked or removed); one the player came within a quarter tile of is
passed over until another objective is done (a key found may open a door visited earlier) or the guide is switched on again.
Pit and Undercity keep the order below unchanged (user, 2026-09-30). Switching the guide on says where it leads ("Hacia el
objetivo de la misión." / "Hacia un objetivo de la mazmorra." / "Hacia zona sin explorar." / the portal lines / "Todo explorado.");
while on, a change of goal is spoken once, and a new floor starts exploring without a word. `--debug` logs `OBJECTIVE seen|reached|done|re-armed`.
It mutes while an unused portal is in sight (`PortalMemory.FreshInSight`: its
beacon takes over) unless an objective still leads, stops on a world change, and when every reachable part is visited leads to the portals seen earlier:
the reachable unused ones, or else the ones already used ("Todo explorado. Te llevo a un portal." / "... al portal por el que
llegaste."). Reaching the last of them, or having none, it stays silent and speaks "Todo explorado." once per dungeon (`interrupt=false`). `--debug` logs one `GUIDE` line per target change and per toggle.
Each target reached plays the arrival chime (a target reached while the beacon leads never chimes late). Once a
Undercity boss-room tile (`*_FLEX_POI_BossRoom_Undercity`) loads, the guide stays off until the world changes:
the last warp lands there and the run timer no longer matters (user request, 2026-09-28).
In endgame worlds, when no unused portal is known and nothing is left to explore, the guide re-checks the dead ends that can host the exit pad
(`Game/ExitCandidateScenes`: scenes named by MarkerSet layers `<scene> (X1_Undercity_AreaExits|BossExits)`), speaking "Salida no encontrada. Revisando los callejones." once per round; it stops when the beacon appears.
`--debug` also logs the candidate count at startup, each round, and once per dungeon visit and actor SNO every gizmo or Portal/Warp/Exit/Beacon actor (`ACTOR seen:`, `Game/ActorSightingLog`).

Restarting the app mid-dungeon keeps what the guide knows: `Game/ExplorationPersistence` saves the map, the explored tiles
and the portals (with their used marks) on every tile poll to `d4assist-exploration.json` next to the exe, and restores them
when the app starts in the same world, CWorldID and game process (start time), unless the saved tiles disagree with the
loaded ones. Leaving the dungeon deletes the file. The log says `Exploration: restored …`. An app started for the first time
mid-dungeon still knows nothing of the way already walked, and takes the arrival portal for a new one.

## Quest routes

`Game/QuestRouteBridge` leads through the campaign stretches listed in `Game/QuestRoute.cs` (`QuestRouteCatalog`). It uses
the exploration guide's ping, aimed straight at the next point: the open world's walk grid isn't loaded, so there
is no path to follow. Each route is resolved offline at startup (`QuestRouteLoader`). Its points are the target actor's
markers in the quest's subzone MarkerSet, and its start is the marker that set holds for the quest itself. The points
are visited in the shortest walk from that start (`RouteOrder`). Data and reasoning:
[campaign-routes](../../research/campaign-routes.md).

- A route starts when one of its quest actors appears in the live actor list, or when the player stands within
  15 units of the quest marker. It speaks "Guía de refugios." and joins at the end of the nearest leg (`RouteGuide`).
- Within 4 units of a point it chimes and speaks "Refugio 1 de 3."; reaching a later point passes the earlier ones.
  The ping then holds while the player stays within 10 units of that point (a shelter is where they wait out the
  storm; the game's own audio says when it ends), and leads on to the next point once they step out. After the last
  point it stops and doesn't restart until the world changes.
- It stops when the player is more than 150 units from the start and from every point, and on any world change.
- `--debug` logs the loaded routes at startup and one `ROUTE` line per start, point, stop and mute.

The only route so far is `sandstorm-shelters`: the three `BCD_Trigger_EliasPalace_SafeZone` safe zones of
`VGN_Kehj_EliasPalace_Sandstorm`. **Not live-tested yet.**

## Controls

- **D-pad left** (any XInput controller, or a DualSense / DualShock 4 over USB or Bluetooth) — toggles the exploration guide for the current kind of world (Undercity and
  Pit vs other dungeons) for the rest of the run, and speaks "Guía activada." / "Guía desactivada.". Outside a dungeon
  it mutes and unmutes a leading quest route the same way, and otherwise does nothing. Ignored while a game menu is open
  (inventory, map, vendor…: the D-pad navigates it; `Input/GatedPad` over `MenuMute`, `--debug` logs `GUIDE toggle ignored`). `Input/XInputPad` only observes the pad (polled every 16 ms, dynamic-loaded XInput, ported from the
  D2R mod's `input_direction.cpp`); the game still receives every press. PlayStation pads never reach XInput, so
  `Input/SonyHidPad` reads their HID input reports (shared, read-only handle; rescans every 3 s for pads plugged in
  later; logs `Pad: … connected` or why it can't open one) and `Input/CombinedPad` drops a second copy of the same
  press within 200 ms (Steam Input / DS4Windows also expose the pad as a virtual Xbox pad). Not saved to `d4assist.json`.
- **Ctrl+Alt+Shift+1..8** (keyboard, anywhere) — sound examples, e.g. for a video without a fight's noise (user request
  2026-09-29): the name is spoken, then 1 exit, 2 open floor, 3 wall, 4 door, 5 unexplored exit (radar cues, once per
  direction up/right/down/left), 6 the quest loop twice, 7 the guide ping three times, 8 the arrival chime. One example
  at a time. `Input/DemoHotkeys` (Win32 `RegisterHotKey`), `SoundDemo`.
- **Ctrl+Alt+Shift+C** (keyboard, anywhere) — opens the settings window (see Notification area and settings window).

## Command-line flags

- `--no-speech` — use `ConsoleSpeechSink` instead of PRISM (debugging without a screen reader).
- `--debug` — log a `TILES` line when the loaded tile set changes, a `SAY` line per announcement with each opening
  (side, position, width, path length, `entry`), and one `RADAR` line per confirmed radar reading
  change (direction, reading, unexplored, player position) and per rebuilt radar map.
- `--sound-test` — speak and play every radar cue in every direction, without the game.
- `--restarted` — added by the tray's Restart: wait up to 30 s for the previous copy to exit instead of refusing to start.

Output goes to `d4assist-log.txt` (there is no console window).

## Layout

- `Speech/` — `ISpeechSink` (the single sink, CLAUDE.md §9), `PrismSpeechSink` (Prismatoid), `ConsoleSpeechSink`.
- `Strings/` — `AppStrings`, every app-level spoken string in one place (one instance per language); `SettingsStrings`,
  the tray menu and settings window text.
- `Config/` — `AppConfig` (`d4assist.json` loading and saving), the sound settings (`SoundSettings`, `SoundCatalog`,
  `SoundMix`/`LiveSoundMix`) and `SettingsSession` (one visit to the settings window).
- `Ui/` — `TrayApp` (notification-area icon, menu, settings hotkey), `SettingsForm`, `CueSlider`, `CuePreview`.
- `SingleInstance.cs` — one running copy per Windows session.
- `Game/` — `WorldLogWatcher`, `WorldClassifier`, the `GameSession` state machine
  (`WaitingForGame -> Idle -> InDungeon`), `IDungeonFeed`/`TileState`/`PlayerState`, and
  `LiveDungeonSessionReader` (the production `IDungeonSessionReader`, built on `D4Memory`), the radar's
  `DungeonRadarWorld`/`RadarBridge`, and `LockedTileWalkability` (one walk-map lookup shared across poll threads).
- `Program.cs` — wiring and the run loop.
- `Portable/LEEME.txt` — the testers' Spanish readme, published next to the exe by `Properties/PublishProfiles/Portable.pubxml`
  (single-file, self-contained; see the root README).

## Tests

`tests/D4Assist.Tests` covers `WorldLogWatcher`'s line parsing, partial-line handling and truncation/rotation
recovery (real temp files, no game install needed), and `GameSession`'s state transitions, diff-gating,
player/tile polling and the one-scan-per-entry fallback rule — all via a fake log feed, a fake
`IWorldClassifier` and a fake `IDungeonSessionReader`, so no process access happens during the build/test loop.
`DungeonRadarWorldTests` builds the radar's world from synthetic tiles and a fake walkability lookup.
