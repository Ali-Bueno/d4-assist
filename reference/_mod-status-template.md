# STATUS — <GAME NAME>

> **What this is:** the per-mod status ledger. Copy this file to the mod's repo root as `STATUS.md` and
> keep it current. **Open it first** when resuming work on this mod — it replaces re-discovering progress
> from the code/git every session (saves context and time). Keep it short; it's a dashboard, not docs.
> Update the **Next step** and the section table whenever you finish a chunk. Derive every value from the
> game's real data — no guessed offsets (see PRINCIPLES §4).

**Last updated:** <YYYY-MM-DD>

## Identity (from the CLAUDE.md dispatch table)

- **Engine / framework:** <e.g. Unreal / UE4SS>
- **Screen-reader transport:** <e.g. Lua C module `prism_bridge.dll` → `prism.dll`>
- **Build command:** <e.g. `dotnet build`, `build.ps1`>
- **Mod install path:** <where the build copies the artifact>
- **Run / test:** <how to launch the game with the mod + screen reader>

## Section status

Track features, not screens. `done` = works with the screen reader on; `wip` = started; `todo` = not begun.

| Section / feature | Status | Notes |
|---|---|---|
| Adapter + Speech (`Speak("test")` through PRISM) | todo | The pipeline validator — do this first |
| <main menu> | todo | |
| <inventory> | todo | |
| <navigation / audio cues> | todo | style(s): … |
| … | | |

## Derived facts (so we never re-RE them)

Offsets, symbols, type names, method signatures already recovered — **with their source**, so the next
session reuses them instead of re-decompiling.

| Fact | Value | Source (how derived) |
|---|---|---|
| <e.g. PlayerController class> | <name> | <Object Explorer / Ghidra / il2cpp dump> |
| … | | |

## Next step

<one line: the single next thing to do>

## Known issues / open questions

- <e.g. menu X not read because …>
