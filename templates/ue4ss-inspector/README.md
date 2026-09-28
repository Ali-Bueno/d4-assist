# UE4SS Inspector — non-visual, zero-restart game inspection

A drop-in UE4SS Lua mod that answers "what classes/objects/properties does this game have?" **while the
game keeps running**, in text. It exists because the built-in Live Property Viewer is an ImGui GUI and
is therefore unusable with a screen reader — and because closing the game to read a 149 MB object dump
is the slowest possible way to learn a class.

Background and rationale: [`reference/engines/ue4ss/ue4ss-live-workflow.md`](../../reference/engines/ue4ss/ue4ss-live-workflow.md).
The reflection API behind each command: [`ue4ss-reflection-cookbook.md`](../../reference/engines/ue4ss/ue4ss-reflection-cookbook.md).

## Install

1. Copy this folder to `<Game>/Binaries/Win64/Mods/Inspector/` (so you get `Mods/Inspector/Scripts/main.lua`).
2. Add `Inspector : 1` to `Mods/mods.txt`, **above** the `; Built-in keybinds, do not move up!` line.
3. In `UE4SS-settings.ini` set `[Debug] ConsoleEnabled = 1` and `[General] EnableHotReloadSystem = 1`.
   (If you also want the in-game console, enable `ConsoleEnablerMod : 1` — `@` / `F10`.)

Remove it (or set it to `0`) before packaging the mod for users: it is dev-only.

## Three ways to drive it

| Channel | How | Needs |
|---|---|---|
| **Command file** (accessible) | write a command line into `inspector_cmd.txt` next to `UE4SS.log`, save; read `inspector_out.txt` | nothing |
| Console | open the game console (`@`/`F10`) or the GUI console and type the command | `ConsoleEnabled` |
| Keybind | fill in the `BINDS` table at the bottom of `main.lua` | — |

Output always goes to the console *and* is appended to `inspector_out.txt`, flushed per line — so you can
keep the file open in your editor and read new results with the screen reader without touching the game.

## Commands

In the console, `dumpclass`, `findall`, `props`, `funcs`, `outer`, `probe` and `watch` are registered as
commands on their own; `help`, `find` and `dump` are only reachable as `inspect help`, `inspect find …`,
`inspect dump …` (their bare names are too likely to collide with a game command). In the **command
file** every name works bare.

| Command | What it answers |
|---|---|
| `findall <Class>` | every live instance of a class (short name), with address |
| `find <substr>` | live UserWidgets/Actors whose full name contains a substring |
| `dumpclass <Class \| /Full/Path>` | the whole class chain: every property with its type, every function |
| `props <Class> [n]` | current values of the scalar properties of instance `n` |
| `funcs <Class>` | functions of the class chain |
| `outer <Class>` | the Outer chain of an instance |
| `probe <Class>` | 1st call = baseline, 2nd = diff — **finds the selection/page index** |
| `watch <Class> <Prop> [ms]` | poll one property and log every change (`watch off` stops) |
| `dump <objects\|cxx\|uht\|usmap\|actors\|meshes\|luatypes>` | fire a built-in dumper in place, mid-session |

Typical session, without ever relaunching:

```
find Pause                 -> locate the live widget
dumpclass Xcmn_Pause_C     -> see what it actually exposes
probe Xcmn_Pause_C         -> baseline, move the cursor in-game, probe again -> the index that stepped
watch Xcmn_Pause_C CursorNo -> confirm it tracks the cursor
```

## Design notes

- Every game read goes through `pcall`; a bad deref logs instead of taking the game down. Property
  scanning is **scalars only** on purpose — struct/array reads can abort uncatchably.
- Dumper globals are probed by name before being called, so an older UE4SS build degrades to a message
  instead of an error. Verified present on **v3.0.1**: `DumpAllObjects`, `GenerateSDK`,
  `GenerateUHTCompatibleHeaders`, `DumpStaticMeshes`, `DumpAllActors`, `DumpUSMAP`.
- Keybinds are only claimed if `IsKeyBindRegistered` says they are free, so the inspector never steals a
  key from the accessibility mod it sits next to.
- Discovery calls (`FindAllOf`, `ForEachUObject`) are **on-demand only** — never put them on a tick.
