# D4Assist

The Diablo 4 Assist companion app: a standalone, read-only .NET app, living in the notification area, that speaks dungeon
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
- Speaks "Diablo 4 Assist activo" at startup and dungeon-enter/leave announcements through PRISM, in
  the game's language (`Strings/AppStrings`, one file per language; see `Language` below). The NVDA voice itself is your choice.
- `--debug` says "Debug mode on" after the startup line and logs changes only (tile set, spoken announcements with their openings, radar readings); `--no-speech`
  swaps PRISM for a console sink so you can run it without a screen reader.
- Everything printed also goes to `d4assist-log.txt` next to the exe, timestamped (`Logging/LogFile`; the previous
  run's log becomes `d4assist-log.old.txt`). Always logged, even without `--debug`: why memory can't be read
  (`Memory: cannot read the game yet, retrying: …`, once per reason; the open is retried every second, since the
  log names a world before the player is placed in it), the player actor and game build once reading, and every
  `CUES muted/unmuted` reason.
- The game's memory addresses change with every game update; the app finds them at session start (`Memory: … game
  object at RVA …`). The game maps its image twice and the game object can be in either copy, so each is tried
  (`addresses in 0x…`), and the session reads from that copy. The conversation addresses can only be found while someone
  talks (`TALK playing list found at RVA …`); they are saved per game build in `d4assist-addresses.json` next to the exe
  (research/client-memory.md §6).
- Self-check (`Game/ReadHealth`): the session reports each read (position, addresses, rooms, actors, attributes, menus).
  The log's first line per read is `Self-check: <read> ok.`; a read that never works in a world for its grace logs
  `Self-check: <read> FAILED after N s: <reason>` and is spoken once per run (`AppStrings.ReadFailures`: "Aviso: no puedo
  leer las salas…"). The log's second line is the app version and commit; a crash speaks `AppStrings.Stopped`.
- Plays the D2R-ported reactive radar (`src/D4Assist.Radar`) while in a dungeon: four fixed stick directions,
  exit/floor/wall cues on confirmed changes only (`Game/RadarBridge`, world from `Game/DungeonRadarWorld`).
- Loops positional beacons at every portal in a dungeon and at configured actors such as the Undercity exit pad (`Game/ActorLoopBridge`, see Beacons and objective sounds).
- Outside dungeons (user-approved 2026-09-29) opens the same read-only session for the player position and the actor
  list. In the open world (`Sanctuary_Eastern_Continent`: towns, roads) that is all: no tiles, radar or announcements
  (`WorldKind.Overworld`); the game's own navigation assist covers it (user, 2026-09-30). Every other non-DRLG world is
  a story area (the Darkened Way, Tristram, the hells, the Mercenary den) and gets the radar too (`WorldKind.RadarOnly`,
  see `RadarOnlyWorlds`). Beacons, objective sounds, the quest routes and D-pad left's quest places run in both.
- Leads through known campaign routes with the guide ping, e.g. shelter to shelter in the desert sandstorm quest with
  Meshif and on to the palace steps (`Game/QuestRouteBridge`, see Quest routes).

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
| `InstallPath` | *(empty: found through Battle.net, then Steam's libraries, then the running game's folder, then a "Diablo IV" folder up to three levels deep on a local drive (`InstallFolderSearch`), then a folder picker; the last two are saved here)* | Game install dir (`FenrisDebug.txt`, the exe for module identification), logged as `Game install:`; a Steam install also logs each `Steam storage:` step ([research/steam-storage.md](../../research/steam-storage.md)). Set it only if detection fails. |
| `DefinitionsPath` | *(empty: the copy embedded in the exe)* | d4data `definitions.json`. The build embeds `data/definitions.json` in the exe when it exists; set a path only to use another file (relative paths resolve against the exe's folder). |
| `SpeechEnabled` | `true` | `false` behaves like `--no-speech` (console sink instead of PRISM). |
| `AutoAnnouncementsEnabled` | `true` | Speak the dungeon enter/leave lines automatically. |
| `Language` | `"auto"` | `"auto"` follows the game's text locale (Battle.net's `Launcher.db`, then `Current Text Locale` in FenrisDebug.txt, mapped by `Navigation.SpeechLanguages`: esES/esMX Spanish, frFR French… 13 languages, English for an unknown locale); a code forces one: `es`, `en`, `fr`, `de`, `it`, `pl`, `pt`, `ru`, `tr`, `ja`, `ko`, `zh-cn`, `zh-tw`. Anything else is a startup error. `--debug` logs the choice and changes. |
| `InterruptOnContextChange` | `true` | `interrupt=true` on those announcements (CLAUDE.md §9: interrupt only on context change). |
| `RadarFrame` | `"Cardinal"` | `"Cardinal"` (stick up/right/down/left) or `"Diagonal"` (along the dungeon's axes). |
| `CompassDirections` | `false` | Say spoken directions as compass points of the screen map (north is up: up-right = northeast) instead of up, right…. Only the wording changes (`NavigationPhrases` with `DirectionStyle.Compass`); the game world's own axes are not used. |
| `CheckForUpdates` | `true` | At start, look for a newer release on GitHub in the background and ask whether to update (see Updates). The tray's **Check for updates** works either way. |
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
| `Sound` | *(everything on, 100)* | The settings window's sound tabs (see Settings window): `MasterVolume`, `Preview`, `NpcVoices` (how many of the nearest NPCs tap at once, 1–4, default 2), and per group (`Groups`: `Radar`, `Objects`, `Quest`, `Guide`, `Characters`) and per cue (`Cues`: the `Config/SoundCue` names) an `Enabled` switch, a `Volume` (0–100) and a repeat `Speed` (25–300 %, repeating cues only). Missing or unknown entries are ignored in favour of the defaults. |

Files from before 2026-09-30 carry flat sound fields (`RadarEnabled`, `RadarVolume`, `BeaconVolume`, `DoorSoundsEnabled`…); they are no longer read, so those sounds start at their defaults.

## Updates

`Update/` — at start and then every 4 hours (when `CheckForUpdates` is on), and from the tray menu, `ReleaseClient` reads
`releases/latest` of `Ali-Bueno/d4-assist` (5 s timeout; errors only go to the log, a manual check also speaks them). A
newer version is spoken and offered in a three-button task dialog (`UpdatePrompt`, own STA thread, topmost, with the release
notes): Update now, Remind me later (also Escape: nothing saved, asked again next check) or Skip this version (saved as
`{"SkippedVersion":"x.y.z"}` in `d4assist-update.json` next to the exe, app-managed and never in the release zip; automatic
checks stay quiet about that version, a newer one or a manual check asks again). An automatic check holds the question back
while the game window is in the foreground (`ForegroundGate`, polled every 5 s); a manual check asks at once and releases a
waiting one. On Update `UpdatePackage` downloads `D4Assist.zip` to `%TEMP%\D4AssistUpdate\<version>`, checks the release's SHA-256 and unpacks it, and
`FileReplacement` renames the running exe to `D4Assist.exe.old`, copies the new files (an existing `d4assist.json` is never
overwritten), rolls back on any failure, and the app restarts through the tray's restart path. The `.old` file is deleted at
the next start. Only the single-file portable build replaces itself; a dev build only logs that it skipped the check.

## Notification area and settings window

The app has no console window: it puts an icon ("D4") in the notification area (`Ui/TrayApp`, its own STA thread). Enter,
a click or the context-menu key on the icon opens its menu: **Settings…**, **Check for updates**, **Restart assistant**, **Exit** (speaks
"Diablo 4 Assist cerrado"). **Ctrl+Alt+Shift+C** opens the settings from anywhere, **Ctrl+Alt+Shift+Q** exits at once (as the tray's Exit). A second copy refuses to start
(`SingleInstance`, a session mutex) and says where the running one is; a restart starts a new copy with `--restarted`, which
waits for the old one to exit. Startup errors (no install, bad `d4assist.json`) show a message box as well as the log.

The settings window (`Ui/SettingsForm`, standard Win32 controls, so NVDA reads every tab, check box, slider and group box)
has one tab per sound group plus **Sound** (master volume, preview switch) and **General**:
- Each group and each cue has a check box (on/off), a volume slider and, for the repeating cues (object and quest loops, the
  guide ping, the NPC tap), a speed slider. A cue's gain is group × cue, each volume squared (`Config/SoundMix`: 50 sounds
  about half as loud); speeds multiply. The radar tab also has one volume per direction (front = screen up), which scales
  every radar sound from that side. The characters box also chooses how many NPCs tap at once (1 to 4). Changes play live
  (`Config/LiveSoundMix`) and are saved by OK/Apply; Cancel or closing restores the saved sound.
- Sliders (`Ui/CueSlider`, user request 2026-09-30): Up/Down ±1, Right/Left ±5, Page Up/Down ±10, Home 100 (the top),
  End 0 (the bottom). Screen readers get a trackbar's value from the MSAA proxy (WinForms' UIA value pattern included), which
  reports the position as a percentage of the range it queries (`TBM_GETRANGEMIN/MAX`): a 25–300 speed at 100 read "27".
  `CueSlider` answers that query as 0–100 (WinForms never sends it), so the proxy reads the number itself ([companion-app-winforms](../../reference/ui-accessibility/companion-app-winforms.md)).
- Check boxes (`Ui/AccessibleCheckBox`, user report 2026-10-02): WinForms raises no toggle event, so Space was silent.
  They have no UIA provider (readers use their MSAA object) and send the MSAA state change themselves ([companion-app-winforms](../../reference/ui-accessibility/companion-app-winforms.md)).
- With **Preview** on, the cue plays at its new level 150 ms after the last change (`Ui/CuePreview`, the mixer's own preview
  voice, audible although the game is in the background); a repeating cue plays two to six times at its speed.
- Speed changes the time between repetitions, never the pitch: the object and quest loops overlap or space out their copies
  (`D4Assist.Audio.LoopVoice` rate), the guide ping and NPC tap divide their interval.
- **General** holds the other options (language, radar orientation, speech, announcements, guide, routes, outside dungeons,
  closed doors, cue pauses). They take effect on restart; saving a change there offers to restart now.
  Its first row is the Diablo IV folder (`InstallPath`, read-only, "Automatic" when empty) with **Browse...**
  (`Ui/InstallFolderPicker`: the picked folder or a `Diablo IV` up to three levels under it, else a warning and a retry).

## Beacons

While in a dungeon the RActor pool (`D4Memory.Actors`) is read every 500 ms (`GameSession.PollActorsOnce`);
`ActorLoopBridge` (with `LoopTargetSelector`) picks the actors named in `BeaconActorNames` (resolved to SNOs through CoreTOC at startup) and,
inside dungeons, every portal (`Game/PortalClassifier`: portal gizmos, warp pads and portal switches, never town portals;
[dungeon-objectives](../../research/dungeon-objectives.md) §7) and,
on every player update, loops the quest-item sound (embedded in `D4Assist.Audio`) at each one, panned and faded by its offset in screen axes
(`ScreenDirections.ToScreen`). The loops share the radar's mixer/output and stop on any world change. If the
actor pool is unavailable the app logs it once and the beacons stay silent. `--debug` logs a `BEACON` line when
a target appears or disappears (`LOOP started/stopped`).

Waypoints the player hasn't activated loop the game's "waypoint discovered" sound (`waypoint.wav`, `Game/WaypointBeacons`, settings cue `Waypoint`, in every world with
waypoints): at their spots in the game data (`D4Data.Readers.WaypointCatalog`), not at streamed actors, within the beacon reach;
each falls silent once the activated list (`ActivatedWaypointsReader`, re-read every 2 s while one is near) holds it.
`--debug` logs `WAYPOINT loop started/stopped`.

`Game/PortalMemory` keeps every portal seen in the dungeon instance (also across warps inside it) and marks the ones the
player appeared or vanished within 15 units of as used: they lead back, the others lead on. A portal is also known as soon as
the tile that places it loads (`Game/ScenePortalReader`, the tile's "Game"/"Shared" marker layers; not in the Pit or the
Undercity, not on rotated tiles); one still missing when the player stands within 12 units is dropped until it appears. A
portal the world's objective quest names (a hub's side dungeons) leads as a dungeon objective, before unexplored ground, until
used (research/dungeon-objectives.md §18). `--debug` logs `PORTAL seen` (with its distance and whether it was arrived by),
`PORTAL placed by tile <tile>: …`, `PORTAL not there: …` and `PORTAL went through / arrived by`.

## Objective sounds

The same bridge loops the quest sound (never spoken, positional, D2R quest profile at full near gain) at the dungeon's
objectives: actors the world's DPO quest names (`D4Data` `DungeonObjectiveReader`: world SNO, Global `zone_progress`,
`DPO_*` quest callbacks; a monster it names is a kill target, reason `killtarget`, such as Maulwood's sentinel; other
monsters stay silent; the "enter the trigger" zones it names, `Trigger_Sphere_8m` and kin, never loop: invisible, never used, and
their generic SNO names every copy, research/dungeon-objectives.md §2), plus every carryable (on the ground; the copy in the player's hands, with no
ACD, stays silent), carryable receptacle (a quest-locked one only while the player holds a carryable it takes; other quest steps
wait meanwhile), quest
switch and quest chest, switches and notes named for a quest (`QST_`, `Quest`, `Prologue`), boss-summoning monoliths (door gizmos
named `Monolith`, reason `monolith`, research/dungeon-objectives.md §15), and
free-able prisoners (name contains `Prisoner`, not `Dead`, monster or switch actor). A closed door within 8 units of a receptacle
still empty, unless the DPO names it, waits silent: filling the receptacles opens it by script (`Game/CarryGoals`,
research/dungeon-objectives.md §14). A hostile unique or boss the followed quests' current phase names is the quest step too
(`ActorState.NamedByQuest`, reason `killtarget`, research/dungeon-objectives.md §16); ordinary monsters a phase names stay silent,
and a kill target's remains (no ACD) are silent. Rules and data:
[dungeon-objectives](../../research/dungeon-objectives.md). The exit pads keep the beacon profile and are the only loops that
mute the exploration guide. A loop stops when its actor leaves the placed list, or when the gizmo was used
(`Game/GizmoUsedSignal`: the game's own mark, ACD attribute `Gizmo_Has_Been_Operated` (corpses, herbs, signs, doors;
research/client-memory.md §4); else a chest's glow effect gone while the player is within 10 units, else the RActor
state word; remembered by spot for the world visit, so a re-created actor stays silent). A door-type objective (monolith, blocker,
quest door) is used only by the ACD marks: operated, or `Gizmo_State` set. Chest kinds seen glowing are saved next to
the exe in `d4assist-chests.json`, so a chest first seen already open is judged too. `--debug` logs one `LOOP started/stopped` line
per loop with its reason (`dpo`, `gizmo`, `prisoner`, `beacon`). Prisoners loop D2R's corpse sound, the others the quest sound.
A prisoner NPC that walks along beside the player (freed, following them, through portals too) falls silent and stops leading
the guide (`Game/FollowerWatch`, `cause=follows`, research/dungeon-objectives.md §23).
Quest gizmos (ACD `Gizmo_Is_Quest_Restricted`, research/client-memory.md §4): one the game shows on the minimap
(`MinimapActive`), or one an active quest's current phase names (resolved offline by `D4Data.Readers.QuestScriptMarkerReader`:
a marker at its spot, or its actor SNO for a gizmo a dungeon tile places), is the quest's current step and gets the quest loop whatever it is
(`ActorState.QuestStep`, reason `quest`). So does an NPC showing a conversation icon whose kind the current phase names (the
one to talk to; it keeps its loop while it moves), or whose `Conversation_Icon_Quest` is a followed quest (`ActiveQuests.Owned`;
the log names each NPC's offered quest once: `Quest conversation: actor … offers quest …`, research/dungeon-objectives.md §26). Any other quest gizmo, or one with `Gizmo_Disabled_By_Script`, isn't usable yet
(`ActorState.Unusable`) and stays silent. Active quests (`ActiveQuests`): the player's tracked quest (`ManuallyTrackedQuestSNO`)
plus the quests in progress here, those with markers in this world or its scenes, or on an actor loaded around the player (an
overworld side quest marks its objects in a subzone layer set), the world's objective quest, and any quest a phase of a
followed quest waits on (`ScriptEventParamQuest`: the game then shows that quest's phase instead, research/dungeon-objectives.md
§18, §24), whose {quest, phase} pair the module data holds. A quest found in progress stays followed while that pair still
reads, so walking away from its objects doesn't drop it. `--debug`
logs `Quest tracked: <snos>; in progress here: <snos>; N object(s) named, <sno> at phase <uid>.` on each change and
`LOOP silent … cause=unusable|used` once per gizmo silent at first sight. It also logs every multi-use switch (braziers, levers)
once seen and on each change of its state signals: `SWITCH <name> sno=<sno> at (x,y): <signal>=<old>→<new>` (`Game/SwitchStateLog`,
research/dungeon-objectives.md §17).
The Undercity attunement switches (spirit hearths and beacons, name token `Enticement`, research/dungeon-objectives.md §5)
are objectives too (reason `enticement`), on their own settings cue; they fall silent once used.
Quest places: the current phase of an active quest names spots such as a trigger to walk into ("chase Lilith"); the
exploration guide leads to them in the quest tier, before unexplored ground, until the phase moves on
(`ObjectiveMemory.ObservePlaces`; research/client-memory.md §4). `--debug` logs `… in progress here: …` and
`OBJECTIVE seen (quest place): …`.
Structures the player must break (a hostile `*_Multistage` actor some quest names: the hatred tumors, a few cracked walls and
gates, research/dungeon-objectives.md §12) loop the quest sound (reason `destructible`) until destroyed, when the remains
lose their game record (ACD).

## Safe zones in boss fights

`Game/SafeZoneClassifier` picks the places the player must stand in to be protected (research/dungeon-objectives.md §10):
allies' bubbles (Donan in the Hell battlefield, Lilith in the Skovos mindscape, Vigo's dome against Lilith's Lament),
lights to stay in (Lingering Hatred, moving lights) and safe zones the game names so. A non-hostile trigger named
`SafeZone`/`Bubble`/`Suppressing` is sized by its proximity shape (`ActorDefinitionInfo.TriggerRadius`); other gizmos named
`SafeZone` and actors named `Bubble_Caster`, `Barrier_Border`, `Lingering_Light` or `MovingLight` get a 5-unit radius. A light
carrier loops instead of the NPC tap. They loop first in the voice budget, with the beacon profile, and fall silent while the player is inside
(radius minus 1 unit). `--debug` logs `LOOP inside`/`LOOP outside`, and `ACTOR seen` for any `Bubble` actor.

## Boss-fight objects

Mephisto's stolen-skill orbs, Akarat's lights against the Harbinger of Hatred and the Horadric Guardian's sigil vessels loop
as objectives, each on its own settings cue (research/dungeon-objectives.md §11). Orbs and lights are no gizmos: they loop
until they leave the actor list. The vessels are used once per barrier, so they never fall silent for being used, only
while the game locks them (`LoopTarget.Reusable`). `Game/StolenSkillsAnnouncer` speaks Mephisto's theft at the first orb
and the recovery once no orb was seen for 2 s.

## Doors, chests and readables

`Game/InteractableClassifier` gives every other gizmo a D2R loop by ActorDefinition gizmo type
([dungeon-objectives](../../research/dungeon-objectives.md) §6):
- doors (`DoorGizmoData`, not `*StartOpen*`) → wooden, metal or stone door, picked from the name;
- chests (`ChestGizmoData`, reward and locked chests) → chest;
- readables (`SignGizmoData`, `*Lorebook*` chests) → scroll;
- the "look" spot (`POI_Look_At`, the only gizmo of type 29) → scroll, and it keeps sounding after use (it can be looked at again);
- healing wells (`Healing_Well_*`, gizmo type 11, which shrines and pylons share) → D2R's well loop (Diablo IV's own drink sound), a
  container for the voice cap; tester request 2026-10-16 (the game spawns most at runtime, research/dungeon-objectives.md §22);
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
  records by a background scan and retries after a miss; every scan logs `MENU located N/M screens in T ms` (no `--debug`
  needed: after a return to character select a tester's log showed only the self-check failure). After the NPC interaction menu (`screen_InteractPopup`) closes,
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

## Jump points in the guide

A gap the walk map can't cross may carry a jump: two `Traversal_Gizmo_Jump` points a few units apart, one on each side, used
with the interact button (the Philios mindscape's `Sightless_Skovos_SW_Traversal_01`: one tile's floor in four islands joined
by three pairs, live 2026-10-18). `Game/JumpLinkCollector` reads each loaded tile's jump points offline (every marker layer:
some tiles keep them in `Traversals`), pairs each with its mutual nearest within 8 units and hands them to
`NavigationBridge.SetJumpLinks`. `RoomPathfinder` then joins the floor within 2.5 units of one end to the floor nearest the
other (the leap costs its length + 4), and while a route needs a jump the guide aims at the first jump point
(`GuideStep.JumpAt`, `--debug`: `GUIDE via jump at (x,y)`, `JUMPS <tile>: …`). No sound of its own: the game sounds jump
points (user, 2026-10-18).

## Barriers in the radar and the guide

`Game/BarrierObstacles` lays every barrier wall a dungeon raises on the 1-unit grid: a non-gizmo actor named
`*barrierWall*` with a box (the door footprint, turned by its yaw). It stays blocked while the client drops it far away,
and is dropped once the player is within 20 units of its spot without it: the game removes the wall when its objective is
done (live, Maulwood). `Game/BarrierBridge` hands the cells to `NavigationBridge.SetBlockedCells` (the guide splits a tile
the wall crosses into regions and never routes through it, src/D4Assist.Navigation/README.md) and to
`DungeonRadarWorld.SetBarriers` (wall cells). The log always says `BARRIER standing …` and `BARRIER gone …`. Config
`LiveBarriersEnabled` (research/dungeon-objectives.md §13).

`Game/LockedDoors` adds, for the guide's routes only (the radar sounds every closed door), the closed doors the player can't open
yet: the game won't let them be used (`QuestLocked` or `ScriptDisabled`, e.g. a key door before its key), or the player stood
within 3 units of the door and walked 20 away with it still closed (tried). A tried door waits until its quest state
(`Unusable`, `QuestStep`, `NamedByQuest`) changes or the guide is switched on; any locked door is open once seen open, or missing
within 15 units of its spot, and stays a wall while the client drops it far away. An objective at a locked door never leads
(`ObjectiveMemory`). When a followed quest moves on from a known phase (`GameSession.QuestMovedOn`), every locked or tried door
leads again and every barrier not listed now stops blocking: a boss room's door opens by script once the objective is done,
often while the player is too far for the client to list it (research/dungeon-objectives.md §22); one still locked locks again
once listed. The log always says `DOOR locked|unlocked|tried|re-armed|open …`. When a barrier or locked door falls, the
guide explores the ground it held back first (src/D4Assist.Navigation/README.md).

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
the nearest NPCs within 15 units, on every player poll: the two nearest by default, 1 to 4 in the settings (`Sound.NpcVoices`;
D2R taps one, user request 2026-10-02). Each repeats on its own rhythm, faster as the NPC gets closer: 450 ms at the edge,
150 ms at contact. It is panned by screen side and pitched lower when the NPC is below the player.
- **Which NPCs:** `Game/NpcClassifier` takes the fIsNPC monsters off the hostile team, including quest followers and
  escorts. It leaves out hired mercenaries (family `NPC_Mercenary` on the player's team; the Den's Raheir, on the neutral
  team, taps), cinematic stand-ins and prisoners, which have their own loop
  ([dungeon-objectives](../../research/dungeon-objectives.md) §9, §26).
- **Followers go quiet:** a quest companion following the player carries the ACD attribute `Follower_Quest`
  (`ActorState.QuestFollower`, research/client-memory.md §4) and never taps (user, 2026-09-30). `--debug` logs
  `NPC follows the player, silent`.
- **No line of sight:** the open world loads no walk grid, so an NPC behind a wall taps too.
- **Muting:** the cue gate mutes it like every cue.
- **Log:** `--debug` logs `NPC tap: <name>` when an NPC starts tapping.

## Enemy sound

`Game/EnemyCueBridge` plays D2R's enemy cue (`Audio/EnemyCuePlayer`, `Audio/EnemyCueSynth`, a port of d2r-access
`entity_cue_synth.cpp`): a short growl with a ring over it for bosses and minibosses, with chattering teeth for unique
monsters. Every world, quest or not (tester request 2026-10-16: an open-world boss and boss lairs had no sound).
- **Which monsters:** `Game/EnemyClassifier` reads the hostile monster's `eOverriddenMonsterRarity` (4 boss, 2 unique;
  [dungeon-objectives](../../research/dungeon-objectives.md) §16). Elites and champions get their rarity at spawn, which
  the offline data doesn't hold: not cued yet (Next step in STATUS).
- **Which ones sound:** the nearest living boss and the nearest living unique within the quest loop's reach (27 units),
  as D2R keeps one per class. An invulnerable one (a boss between phases) and remains (no ACD) are silent.
- **Cadence:** 600 ms at the edge, 150 ms at contact, never two starts within 100 ms (d2r-access `entity_cues.h`); panned by
  screen side, lower when below the player. Settings: `Boss` and `Elite` in the characters group.
- **Kill targets:** a boss or unique a quest or the dungeon names keeps its objective (the guide leads to it) but plays
  this cue instead of the quest loop (`LoopTarget.EnemyCue`).
- **Log:** `--debug` logs `ENEMY cue (<class>): <name>` when an enemy starts sounding.

## Exploration guide

`Game/ExplorationGuideBridge` asks the navigation engine's `ExplorationGuide` for a waypoint on every player poll
and tile update and aims `GuidePingPlayer` at it (no buttons). Outside the endgame it first leads to an objective heard
earlier and not done: `Game/ObjectiveMemory` remembers every objective loop (`ActorLoopBridge.ObjectivesSeen`), the
tracked quest's step before the dungeon's own objectives (prisoners, altars, levers, carryables). A receptacle leads only while
the player holds an item it takes, then in the quest tier; empty-handed, the guide looks for the next carryable. An objective that falls
silent within 20 units of the player is done (used, locked or removed). The guide walks the last stretch over the room's floor (the
walk-grid path to the floor within reach of the object, not a straight line through what stands before it) and, once the player is
within reach (3 units past the edge of the actor's box, a trigger's own radius), chimes once and holds silent while the objective's
own loop sounds; a step away it points again. One the player stood at and then walked 20 units away from, still undone, is passed
over until another objective is done (a key found may open a door visited earlier) or the guide is switched on again. A door
the player can't open yet is a wall for the guide and never its goal; once it or a barrier falls, the ground past it is explored
before the ground left behind (Barriers in the radar and the guide).
A gizmo the current phase names is a goal before it is heard: where a loaded tile places it (not triggers: the map marks the copy
a phase means) or where the game's map marks it (research/dungeon-objectives.md §19-20). Near the player it is done once its actor
is silent (used); a tile's copy with no actor there was never spawned; one standing there that loops as something else (a door) or
not at all waits for the phase to move on, as does any copy the map marks. An objective the live game placed in this world (heard,
or on the map) but on ground not loaded or reachable yet draws exploring towards it: each unexplored target costs its route plus the
straight distance on to the objective, and the guide says it leads to the objective.
Pit and Undercity keep the order below unchanged (user, 2026-09-30). Switching the guide on says where it leads ("Hacia el
objetivo de la misión." / "Hacia un objetivo de la mazmorra." / "Hacia zona sin explorar." / the portal lines / "Todo explorado.");
while on, a change of goal is spoken once, and a new floor starts exploring without a word. `--debug` logs
`OBJECTIVE seen|seen ahead|seen ahead (map)|reached|left undone|done|re-armed` and `GUIDE holding at <id>`.
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
are visited in the shortest walk from that start to the route's end (`RouteOrder`). A route may end past its last
point: the one marker of a named actor that the next quest's scripts name (`QuestRoute.EndQuest`/`EndActor`). Data and
reasoning: [campaign-routes](../../research/campaign-routes.md).

- A route starts when one of its quest actors appears in the live actor list within 15 units of the quest marker or
  of a point, with the player inside the route's area, or when the player stands within 15 units of the quest marker.
  The guide NPC also stands at other quests' places (the drug den, the Forsaken Chapel, the palace steps), which never
  start it. It speaks "Guía de refugios." and joins at the end of the nearest leg (`RouteGuide`).
- Within 4 units of a point it chimes and speaks "Refugio 1 de 3."; reaching a later point passes the earlier ones.
  The ping then holds while the player stays within 10 units of that point (a shelter is where they wait out the
  storm; the game's own audio says when it ends), and leads on to the next point once they step out. After the last
  point it leads on to the route's end, if it has one; there it chimes, speaks "Fin de la guía de refugios.", stops and
  doesn't restart until the world changes.
- It stops when the player is more than 150 units from the start, every point and the end, and on any world change.
- A trail (`RouteKind.Trail`, no quest marker) is a row of markers the set lists in walking order; it starts at the
  first, and only a live quest actor starts it. It speaks "Guía del rastro de pétalos.", counts a point reached within
  6 units, leads straight on without holding, and speaks only "Fin del rastro." at its last point.
- `--debug` logs the loaded routes at startup and one `ROUTE` line per start, point, stop and mute.

Routes so far: `sandstorm-shelters`, the three `BCD_Trigger_EliasPalace_SafeZone` safe zones of
`VGN_Kehj_EliasPalace_Sandstorm`, ending where `QST_Kehj_EliasPalace_03` has Meshif on the palace steps (a player's log
of 2026-10-19, no `--debug`, confirms the order; not live-tested with `--debug` yet), and `philios-blood-petals`, the
trail of 21 `QST_X2_Skov_Lilith_BloodPetal_Trigger_Sphere` markers in `Skov_Philios (Skov_Philios_Campaign_Design)`
from Lorath to the waterfall shrine in "Auf blutigem Pfad" (`QST_Skov_Philios_06`, 2440931), started by a live petal; and
`warlock-pentagram` (`RouteKind.Pentagram`), "Folgt dem Pentagramm." in "Hexenmeister: Bauch der Bestie"
(`QST_Warlock_ClassQ_02` phase 5, sub-quest `QST_Warlock_ClassQ_Trace_Ritual`): the 20 `Trigger_Sphere_8m` markers of
`Step_WesternFlats (QST_Warlock_ClassQ_2a)`, ~2.5 units apart in strokes ~10 units long, which the game's map doesn't mark.
It starts only once `QST_Warlock_ClassQ_Trace_Sacrifice` is used (`StartWhenUsed`), counts a point within 1.5 units
(`ReachUnits`), and is traced strictly in order from its first point (`RouteGuide` `inOrder`: its strokes cross, and a
later point 0.4 units from an earlier one must not skip a stroke). It speaks "Guía del pentagrama." and "Fin del
pentagrama." (live 2026-10-19: worked). It is walked in parts (`Sections` 5, 7, 8): live the quest moved on to its dungeon after the start and the first stroke. At a part's end the ping waits up to 6 s; if a followed quest moves on then (`GameSession.QuestMovedOn`), the route stops and goes on with the next part once the player has been 150 units away and comes back (the sacrifice stays used); otherwise it leads on. While a route leads, D-pad left mutes it and starts no quest-place guide.

### Quest places over a found path

`Game/QuestPathBridge` leads, in the open world and in the story areas (`WorldKind.RadarOnly`, which have no exploration
guide; tester 2026-10-22 in the Sightless Eye), to a quest place the game's map marks, for when the game's own
navigation assist doesn't lead there (live 2026-10-19: "Jünger des Verbotenen" until the quest was untracked and tracked
again). A quest place is a map marker (`GameSession.MapMarkersRead`) whose actor a quest's scripts name at that spot
(`QuestScriptMarkerReader.QuestsNaming`, 2 units), named by the quest's title in the game's language, or the game's own quest marker `Symbol_Quest_Proxy` or `MarkerLocation` (map kind 12; a `MarkerLocation` marked the last siege tower of "Verwundete Seelen", live 2026-10-21), which it sets on area steps ("kill the fiend roaming here", a pentagram, a dungeon entrance; live 2026-10-19): it is offered first and named after a quest read as in progress (`ReadOwnedQuests`) with an object within 60 units, else "Marcador de misión" (the nearest quest of all named the fiend's spot after a side quest). Quest objects are offered only for quests read as in progress: the map also shows objects of quests not taken. A quest the map names counts as in progress when its {quest, phase} pair is found, or else at the phase last read, kept across runs in `d4assist-quests.json` next to the exe (`QuestPhaseHistory`, a phase not read for 30 days is dropped): the module data holds one quest's pair at a time, and after an app restart "Verwundete Seelen" was never found again, its pair taken by a nearby collectible quest (live 2026-10-21). Places within 8 units are skipped (standing at one, it was reached at once and the next press started over with it). It follows a real
path: `Game/OverworldPaths` reads the surfaces of the world's chunks around the walk (`SceneReader.SurfaceVoxels`, the
scenes' compressed `m_cellBytes` with their heights) and `D4Assist.Navigation/SurfacePathfinder` searches them as a 2.5D
map: a step joins two floors only when their heights differ by at most 1 unit, so a cliff's top and foot stay apart; a step
near an edge costs more. Traversals join the floors they split: every two traversal ends the chunks place
(`Traversal_Gizmo_*`, `MarkerLocation_TraversalExit`, any marker layer) within 8 units are a link, a slide one way down
(`SurfaceLink`, [dungeon-data](../../research/dungeon-data.md) §4.1c).

- **D-pad left** there leads to the nearest marked quest place within 1500 units and speaks "Objetivo de
  misión: <quest>, N metros.", with "K de M." added when several places are marked; each further press moves on to the next nearest; a press after the last says "Guía
  desactivada." and stops. With none marked it says "No hay objetivos de misión en el mapa.".
- The ping aims 8 units ahead of the player's spot on the path (`PathFollower`), so a path point passed aside never
  pulls back. More than 12 units off the path, a new path is searched from where the player stands, at most every 3 s.
  With no path it says "No encuentro un camino; te indico la dirección." and points straight at the place.
- Within 4 units of the place it chimes and speaks "Objetivo de la misión alcanzado.". It stops beyond 1800 units (a
  teleport) and on any world change; torn player reads (far beyond any world) are ignored.
- `--debug` logs one `PATH` line per start (quest, spot, distance), path (points, length, search time), stray and stop,
  and where the ping aims every 5 s.

## Controls

- **D-pad left** (any XInput controller, or a DualSense / DualShock 4 over USB or Bluetooth) — toggles the exploration guide for the current kind of world (Undercity and
  Pit vs other dungeons) for the rest of the run, and speaks "Guía activada." / "Guía desactivada.". Outside a dungeon
  it mutes and unmutes a leading quest route the same way, leads in the open world and the story areas to the quest
  places the map marks (see Quest places over a found path), and otherwise says "La guía solo funciona en mazmorras." (a
  silent press read as a pad the app can't hear, tester 2026-10-16). A press acts as the button comes back up; **held**
  for 1 s (`Input/PressHold`) it switches the dungeon guide between the objectives and exploring instead ("Guía:
  exploración." / "Guía: objetivos.", switching the guide on if off; exploring leads past every objective to unexplored
  ground; each new world starts on the objectives; endgame worlds only explore; tester 2026-10-27, user's binding
  2026-10-28; logs `Guide mode pressed.`, `--debug` `GUIDE mode explore|objectives`). Ignored while a game menu is open
  (inventory, map, vendor…: the D-pad navigates it; `Input/GatedPad` over `MenuMute`, `--debug` logs `GUIDE toggle ignored`),
  and while Diablo IV isn't the foreground window (another game, or the game closed: the pad is read globally);
  every press past that gate logs `Guide toggle pressed.`. `Input/XInputPad` only observes the pad (polled every 16 ms, dynamic-loaded XInput, ported from the
  D2R mod's `input_direction.cpp`; logs `Pad: XInput loaded from …` and `Pad: XInput slot N connected|disconnected`); the game still receives every press. PlayStation pads never reach XInput, so
  `Input/SonyHidPad` reads their HID input reports (shared, read-only handle; rescans every 3 s for pads plugged in
  later; logs `Pad: … connected` or why it can't open one) and `Input/CombinedPad` drops a second copy of the same
  press within 200 ms (Steam Input / DS4Windows also expose the pad as a virtual Xbox pad). Not saved to `d4assist.json`.
- **Ctrl+Alt+Shift+1..8** (keyboard, while the game is in front) — sound examples, e.g. for a video without a fight's noise (user request
  2026-09-29): the name is spoken, then 1 exit, 2 open floor, 3 wall, 4 door, 5 unexplored exit (radar cues, once per
  direction up/right/down/left), 6 the quest loop twice, 7 the guide ping three times, 8 the arrival chime. One example
  at a time. `Input/GlobalHotkeys` (Win32 `RegisterHotKey`), `SoundDemo`. The digits and G are registered only while the game
  window is in front (checked every 250 ms) and freed for other programs otherwise (user, 2026-10-26: only C and Q work everywhere).
- **Ctrl+Alt+Shift+G** (keyboard, while the game is in front) — the D-pad left toggle for keyboard players (user request 2026-10-02), also
  while a game menu is open (no menu uses it). `Input/GlobalHotkeys` joins the pads' `CombinedPad` after the menu gate.
- **Ctrl+Alt+Shift+E** (keyboard, while the game is in front) — holding D-pad left: the guide's mode, objectives or exploring.
- **Shift+F1** (keyboard, world map only) — the waypoint list (user request 2026-10-18; `WaypointListEnabled`):
  the waypoints the local player's `PlayerSavedData.arWaypointsActivated` lists (`ActivatedWaypointsReader`; nothing when
  it is unreadable, never all of them) in the current world (`WaypointCatalog`, names from the game's StringLists),
  nearest first. Up/down speak `name, N meters, direction. i of n`; Enter moves the mouse to the waypoint's pixel
  (`MapViewReader` center and zoom, `MapProjection`) with `SendInput` (`Input/GameMouse`, the app's only input to the
  game, never a click) and closes the list; Escape closes it. A waypoint off the view that zooming out would show says
  "zoom out"; one farther than the map shows even all the way out (zoom 3046: ~1400 units above and below the center,
  live 2026-10-19, Farobru from Temis and Ichorfall) puts the cursor on a stopover instead: the activated waypoint in
  view nearest to it, preferring one from which it is in view (`WAYPOINT hop …`), and says to choose it again after the
  trip; with no stopover it says which way to move the map. `Input/MapListKeys` registers Shift+F1 only while
  `screen_Map` is open and the game has the focus, the list keys only while the list is open (the map closing or the game leaving the front closes it). `--debug` logs
  `WAYPOINTS …` and `WAYPOINT jump <name> -> (x,y) …`. research/client-memory.md §4.
- **Ctrl+Alt+Shift+C** (keyboard, anywhere) — opens the settings window (see Notification area and settings window).
- **Ctrl+Alt+Shift+Q** (keyboard, anywhere) — exits the app at once, as Exit in the notification-area menu.

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
- `Update/` — the updater (see Updates).
- `Ui/` — `TrayApp` (notification-area icon, menu, settings hotkey), `SettingsForm`, `CueSlider`, `CuePreview`.
- `SingleInstance.cs` — one running copy per Windows session.
- `Game/` — `WorldLogWatcher`, `WorldClassifier`, the `GameSession` state machine
  (`WaitingForGame -> Idle -> InDungeon`), `IDungeonFeed`/`TileState`/`PlayerState`, and
  `LiveDungeonSessionReader` (the production `IDungeonSessionReader`, built on `D4Memory`), `ReadHealth` (the
  self-check), the radar's
  `DungeonRadarWorld`/`RadarBridge`, and `LockedTileWalkability` (one walk-map lookup shared across poll threads).
- `Program.cs` — wiring and the run loop.
- `Portable/README.txt` — the players' readme (English), published next to the exe by `Properties/PublishProfiles/Portable.pubxml`
  (single-file, self-contained; see the root README).

## Tests

`tests/D4Assist.Tests` covers `WorldLogWatcher`'s line parsing, partial-line handling and truncation/rotation
recovery (real temp files, no game install needed), and `GameSession`'s state transitions, diff-gating,
player/tile polling and the one-scan-per-entry fallback rule — all via a fake log feed, a fake
`IWorldClassifier` and a fake `IDungeonSessionReader`, so no process access happens during the build/test loop.
`DungeonRadarWorldTests` builds the radar's world from synthetic tiles and a fake walkability lookup.
