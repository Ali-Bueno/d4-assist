# The zero-restart UE4SS loop

**The problem this kills:** *launch the game → press a dump key → alt-tab → close the game → read a
149 MB dump → edit the script → launch again*. Almost none of that is necessary. UE4SS is designed to be
driven **while the game runs**: the dumpers are ordinary functions you can call from a command or a hook,
reflection answers most questions without any dump at all, and Lua mods reload in place.

> **Read this before starting a new Unreal game**, together with
> [`workflow-per-game.md`](workflow-per-game.md) (what to do, in order) — this doc is *how to do it
> without restarting*.

---

## 0. What replaces what

| You want to know… | Old way (restart loop) | In-place way |
|---|---|---|
| What properties/functions does class X have? | `Ctrl+H`, close game, read `.hpp` | `dumpclass X` — reflection chain, printed live ([cookbook §2](ue4ss-reflection-cookbook.md)) |
| Which widget/actor is on screen right now? | `Ctrl+J`, close game, grep 149 MB | `findall UserWidget` / `find <substr>` |
| Which property is the selection index? | guess from a header dump | `probe X` (snapshot → move cursor → diff), or a Live View watch |
| Did my script change work? | restart the game | `Ctrl+R` hot reload, or auto-reload on save |
| What's the real value of a field right now? | add a print, restart | `props X`, or `watch X Prop` |
| I still need a full offline dump | — | run the dumper **from a command/hook**, keep playing |

Everything on the right is implemented in [`templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/) —
drop it in `Mods/Inspector/`, enable it in `mods.txt`, done.

---

## 1. One-time setup (this is what unlocks the loop)

In `UE4SS-settings.ini` — full key reference in [`ue4ss-settings-reference.md`](ue4ss-settings-reference.md):

```ini
[Debug]
ConsoleEnabled     = 1     ; without this there is no console at all -> no in-place inspection
GuiConsoleEnabled  = 1     ; the GUI console + Live View (visual only)
GuiConsoleVisible  = 1
GraphicsAPI        = opengl  ; if the GUI window is blank, try dx11

[General]
EnableHotReloadSystem = 1  ; Ctrl+R reloads Lua mods in place
```

> **Version check.** `EnableAutoReloadingLuaMods` (reload on save), `HotReloadKey`, `DoEarlyScan` and
> `bEnableSeachByMemoryAddress` do **not** exist in the `UE4SS-settings.ini` shipped with **v3.0.1** —
> adding them there does nothing. On 3.0.1 the reload is `Ctrl+R` (fixed) or the *Restart All Mods*
> button. Confirm against your own ini before relying on a key.

> **Check this first on an existing mod.** A game whose `ConsoleEnabled=0` and
> `EnableHotReloadSystem=0` *forces* the restart loop — there is no way to ask anything at runtime and
> every edit needs a relaunch. Two lines of ini turn that around.

Ship-time is the opposite: consoles off (they are dev surface and are **not screen-reader accessible**).
The mistake to avoid is keeping **one** ini that gets shipped to users — that pins you to the release
profile while developing. Keep `UE4SS-settings.dev.ini` next to it and have the packaging script copy
the release one, or use the `zDEV-UE4SS_vX` distribution for development and the basic one for release.

---

## 2. The dumpers are functions — call them without stopping

Each built-in dumper is reachable three ways: a **keybind**, a **button** in the GUI's *Dumpers* tab,
and a **global Lua function**. The third one is the one that breaks the loop: you can trigger a dump
from a console command, a keybind of your own, or a hook — *at the exact moment the game is on the
screen you care about* — and keep playing afterwards.

| Dumper | Default keybind | Global function | Output |
|---|---|---|---|
| Object dump | `Ctrl+J` | `DumpAllObjects()` | `UE4SS_ObjectDump.txt` |
| C++ headers (CXX) | `Ctrl+H` | `GenerateSDK()` | `CXXHeaderDump/*.hpp` |
| UHT headers | `Ctrl+Num9` | `GenerateUHTCompatibleHeaders()` | `UHTHeaderDump/` |
| Static meshes | `Ctrl+Num8` | `DumpStaticMeshes()` | `<timestamp>-ue4ss_static_mesh_data.csv` |
| All actors | `Ctrl+Num7` | `DumpAllActors()` | `<timestamp>-ue4ss_actor_data.csv` |
| `.usmap` | `Ctrl+Num6` | `DumpUSMAP()` | `Mappings.usmap` |
| Lua type bindings | GUI button *Dump Lua Bindings* | `GenerateLuaTypes()` | `Mods/shared/types/` |

Keybinds are defined in `Mods/Keybinds/Scripts/main.lua` — **that file is the ground truth for your
build**; open it to see exactly which globals exist in the UE4SS version you have, and rebind freely.
On **v3.0.1** it reads, literally, `RegisterKey("ObjectDumper", function() DumpAllObjects() end)` and so
on for all six — i.e. the first six rows above are confirmed present, and the keybind is nothing more
than a wrapper you can bypass. `GenerateLuaTypes()` is *not* wired to a key there; on 3.0.1 use the GUI
button. The inspector template probes for each name before calling it, so it degrades to "use the
keybind" instead of erroring on a build that lacks one.

**Auto-dump at the right moment** — the trick that removes most manual dumping:
```lua
RegisterHook("/Script/UMG.UserWidget:Construct", function(self)
    if self:get():GetFullName():find("Pause") and not dumped then
        dumped = true
        DumpAllObjects()          -- fires the instant the pause menu exists, mid-session
    end
end)
```

### Dumper gotchas
- `LoadAllAssetsBeforeDumpingObjects` / `LoadAllAssetsBeforeGeneratingCXXHeaders` = `1` makes dumps
  complete (lazily-loaded UI classes included) but costs **gigabytes of RAM** and, per the official
  warning, **crashes the game if you keep playing past the main menu afterwards**. Use it for a
  deliberate one-shot dump, then restart — that is the *one* case where restarting is correct.
- The CXX generator is officially "a very shoddily made generator": no UHT macros, no correct includes,
  and **memory layout/padding is not accurate** (`KeepMemoryLayout=1` is documented as having no purpose
  yet). Bitfields always come out as `uint8` even when the original is `uint32`. Great as a property
  catalogue; never as a memory map.
- `.usmap` matters if you also open the game's `.pak` in **FModel**: shipping builds use unversioned
  properties, and without `Mappings.usmap` FModel shows types/offsets instead of real names.

---

## 3. Reading the answers without alt-tabbing out of the game

Lua in UE4SS is real PUC Lua 5.4, so `io.open` works: have every inspection command **append to a text
file** (and `flush()` per line). You read that file in your editor with the screen reader while the game
keeps running in the background — no closing, no relaunching.

For a blind developer this is the important half of the loop, because the **Live Property Viewer is an
ImGui GUI and is not accessible**. The text pipeline replaces it one-for-one:

| Live View feature | Text equivalent |
|---|---|
| search by class/name, regex | `findall` / `find` |
| expand an object's properties | `props`, `dumpclass` |
| **add watch → see which value flips** | `probe` (baseline → diff) / `watch Class Prop` |
| *Find functions* → call a UFunction | `funcs` + `obj:CallFunction(fn, ...)` |

And when there is no console at all (or typing into the game's console is impractical), use the
**command-file channel** in the inspector template: write `dumpclass Foo` into `inspector_cmd.txt`, save,
and the answer appears in `inspector_out.txt`. The mod polls the file with `LoopAsync` and executes it
via `ExecuteInGameThread`. That is a complete, fully non-visual, zero-restart interface to the game.

---

## 4. Hot reload — and what it does *not* reset

- `Ctrl+R` (`HotReloadKey`, always with Ctrl) or the **Restart All Mods** button reloads Lua mods.
- `EnableAutoReloadingLuaMods=1` reloads when a file under the mod's `Scripts/` changes — no keypress.
- **Hooks are not cleaned up for you.** A `RegisterHook` that runs again on every reload stacks duplicate
  callbacks (your announcements start firing twice, then three times). Either keep the returned
  `preId, postId` and `UnregisterHook` on reload, or make registration idempotent behind a flag stored in
  a shared variable (`Mod:SetSharedVariable` survives reloads by design).
- Registering the same keybind twice also stacks — guard with `IsKeyBindRegistered`.
- Adding a *new* mod folder still needs "Restart All Mods" (or a relaunch); editing an existing one does not.

---

## 5. Free tools already installed (the universal mods)

Shipped in `Mods/`, toggled in `mods.txt`. Worth knowing before writing your own:

| Mod | Default | What it gives you |
|---|---|---|
| `ConsoleEnablerMod` | on | Unlocks the game's own console (`@` / `F10`) by editing `InputSettings.ConsoleKeys` |
| `ConsoleCommandsMod` | on | `set <object/class> <prop> <value>` (edit any property live), `summon <asset>`, `dump_object` |
| `CheatManagerEnablerMod` | on | Constructs the `CheatManager` if the game disabled it — unlocks its commands |
| `ActorDumperMod` | off | `Ctrl+Num3`: dump address + full name of every actor in the current level |
| `LineTraceMod` | off | Line traces from the player controller — a ready-made spatial probe |
| `BPModLoaderMod` | on | Loads Blueprint mods from `.pak` and spawns them as an actor (`ModActor_C`, `load_order.txt`) |
| `jsbLuaProfilerMod` | off | Lua profiler — use it before optimising a per-tick read |

`set` from `ConsoleCommandsMod` deserves a mention: it is a live property editor from the console, which
is the accessible counterpart of editing a value in the Live View.

---

## 6. The loop, in practice

1. **Once per game:** dev ini (§1) → install the inspector mod → `Ctrl+H` with *load all assets* for a
   one-shot property catalogue (this is the one legitimate restart) → `GenerateLuaTypes()` for editor
   autocompletion.
2. **Per screen/feature, without ever restarting:**
   `find <name>` to locate the live object → `dumpclass` it → `probe` while you move the cursor to find
   the selection index → write the reader code → `Ctrl+R` → listen → repeat.
3. **Only restart when:** you changed `UE4SS-settings.ini`, you ran a *load-all-assets* dump, you
   rebuilt a C++ mod, or you added a brand-new mod folder.

Anything else that sends you back to the launcher is worth a minute of investigation — it usually means
a switch in §1 is off.
