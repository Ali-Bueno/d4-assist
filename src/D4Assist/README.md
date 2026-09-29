# D4Assist

The Diablo IV Dungeon Assist companion app: a standalone, read-only .NET console app that speaks dungeon
entry/exit and (once wired up further) tile/position state through a screen reader while you play. It never
injects into or writes to the game process — see `src/D4Memory`'s safety policy, which this app reuses as-is.

## What it does today

- Tails `<install>\FenrisDebug.txt` for world-transition lines (`WorldLogWatcher`) — no memory access at all
  while not in a dungeon.
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
- Plays the D2R-ported reactive radar (`src/D4Assist.Radar`) while in a dungeon: four fixed stick directions,
  exit/floor/wall cues on confirmed changes only (`Game/RadarBridge`, world from `Game/DungeonRadarWorld`).
- Loops positional beacons at configured actors such as the Undercity exit pad (`Game/ActorLoopBridge`, see Beacons and objective sounds).

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
| `DefinitionsPath` | `definitions.json` | d4data `definitions.json`; relative paths resolve against the exe's folder. The build copies `data/definitions.json` there when it exists. |
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
| `ObjectiveSoundsEnabled` | `true` | Play quest loops at dungeon objectives (see Objective sounds). |
| `ObjectiveVolume` | `1.0` | Objective loop volume, 0 to 1. |
| `BeaconActorNames` | `["X1_Undercity_WarpPad"]` | CoreTOC actor names that get a beacon; unknown names are logged once and ignored. |

## Beacons

While in a dungeon the RActor pool (`D4Memory.Actors`) is read every 500 ms (`GameSession.PollActorsOnce`);
`ActorLoopBridge` (with `LoopTargetSelector`) picks the actors named in `BeaconActorNames` (resolved to SNOs through CoreTOC at startup) and,
on every player update, loops `Sounds/quest-item.wav` at each one, panned and faded by its offset in screen axes
(`ScreenDirections.ToScreen`). The loops share the radar's mixer/output and stop on any world change. If the
actor pool is unavailable the app logs it once and the beacons stay silent. `--debug` logs a `BEACON` line when
a target appears or disappears (`LOOP started/stopped`).

## Objective sounds

The same bridge loops the quest sound (never spoken, positional, D2R quest profile at full near gain) at the dungeon's
objectives: actors the world's DPO quest names (`D4Data` `DungeonObjectiveReader`: world SNO, Global `zone_progress`,
`DPO_*` quest callbacks) minus monsters, plus every carryable, carryable receptacle, quest switch and quest chest, and
free-able prisoners (name contains `Prisoner`, not `Dead`, monster or switch actor). Rules and data:
[dungeon-objectives](../../research/dungeon-objectives.md). The exit pads keep the beacon profile and are the only loops that
mute the exploration guide. A loop stops when its actor leaves the placed list; the "used" signal is unknown, so
`ActorLoopBridge` takes a `isUsed` predicate (default never) for it. `--debug` logs one `LOOP started/stopped` line
per loop with its reason (`dpo`, `gizmo`, `prisoner`, `beacon`).

## Exploration guide

`Game/ExplorationGuideBridge` asks the navigation engine's `ExplorationGuide` for a waypoint on every player poll
and tile update and aims `GuidePingPlayer` at it (no buttons). It mutes while `ActorLoopBridge.HasTarget(LoopKind.ExitBeacon)` (the exit pad
takes over), stops on a world change, and when every reachable part is visited stays silent and speaks
"Todo explorado." once per dungeon (`interrupt=false`). `--debug` logs one `GUIDE` line per target change and per toggle.
Each target reached plays the arrival chime (a target reached while the beacon leads never chimes late). Once a
Undercity boss-room tile (`*_FLEX_POI_BossRoom_Undercity`) loads, the guide stays off until the world changes:
the last warp lands there and the run timer no longer matters (user request, 2026-09-28).

## Controls

- **D-pad left** (any XInput controller) — toggles the exploration guide for the current kind of world (Undercity and
  Pit vs other dungeons) for the rest of the run, and speaks "Guía activada." / "Guía desactivada.". It does nothing
  outside a dungeon. `Input/XInputPad` only observes the pad (polled every 16 ms, dynamic-loaded XInput, ported from the
  D2R mod's `input_direction.cpp`); the game still receives every press. Not saved to `d4assist.json`.

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

## Tests

`tests/D4Assist.Tests` covers `WorldLogWatcher`'s line parsing, partial-line handling and truncation/rotation
recovery (real temp files, no game install needed), and `GameSession`'s state transitions, diff-gating,
player/tile polling and the one-scan-per-entry fallback rule — all via a fake log feed, a fake
`IWorldClassifier` and a fake `IDungeonSessionReader`, so no process access happens during the build/test loop.
`DungeonRadarWorldTests` builds the radar's world from synthetic tiles and a fake walkability lookup.
