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
- Speaks "Asistente de mazmorras activo" at startup and dungeon-enter/leave announcements in Spanish
  (`Strings/SpanishStrings`), through PRISM.
- `--debug` prints the player position and tile count once per second; `--no-speech` swaps PRISM for a
  console sink so you can run it without a screen reader.
- Plays the D2R-ported reactive radar (`src/D4Assist.Radar`) while in a dungeon: four fixed stick directions,
  exit/floor/wall cues on confirmed changes only (`Game/RadarBridge`, world from `Game/DungeonRadarWorld`).

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
error. Only `DefinitionsPath` has no usable default.

| Field | Default | Meaning |
|---|---|---|
| `InstallPath` | `D:\games\battlenet\Diablo IV` | Game install dir (`FenrisDebug.txt`, the exe for module identification). |
| `DefinitionsPath` | *(none — must be set)* | Path to the CASC `definitions.json` dump. |
| `SpeechEnabled` | `true` | `false` behaves like `--no-speech` (console sink instead of PRISM). |
| `AutoAnnouncementsEnabled` | `true` | Speak the dungeon enter/leave lines automatically. |
| `InterruptOnContextChange` | `true` | `interrupt=true` on those announcements (CLAUDE.md §9: interrupt only on context change). |
| `RadarEnabled` | `true` | Play the reactive radar cues while in a dungeon. |
| `RadarFrame` | `"Cardinal"` | `"Cardinal"` (stick up/right/down/left) or `"Diagonal"` (along the dungeon's axes). |
| `RadarVolume` | `1.0` | Radar cue volume, 0 to 1. |

## Command-line flags

- `--no-speech` — use `ConsoleSpeechSink` instead of PRISM (debugging without a screen reader).
- `--debug` — print `player=(x,y,z) tiles=N` once per second, and one `RADAR` line per confirmed radar reading
  change (direction, reading, unexplored, player position) and per rebuilt radar map.
- `--sound-test` — speak and play every radar cue in every direction, without the game.

## Layout

- `Speech/` — `ISpeechSink` (the single sink, CLAUDE.md §9), `PrismSpeechSink` (Prismatoid), `ConsoleSpeechSink`.
- `Strings/` — `SpanishStrings`, every spoken string in one place.
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
