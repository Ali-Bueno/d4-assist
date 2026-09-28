# Templates

Runnable **example/starter code** to copy when bootstrapping a new mod. Unlike
[`reference/`](../reference/) (which is markdown documentation), this folder holds actual code — build
scripts, source, and bundled libs — meant to be **taken as an example**, not maintained as a product.

Copy a template into a new mod's working dir and adapt it; don't edit these in place as if they were the
mod. The matching *documentation* for each template lives under `reference/engines/<engine>/`.

## Available templates

| Template | Engine | What it is | Docs |
|---|---|---|---|
| [`ue4ss-prism-bridge/`](ue4ss-prism-bridge/) | UE4SS (Unreal) | Working PRISM screen-reader bridge: `prism_bridge.c` Lua C module + static Lua 5.4 + PRISM libs + example mod scripts (`screen_reader.lua`, `announcer.lua`, `game_state_tracker.lua`, `keybind_manager.lua`) and `build.ps1` | [engines/ue4ss](../reference/engines/ue4ss/README.md), [screen-reader-integration](../reference/screen-reader-integration/README.md) |
| [`bepinex-interop-fixer/`](bepinex-interop-fixer/) | BepInEx (Unity IL2CPP) | Game-agnostic CLI that repairs the corrupt interop assemblies Il2CppInterop generates on **Unity 6** (`Duplicate type with name '<>O'` → "no plugins will be loaded"). Mandatory after every interop regeneration on Unity 6 | [engines/bepinex](../reference/engines/bepinex/bepinex-il2cpp-interop.md) |

## Adding a template

1. Put the starter under `templates/<slug>/` with its own `README.md` (how to build/deploy).
2. Add a row above.
3. Point the relevant `reference/engines/<engine>/README.md` at it (docs ↔ code cross-link).
