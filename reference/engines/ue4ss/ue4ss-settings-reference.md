# UE4SS-settings.ini reference

Every option in `UE4SS-settings.ini`, by section, with real defaults from the `assets/UE4SS-settings.ini`
of the UE4SS-RE/RE-UE4SS repo. Lives next to `UE4SS.dll` (flat layout) or in `ue4ss/` (subfolder layout).
The load-bearing options for **getting UE4SS to run at all** are in
[ue4ss-compatibility-and-crashes](ue4ss-compatibility-and-crashes.md); this is the full menu. Dump-trigger
keybinds and the Live View workflow are in [ue4ss-live-workflow.md](ue4ss-live-workflow.md) — the keys
below only shape *what* a dump contains, not *how* to fire one.

## [Overrides]

| Key | Default | Effect |
|---|---|---|
| `ModsFolderPath` | `<dll_directory>/Mods` | Where mods are loaded from. |
| `+ModsFolderPaths=` / `-ModsFolderPaths=` | (none) | Add/remove an extra mods folder. Repeatable; the last one to appear wins on priority. |
| `ControllingModsTxt` | (empty) | If set, only that specific `mods.txt` is parsed — every other `mods.txt` is ignored. |

## [General]

| Key | Default | Effect |
|---|---|---|
| `EnableHotReloadSystem` | `1` | Enables mod reload via `HotReloadKey`. |
| `HotReloadKey` † | `R` | Reload hotkey; **CTRL is always required** → effectively Ctrl+R. Valid values are the same set as `Mods/Keybinds`/`Scripts/main.lua`. |
| `EnableAutoReloadingLuaMods` † | `0` | Set `1` to auto-reload the moment a file under `Scripts/` changes or is added, with no keypress. |
| `UseCache` | `1` | Cache AOB scan results. |
| `InvalidateCacheIfDLLDiffers` | `1` | Drop the cache if the target DLL changed since it was built. |
| `SecondsToScanBeforeGivingUp` | `30` | AOB scanner timeout. |
| `bUseUObjectArrayCache` | `true` | Installs listeners on `GUObjectArray` for a fast cache instead of iterating it live. **Set `false` if the game crashes on startup.** (Some non-zDev release presets ship this already `false`.) |
| `DoEarlyScan` † | `0` | Run the one-shot AOB scan as early as possible after launch. |
| `bEnableSeachByMemoryAddress` † | `false` | Sic — the key is misspelled "Seach" in the ini itself. Enables searching for a UObject by raw hex address in Live View. |
| `DefaultExecuteInGameThreadMethod` † | `EngineTick` | How `ExecuteInGameThread` is dispatched: `EngineTick` or `ProcessEvent`. |
| `DefaultFNameToStringMethod` † | `Scan` | FName→string strategy: `Scan` or `Conv_NameToString`. |

† Not present in the 3.0.1 ini — see the version reality check below.

> **Version reality check (verified on v3.0.1).** The ini shipped with **3.0.1** contains only
> `EnableHotReloadSystem`, `UseCache`, `InvalidateCacheIfDLLDiffers`, `SecondsToScanBeforeGivingUp` and
> `bUseUObjectArrayCache` in `[General]`. `HotReloadKey`, `EnableAutoReloadingLuaMods`, `DoEarlyScan`,
> `bEnableSeachByMemoryAddress`, `DefaultExecuteInGameThreadMethod` and `DefaultFNameToStringMethod` are
> **not present** there — adding them by hand does nothing on that build. On 3.0.1 the reload is a fixed
> `Ctrl+R` (or the *Restart All Mods* button). Always diff this table against the ini in front of you
> before recommending a key.

> **`DefaultExecuteInGameThreadMethod` naming trap.** Valid values are `EngineTick` (default — once per
> frame, predictable cadence) and `ProcessEvent` (many times per frame, lower latency). The official docs'
> prose calls this key `DefaultGameThreadExecutionMethod`, which does not exist — the ini spelling above
> is the correct one.

## [EngineVersionOverride]  ← the #1 compatibility fix

| Key | Default | Effect |
|---|---|---|
| `MajorVersion` | (empty) | Force the engine major version the scanner assumes, for games that hide/alter their real engine version. |
| `MinorVersion` | (empty) | Force the minor version. For a stubborn game, try **one minor below** the real version first (e.g. real 4.21 → `4`/`20`), then the exact version. This is the single most common fix for `Failed to find GUObjectArray`. |
| `DebugBuild` | `false` | Target executable is a Debug/Development build. |

## [ObjectDumper]

| Key | Default | Effect |
|---|---|---|
| `LoadAllAssetsBeforeDumpingObjects` | `0` | Force-loads every asset before the object dump. Literal warning in the docs: can need several extra GB of RAM, is unstable, and **crashes the game** if you keep playing past the main menu after the dump. |
| `UseModuleOffsets` | `0` | Show the offset from the executable base instead of a raw function pointer. |

## [CXXHeaderGenerator]

| Key | Default | Effect |
|---|---|---|
| `DumpOffsetsAndSizes` | `1` | Include property offsets/sizes in generated headers. |
| `KeepMemoryLayout` | `0` | Literal warning in the ini itself: "A VALUE OF 1 HAS NO PURPOSE YET! MEMORY LAYOUT IS NOT ACCURATE EITHER WAY!" — don't rely on it. |
| `LoadAllAssetsBeforeGeneratingCXXHeaders` | `0` | Same RAM/instability/crash warning as `ObjectDumper.LoadAllAssetsBeforeDumpingObjects`, applied to the header generator. |

## [UHTHeaderGenerator]

| Key | Default | Effect |
|---|---|---|
| `IgnoreAllCoreEngineModules` | `0` | Skip all core engine modules when generating headers. |
| `IgnoreEngineAndCoreUObject` | `0` | Skip the `Engine` and `CoreUObject` packages specifically. |
| `MakeAllFunctionsBlueprintCallable` | `1` | Forces `BlueprintCallable` on generated functions. Warning: generates header errors that need manual fixing. |
| `MakeAllPropertyBlueprintsReadWrite` | `1` | Forces `BlueprintReadWrite` + `meta=(AllowPrivateAccess=true)` on generated properties. |
| `MakeEnumClassesBlueprintType` | `1` | Forces `BlueprintType` on enum classes, and forces an explicit `uint8` underlying type where it was implicit. |
| `MakeAllConfigsEngineConfig` | `1` | Forces `config=Engine` on generated config properties. |

## [Debug]

| Key | Default | Effect |
|---|---|---|
| `ConsoleEnabled` | `1` | External text debug console. |
| `GuiConsoleEnabled` | `1` | GUI console — this is where **Live View** lives. |
| `GuiConsoleVisible` | `1` | Show the GUI console on startup. |
| `GuiConsoleFontScaling` | `1` | GUI console font scale multiplier. |
| `GraphicsAPI` | `opengl` | `dx11` / `d3d11` / `opengl`, case-insensitive. **If the GUI window comes up blank, change this.** |
| `RenderMode` | `ExternalThread` | `ExternalThread` / `EngineTick` / `GameViewportClientTick` — how the GUI console renders. (Not present in the 3.0.1 ini.) |
| `LiveViewObjectsPerGroup` | `32768` | Live View group size: lower = more groups but less lag when one is expanded; higher = fewer groups, more lag. Present in 3.0.1. |

## [Threads]

| Key | Default | Effect |
|---|---|---|
| `SigScannerNumThreads` | `8` | Minimum 1. If the game is modular, multi-threaded scanning is always disabled regardless of this value. |
| `SigScannerMultithreadingModuleSizeThreshold` | `16777216` | Module size (bytes) above which multithreaded scanning kicks in. |

## [Memory]

| Key | Default | Effect |
|---|---|---|
| `MaxMemoryUsageDuringAssetLoading` | `85` | RAM usage ceiling (%). Once hit, the asset loader stops and lets the current operation finish. |

## [Hooks]  ← function-dispatch detours UE4SS installs

All default `1`.

| Key | Default | Effect |
|---|---|---|
| `HookProcessInternal` | `1` | UFunction-dispatch detour backing Lua `RegisterHook`. **Setting to 0 does not reliably stop the detour**: any mod calling `RegisterHook` makes UE4SS install it on demand anyway. |
| `HookProcessLocalScriptFunction` | `1` | Same demand-install caveat as above. |
| `HookUObjectProcessEvent` | `1` | Same demand-install caveat as above. |
| `HookInitGameState` | `1` | Enables the `RegisterInitGameStateHook` Lua API. |
| `HookLoadMap` | `1` | Enables the `RegisterLoadMapHook` Lua API. |
| `HookCallFunctionByNameWithArguments` | `1` | Enables the corresponding Register*Hook Lua API. |
| `HookBeginPlay` | `1` | Enables the `RegisterBeginPlayHook` Lua API. |
| `HookEndPlay` | `1` | Enables the `RegisterEndPlayHook` Lua API. |
| `HookLocalPlayerExec` | `1` | Enables the corresponding Register*Hook Lua API. |
| `HookAActorTick` | `1` | Enables the corresponding Register*Hook Lua API. |
| `HookEngineTick` | `1` | Backs `ExecuteInGameThread` via tick. **Must stay on** for tick-based game-thread execution. |
| `HookGameViewportClientTick` | `1` | Enables the corresponding Register*Hook Lua API. |
| `HookProcessConsoleExec` | `1` | Enables the corresponding Register*Hook Lua API. |
| `HookUStructLink` | `1` | Enables the corresponding Register*Hook Lua API. |
| `EngineTickResolveMethod` | `Scan` | `Scan` with fallback to VTable, or the reverse — resolution strategy for the engine tick hook. |
| `FExecVTableOffsetInLocalPlayer` | `0x28` | VTable offset used to resolve `FExec` in `LocalPlayer`. |

## [CrashDump]

| Key | Default | Effect |
|---|---|---|
| `EnableDumping` | `1` | Write a crash dump when UE4SS/the game crashes. |
| `FullMemoryDump` | `0` | Include a full memory dump alongside the crash dump. |

> Undocumented upstream — zero inline comments in the shipped ini, zero mentions in either docs track.
> The effects above are inferred from the key names, not confirmed by a source.

## [ExperimentalFeatures]

Empty by default in the reference ini — no experimental features are populated today.

---

## Ready-to-copy profiles

### DEV profile (live discovery / exploration session)

```ini
[General]
EnableHotReloadSystem = 1
EnableAutoReloadingLuaMods = 1
bEnableSeachByMemoryAddress = true

[Debug]
ConsoleEnabled = 1
GuiConsoleEnabled = 1
GuiConsoleVisible = 1
```

Alternative: use the `zDEV-UE4SS_vX` distro directly — it already ships with this profile applied.

### RELEASE profile (accessibility mod, end-user facing)

```ini
[Debug]
ConsoleEnabled = 0
GuiConsoleEnabled = 0
GuiConsoleVisible = 0

[General]
EnableAutoReloadingLuaMods = 0

[ObjectDumper]
; leave DoEarlyScan-related discovery aids off; not part of ObjectDumper but see [General] below
```

```ini
[General]
DoEarlyScan = 0
EnableAutoReloadingLuaMods = 0
```

Base this on the `basic` distro. Reason: the debug console and the GUI console/Live View are **not
accessible to a screen reader** — they're pure dev-time surface and must not ship to a blind end user.

---

## What to change when X

**The GUI console comes up blank.**
`[Debug] GraphicsAPI` — flip between `opengl` and `dx11`/`d3d11`.

**The game crashes on startup (before or during UE4SS init).**
1. `[General] bUseUObjectArrayCache` → try `false`.
2. `[EngineVersionOverride] MajorVersion` / `MinorVersion` → force the version, starting one minor below
   the real one (e.g. `4`/`20` for a real 4.21 target), since a wrong auto-detected version is the most
   common cause of `Failed to find GUObjectArray`.

**I want dumps to include lazy-loaded classes.**
`[ObjectDumper] LoadAllAssetsBeforeDumpingObjects = 1` (for `Ctrl+J`-style object dumps) and
`[CXXHeaderGenerator] LoadAllAssetsBeforeGeneratingCXXHeaders = 1` (for header generation). Both force
every asset to load before the dump, which surfaces classes that only get loaded lazily during normal
play. Trade-off: several extra GB of RAM and instability — the docs explicitly warn the game **will
crash** if you keep playing past the main menu after the dump, so do the dump, then restart before
playing further.

**I want to know whether a slowdown is my mod or UE4SS itself.**
Launch with `--disable-ue4ss` (proxy-DLL installs only) to disable UE4SS entirely without uninstalling —
the clean A/B baseline. See [`ue4ss-performance.md`](ue4ss-performance.md) §8.

---

None of the dumper/header-generator keys above trigger a dump by themselves — they only shape what a
dump contains once you fire one. The actual dump keybinds and the live discovery workflow are in
[ue4ss-live-workflow.md](ue4ss-live-workflow.md).
