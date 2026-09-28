# Engines — platform knowledge

Per-engine/per-framework knowledge: how each modding platform works, its scripting API, hooking model,
and constraints. This is the **engine axis** of the reference library, orthogonal to the **technique
axis** (`audio-navigation/`, `ui-accessibility/`, `screen-reader-integration/`).

- **Technique docs** (elsewhere) answer *"how do we implement radar / a menu reader?"* — cross-engine.
- **Engine docs** (here) answer *"how does this platform let us hook, read state, and call native?"* —
  cross-technique.

A technique doc should **link** to the relevant engine doc rather than duplicate platform details.

> **Docs only — no code.** Runnable starters/templates (e.g. the UE4SS PRISM-bridge C module) do **not**
> live here; keep them in a separate `templates/` area or their study repo. This folder is markdown.

## Engines

| Engine / framework | Games | Lua? | Screen-reader path (see [screen-reader-integration](../screen-reader-integration/)) |
|---|---|---|---|
| [REFramework](reframework/) | Capcom RE Engine (RE7, SF6, MH…) | PUC Lua 5.4 (no `ffi`) | C# P/Invoke, or native C++ plugin injecting `prism.*` into Lua |
| [UE4SS](ue4ss/) | Unreal (UE4/UE5) | PUC Lua 5.4 (`require` C modules ✓) | Lua C module `prism_bridge.dll` → `prism.dll` |
| [Source 1 / L4D2](source-l4d2/) | Source engine 2009 (Left 4 Dead 2, **x86**) | Squirrel VScript (server-side, sandboxed) | **x86** MetaMod:Source plugin loads Tolk in-process (PRISM prebuilt is x64-only) |
| [BepInEx](bepinex/) | Unity — Mono (BepInEx 5/6) and IL2CPP (BepInEx 6 bleeding edge) | No (C# + Harmony) | C# P/Invoke → PRISM (`Prismatoid` NuGet targets .NET 10 → not loadable here) |
| [BmSDK](bmsdk/) | Unreal **Engine 3** — Batman: Arkham City (**x86**) and Arkham Knight (x64) | No (C# 14 / .NET 10, mods are loose `.cs` compiled at runtime) | AK: C# P/Invoke → PRISM. **AC: unresolved — no PRISM x86 prebuilt** (same problem as L4D2) |

## Adding an engine

1. `reference/engines/<engine>/` with the platform docs (API, hooks, setup, constraints).
2. Add a row above and, if it changes how we reach the screen reader, a row in
   [screen-reader-integration](../screen-reader-integration/README.md).
3. Identify the engine **before assuming** the framework (see [root CLAUDE.md](../../CLAUDE.md) §2).
