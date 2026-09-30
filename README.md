# Diablo IV Dungeon Assist

An external companion app that makes Diablo IV's procedural dungeons navigable for blind players. It runs
next to the game (controller + NVDA through D4's third-party screen-reader option) and adds:

- **Spoken room announcements** through PRISM, in Spanish or English following the game's text language:
  junctions, dead ends, big rooms and the dungeon entrance, with their exits as controller-stick directions
  ("Cruce. Salidas: arriba, derecha sin explorar." / "Junction. Exits: up, right unexplored.").
- **A reactive audio radar**, a port of the author's Diablo II Resurrected radar: four screen directions,
  a bright tin for a passage into another room or unexplored area, a breath for open floor and a dull tock
  for a nearby wall.
- **Positional quest loops** (the D2R quest sound): every portal in a dungeon (its exit, the portals between floors,
  the Undercity next-floor pad), and dungeon objectives
  (prisoners, switches, carryables and receptacles, quest chests, the actors the dungeon's objective quest names).
- **An exploration guide**: an automatic ping along the path to the nearest unexplored part (on by default in
  the Undercity and the Pit, D-pad left toggles it, ignored while a game menu is open), with an arrival chime. With
  nothing left to explore it leads to a portal seen earlier, preferring one the player has not come through.
- **Campaign routes** outside dungeons: the same ping leads point by point through known story stretches, such as
  the shelters from the desert sandstorms on the way to Elias's palace with Meshif ("Refugio 1 de 3.").
- **A settings window** from the notification-area icon (or Ctrl+Alt+Shift+C): every sound can be switched off, levelled
  and, when it repeats, sped up or slowed down, per group or one by one, with a live preview; screen-reader friendly
  standard controls. The app has no console window; the icon's menu also restarts and exits it.

Works in every DRLG dungeon: normal, campaign, expansion and Nightmare dungeons, the Undercity and the Pit.
Outside dungeons only the quest loops and the campaign routes run (no radar or announcements there). It never
moves the character and never reveals enemies or loot.

## How it works

Dungeon layouts are generated on the server, so the app reads them from the running game, **read-only**. It
never writes to the game, injects code or hooks it.

- **Offline data:** the scene library comes from the local game install (CASC): tile exits and walkability.
- **World identity:** read from the game's own log (`FenrisDebug.txt`).
- **Live data:** the placed tiles, the player position and nearby actors are read from process memory. Tiles
  are read only in dungeons; the position and actors also outside them (`OutsideDungeonsEnabled`), never in
  menus or loading screens.

## Requirements

- Windows, the .NET 10 SDK, and Diablo IV installed (build 3.2.1.73552; memory offsets are per build). The
  install is found through Battle.net.
- A screen reader (NVDA tested), reached through PRISM (Prismatoid NuGet package).
- `data/definitions.json` from d4data for the same build (not committed); the build embeds it in the exe.

## Build and run

```
dotnet build D4DungeonAssist.sln
dotnet test D4DungeonAssist.sln
src\D4Assist\bin\Debug\net10.0-windows\win-x64\D4Assist.exe [--debug] [--no-speech] [--sound-test]
```

Configuration lives in `d4assist.json` next to the exe, edited from the settings window; see
[src/D4Assist/README.md](src/D4Assist/README.md). Output goes to `d4assist-log.txt` next to the exe.

Portable build for testers (no .NET install needed):

```
dotnet publish src/D4Assist -p:PublishProfile=Portable
```

It lands in `artifacts/D4DungeonAssist/` (git-ignored) with three files: a single self-contained `D4Assist.exe`
(the .NET runtime, every assembly with embedded symbols, and `prism.dll`, which is extracted to
`%TEMP%\.net\D4Assist\` on first run), `d4assist.json` and the Spanish `LEEME.txt` (`src/D4Assist/Portable/`).
The sounds and `definitions.json` are embedded in the exe too. `tolk.dll` is left out. Zip that folder for testers.

## Where to start reading

- [STATUS.md](STATUS.md): current state, derived offsets and facts, next step, known issues. **Start here
  when resuming.**
- [research/](research/): the findings behind the code (client memory layout, offline dungeon data, runtime
  signals, the D2R radar study).
- Each project under `src/` and `tools/` has its own README.
- [CLAUDE.md](CLAUDE.md), [principles.md](principles.md), [reference/](reference/) and [templates/](templates/)
  are the general accessibility-modding playbook this repo started from.

## Risk

Reading game memory can conflict with Blizzard's terms of service. This is a personal accessibility tool.
The repository is private and must stay that way.
