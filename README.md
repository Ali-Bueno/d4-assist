# Diablo IV Dungeon Assist

An external companion app that makes Diablo IV's procedural dungeons navigable for blind players. It runs
next to the game (controller + NVDA through D4's third-party screen-reader option) and adds:

- **Spoken room announcements** in Spanish through PRISM: junctions, dead ends, big rooms and the dungeon
  entrance, with their exits as controller-stick directions ("Cruce. Salidas: arriba, derecha sin explorar.").
- **A reactive audio radar**, a port of the author's Diablo II Resurrected radar: four screen directions,
  a bright tin for a passage into another room or unexplored area, a breath for open floor and a dull tock
  for a nearby wall.

It never moves the character and never reveals enemies or loot.

## How it works

Dungeon layouts are generated on the server, so the app reads them from the running game, **read-only**. It
never writes to the game, injects code or hooks it.

- **Offline data:** the scene library comes from the local game install (CASC): tile exits and walkability.
- **World identity:** read from the game's own log (`FenrisDebug.txt`).
- **Live data:** the placed tiles and the player position are read from process memory, and only while in a
  dungeon.

## Requirements

- Windows, the .NET 10 SDK, and Diablo IV installed (build 3.2.1.73552; memory offsets are per build).
- A screen reader (NVDA tested), reached through PRISM (Prismatoid NuGet package).
- `data/definitions.json` from d4data for the same build (not committed).

## Build and run

```
dotnet build D4DungeonAssist.sln
dotnet test D4DungeonAssist.sln
src\D4Assist\bin\Debug\net10.0-windows\win-x64\D4Assist.exe [--debug] [--no-speech] [--sound-test]
```

Configuration lives in `d4assist.json` next to the exe; see [src/D4Assist/README.md](src/D4Assist/README.md).

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
