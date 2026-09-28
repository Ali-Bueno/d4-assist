# UE4SS compatibility & crash troubleshooting

A field guide for getting UE4SS to load on a stubborn Unreal game, written from real
debugging (DBZ Kakarot, UE 4.21). Work top to bottom — the earlier steps are cheaper
and more common. Each step says **how to recognize it** and **the fix**.

> TL;DR of the Kakarot case: the game crashed, then hung, purely because of UE4SS
> **address resolution**, not our mod. The two load-bearing fixes were
> **`[EngineVersionOverride]` = one minor below the real version** and **using a UE4SS
> build known to work with that game**. Everything else was a red herring.

---

## 0. Where the evidence lives

- **UE4SS log** — `UE4SS.log`, next to `UE4SS.dll` (flat layout) or in `ue4ss/`
  (subfolder layout). Read the **tail** and grep for `Failed to find`, `Found`,
  `Failed to add hook`, `Starting Lua mod`, `Event loop start`, and your mod's own
  load print. If your mod's load line is **absent**, the mod never ran — the problem is
  upstream (UE4SS init), not your Lua.
- **UE crash dumps** — `%LOCALAPPDATA%\<Project>\Saved\Crashes\UE4CC-*\`. Open
  `CrashContext.runtime-xml` and read `<PCallStack>` (top = crash point) and
  `<SecondsSinceStart>` (`0` = crashed during engine init / first frames). A crash with
  **UE4SS frames sandwiched between game frames** and `VCRUNTIME140` (memcpy) on top is
  the classic "UE4SS hooked/scanned a wrong address and copied garbage".

You rarely need a debugger. The log + the callstack pattern usually name the culprit.

---

## 1. Wrong engine version → globals not found (the big one)

**Recognize:** log spams
```
[PS] Failed to find GUObjectArray: expected at least one value
[PS] Failed to find StaticConstructObject_Internal: expected at least one value
    You can supply your own AOB in 'UE4SS_Signatures/...'
```
and then the game crashes at `SecondsSinceStart=0`, or hangs. UE4SS's pattern scanner
(patternsleuth) can't locate the core engine globals, so it operates on misidentified
addresses.

**Fix (try first — one line):** override the engine version in `UE4SS-settings.ini`,
usually to **one minor version BELOW** the real one:
```ini
[EngineVersionOverride]
MajorVersion = 4
MinorVersion = 20      ; for a real UE 4.21 game (Kakarot). Try N-1 first, then exact N.
```
Why it works: the object/struct member layout and scan strategy are keyed off this
version; the game's real build often matches the previous minor's layout. With the right
version, the scanner's final pass finds everything (`PS scan successful`,
`FName constructor verified`, `Using engine version: 4.20`).

**Also set:**
```ini
[General]
bUseUObjectArrayCache = true   ; if it still crashes on startup, try = false
```

**Only if the override doesn't resolve the globals:** author per-game AOB signatures in
`UE4SS_Signatures/<name>.lua` (GUObjectArray.lua, StaticConstructObject.lua, …). This is
the advanced route (x64dbg + a blank UE project of the same version with PDBs). See the
UE4SS docs "Fixing missing AOBs (Advanced)". For Kakarot this was **not** needed.

---

## 2. Hang at "Locating KismetSystemLibrary CDO…" → UE4SS build regression

**Recognize:** with the version override in place the scan now succeeds, the log reaches
```
Waiting for object construction...
Locating KismetSystemLibrary...
Locating KismetSystemLibrary CDO...
```
…and stops forever. The Lua mods never start (your mod's load print never appears), so
F-keys do nothing. The game itself runs (you reach the menu).

**Fix:** this is a regression in some newer UE4SS builds for older engines (4.20/4.21).
**Use a UE4SS build that is known to work with your game.** Community per-game bundles
are gold here:
- For **DBZ Kakarot**: the `DBZK_Fix` mod (Naitrate/KingKrouch) bundles a working UE4SS
  (`UE4SS.dll` ~19 MB, **built 2024-04-10**, patternsleuth, Lua 5.4.4). The Dec-2024
  official `experimental-latest` build hangs here; the April build does not.
- General tactic: search "`<game>` UE4SS" on Nexus/GitHub; if a fix mod ships UE4SS,
  copy **its** `UE4SS.dll` + `dwmapi.dll` + `UE4SS-settings.ini`.

**Keeps the bridge working:** as long as the working build still bundles **Lua 5.4.4**
(check with `grep -a "Lua 5.4" UE4SS.dll`), `prism_bridge.dll` needs no rebuild.

---

## 3. Crash inside UE4SS's function-dispatch hook

**Recognize:** callstack is `game → UE4SS → game ProcessInternal → UE4SS → memcpy`, and
the log shows `[UE4SS.ProcessInternal.LuaModImplScriptHook] Added posthook`. This is
usually a **downstream symptom of step 1** (bad addresses make the param marshaling
memcpy fault). Fix step 1 first; the hooks then behave.

**Note on the `[Hooks]` flags:** setting `HookProcessInternal = 0` does **not** reliably
prevent the detour — any mod that calls `RegisterHook` makes UE4SS install the
ProcessInternal detour **on demand** regardless of the flag. The bundled
`ConsoleEnablerMod` calls `RegisterHook` on `ClientRestart`, so it alone triggers it. If
you must run with the hook off, also disable the mods that call `RegisterHook`.

**For a first clean boot** of a new game, enable only your mod + `Keybinds` in
`mods.txt`; add the demo/console mods back once it's stable.

---

## 4. Layout: flat vs `ue4ss/` subfolder

Two install layouts exist; the `dwmapi.dll` proxy must match its `UE4SS.dll`:
- **Flat** (older / most per-game bundles): `Win64/dwmapi.dll`, `Win64/UE4SS.dll`,
  `Win64/UE4SS-settings.ini`, `Win64/Mods/`.
- **Subfolder** (newer official builds): `Win64/dwmapi.dll` + everything else under
  `Win64/ue4ss/` (`ue4ss/UE4SS.dll`, `ue4ss/Mods/`, `ue4ss/UE4SS.log`).

Don't mix a proxy from one layout with a `UE4SS.dll` from the other. When you swap the
whole build (step 2), swap the matching `dwmapi.dll` too and use that build's layout.

---

## 5. Screen reader silent but game fine

- A **system screen reader must be running** (NVDA/JAWS) — PRISM speaks through it.
- Confirm the bridge loaded: log should show `prism_bridge loaded, screen reader: NVDA`.
  If it shows `FAILED to load`, the 3 DLLs (`prism_bridge.dll`, `prism.dll`, `tolk.dll`)
  aren't all in the mod's `Scripts/` folder, or the bridge's Lua ABI doesn't match (the
  build isn't Lua 5.4.x — recompile `prism_bridge` against that build's Lua).
- Keybinds only fire once the mod has loaded (see step 2) and the game window has focus.

---

## Settings quick-reference (the values that mattered for Kakarot)

```ini
[General]
EnableHotReloadSystem = 1
bUseUObjectArrayCache = true
[EngineVersionOverride]
MajorVersion = 4
MinorVersion = 20
[Debug]
ConsoleEnabled = 1
GuiConsoleEnabled = 1        ; the GUI console + Live View are core (no mod needed)
GuiConsoleVisible = 1
GraphicsAPI = opengl
```

## The Kakarot resolution, in order

1. Crash at startup → read crash dump: UE4SS frame + memcpy → suspected hooks (wrong).
2. Updated UE4SS to experimental-latest → still crashed identically (not a version-age fix).
3. Disabled hooks / demo mods → still crashed (RegisterHook installs the detour anyway).
4. Log said `Failed to find GUObjectArray` → **root cause: address resolution.**
5. `[EngineVersionOverride] 4/20` → crash gone, scan succeeds.
6. Now **hung** at `Locating KismetSystemLibrary CDO` (Dec-2024 build regression).
7. Swapped to `DBZK_Fix`'s April-2024 UE4SS build (flat layout, Lua 5.4.4) → **works.**
8. `prism_bridge` unchanged throughout (Lua 5.4.4 everywhere). F8 speech + F9 read confirmed.
