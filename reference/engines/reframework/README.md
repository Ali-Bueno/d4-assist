# REFramework (Capcom RE Engine)

Platform knowledge for modding Capcom RE Engine games (RE7, RE2/3/4, SF6, Monster Hunter…) via
**REFramework** (praydog). Sourced from the former `re engine study` docs.

**Key facts:**
- Two scripting surfaces: **Lua** (`reframework/autorun/*.lua`, auto-loaded) and **C#**
  (REFramework.NET). Lua filesystem access is **sandboxed to `reframework/data/`**.
- Lua runtime is **PUC Lua 5.4.3 — NOT LuaJIT** → **no `ffi`**, and scripts can't reliably load native
  DLLs. Reaching native code needs a **native C++ plugin** (see
  [screen-reader-integration](../../screen-reader-integration/README.md) and `accessibility-patterns.md`).
- **Lua vs C# — performance:** C# is roughly **3–7× faster than Lua single-threaded, up to ~80×
  multi-threaded**. Rule of thumb: **Lua for rapid prototyping / cold paths, C# for hot paths** (per-frame
  polling, heavy hooks). This is the main trade-off when deciding whether to write/port a mod in Lua.
- ⚠️ **RE Engine IL2CPP is NOT Unity IL2CPP.** No Unity tooling (Il2CppDumper/cpp2il/BepInEx) applies
  here — discovery is done with REFramework's **Object Explorer**, not Unity dumpers.
- Object access via `sdk.*` (managed singletons, native funcs), hooks via `sdk.hook`, UI via `imgui.*`.
- **Supported games:** RE2/3/4/7/8, DMC5, Monster Hunter Rise/Wilds, and other Capcom RE Engine titles.

## Docs

| File | Purpose |
|---|---|
| `setup.md` | Install REFramework + dev setup |
| `lua-api-core.md` | Lua SDK reference (`sdk`, `re`, `log`, `draw`, `fs`, `json`) |
| `lua-api-imgui.md` | ImGui overlay API for Lua |
| `lua-api-types.md` | Type system, object/array creation |
| `lua-hooks-and-patterns.md` | Hooking patterns in Lua |
| `discovery-and-gotchas.md` | Object Explorer discovery tricks, enumeration primitive, data-reading & perf gotchas |
| `hot-reload-workflow.md` | Iterate without restarting the game — C# source-plugin & Lua reload; C#/Lua/native decision; driving PRISM |
| `csharp-api.md` | REFramework.NET (C#) API reference |
| `csharp-hooks.md` | C# hooking patterns |
| `csharp-objects-and-arrays.md` | C# object/array manipulation |
| `examples.md` | Code examples (Lua & C#) |
| `tools.md` | Object Explorer, debugger, SDK dump |
| `accessibility-patterns.md` | Architecture recommendations for accessibility mods (screen reader, hooks, spatial audio) |

Live mods using this: `D:\code\re engine\Re7Access`, `D:\code\re engine\sf6Access` (both C#/TolkDotNet).
