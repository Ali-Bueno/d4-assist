# Reference Library

Accumulated knowledge for our accessibility mods, written engine-agnostically so it ports to new
targets. Two kinds of material live here:

- **Reverse-engineering studies** — how real games implement the systems we want to reproduce
  (e.g. `audio-navigation/`).
- **Behavior specs** — prescriptive rules for how *our* mods must behave (e.g. `ui-accessibility/`).
- **Engine/platform knowledge** — how each modding framework works (API, hooks, native interop)
  (e.g. `engines/`).

This library has **two axes**: **technique** (what we build — navigation, menus, screen-reader) and
**engine** (the platform we build it on — REFramework, UE4SS…). Technique docs are cross-engine; engine
docs are cross-technique; each links to the other instead of duplicating.

This is reference material, not project code. The playbook that *uses* it is [the root CLAUDE.md](../CLAUDE.md).

## Layout

```
reference/
├── _engine-study-template.md     ← copy this to start a new game/engine study
├── _mod-status-template.md       ← copy to a mod's repo as STATUS.md (per-mod progress ledger)
├── README.md                     ← this index
├── engines/                      ← ENGINE axis: platform knowledge per framework
│   ├── README.md                 ← engine index
│   └── <engine>/                 ← REFramework, UE4SS… (docs only, no code)
└── <topic>/                      ← TECHNIQUE axis: one folder per subject area
    ├── README.md                 ← cross-game synthesis: shared model + which doc covers what
    └── <game-slug>/              ← one folder per game studied…
        └── …docs…                  …or a <technique-slug>/ folder synthesizing several mods/games

```

## Topics

| Topic | What it covers | Sources |
|-------|----------------|---------|
| [Audio navigation](audio-navigation/) | Three navigation styles (reactive radar, polar scan, wall sonification) + coordinate math, raycasting, scanning, zones, beacons, spatial audio, speech | A Hero's Call (.NET/MonoGame decompile), Swamp (native VB6 decompile), Kirandur (NativeAOT decompile), United Minecraft (Fabric/JVM — real GPL source), our own Unity mods — Core Keeper / DRG / Megabonk |
| [UI accessibility](ui-accessibility/) | Prescriptive **behavior specs** (not RE) for in-game UI: menus (entering, tabs, navigation, controls, popup/confirmation dialogs) and inventories (item readout, subsections, crafting); plus a verbatim copy of the third-party **graph navigator** spec (mod-owned parallel UI tree) | Our own accessibility playbook; WH40KRTAccess graph a11y spec (MIT, blind developers, 5 shipping mods) |
| [Token efficiency](token-efficiency/) | How we keep Claude Code usage low: how Max limits are counted, what drives usage (1M window, subagents, always-loaded instructions), the practices and settings we use, and the code-navigation tools evaluated and rejected (graphify, LSP plugins, Serena, vector search) | Anthropic's Claude Code docs and support articles, tool issue trackers and user reports (2026-10-12, updated 2026-10-15) |
| [Screen-reader integration](screen-reader-integration/) | Per-engine **transport** for reaching the OS screen reader (PRISM/Tolk): which hosts load native code, whether a Lua script can reach native or needs a host plugin, whether the UE4SS C-module bridge ports, and the shared adapter contract | UE4SS, REFramework/RE Engine, Project Zomboid (Kahlua), BepInEx/Unity, native |

Plus the **engine axis** (platform knowledge, cross-technique):

| Axis | What it covers | Contents |
|-------|----------------|----------|
| [Engines](engines/) | How each modding framework works: scripting API, hooking model, native-interop constraints | REFramework (RE Engine), UE4SS (Unreal), BepInEx (Unity Mono/IL2CPP), Source 1/L4D2 — more as added |

For Unity specifically, start at [picking the BepInEx build](engines/bepinex/bepinex-versions-and-install.md):
on IL2CPP the bundled Cpp2IL version must match the game's metadata version, and on Unity 6 the
generated interop is corrupt until repaired with
[`templates/bepinex-interop-fixer/`](../templates/bepinex-interop-fixer/) — until then no plugin loads
at all, with an error that blames Unity rather than the generator.

For Unreal specifically, start at [the zero-restart loop](engines/ue4ss/ue4ss-live-workflow.md): UE4SS's
dumpers are callable Lua functions and reflection answers most questions in place, so class discovery
should never mean relaunching the game. The runnable, non-visual companion is
[`templates/ue4ss-inspector/`](../templates/ue4ss-inspector/).

## Adding a new engine study

1. Decide the **topic** (reuse an existing folder, or create a new `reference/<topic>/`).
2. Copy [the engine-study template](_engine-study-template.md) into
   `reference/<topic>/<game-slug>/` — one file for a compact study, or a numbered doc set
   (`00-…`, `01-…`) for a large one (see `audio-navigation/a-heros-call/`).
3. Identify the engine **before assuming** the framework (see CLAUDE.md "Investigar el Engine").
4. Fill the template; note **confidence per section** and what wasn't recovered.
5. Update the topic's `README.md` synthesis (shared model + comparison table) and add a row to the
   **Topics** table above.
