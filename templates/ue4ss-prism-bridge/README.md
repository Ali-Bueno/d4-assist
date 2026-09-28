# UE4SS + PRISM screen-reader template

Drop-in starting point so **any** UE4SS-modded Unreal game gets a screen reader (via
[PRISM](https://github.com/ethindp/prism)) with almost no setup. The hard part — a
Lua↔PRISM bridge that survives UE4SS's bundled Lua — is already solved and prebuilt here.

## What's inside

```
template/
  libs/
    lua54/            Vendored Lua 5.4.4 (source + prebuilt lua54.lib) — UE4SS's exact version
    prism/            PRISM prebuilt release (include/ + bin/ prism.dll,tolk.dll + lib/)
  src/prism_bridge/
    prism_bridge.c    The Lua C module: statically links Lua 5.4.4, dynamically loads prism.dll
    build.ps1         Self-contained MSVC build (no CMake); compiles + deploys the 3 DLLs
  mod/Scripts/                 <- copy/junction this into Mods/<YourMod>/Scripts
    prism_bridge.dll    Prebuilt bridge (ready to use — rebuild only if you change the .c)
    prism.dll           PRISM runtime
    tolk.dll            PRISM's NVDA/JAWS fallback backend
    main.lua            Entry point: wires services + registers hooks (LoadMap/BeginPlay/new widgets)
    screen_reader.lua   The single speech sink (PRISM-backed): Init/Output/Silence/BackendName
    announcer.lua       Anti-spam layer over the sink (dedup + min interval, diff-gating)
    game_state_tracker.lua  Tracks menu/gameplay/combat/… state, announces changes
    ui_reader.lua       Reads UMG widget text (TextBlocks); hook per-game widget classes
    keybind_manager.lua Hotkeys — F1 state, F2 player status, F3 read UI, F4 verbose, F8 speech test, Ctrl+Esc silence
    utils.lua           Safe property/method reads + discovery dumps (DumpProperties/Functions/ListInstances)
```

Architecture (matches the accessibility service pattern): patches/hooks in `main.lua`
delegate to services; **`screen_reader.lua` is the only place that talks to the screen
reader**, so you can swap the transport in one file. Everything speaks through
`Announcer` (dedup/anti-spam), never `screen_reader` directly, except the F8 raw test.

## How the bridge works (and why)

UE4SS runs its own **Lua 5.4.4**. You cannot just call a normal DLL from Lua and pass
the `lua_State` around unless the ABI matches. So `prism_bridge.dll`:

1. **Statically links its own Lua 5.4.4** (same version UE4SS bundles → identical
   `lua_State` layout + C ABI on x64).
2. **Dynamically loads `prism.dll`** at runtime (`LoadLibrary` + `GetProcAddress` by
   name — PRISM's C API is undecorated on x64).
3. UE4SS does `require("prism_bridge")` → calls `luaopen_prism_bridge` with UE4SS's own
   `lua_State`; our static Lua manipulates that state.

No UE4SS source build, no Rust, no external process. A UE4SS update needs **no rebuild**
as long as its bundled Lua stays 5.4.x.

## Use it in a new game

1. Copy `mod/Scripts/*` into the game's `Mods/<YourMod>/Scripts/` (or junction it:
   `mklink /J "<game>\...\Mods\<YourMod>\Scripts" "<repo>\mod\Scripts"`).
2. Enable `<YourMod>` in `Mods/mods.txt` (`<YourMod> : 1`).
3. Launch with a screen reader (NVDA/JAWS) running. Press **F8** — you should hear
   "Screen reader online. Backend: NVDA".
4. Edit the `.lua` and press **Ctrl+R** in the UE4SS console to hot-reload.

If F8 is silent or the game crashes/hangs on launch, read
[`../docs/ue4ss-compatibility-and-crashes.md`](../docs/ue4ss-compatibility-and-crashes.md)
— it documents every failure we hit and its fix.

## Rebuilding the bridge (only if you edit prism_bridge.c)

```
pwsh -File src\prism_bridge\build.ps1
```
Needs MSVC C++ build tools (VS Build Tools "Desktop development with C++"). It compiles
`lua54.lib` (once), builds `prism_bridge.dll`, and deploys the 3 DLLs into `mod/Scripts/`.

## Speaking (from Lua)

Normal path — through the anti-spam layer:
```lua
local Announcer = require("announcer")
Announcer.SpeakInterrupt("New menu")   -- interrupt: context change
Announcer.Speak("item 3 of 5", false)  -- queue: incremental info
Announcer.Silence()
```

The sink itself (swap the transport here, or for a raw test):
```lua
local ScreenReader = require("screen_reader")
ScreenReader.Init()                     -- once, at load (main.lua does this)
ScreenReader.Output("text", true)       -- interrupt=true on context changes
ScreenReader.BackendName()              -- "NVDA" / "JAWS" / ...
ScreenReader.SetRate(v) / SetVolume(v) / SetPitch(v)
```
