# UE4SS built-in discovery tools (dumpers + Live View)

**Use these BEFORE writing custom reflection code.** UE4SS ships tools that map a game's classes,
properties and functions for you — and, crucially, let you **watch which property changes as you
navigate a menu**. Reaching for a hand-written `discover.lua` (FindAllOf + ForEachProperty dumps) should
be the *last* resort, not the first: it's slow to iterate, easy to crash on a stale pointer, and it can't
see what the Live View shows live.

---

## 1. The dumpers (offline class/property maps)

Each dumper has **three** triggers: a keybind, a button in the GUI's *Dumpers* tab, and a **global Lua
function**. The function is the important one — it means a dump is something you fire from a console
command or a hook, mid-session, without stopping ([`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) §2).
Output goes next to `UE4SS.dll`. Keybinds are declared in `Mods/Keybinds/Scripts/main.lua` (rebindable),
which is also the ground truth for which globals your build exposes.

| Dumper | Keybind | Global function | Output | Contains | Config section |
|---|---|---|---|---|---|
| **Object dump** | `Ctrl+J` | `DumpAllObjects()` | `UE4SS_ObjectDump.txt` | every live UObject: name, class, outer, flags, property offsets | `[ObjectDumper]` |
| **C++ headers (CXX)** | `Ctrl+H` | `GenerateSDK()` | `CXXHeaderDump/*.hpp` (+ `_enums`) | full class/struct definitions: **every UPROPERTY + UFUNCTION signature**, per Blueprint/base class | `[CXXHeaderGenerator]` |
| **UHT headers** | `Ctrl+Num9` | `GenerateUHTCompatibleHeaders()` | `UHTHeaderDump/` | headers for a mirror `.uproject` (C++ modding) | `[UHTHeaderGenerator]` |
| **.usmap** | `Ctrl+Num6` | `DumpUSMAP()` | `Mappings.usmap` | property mappings for unversioned-property tools (FModel) | — |
| **All actors** | `Ctrl+Num7` | `DumpAllActors()` | `<timestamp>-ue4ss_actor_data.csv` | every loaded actor | — |
| **Static meshes** | `Ctrl+Num8` | `DumpStaticMeshes()` | `<timestamp>-ue4ss_static_mesh_data.csv` | mesh transforms for in-editor level rebuild (needs `zMapGenBP.zip` from Releases) | — |
| **Lua type bindings** | — | `GenerateLuaTypes()` (GUI: *Dump Lua Bindings*) | `Mods/shared/types/` | LuaCATS annotations for the game's own classes → editor autocompletion | — |

*Verified on UE4SS **v3.0.1**: the first six globals are exactly what the built-in `Keybinds` mod calls.
`GenerateLuaTypes` is not keybound there — use the GUI button on that version.*

**Accuracy warnings that matter:**
- The CXX generator is described by its own authors as *"a very shoddily made generator"* — no UHT
  macros, no correct includes, and **the memory layout is not accurate** (`KeepMemoryLayout=1` is
  documented as having no purpose yet). Bitfields are always emitted as `uint8` even when the original is
  `uint32` — spot them by two bitfields sharing an offset with the next property 4 bytes later. Treat CXX
  headers as a **property catalogue**, never as a memory map.
- `LoadAllAssetsBefore…=1` makes a dump complete but costs **gigabytes of RAM** and, per the official
  warning, **crashes the game if you keep playing past the main menu afterwards**. One-shot only.

### Which one to use for UI work
- **`Ctrl+H` (CXX headers) is the authoritative source for a widget's structure.** Want to know if
  `List_Bar_C` has a `bSelected`/index property, or what nodes a tutorial row exposes? Open its `.hpp` and
  read the real UPROPERTY/UFUNCTION list — no guessing, no runtime reflection. Set
  `[CXXHeaderGenerator] LoadAllAssetsBeforeGeneratingCXXHeaders = 1` first so every menu's classes are
  present.
- **`Ctrl+J` (object dump)** shows what's actually instantiated at that moment — handy to learn the live
  container names (e.g. `…BP_ATGameInstance_C_0.Xcmn_Pause_C_0`).

> If a class truly has **no** selection/index/bool property in its CXX header, that state is not in
> reflection (it's material/animation/native-driven) — stop hunting via dumps and use the Live View to
> confirm, then fall back to a behavioural read (read the whole menu, or track input).

---

## 2. Live Property Viewer (the GUI) — the fast path

The GUI console's **Live View** lets you search, view, edit and **watch** the properties of every object
in real time. This is the single biggest time-saver for UI discovery.

### Enable it
```ini
[Debug]
ConsoleEnabled     = 1
GuiConsoleEnabled  = 1
GuiConsoleVisible  = 1
GraphicsAPI        = opengl   ; if the window is blank/white, try dx11 (or d3d11)
GuiConsoleFontScaling = 1     ; bump if the text is tiny
```
No mod is required — the console + Live View are core UE4SS. (You can keep them **off** for shipping/play
and only turn them on for a discovery session.)

> **Accessibility caveat — read this first.** The Live View is an **ImGui GUI**: a blind developer
> cannot use it. Every capability below has a text equivalent driven from console commands and files —
> see [`ue4ss-reflection-cookbook.md`](ue4ss-reflection-cookbook.md) and
> [`templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/). This section is kept for sighted
> collaborators and for cross-checking.

### Find an object
Right-click the search bar for filters:
- **Instances only** — live actors/widgets/components with real-time values (what you usually want).
  Named `<package>_C` or `<package>_C_<instance id>`.
- **Non-instances only** — loaded package defaults not present in the level; **not editable**.
- **CDOs only** / **Include CDOs** — class default objects (`…_GEN_VARIABLE`, `Default__<package>`).
- **Include inheritance** — also match children of the results.
- **Use Regex for search**.
- **Exclude class names** / **Include class names** — comma-separated list, e.g.
  `CanvasPanelSlot, StaticMeshComponent, Package, Function`.
- **Has property** — objects owning a property with that exact name (only applies with search text).
- **Has property type** — e.g. `BoolProperty`, `MulticastInlineDelegateProperty`.
- **Function parameter flags** — filter functions by parameter/return flags.
- **Match Memory Address** — if the query is a valid hex address, resolve the object there (needs
  `[General] bEnableSeachByMemoryAddress`, which does not exist in the 3.0.1 ini).
- **Save filters** — persisted to `<working dir>/liveview/filters.meta.json` and restored on restart.

Expand an instance to walk its children (WidgetTree → panels → nodes). `<<` / `>>` navigate the history
of inspected objects. `[Debug] LiveViewObjectsPerGroup` (default `32768`) trades group count for lag
when a group is expanded.

### Watch a property change live — THE technique for "which field is the selection?"
1. Open the menu in-game, find its container instance in Live View (e.g. `Xcmn_Pause_C_0`).
2. Right-click a candidate property (or a child row) → **Add watch**.
3. Open the **Watches** tab; expand values with the `+` buttons.
4. **Move the cursor in-game and watch which value flips.** That's your selection signal — no colour, no
   guessing. If nothing in reflection flips, the highlight is material/animation-driven (read the menu
   behaviourally instead).
5. Each watch has two checkboxes (enable/disable, **write to file** → a `.txt` under
   `<working dir>/watches/` when the game closes) and a save option that re-adds it on the next launch,
   persisted in `watches/watches.meta.json`.

**Text equivalent:** `probe <Class>` in the inspector template — snapshot the scalar properties, move the
cursor, snapshot again, print the diff. Same answer, no GUI ([cookbook §4](ue4ss-reflection-cookbook.md)).

### Call any function
The **"Find functions"** button opens a search over the object's UFUNCTIONs and lets you **call them**
in-game with no code — great for confirming what a setter/among does before you hook it.

### Limitation
The viewer only shows **reflected** data. Custom-engine primitives (e.g. CyberConnect2's
`CFUIMultiLineTextBox`) may hide their real fields (a text colour that reads as `-`), and material/native
state won't appear.

---

## 3. Recommended discovery flow (replaces most custom dumping)

Full version — including how to avoid restarting at all — in
**[`ue4ss-live-workflow.md`](ue4ss-live-workflow.md)**. In short:

1. Enable the console + hot reload (settings above), launch.
2. **One** `Ctrl+H` / `GenerateSDK()` with `LoadAllAssets…=1` → the offline property catalogue.
   (This is the one dump that justifies a restart afterwards.)
3. From then on ask the **running game**: `find` / `dumpclass` / `props` / `probe` from a console command
   or the command file, output to a text file you read live
   ([cookbook](ue4ss-reflection-cookbook.md)). Sighted alternative: Live View + watches.
4. Only if reflection can't express the state, fall back to §4 below — and guard **every** deref with
   `IsValid`, avoiding `RenderTransform`/nested-struct reads (they can abort uncatchably; see
   [accessibility-patterns](accessibility-patterns.md)).
5. Iterate with **hot reload** (`Ctrl+R`), never a relaunch.

---

## 4. Reading state that reflection CANNOT see (two proven techniques)

Some menu state is a **non-`UPROPERTY` C++ member** — a private selection/page/window index the game never
exposed to reflection. It's absent from the object dump and the Live View, and the CXX header shows it only
as an unnamed tail gap. Two techniques recover it. Both were used to accessibilize DBZ Kakarot menus that
were otherwise dead ends (battle pause, overworld menu, party slots, the Tips book).

### 4a. Native memory diff — find a hidden index by watching raw memory (the "F4 probe")
When Live View shows nothing flipping as you navigate, the index still lives in the object's memory tail.
Find its offset by **diffing raw bytes as you move the cursor** — the memory-level equivalent of a Live
View watch:

1. A tiny **separate** in-process Lua C module (`mem_bridge.dll`) does **SEH-guarded** reads
   (`read_i32/u8/ptr/bytes` at `obj:GetAddress() + off`; a bad address returns `nil`, never crashes). Keep
   it apart from the screen-reader bridge.
2. A dev keybind (we use **F4**) snapshots a broad tail window (e.g. `0x398..0x518`) of every candidate
   on-screen object, and on the next press reports each `int32` that changed to a small value
   (`0 ≤ v < 64`) — filtering out pointers/floats. Open a menu → press (baseline) → move the cursor →
   press again: the offset whose value **steps `0→1→2…` and wraps** is the selection/page index.
3. Read it at runtime via the same bridge and use it directly. A game patch → re-run the probe, edit one
   offsets data file (no recompile).

**Confirmed finds (DBZ Kakarot):** battle-pause row (`UAT_UIXCmnPause+0x43C`), overworld ring index
(`UAT_UIStartTop+0x4E4`) + submenu depth flag (`+0x4DC`) + sub-cursor (`+0x4EC`), Tips book front-window
index (`UAT_UITips+0x420`) + current-page index (`+0x424`). Each took ONE probe session. **This is the
go-to whenever a "current selection / page / tab / window" isn't reflected** — faster and more reliable
than any highlight-image heuristic. (For a purely static offset — e.g. a total-page count that doesn't
change as you navigate — the diff won't catch it; read it from the CXX header gap instead.)

### 4b. Fixed-array UPROPERTY collapse → `RegisterCustomProperty`
A widget member declared as a **fixed C array** — `SomeType* Name; // (size: 0xNN)` where `NN > 8` (so it's
`Name[NN/8]` inline pointers, not a `TArray`, whose size is `0x10`) — is **collapsed by UE4SS reflection to
element 0 only**. `host.Name` yields element 0; `host.Name[i]` yields garbage/nil; and array methods
(`:GetArrayNum()` / `#`) **abort uncatchably**. This hides every row of a multi-slot list (party slots,
standby lists, character bars…).

Recover the other elements with UE4SS's own **`RegisterCustomProperty`** — register each hidden pointer at
`base + i*8` as an `ObjectProperty`, and UE4SS then exposes it as a normal reflected property:
```lua
RegisterCustomProperty({
    ["Name"] = "Slot1",
    ["Type"] = PropertyTypes.ObjectProperty,
    ["BelongsToClass"] = "/Game/…/Start_Party_List.Start_Party_List_C",  -- runtime blueprint class path
    ["OffsetInternal"] = 0x3A0,                                          -- base (0x398) + 1*8
})
-- then: host.Slot1  ->  the element-1 UObject, read normally
```
Register lazily **once** (guarded) when the screen first appears. Get `BelongsToClass` from a live instance
(`obj:GetClass():GetFullName()` minus the `WidgetBlueprintGeneratedClass ` prefix), and confirm the offset
with a one-shot probe: `Mem.ptr(host, base)` must equal `Mem.addr(host.Name)` (element 0), and
`base + i*8` are the sibling pointers. **This unlocks any fixed-array row menu that reflection flattens.**

### Related helper — pooled instances whose active copy alternates
Unrelated to the above but a common "found but silent" cause: some screens have **several pooled instances**
(`Start_Char_C_3`/`_4`) and only one is on-screen at a time, alternating. Caching a single instance locks
onto one and goes mute when the other is live. Pick the on-screen one each tick instead (find-all +
`on_screen` filter), not a single cached ref.
