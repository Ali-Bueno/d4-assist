# Getting the game's classes, data and text into files

**Goal: stop discovering the game at runtime.** Every widget class name, every property name and every
on-screen string that a UE game has is sitting in its `.pak` files and in its reflection data. Almost
all of it can be extracted **once, to disk, offline** — then grepped in a text editor instead of probed
with a live mod.

This matters twice over:
- **Dev time.** Kakarot's `discover.lua` is 1234 lines of runtime probing; Sparking ZERO has three
  separate probe modules. That is a lot of code whose only job is to answer questions a file could have
  answered.
- **Game-thread time.** Every probe is a scan (65–115 ms — see
  [`ue4ss-performance.md`](ue4ss-performance.md) §2). Discovery you did offline costs zero at runtime.

Reserve live reflection for the one thing files genuinely cannot tell you: **which object is active
right now**.

> Related: [`ue4ss-discovery-tools.md`](ue4ss-discovery-tools.md) covers UE4SS's built-in dumpers as
> tools; [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) covers the zero-restart *live* loop. This
> doc is about the **offline corpus** you should build before either of those.

---

## 1. Route map

| Route | Tool | What you get | Needs |
|---|---|---|---|
| **1. `.usmap` + asset browser** | UE4SS `DumpUSMAP()` → **FModel** | Widget trees, Blueprint properties **with default values**, DataTables, StringTables, level/actor data | Game run once; AES key if paks are encrypted |
| **2. Reflection dumps as text** | UE4SS `GenerateSDK()` (CXX) / `GenerateUHTCompatibleHeaders()` / `DumpAllObjects()` | Authoritative class-level property + function lists for **every** loaded class, incl. members no instance exposes | Game at main menu |
| **3. Localisation** | `.locres` extractors | Every on-screen string as `key → text` | Nothing beyond the pak |
| **4. Asset index** | `AssetRegistry.bin` → JSON | Path + class + tags of **every asset in the game** | Nothing beyond the pak |
| **5. SDK fallback** | Dumper-7 | C++ SDK + `.usmap` + machine-readable Dumpspace JSON | Injecting a second DLL |

Routes 1–4 are complementary, not alternatives. Do all four once per game, in that order.

---

## 2. Route 1 — `.usmap` + FModel (the daily driver)

**Get the mappings.** `DumpUSMAP()` from the UE4SS console (or Ctrl+Numpad6) writes `Mappings.usmap`
next to the game exe. Alternatives: [`UnrealMappingsDumper`](https://github.com/TheNaeem/UnrealMappingsDumper)
(standalone injected DLL), or check [FModel/Unreal-Game-Keys](https://github.com/FModel/Unreal-Game-Keys)
— mappings and AES keys for many games are already published, so you may not need to dump at all.

**Do you even need it?** `.usmap` is only required for **unversioned property serialization** — the
default in UE5, opt-in from UE4.25. Classic versioned UE4 cooked assets embed each property's name and
type inline and open with no mappings file at all. If FModel says *"Package has unversioned properties
but mapping file is missing"*, you need it; otherwise skip.

**Browse and export with [FModel](https://fmodel.app).** Point it at the game's `Paks` folder, set the
UE version, add the `.usmap` and AES key if needed. What it gives you that matters for an accessibility
mod:

- **Widget Blueprints** (`WBP_*`) are `UBlueprintGeneratedClass` exports containing a `WidgetTree`
  subobject graph. Exported as JSON you get the child widget objects, their classes, their instance
  names and their serialized default values — i.e. **the layout of a menu screen, offline**.
- **DataTables and StringTables** export straight to JSON — item names, quest text, tooltips.
- **Blueprint logic** is shown as disassembled Kismet bytecode, not a graph. Useful for understanding
  a menu transition, useless for names. Not re-importable.

**Scripting it in bulk.** FModel is a GUI over [CUE4Parse](https://github.com/FabianFG/CUE4Parse) (C#).
For repeatable extraction without writing C#, [`Ue4Export`](https://github.com/CrystalFerrai/Ue4Export)
is a config-driven CLI (asset path list + type tags → JSON/text/PNG, wildcards supported, `--key` /
`--mappings` flags). A native Python reimplementation exists in
[`pyUE4Parse`](https://github.com/MinshuG/pyUE4Parse) — moderate adoption, current maintenance
unconfirmed.

**Limits.** Encrypted paks need the AES key. Nothing here shows **non-reflected native C++ members** —
UE reflection is opt-in, so anything without `UPROPERTY`/`UFUNCTION` simply does not exist in any of
these outputs (that is Ghidra territory — see §7).

---

## 3. Route 2 — UE4SS's own dumps, used as text

All three are **plain Lua globals**, callable mid-session from the console or the
[inspector template](../../../templates/ue4ss-inspector/) — no relaunch cycle.

| Dump | Trigger | Output |
|---|---|---|
| C++ headers | `GenerateSDK()` / Ctrl+H | `CXXHeaderDump/` — one `.hpp` per Blueprint, `<Module>.hpp` per native module, `<Class>_enums.hpp` |
| UHT headers | `GenerateUHTCompatibleHeaders()` / Ctrl+Numpad9 | `UHTHeaderDump/` |
| Object dump | `DumpAllObjects()` / Ctrl+J | `UE4SS_ObjectDump.txt` — every *loaded* object, its class, property offsets and values |

**Treat the header dumps as a grep-able catalogue, never as compilable code.** The CXX generator's own
authors call it shoddily made; its `KeepMemoryLayout` flag — the one that would make the headers safe
to `#include` in a real project — is documented as currently having *"no purpose yet as memory value is
not accurate either way"*. Bitfields always emit as `uint8` regardless of their real width.

What it *is* good for is unbeatable: `grep -r "Focused" CXXHeaderDump/` across ~1859 native classes
answers "does this game publish its own UI state?" — rung 1 of the decision ladder in
[`ue4ss-mod-architecture.md`](ue4ss-mod-architecture.md) — in seconds. Header dumps also list
**class-level** members that no single live instance happens to expose, which runtime probing can never
find.

**The one-shot rule.** `LoadAllAssetsBeforeGeneratingCXXHeaders=1` / `LoadAllAssetsBeforeDumpingObjects=1`
give you a far more complete dump, and cost *multiple gigabytes of RAM* and **crash the game if you
keep playing past the main menu afterwards**. Do exactly one load-all dump per game, at the main menu,
then set both back to 0 and never dump again.

**Do not run the UHT dump through
[`UE4GameProjectGenerator`](https://github.com/Buckminsterfullerene02/UE4GameProjectGenerator)** for a
Lua accessibility mod. That commandlet turns the dump into a compilable mirror `.uproject` — genuinely
useful if you are writing native C++ or `.pak` content mods, wasted effort if you only need names. The
useful artifact is the raw header text from step 1 alone. (It also requires a matching engine version,
UE ≥ 4.22, manual plugin wiring, and has known generation errors you fix by hand.)

---

## 4. Route 3 — `.locres` → every on-screen string

The game's `Localization/**/*.locres` files are a `key → text` table for every localised string in the
game. Extract them and you have the mod's entire string layer handed to you, in every shipped language,
without reading a single `FText` at runtime.

Tools (all mature, pick one): [`pylocres`](https://github.com/stas96111/pylocres) (Python, all locres
versions), [`UnrealLocres`](https://github.com/akintos) (C#),
[`UEExtractor`](https://github.com/SolicenTEAM/UEExtractor) (.NET, drag-and-drop → CSV),
[`LocResUtility`](https://github.com/anubi47/LocResUtility). FModel also browses and exports `.locres`
directly, since it is just another asset in the pak.

Why this matters for accessibility specifically: it lets you **map a runtime string back to a stable
key**. Reading a label off a widget gives you text that changes with the user's language; the locres
table lets you recognise *which* label it is regardless of language, and lets you ship your own
translations keyed the same way (see the i18n layer in
[`accessibility-patterns.md`](accessibility-patterns.md)).

---

## 5. Route 4 — `AssetRegistry.bin` → enumerate the whole game

`AssetRegistry.bin` lists **every asset in the game**: path, class, and tags. Converted to JSON (e.g.
[`AssetRegistryTool`](https://github.com/Code-Vein-Tool-Hub/AssetRegistryTool)) it answers "what UI
screens does this game even have?" up front — every `WBP_*`, every menu asset — instead of discovering
screens one at a time as a tester stumbles into them.

For a mod that must reach **100 % menu coverage** ([`ui-accessibility/generic-strategy.md`](../../ui-accessibility/generic-strategy.md)),
this is the difference between a checklist and a guess. It is the cheapest possible completeness audit.

---

## 6. Route 5 — Dumper-7 (fallback / machine-readable)

[`Dumper-7`](https://github.com/Encryqed/Dumper-7) is an injected one-shot dumper covering UE4.11–UE5.x
with no hardcoded offsets. It outputs a compilable C++ SDK, its own `.usmap`, IDA `.idmap` scripts, an
offset header, and **Dumpspace JSON**.

Largely redundant if UE4SS's dumpers already work on your target — but worth knowing for two cases:
UE4SS's generators choke on the game, or you want to **script** over the class/property corpus, where
Dumpspace JSON is far nicer to consume than raw `.hpp` text.

---

## 7. What offline extraction can never give you

| Not available offline | Where it comes from |
|---|---|
| Non-reflected native C++ members (no `UPROPERTY`) | Decompiler + runtime diff + a native read bridge — rung 6 of the ladder. Kakarot does this: Ghidra 12 headless with MSVC RTTI/demangler → `native_offsets.lua` |
| Which widget/screen is **active right now** | Live reflection — this is the legitimate remaining job of a per-tick read |
| Actual runtime values (HP, position, focus index) | Same |
| Whether a UFunction is dispatched through Blueprint (i.e. hookable) | Live test — both our games answered "no" only when probed |

---

## 8. Recommended bring-up order for a new game

1. Boot once with UE4SS, at the main menu: `DumpUSMAP()`, then one load-all `GenerateSDK()`, then
   `DumpAllObjects()`. Revert the `LoadAllAssetsBefore*` flags immediately.
2. Extract `.locres` and `AssetRegistry.bin` from the paks. You now have every string and every asset
   path.
3. Open FModel with the `.usmap`; export the `WBP_*` widget trees for the 3–4 screens on the critical
   path to JSON.
4. **Only now** write Lua. Grep the CXX dump for a published focus/selection member before designing
   anything (ladder rung 1) — if the game publishes its own UI state, the mod is nearly free.
5. Keep the corpus **out of git** (it is large and re-derivable), but record in `STATUS.md` *what was
   dumped, from which game build, and where it lives locally*, so it is never re-derived by accident.
