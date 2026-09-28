# Accessibility Modding — Playbook (CLAUDE.md)

> **What this file is:** the manual for building accessibility mods (screen reader via **PRISM** +
> audio cues). It's a **template**: apply it game by game, section by section. The reusable
> reverse-engineering knowledge lives in [the reference library](reference/), and the general
> engineering & accessibility ground rules that apply under this playbook live in
> [`PRINCIPLES.md`](PRINCIPLES.md) — read those first.

## Goal

Make the game **[GAME NAME]** accessible so a blind player can complete it start to finish with the
same mechanical and narrative experience as a sighted player, via:
- Accessible screen-reader output using **PRISM** (https://github.com/ethindp/prism).
- Audio cues **only** when they add spatial orientation or feedback that text can't convey.
- Keeping gameplay faithful to the original.
- Clean, modular, maintainable code.

## Table of contents

1. [Development flow](#1-development-flow)
2. [Investigate the engine BEFORE assuming anything](#2-investigate-the-engine-before-assuming-anything)
3. [Dependencies](#3-dependencies)
4. [PRISM — integrating the prebuilt release](#4-prism--integrating-the-prebuilt-release)
5. [Reference library and section-by-section build](#5-reference-library-and-section-by-section-build)
6. [Code architecture and best practices](#6-code-architecture-and-best-practices)
7. [Patches / Hooks](#7-patches--hooks)
8. [Accessibilizing UI (menus, inventories)](#8-accessibilizing-ui-menus-inventories)
9. [Speech, audio cues and state tracking](#9-speech-audio-cues-and-state-tracking)
10. [Conventions, logging and publishing](#10-conventions-logging-and-publishing)
11. [Per-mod status ledger](#11-per-mod-status-ledger)
12. [Working model: expensive model as director](#12-working-model-expensive-model-as-director)

---

## 1. Development flow

Build **section by section**, validating with the screen reader active at every step:

0. **Open the mod's `STATUS.md` first** (the per-mod ledger — see §11). On a fresh mod, create it from
   [the status template](reference/_mod-status-template.md). It tells you what's done, what's next, and
   the offsets/symbols already derived — so you don't rebuild that context from the code each session.
1. Identify the engine and the modding framework (§2).
2. Identify the game's critical screens and states.
3. Locate the key methods/data to patch or read.
4. **Adapter + Speech first**: get `Speak("test", interrupt)` working through PRISM and read a simple
   state (position/heading) on a key. This validates the whole pipeline.
5. Create **minimal** patches/hooks (no logic) that delegate to accessibility services.
6. Test with the screen reader; tune verbosity and timing.
7. Add audio cues only when they add real value.

For navigation mods, the recommended order and which document to read for each section live in
[the audio-navigation guide](reference/audio-navigation/README.md) (§5).

---

## 2. Investigate the engine BEFORE assuming anything

> **CRITICAL RULE: do NOT assume the game uses BepInEx or Unity.** Investigate the engine and the
> modding framework of THAT specific game before writing a single line. BepInEx is just **one** of the
> possible cases.

### Cold-start dispatch table

**Start here.** Match the clue in the game's install folder → this is your engine, framework, screen-reader
transport, and which reference docs to open. One glance routes the whole mod; skip the docs that don't apply.

| Clue in install folder | Engine | Framework | SR transport | Read before coding |
|---|---|---|---|---|
| `UnityPlayer.dll` + `*_Data/`, **no** `GameAssembly.dll` | Unity (Mono) | **BepInEx 5** (Harmony) | C# P/Invoke → PRISM | [SR integration](reference/screen-reader-integration/README.md), [engines/bepinex](reference/engines/bepinex/README.md) |
| Same **plus** `GameAssembly.dll` | Unity (IL2CPP) | **BepInEx 6 bleeding edge** (Harmony, IL2CPP interop) | C# P/Invoke → PRISM (correct IL2CPP interop) | [SR integration](reference/screen-reader-integration/README.md), [engines/bepinex](reference/engines/bepinex/README.md), [pick the build](reference/engines/bepinex/bepinex-versions-and-install.md) — the bundled Cpp2IL must match the game's metadata version, and Unity 6 interop needs [repairing](reference/engines/bepinex/bepinex-il2cpp-interop.md) |
| `*.pak` + `Engine/` + `*-Win64-Shipping.exe` | Unreal | **UE4SS** / native | Lua C module `prism_bridge.dll` → `prism.dll` | [SR integration](reference/screen-reader-integration/README.md), [engines/ue4ss](reference/engines/ue4ss/README.md), [zero-restart loop](reference/engines/ue4ss/ue4ss-live-workflow.md) |
| Capcom **RE Engine** DLLs | RE Engine | **REFramework** + native plugin / Lua (NOT BepInEx) | Native C++ plugin loads PRISM, injects `prism.*`; or C#/P-Invoke | [SR integration](reference/screen-reader-integration/README.md), [engines/reframework](reference/engines/reframework/README.md) |
| **Batman: Arkham City / Arkham Knight** — `BmGame\CookedPCConsole\*.upk`, `Binaries\Win32\BatmanAC.exe` | Unreal **Engine 3** | **BmSDK** (C#/.NET 10 script hook via ASI loader — NOT UE4SS, NOT BepInEx); mods are loose `.cs` compiled at runtime | AK (x64): C# P/Invoke → `prism.dll`. **AC is x86 → no PRISM prebuilt**, decide transport first | [SR integration](reference/screen-reader-integration/README.md), [engines/bmsdk](reference/engines/bmsdk/README.md) |
| **Dragon Ball Xenoverse 2** | proprietary | **XV Patcher** / game mods (NOT BepInEx) | native hook → `prism.dll` | [SR integration](reference/screen-reader-integration/README.md) |
| Java + Kahlua Lua (Project Zomboid) | JVM | game Lua + Java agent | Lua → Java (**JNA**) → PRISM | [SR integration](reference/screen-reader-integration/README.md) |
| Native C/C++/VB6 exe, no managed runtime | native | native plugin / custom hook (no Harmony) | native hook `LoadLibrary("prism.dll")` | [SR integration](reference/screen-reader-integration/README.md) |
| **Decomp / native PC port** — `libultraship/` (Ship of Harkinian), `.o2r` archives, CMake, decomp-style C + C++ port layer | native (source-level) | accessibility **compiled into the exe**; a per-frame `Accessibility_Tick()` hook in the game loop; CMake build | PRISM **static-linked** (`prism.lib` compiled in) — not dynamic-load | [SR integration](reference/screen-reader-integration/README.md), [prism-prebuilt.md](reference/screen-reader-integration/prism-prebuilt.md) |

- Every engine ships PRISM via [prism-prebuilt.md](reference/screen-reader-integration/prism-prebuilt.md) (§4); the adapter contract is identical everywhere. Injected/plugin hosts dynamic-load `prism.dll`; source-level ports (decomp / libultraship) can **static-link** `prism.lib` instead.
- Choose **Harmony** only if the framework supports it (.NET/BepInEx). On native engines, use the framework's own hooking system.
- **Unreal/UE4SS:** never discover by dumping-and-relaunching. The dumpers are callable Lua functions and reflection answers most questions in place — read [the zero-restart loop](reference/engines/ue4ss/ue4ss-live-workflow.md) and drop in [`templates/ue4ss-inspector/`](templates/ue4ss-inspector/) (non-visual: console **or** a command file, since the Live View GUI is ImGui and not screen-reader accessible).
- **Unity/BepInEx:** the runtime decides the whole setup (Mono → net472 + `BaseUnityPlugin`; IL2CPP →
  BepInEx 6 bleeding edge + net6.0 + `BasePlugin` + generated interop). On IL2CPP, get BepInEx loading
  and the interop repaired **before** writing any mod code — read
  [engines/bepinex](reference/engines/bepinex/README.md) and run
  [`templates/bepinex-interop-fixer/`](templates/bepinex-interop-fixer/) on Unity 6 targets.
- **Navigation mods:** pick the style(s) via the [audio-navigation decision guide](reference/audio-navigation/README.md) (§5).
- If after investigating the framework is still unclear, **ask the user** before assuming.

---

## 3. Dependencies

Per the engine/framework detected in the §2 dispatch table:

- **Modding framework + Harmony**: per the dispatch table (Harmony only on .NET/BepInEx; native engines
  use their own hooking mechanism).
- **PRISM** (https://github.com/ethindp/prism) — the **only** screen-reader library on every target
  (no Tolk fallback); consume the prebuilt release per §4. Official bindings ship under the name
  **Prismatoid**: .NET (`Prismatoid` on NuGet — targets **.NET 10**, so NOT loadable inside BepInEx
  net472/net6 hosts; there, P/Invoke `prism.dll` directly), Python (`Prismatoid` on PyPI, for
  tooling) and Godot (in-tree in the prism repo); the core itself is a stable C API. The library
  choice, placement and transport per engine are in the dispatch table and
  [the SR integration index](reference/screen-reader-integration/README.md) — not repeated here.

---

## 4. PRISM — integrating the prebuilt RELEASE

> The full **verified** consumption reference (release layout, undecorated x64 exports + dynamic-load
> path, runtime deps / no VC++ redist, the minimal `prism_init → … → prism_shutdown` sequence, what to
> ship) lives in **[reference/screen-reader-integration/prism-prebuilt.md](reference/screen-reader-integration/prism-prebuilt.md)**.
> Read it once per mod when wiring PRISM; no need to re-investigate or compile the PRISM repo.
> For *how the call physically reaches PRISM on each engine* (the transport), see
> [the screen-reader integration index](reference/screen-reader-integration/README.md).

- **In one line:** download `prism-windows-x64.zip`, `LoadLibrary("prism.dll")` + `GetProcAddress` by
  name (x64 exports are undecorated), ship **only** `prism.dll` next to the mod — the release's
  `tolk.dll` is an optional Tolk-compat backend, NOT needed (NVDA/JAWS/SAPI are built into
  `prism.dll`). No x86 build exists. Everything else is in the doc above.

---

## 5. Reference library and section-by-section build

The reusable reverse-engineering knowledge lives in [the reference library](reference/), organized by topic →
game, with a cross-game synthesis per topic. To build a mod, read the relevant section before
implementing it.

- **General index:** [the reference library index](reference/README.md)
- **Audio navigation**:
  [the audio-navigation guide](reference/audio-navigation/README.md) — presents the **three
  navigation styles** (pick one, two, or all three depending on the game) with a **decision guide**, the
  **"which document to read for each mod section"** table, the shared model (queryable world + raycast →
  sound), and the convention differences that cause mirrored-direction bugs.
  - **Style 1 — Reactive radar** (cues as you move: an impassable obstacle — wall, rock, tree,
    invisible barrier — closing in / space opening-closing). Primary
    study (.NET/MonoGame, near-source level):
    [`a-heros-call/`](reference/audio-navigation/a-heros-call/) (docs 00–10), radar in doc 02.
  - **Style 2 — Polar scan radar** (on-demand sweep of N rays). Corroborating study (native VB6, Ghidra):
    [`swamp/navigation-spec.md`](reference/audio-navigation/swamp/navigation-spec.md) §3.
  - **Style 3 — Wall sonification** (one continuous tone per direction; volume = proximity of the
    nearest **impassable obstacle** — walls, rocks, trees, invisible barriers, anything the player
    can't pass through).
    Synthesis of our own Unity mods (Core Keeper / DRG / Megabonk):
    [`wall-sonification/`](reference/audio-navigation/wall-sonification/).
- **UI accessibility** (behavior specs, not RE knowledge):
  [the UI accessibility index](reference/ui-accessibility/README.md) — prescriptive rules
  for [menus](reference/ui-accessibility/menus.md) and
  [inventories](reference/ui-accessibility/inventories.md) (see §8).

**If you ask me to study a new engine's behavior:** copy
[the engine-study template](reference/_engine-study-template.md) into
`reference/<topic>/<game>/`, identify the engine first (§2), and fill it in with per-section confidence
notes.

---

## 6. Code architecture and best practices

- **Modular, decoupled** code with clear separation of responsibilities:
  `AccessibilityManager`, `ScreenReaderService` (PRISM), `AudioCueService`, `GameStateTracker`.
- **No accessibility logic inside the patches**: every patch/hook delegates to an external service.
- **Engine adapter**: isolate the game behind a minimal interface (e.g. `PlayerPosition`,
  `HeadingDegrees`, `Raycast(...)`, `Speak(...)`, `PlaySound(...)`). Accessibility logic is written
  against the adapter, not the engine. (See porting in `reference/audio-navigation`.)
- Every accessibility feature must be easy to **toggle on/off**.
- Prioritize readability over premature optimization; prefer composition over inheritance; **don't
  duplicate logic** across patches; **don't hardcode** offsets, fragile names, or magic numbers (derive
  them from the game's real data). Centralize accessible constants and strings in a single layer.

---

## 7. Patches / Hooks

> The mechanism depends on the framework (Harmony in BepInEx, native hooks in REFramework/XV Patcher/UE4SS).
> The principles are the same.

- Patch/hook **only** the methods you need; never alter the original logic unless strictly necessary.
- In Harmony (BepInEx/.NET): prefer **`Postfix`** for reading state; `Prefix` only when essential.
- On native frameworks: use their hooking system with the same minimalist philosophy.
- Every patch/hook delegates to an accessibility service (no logic inside the patch).

---

## 8. Accessibilizing UI (menus, inventories)

> The **behavior specs** for in-game UI live in
> [the UI accessibility specs](reference/ui-accessibility/) — prescriptive rules the patches/hooks
> delegate to. Read the relevant doc before implementing that part of a mod. Read
> [the generic UI strategy](reference/ui-accessibility/generic-strategy.md) **first**: coverage by
> default — classify **widgets, never screens**, so reusable archetype readers cover every screen
> (including unseen ones) automatically; per-screen overrides only for genuine quirks; generic
> tree-walk fallback and fault isolation so a changed screen degrades locally instead of breaking the
> mod. **Never implement UI screen by screen.** Menus are a **fixed-cost phase**: when a shared
> pattern exists, implement it once for all menus at the same time — build the pipeline, validate on
> 2–3 representative screens, sweep the critical path. Menus must end up **100% readable** in the
> least time possible; how much of the mod that phase is depends on the genre (in point-and-click /
> visual novels the UI *is* the gameplay — same architecture, bigger share). See the workflow in the
> generic strategy doc. The menu/inventory rules below are the per-archetype read behavior that plugs
> into it.

- **Menus** — [the menus spec](reference/ui-accessibility/menus.md). Trigger:
  **"accessibilize menus"** / **"accesibilizar menús"**. Covers entering a menu/screen, tabs/sections,
  vertical navigation, horizontal controls (sliders/checkboxes/dropdowns), and popup & confirmation
  dialogs.
- **Inventories** — [the inventories spec](reference/ui-accessibility/inventories.md).
  Trigger: **"accessibilize the inventory"** / **"accesibilizar inventario"**. Covers item readout,
  subsections (belt/equipment/storage), and crafting recipes (have/needed). Builds on the menu rules.

Shared principle for both: **context change → `interrupt=true`**; incremental value change → read only
what changed; always follow the game's on-screen order. Future UI specs (HUD, combat) get their own doc
in that folder.

---

## 9. Speech, audio cues and state tracking

### Speech with PRISM

- Initialize PRISM when the plugin loads / the game starts; release it on shutdown. Log the chosen
  backend (`prism_registry_name`).
- Clear, concise announcements; avoid spam. **Interrupt (`interrupt=true`) only when context changes**
  (new menu, new region/state); `interrupt=false` for incremental info.
- A **single sink** that routes to the backend (PRISM everywhere), so a backend swap or a test mock
  happens in one place.
- The same code must work with NVDA, JAWS, VoiceOver, Orca, etc.

### Audio cues

- Use them **only** when there's no clear textual equivalent or when spatial orientation / immediate
  feedback is needed. Simple, distinguishable, reusable sounds.
- **Pause cues during dialogues and pausing menus.** Whenever an NPC dialogue is active, or a menu that
  pauses the game opens, pause/silence all audio cues — **except when the game does NOT pause on opening
  that menu** (then keep the cues running). Resume when the dialogue/menu closes. Drive this from the
  centralized accessible state (see *State tracking* below), the same way the wall-sonification bed is
  silenced in menus.

### State tracking

- Keep a **centralized accessible state**: current menu, active submenu, combat state,
  interaction/dialogue.
- **Detect real changes before announcing** (diff-gating): never repeat the same state.
- Centralize all spoken text in a single layer (translatable, outside the patch logic).

---

## 10. Conventions, logging and publishing

### Language

- **All code in English** (classes, methods, variables, namespaces) and **comments in English**.
- **Commits in English**, short and descriptive: `Add screen reader support for main menu`,
  `Refactor accessibility services`, `Patch combat state announcements`.

### Logging and debug

- Use the framework's logger (BepInEx/REFramework/XV Patcher/custom) for important states, patching
  errors, and service initialization. Logs in English. **Never log every frame.**

### Compatibility and maintenance

- Plan for future game updates. Avoid fragile offsets/names; centralize constants.

### Publishing on GitHub

- A clear, documented repo with a `README.md` in English explaining: what the mod does, which parts are
  accessibilized, and the requirements (framework if applicable, PRISM, system screen reader).
- Avoid committing unnecessary binaries. **Don't create releases automatically**: only when the user
  explicitly asks.

---

## 11. Per-mod status ledger

Each mod keeps a short **`STATUS.md`** at its repo root — a dashboard of where the mod is, so resuming it
is instant instead of re-derived from the code/git every session (the biggest context/time saver
day-to-day). Copy it from [the status template](reference/_mod-status-template.md).

- **Open it first** when starting a session on a mod (§1, step 0); **update it** (the *Next step* line and
  the section table) whenever you finish a chunk.
- It records: identity (engine/framework/transport/build/install, from the §2 dispatch table), a
  **section-status table** (features `done`/`wip`/`todo`), **derived facts** (offsets, symbols, type names
  already recovered — *with their source*, so they're never re-RE'd), the single **next step**, and known
  issues.
- Keep it **short** — a dashboard, not documentation. The *how* lives in `reference/`; `STATUS.md` only
  tracks *where this mod is*. Derive every value from real game data (no guessed offsets — PRINCIPLES §4).

---

## 12. Working model: expensive model as director

> **Source of truth: the user-level `~/.claude/CLAUDE.md`** ("Modelo caro = director"), which loads in
> **every** session and every mod repo — nothing here needs copying into a mod's own playbook. This
> section only spells out what the rule means *for accessibility modding*; if the two ever disagree, the
> global wins.
>
> Applies when the session's main model is **Fable** (`claude-fable-5`) or **Opus** (`claude-opus-4-8`,
> incl. `[1m]`). Fable is the extreme case — delegate aggressively; with Opus the pattern is the same but
> the bar to delegate is slightly higher (short lookups where the target is already known stay in the main
> thread). If the main model is Sonnet or Haiku, ignore this section — the agent overhead isn't worth it.

Reverse-engineering a game is exactly the kind of work that burns context on raw volume: decompiler
dumps, IL2CPP symbol lists, engine folder sweeps, build logs, screenshots of menus. **The main model must
not spend its context on that.** It acts as the **director**: it forms the RE hypotheses, decides the
architecture (adapter, services, hooks), writes the critical code, and reviews the result. Everything
that mainly produces bulk intermediate output goes to a cheaper model.

- **Delegate by difficulty**, picking the subagent's model with `Agent`'s `model` parameter (or
  `opts.model` per stage inside a `Workflow`). Not everything drops to Sonnet/Haiku: pick the cheapest
  tier that will actually solve that sub-task — **hard sub-tasks go to Opus**.
  - `model: 'haiku'` — sweeping the game folder to identify the engine (§2 dispatch table), grepping
    dumps/`global-metadata` output, reading Ghidra/ILSpy exports, filtering long BepInEx/REFramework
    logs, describing UI screenshots, running the build and returning only the errors.
  - `model: 'sonnet'` — sub-tasks that need reasoning but not direction: tracing a call flow to a
    candidate hook site, summarizing a subsystem, drafting a `reference/` doc, proposing a bounded patch.
  - `model: 'opus'` — **hard** sub-tasks Sonnet would get wrong, but that still shouldn't eat the
    director's context: reversing a gnarly decompiled function, working out a struct layout / vtable from
    Ghidra output, debugging a crash or a hang in the injected mod, adversarially verifying a finding.
    When the director is Fable, Opus is still *cheaper than the director* — use it for the hard parts
    instead of forcing Sonnet and paying twice when the work has to be redone.
- **Subagents return conclusions, not dumps**: a short structured list (`path:line` / `RVA` / symbol +
  finding). The director never re-reads what it delegated.
- **The director keeps for itself**: engine/framework decision when the clues conflict, the accessibility
  design, the hook strategy, the PRISM/adapter wiring, and the final `STATUS.md` update.
- **Don't delegate a known lookup**: if the exact file/symbol is already known, a direct `Read`/`Grep` is
  cheaper than spawning an agent.
- Independent investigations (e.g. map the menu system, the player controller and the world/collision
  layer) go out as **parallel** subagents in one message — each at whatever tier its difficulty warrants;
  the director synthesizes their answers.
