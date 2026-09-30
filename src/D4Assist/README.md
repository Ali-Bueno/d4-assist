# D4Assist

The Diablo IV Dungeon Assist companion app: a standalone, read-only .NET console app that speaks dungeon
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
- Plays the D2R-ported reactive radar (`src/D4Assist.Radar`) while in a dungeon: four fixed stick directions,
  exit/floor/wall cues on confirmed changes only (`Game/RadarBridge`, world from `Game/DungeonRadarWorld`).
- Loops positional beacons at every portal in a dungeon and at configured actors such as the Undercity exit pad (`Game/ActorLoopBridge`, see Beacons and objective sounds).
- Outside dungeons (overworld, towns, story areas; user-approved 2026-09-29) opens the same read-only session for the
  player position and the actor list only: no tiles, radar or announcements there (`WorldKind.Overworld`). Beacons,
  objective sounds and the quest routes run there.
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
| `RadarEnabled` | `true` | Play the reactive radar cues while in a dungeon. |
| `RadarFrame` | `"Cardinal"` | `"Cardinal"` (stick up/right/down/left) or `"Diagonal"` (along the dungeon's axes). |
| `RadarVolume` | `1.0` | Radar cue volume, 0 to 1. |
| `BeaconSoundsEnabled` | `true` | Play looping beacons at the `BeaconActorNames` actors (works with the radar off). |
| `BeaconVolume` | `1.0` | Beacon loop volume, 0 to 1. |
| `ExplorationGuideEnabled` | `true` | Automatic ping that leads to the nearest unexplored part of the dungeon (see Exploration guide). |
| `ExplorationGuideInDungeons` | `false` | Guide default in dungeons other than the Undercity and the Pit (`X1_Undercity_*`, `PIT_*` always start on). `ExplorationGuideEnabled=false` removes the guide and the toggle entirely. |
| `ExplorationGuideVolume` | `1.0` | Exploration ping volume, 0 to 1. |
| `RadarOnlyWorlds` | `[]` | Exact (case-sensitive) CoreTOC names of non-DRLG worlds where the assistant also runs, **radar and actor loops only**: no room announcements, no exploration guide, no enter/leave speech. Their tiles come offline from the world's placed scenes (`Game/StaticWorldTiles`), not the live scene route. Explicit, user-approved exception to "read memory only in a dungeon" (STATUS); opt-in by this list, so a world not listed is never read. `--debug` logs the world kind. Empty by default: the Mercenary den (`Mercenary_Hideout`) was tried live 2026-09-28, but its scene ships only 6 coarse 32-unit walkable boxes, no detailed grid, so the radar has nothing to scan (`RADAR map: ... 1 without walkability`). |
| `OutsideDungeonsEnabled` | `true` | Read the player and nearby actors outside dungeons too (every non-DRLG world except menus, loading screens, cinematics and test maps), for the beacons, objective sounds and quest routes there. No radar or announcements outside dungeons. `false` restores "memory only in a dungeon". |
| `QuestRoutesEnabled` | `true` | Lead through the known campaign routes (see Quest routes). Needs `OutsideDungeonsEnabled`. Uses the exploration ping's volume. |
| `ObjectiveSoundsEnabled` | `true` | Play quest loops at dungeon objectives (see Objective sounds). |
| `ObjectiveVolume` | `1.0` | Objective loop volume, 0 to 1. |
| `DoorSoundsEnabled` | `true` | Loop D2R's door sounds at the 2 nearest doors (see Doors, chests and readables). |
| `ChestSoundsEnabled` | `true` | Loop D2R's chest sound at chests and other lootable containers. |
| `ReadableSoundsEnabled` | `true` | Loop D2R's scroll sound at lore books, notes and notices. |
| `InteractableVolume` | `1.0` | Door, chest and readable loop volume, 0 to 1. |
| `PauseCuesInCutscenes` | `true` | Silence every cue while a cutscene plays (see Cutscenes). |
| `PauseCuesInBackground` | `true` | Silence every cue while the game window is not in the foreground, like D2R. The sound examples still play. |
| `PauseCuesInConversations` | `true` | Silence every cue while the dialogue menu is open, while the conversation picked from it plays, while a story conversation plays and while an NPC talks to you without a menu. Your own character's lines never mute. |
| `PauseCuesInMenus` | `true` | Silence every cue while a game menu is open: inventory, skills, map, pause, signs, Horadric shipments, vendor, artisans, stash and every other panel or full menu (`MenuScreens.Watched`, 85 screens). |
| `RadarClosedDoorsEnabled` | `true` | A closed door sounds as a door in the radar (two knocks) instead of open floor. |
| `BeaconActorNames` | `["X1_Undercity_WarpPad"]` | CoreTOC actor names that get a beacon; unknown names are logged once and ignored. |

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
`DPO_*` quest callbacks) minus monsters, plus every carryable, carryable receptacle, quest switch and quest chest, and
free-able prisoners (name contains `Prisoner`, not `Dead`, monster or switch actor). Rules and data:
[dungeon-objectives](../../research/dungeon-objectives.md). The exit pads keep the beacon profile and are the only loops that
mute the exploration guide. A loop stops when its actor leaves the placed list, or when the gizmo was used
(`Game/GizmoUsedSignal`: a chest's glow effect gone while the player is within 10 units, else the RActor state word;
remembered by spot for the world visit, so a re-created actor stays silent). Chest kinds seen glowing are saved next to
the exe in `d4assist-chests.json`, so a chest first seen already open is judged too. `--debug` logs one `LOOP started/stopped` line
per loop with its reason (`dpo`, `gizmo`, `prisoner`, `beacon`). Prisoners loop D2R's corpse sound, the others the quest sound.

## Doors, chests and readables

`Game/InteractableClassifier` gives every other gizmo a D2R loop by ActorDefinition gizmo type
([dungeon-objectives](../../research/dungeon-objectives.md) §6):
- doors (`DoorGizmoData`, not `*StartOpen*`) → wooden, metal or stone door, picked from the name;
- chests (`ChestGizmoData`, reward and locked chests) → chest;
- readables (`SignGizmoData`, `*Lorebook*` chests) → scroll;
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
  records by a background scan and retries after a miss.

A sound example lifts the pause while it plays.

## Closed doors in the radar

`Game/DoorObstacles` lays every closed door on the radar grid. A door is closed when it is a door gizmo, not
`*StartOpen*`, not used (`StateChanged`) and on the player's floor. The footprint is its `aabbBounds` box, turned by the
RActor yaw, at least half a cell diagonal thick and one cell wider on each side. `DungeonRadarWorld` turns open floor
under it into `RadarCell.Door`, so `PassageScanner` reads the ray as `Door` (D2R's two knocks) when the door stops it
within the closing depth. `--debug` logs `RADAR door … ends=A/B` once per door; both ends should be `Blocked`, which
means the yaw lays the door across its doorway.

## Cutscenes

`Game/CutscenePause` sets `AudioMixer.Paused` on the log's `ptCutscene->m_eCutsceneState: PLAYBACK` line. A paused
mixer renders silence, drops radar cues and guide pings, and holds its loops. The game never logs a cutscene's end
([runtime-signals](../../research/runtime-signals.md) §2), so the pause lifts on the next world change, or once the
player has walked one map cell. `--debug` logs `CUTSCENE state …` and `CUES paused/resumed (cause)`.

## Exploration guide

`Game/ExplorationGuideBridge` asks the navigation engine's `ExplorationGuide` for a waypoint on every player poll
and tile update and aims `GuidePingPlayer` at it (no buttons). It mutes while an unused portal is in sight (`PortalMemory.FreshInSight`: its
beacon takes over), stops on a world change, and when every reachable part is visited leads to the portals seen earlier:
the reachable unused ones, or else the ones already used ("Todo explorado. Te llevo a un portal." / "... al portal por el que
llegaste."). Reaching the last of them, or having none, it stays silent and speaks "Todo explorado." once per dungeon (`interrupt=false`). `--debug` logs one `GUIDE` line per target change and per toggle.
Each target reached plays the arrival chime (a target reached while the beacon leads never chimes late). Once a
Undercity boss-room tile (`*_FLEX_POI_BossRoom_Undercity`) loads, the guide stays off until the world changes:
the last warp lands there and the run timer no longer matters (user request, 2026-09-28).
In endgame worlds, when no unused portal is known and nothing is left to explore, the guide re-checks the dead ends that can host the exit pad
(`Game/ExitCandidateScenes`: scenes named by MarkerSet layers `<scene> (X1_Undercity_AreaExits|BossExits)`), speaking "Salida no encontrada. Revisando los callejones." once per round; it stops when the beacon appears.
`--debug` also logs the candidate count at startup, each round, and once per dungeon visit and actor SNO every gizmo or Portal/Warp/Exit/Beacon actor (`ACTOR seen:`, `Game/ActorSightingLog`).

## Quest routes

`Game/QuestRouteBridge` leads through the campaign stretches listed in `Game/QuestRoute.cs` (`QuestRouteCatalog`). It uses
the exploration guide's ping, aimed straight at the next point: overworld scenes have no detailed walkable grid, so there
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

## Command-line flags

- `--no-speech` — use `ConsoleSpeechSink` instead of PRISM (debugging without a screen reader).
- `--debug` — log a `TILES` line when the loaded tile set changes, a `SAY` line per announcement with each opening
  (side, position, width, path length, `entry`), and one `RADAR` line per confirmed radar reading
  change (direction, reading, unexplored, player position) and per rebuilt radar map.
- `--sound-test` — speak and play every radar cue in every direction, without the game.

## Layout

- `Speech/` — `ISpeechSink` (the single sink, CLAUDE.md §9), `PrismSpeechSink` (Prismatoid), `ConsoleSpeechSink`.
- `Strings/` — `AppStrings`, every app-level spoken string in one place (one instance per language).
- `Config/` — `AppConfig` (`d4assist.json` loading).
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
