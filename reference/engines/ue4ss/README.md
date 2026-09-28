# UE4SS (Unreal Engine 4/5)

Platform knowledge for modding Unreal Engine games via **UE4SS** (Lua scripting + C++ mods). Sourced
from the former `UE4ss study` docs.

**Key facts:**
- Lua runtime is **PUC Lua 5.4** (native, real) and **scripts can `require` a native Lua C module** →
  the cleanest screen-reader path: a `prism_bridge.dll` C module that loads `prism.dll` (see
  [screen-reader-integration](../../screen-reader-integration/README.md)).
- Object discovery via UE reflection (`FindAllOf`, `StaticFindObject`, etc.).
- **The dumpers are plain Lua functions** (`DumpAllObjects()`, `GenerateSDK()`, …), so discovery does
  **not** require the launch → dump → close → read → relaunch cycle. Start with
  `ue4ss-live-workflow.md`.

## Docs

| File | Purpose |
|---|---|
| **`ue4ss-mod-architecture.md`** | **READ FIRST — the design layer**: *resolve, don't scan*. The decision ladder, scan cost measured on two games, and what both rejected (construction notifies, UFunction hooks, map-load hooks) |
| **`ue4ss-performance.md`** | **READ BEFORE OPTIMISING — the cost layer**: the frame budget, measured cost ladder, the property-chain rule, caching idioms (and the UEHelpers trap), thread model, settings that cost frames, how to measure, pre-ship checklist |
| **`ue4ss-live-workflow.md`** | **START HERE for the dev loop — zero restarts**: what replaces dumping, dumpers as callable functions, hot reload, the non-visual text pipeline |
| **`ue4ss-offline-extraction.md`** | **Do this before writing Lua**: get the game's classes, data and strings into files (`.usmap` + FModel, header dumps as text, `.locres`, AssetRegistry) so discovery stops being runtime probing |
| `ue4ss-reflection-cookbook.md` | Enumerate a class **in place** (properties, types, functions, super chain) + the text-mode "which value flips?" probe |
| `ue4ss-installation-guide.md` | Install UE4SS on a target game, central install, custom game configs, missing AOBs |
| `ue4ss-api-reference.md` | UE4SS Lua API (finding, hooks, console, threading) + **cost matrix** |
| `ue4ss-discovery-tools.md` | Built-in dumpers + Live View filters/watches — and what each looks like in text |
| `ue4ss-settings-reference.md` | Every `UE4SS-settings.ini` option, by section, + dev/release profiles |
| `ue4ss-cpp-mods.md` | C++ mods: lifecycle, build, hooks, own GUI tab — and when it's *not* worth it |
| `ue4ss-compatibility-and-crashes.md` | Version/compat notes and crash troubleshooting |
| `workflow-per-game.md` | Per-game bring-up workflow |
| `accessibility-patterns.md` | Accessibility architecture + **hard-won lessons** (on_screen, caching, aborts) |

## Code templates (NOT in this folder)

This reference folder is docs only. The runnable starters live in `templates/`:

| Template | What it is |
|---|---|
| [`templates/ue4ss-prism-bridge/`](../../../templates/ue4ss-prism-bridge/) | The PRISM-bridge starter: `prism_bridge.c` Lua C module, static Lua 5.4, PRISM libs, example mod scripts |
| [`templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/) | Dev-only inspector mod: `dumpclass` / `props` / `probe` / `watch` / `dump` over console **or a command file** — non-visual, no restarts |
