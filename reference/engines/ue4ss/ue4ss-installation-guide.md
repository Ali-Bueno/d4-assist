# UE4SS Installation Guide

## What is UE4SS?

UE4SS (Unreal Engine 4/5 Scripting System) is a modding framework for Unreal Engine games (4.12+ and
UE5). It provides:
- Lua scripting API for runtime game manipulation
- C++ mod API for compiled mods
- Blueprint mod loading
- Live object viewer/editor (GUI debugger)
- Header/object dumpers for reverse engineering

## 1. Which distribution

- **Basic** — `UE4SS_v{version}.zip` (e.g. `UE4SS_v3.0.0.zip`). No debug windows, minimal footprint.
  Use for end users / a finished mod.
- **Developer** — `zDEV-UE4SS_v{version}.zip`. Same directory layout as Basic but ships extra files
  (including example `CustomGameConfigs`) and a `UE4SS-settings.ini` with the console and the GUI
  console **enabled by default**. **Always use this for development.**
  - Dev vs Basic is **not a reinstall decision**: it's the same binary with different default config.
    To switch, just edit `UE4SS-settings.ini` — no need to swap distributions.
- **Experimental** — release tag `experimental-latest`, rebuilt on every commit to `main`; asset names
  look like `UE4SS_v3.0.0-5-ga5e818e.zip` / `zDEV-UE4SS_v3.0.0-5-ga5e818e.zip`. Only use this to pick up
  an unreleased fix. If you do, read the docs at docs.ue4ss.com/dev/ instead of the stable docs, since
  behavior may have drifted.

## 2. The four directories

UE4SS reasons about four distinct locations; know which is which before troubleshooting anything:

| Directory | Meaning | Typical contents |
|---|---|---|
| **Root directory** | Where `UE4SS.dll` lives | `UE4SS.dll`, `dwmapi.dll` (proxy, standard install) |
| **Working directory** | Config + mods, inside the root directory | `UE4SS-settings.ini`, `UE4SS.log`, `Mods/mods.txt`, `Mods/<ModFolder>/scripts/main.lua` |
| **Game directory** | The launcher/wrapper folder | Varies per game |
| **Game executable directory** | Where the real `.exe` lives | The actual game `.exe`, plus `dwmapi.dll` in a standard (non-central) install |

In the common single-game install, root directory == game executable directory and the working
directory is just the same folder. They only diverge in a **central install** (§4) or when overriding
the DLL path (§5).

## 3. Installing on a game (standard steps)

1. Find the game's executable directory:
   ```
   GameName/Binaries/Win64/
   ```
   Look for the **large** `.exe` (not a small launcher wrapper) — that's the game executable directory.
2. Extract the UE4SS zip contents directly into that directory. This makes it simultaneously the root
   directory and the working directory.
3. The proxy DLL (`dwmapi.dll`) loads automatically when the game starts — it can be renamed to any DLL
   name the engine already loads, if `dwmapi.dll` collides with something.
4. Launch the game and verify per §7.

## 4. Central install location

Use this to run **one** UE4SS install shared across several games, injected manually (not via the
proxy DLL) into each:

1. Extract the zip **outside** any game directory — that extracted folder is the root directory.
2. Inside it, create a subfolder whose name is the **exact name** of that game's executable directory.
   Example: if the exe lives at `...\SatisfactoryEarlyAccess\FactoryGame\Binaries\Win64\...`, create
   `<root>\SatisfactoryEarlyAccess\`.
3. Put a `UE4SS-settings.ini` inside that subfolder — UE4SS auto-detects it as the working directory for
   that game.
4. Point the injector at `<root>/UE4SS.dll`.
5. A `UE4SS-settings.ini` placed directly in the root directory acts as the **default** config for any
   game that doesn't have its own working-directory subfolder.

## 5. Overriding the UE4SS.dll location (CLI / files / env)

UE4SS resolves which `UE4SS.dll` to load in this priority order (highest first):

1. Command-line argument: `--ue4ss-path "<path>"` — absolute, or relative to the game executable
   directory.
2. An `override.txt` file next to `dwmapi.dll`, containing the path to `UE4SS.dll`.
3. `<game executable directory>/ue4ss/UE4SS.dll`
4. `<game executable directory>/UE4SS.dll`

Other CLI / environment options:
- `--disable-ue4ss` — disables UE4SS without uninstalling it. Only works with a **proxy-DLL** install
  (not manual injection). **Version check (2026-07-29): this flag does NOT exist on v3.0.1** — zero
  occurrences in the whole v3.0.1 tree. It is generated into the proxy stub on **main** only
  (`UE4SS/proxy_generator/main.cpp:273`). On 3.0.x it is silently ignored, so it is not the clean A/B
  baseline for "is this slowdown the mod or the framework?" that it looks like — rename/move the proxy
  DLL instead.
- Env var `UE4SS_MODS_PATHS` — extra mod directories, separated by `;`. Processed in **reverse order**:
  the **first** path listed gets the **highest** priority.
  - Ini equivalent: `[Overrides]` section, `+ModsFolderPaths=` / `-ModsFolderPaths=` entries — those are
    read in **order of appearance**, and the **last** one gets the highest priority (opposite ordering
    rule from the env var — don't mix up the two).

## 6. Manual injection

1. Download the Basic or Developer zip.
2. Delete `dwmapi.dll` from the extracted contents (it's the proxy-load mechanism; injection replaces
   it).
3. Launch the game.
4. Inject `UE4SS.dll` with your own injector.

Use this when the proxy DLL doesn't load (anti-cheat, packaging quirks) or for a central install (§4).

## 7. Verifying it loaded

- **`UE4SS.log`** — sits next to `UE4SS.dll` in the working directory. Check for fresh timestamps and no
  AOB-scan-failure errors.
- **In-game console** — press `@` or `F10` (bound by the built-in `ConsoleEnablerMod`).
- **GUI console** — with `[Debug] GuiConsoleEnabled=1` and `GuiConsoleVisible=1` set, a separate console
  window should open alongside the game.

## 8. Custom game configs

- **What they're for**: per-game overrides for engines that have been modified enough to break UE4SS's
  autodetection (AOB scans, member layouts, vtables).
- **Where**: `Binaries/Win64/CustomGameConfigs/<GameName>/`, mirroring the folder structure of the UE4SS
  repo itself.
- **What a game's folder can contain**:
  - `UE4SS_Signatures/*.lua` — custom AOB patterns (see §9)
  - `UE4SS-settings.ini` — settings override that applies only to this game
  - `MemberVariableLayout.ini`
  - `VTableLayout.ini`
- The Developer distribution ships example `CustomGameConfigs`.
- Upstream repo of known configs: github.com/UE4SS-RE/RE-UE4SS/tree/main/assets/CustomGameConfigs
- **Rule: try without a custom config first.** The current UE4SS release may already fix what an old
  custom config worked around.

## 9. Fixing missing AOBs

If `UE4SS.log` reports failed AOB scans, supply custom signatures:

1. Create a `UE4SS_Signatures/` folder — either in the working directory, or inside
   `CustomGameConfigs/<Game>/` for a config scoped to one game.
2. Add Lua files by **exact, case-sensitive** name. Usually required:
   `GUObjectArray.lua`, `FName_ToString.lua`, `FName_Constructor.lua`, `StaticConstructObject.lua`,
   `GMalloc.lua`. Rarely needed: `GUObjectHashTables.lua`, `GNatives.lua`, `ConsoleManager.lua`,
   `ProcessLocalScriptFunction.lua`, `ProcessInternal.lua`, `CallFunctionByNameWithArguments.lua`.
3. Each file defines two functions:
   - `Register()` → returns the AOB pattern as a string.
   - `OnMatchFound(MatchAddress)` → returns the final address derived from the match.
   - Alternative: skip both functions and just return an address directly at file scope — useful with
     `LoadExport` in modular games.
4. **AOB syntax**: nibbles, two per byte; spaces are optional (`"8B 51 04 85"` == `"8B510485"`);
   wildcard byte is `??`.
5. **Helpers available inside `OnMatchFound`**: `DerefToInt32(addr)` (returns an `int32` or `nil`), and
   `print()` for debug output.
6. **What each symbol must resolve to**:
   - `GUObjectArray` → address of the global.
   - `FName_ToString` → start of `FName::ToString(FString&) const`.
   - `FName_Constructor` → start of `FName::FName(wchar_t const*, EFindName)`.
   - `StaticConstructObject` → start of `StaticConstructObject_Internal(FStaticConstructObjectParameters const&)`.
   - `GMalloc` → address of the global.
   - The rest (`GUObjectHashTables`, `GNatives`, etc.) are rarely needed.
7. Example:
   ```lua
   function Register()
       return "48 8B C4 57 48 83 EC 70 80 3D ?? ?? ?? ?? ?? 48 89"
   end

   function OnMatchFound(MatchAddress)
       return MatchAddress
   end
   ```
8. **Verify**: clear `UE4SS.log`, restart the game. If `Register`/`OnMatchFound` don't throw and the log
   shows no AOB-scan failures, the pattern is good.
9. **Finding the AOB in the first place**: get a clean build of the game with PDBs, open it in x64dbg,
   locate the target function/signature, extract the bytes (e.g. with the SwissArmyKnife plugin), then
   generalize volatile bytes into `??` wildcards.

## Dumper hotkeys (reference)

| Hotkey | Output | Purpose |
|--------|--------|---------|
| Ctrl+H | `.hpp` files | C++ headers per class (with offsets) |
| Ctrl+Numpad 9 | UHT headers | UHT-compatible for `.uproject` reconstruction |
| Ctrl+J | `UE4SS_ObjectDump.txt` | All loaded objects |
| Ctrl+Numpad 6 | `.usmap` | Mapping files |
| Ctrl+Numpad 7 | CSV | Static mesh / actor data |

## 10. Where to go next

- Full `UE4SS-settings.ini` option reference (every section, defaults):
  [ue4ss-settings-reference.md](ue4ss-settings-reference.md)
- Step-by-step, per-game accessibility workflow once UE4SS is installed and loading:
  [workflow-per-game.md](workflow-per-game.md)
