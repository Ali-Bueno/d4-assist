# Hot-Reload Workflow — iterate without restarting the game

> See also: `lua-hooks-and-patterns.md`, `csharp-hooks.md`, `tools.md`, `discovery-and-gotchas.md`.

The pain this doc kills: the `compile → close game → reopen game` loop. **Both C# and Lua can
hot-reload without restarting the game** — the trick is using the right pipeline. Pick your language
by capability (below); the iteration loop is a solved problem in both.

## What survives a reload (applies to both languages)

- **Survives:** the game/engine state itself — player position, current scene, health. The game
  process is **not** restarted, only your plugin/script sandbox. This is the entire point vs. a native
  recompile+relaunch: you never lose the play session while iterating.
- **Does not survive:** your mod's own state — static fields / Lua globals, registered
  callbacks/hooks from the old instance, and any cached managed-object handles (they become invalid).
  Re-acquire singletons/objects on reload.

---

## C# (REFramework.NET) — source-plugin hot-reload

**This is the path for this repo's RE mods** (Re7Access, sf6Access), which are C#/REFramework.NET.

- **Source plugins**: drop a loose `.cs` file in **`reframework/plugins/source/`**. REFramework.NET
  compiles it at runtime and, per the docs, *"Edit, save, and the plugin hot-reloads without
  restarting the game."* Source plugins also always compile against the latest generated reference
  assemblies (highest forward-compat).
- **Lifecycle**: `[PluginEntryPoint]` runs on load; `[PluginExitPoint]` is *"called when the plugin
  unloads — either during a hot-reload cycle or when the game exits."* On save: exit point runs → plugin
  torn down → recompiled → entry point fires again, all in the live process. Use the exit point to
  release hooks/state (the C# analogue of Lua's `re.on_script_reset`).
- **Compiled `.dll` plugins** (`reframework/plugins/managed/`): the docs only ever advertise hot-reload
  for the *source* pipeline. Live hot-swap of a compiled managed DLL is **undocumented — assume it
  needs a game restart** (a loaded managed assembly is file-locked, and clean runtime unload would need
  a collectible `AssemblyLoadContext`, which the docs never mention for this path).
- **Recommended dev loop**: iterate as a **source `.cs` plugin** for save-and-see-it-live; keep
  compiling to a `.dll` only for the **release** build. Same code, two delivery modes.
- **Driving PRISM stays trivial**: `[DllImport("prism.dll")]` P/Invoke works directly — P/Invoke is a
  base CLR feature, independent of how the assembly was produced, so it keeps working under the
  source-plugin pipeline. No native shim needed (unlike Lua, below). *Worth a one-time smoke test:*
  call one PRISM function from a `[PluginEntryPoint]` in a source `.cs` file before trusting it for the
  whole mod.
- **GC / threading caveats** (C#-specific, confirmed in the C# docs):
  - `.Globalize()` any `ManagedObject` that must persist across frames (static fields, objects in
    collections, arrays you create) — otherwise the engine GC collects it. Not needed for temporaries
    used within a single callback (the GC won't run mid-callback).
  - On your **own** (non-engine) threads, call `REFrameworkNET.API.LocalFrameGC()` periodically or the
    thread heap grows unbounded and crashes.
  - Hooks run **concurrently** (true multithreading) — `lock` any shared state (statics/collections)
    written from a hook.

---

## Lua — autorun hot-reload

- Scripts auto-load from `reframework/autorun/*.lua`. Manual load: REFramework menu → **ScriptRunner** →
  "Run Script".
- The **"Reset Scripts"** button reloads scripts **without restarting the game**. It fully **recreates
  the entire Lua VM** (not a per-script hot-swap); the log line `[ScriptRunner] Lua state initialized`
  confirms the teardown+rebuild. All autorun scripts then re-execute top-to-bottom into the fresh state.
- `re.on_script_reset(fn)` is the teardown hook: it fires around the reset and also triggers
  `on_config_save()` automatically. Use it to release/reset held state (cached singletons, hook storage,
  timers).
- Lua uses **one shared global state across all scripts** — always use `local` to avoid cross-script
  collisions (see `lua-hooks-and-patterns.md` § Best Practices).

> **Known rough edge** ([REFramework #608](https://github.com/praydog/REFramework/issues/608)): on large
> multi-file mods, after "Reset Scripts" sometimes only some files reload correctly. Test after every
> reset — don't assume it's equivalent to a fresh launch.

### Persisting settings across reloads (Lua)

Config APIs resolve under `reframework/data/`: `json.load_file(path)` (returns `nil` if missing),
`json.dump_file(path, value, indent=4)`, plus `json.load_string`/`json.dump_string`.

```lua
local config = { enabled = true, verbosity = 2 }

local config_path = "my_mod/config.json"
local saved = json.load_file(config_path)
if saved then config = saved end

re.on_config_save(function()
    json.dump_file(config_path, config)
end)
```
Since `re.on_script_reset` triggers `on_config_save`, a "Reset Scripts" click loses nothing
user-visible. `reframework:get_game_name()` (e.g. `"re2"`, `"dmc5"`) is a handy subfolder key so shared
code across games doesn't clobber one settings file. `fs` namespace (path-sandboxed): relative paths
only, no `..`, cannot write `.dll`/`.exe`, base dir is `reframework/data/`.

### Driving PRISM from Lua (the one catch)

Lua **cannot** open `prism.dll` directly. `require()`-ing an arbitrary native DLL is **not reliably
supported** ([REFramework #623](https://github.com/praydog/REFramework/issues/623)), and Lua's C-module
convention expects a `luaopen_<name>` export registering `lua_CFunction`s, whereas `prism.dll` exports a
plain C ABI (`prism_init`, `prism_speak`, …). There is no P/Invoke equivalent from Lua.

**Confirmed-working solution** (already documented for this repo): write one thin **native REFramework
plugin** (`reframework/plugins/*.dll`) that links `prism.dll` internally and, in its
`on_lua_state_created` callback, uses sol2 to register a small `prism` table into the Lua global state —
`prism.init()`, `prism.speak(text, interrupt)`, `prism.shutdown()`. No `require()` involved. See
`accessibility-patterns.md` § Strategy Overview. This shim is small, stable, compiled **once** — not
part of the iteration loop; only it needs a restart to update, and it rarely changes.

---

## Choosing: C# vs Lua vs native C++ plugin

For accessibility mods the real choice is **C# vs Lua** — both hot-reload; C# is the stronger surface.
A full native C++ plugin buys raw power (D3D device, non-TDB hooks, own threads) but **cannot
hot-reload** and is only worth it for what C#/Lua genuinely can't reach.

| | C# (REFramework.NET) | Lua | Native C++ plugin |
|---|---|---|---|
| Hot-reload without restart | ✅ source plugins (`plugins/source/*.cs`); ❌ compiled `.dll` | ✅ autorun + "Reset Scripts" | ❌ restart to reload |
| Drive `prism.dll` | ✅ `[DllImport]` direct, no shim | ⚠️ needs a native shim (`on_lua_state_created` inject) | ✅ direct native call |
| Multithreading | ✅ true concurrent hooks/threads | ❌ single VM lock (serializes) | ✅ |
| Speed vs Lua | ✅ ~3–7× single-thread, ~10–80× multi-thread | baseline | fastest |
| Typed proxies / IDE autocomplete | ✅ generated reference assemblies (`.As<T>()`, `[MethodHook]`) | ❌ dynamic/untyped `sdk.*` | n/a (raw C++) |
| Free convenience wrappers | ⚠️ fewer documented (`ImGuiRender` callback exists; no first-class `draw.*`/`object_explorer` helpers) | ✅ `imgui.*`, `draw.*`, `json.*`, `fs.*`, `object_explorer` | ❌ build your own |
| Per-frame / UI callbacks | `[Callback]` `UpdateBehavior`/`LateUpdateBehavior`/`ImGuiRender` (Pre/Post) | `re.on_frame` / `re.on_draw_ui` | own `on_frame`/imgui |
| GC caveats | `.Globalize()` cross-frame objs; `API.LocalFrameGC()` on own threads | handled by VM | manual |

**Bottom line for this repo:** stay in **C#**. It already gives you hot-reload (via source plugins),
plus direct `[DllImport]` to PRISM, true threading, and typed proxies — Lua unlocks **no new capability**
and would *remove* the direct PRISM call. Switch only your **dev loop** to a source `.cs` plugin; keep
shipping a compiled `.dll` release. The one thing Lua still does more conveniently is `draw.*`/`imgui.*`
overlays and `object_explorer` helpers — reach for a Lua script only if you want a quick throwaway debug
overlay, not for the mod itself.
