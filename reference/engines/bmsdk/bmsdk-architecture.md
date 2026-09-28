# BmSDK — architecture: how the SDK gets into the game and takes control

> How a C# assembly ends up running inside a 2011/2015 Unreal Engine 3 game, what it hooks, and where
> the whole thing is fragile. Read this once before writing mod code; the day-to-day API is in
> [mod authoring](bmsdk-mod-authoring.md).
>
> Evidence below is from the repos at `D:\code\unreal\bmsdk\` (`BmSDK-AC` @ `v0.17.2-7-g3b29d7c`,
> `BmSDK-AK`). `etkramer/BmSDK` is the *same repository* as `Team-BmSDK/BmSDK-AC` (identical commit) —
> the `Team-BmSDK` org is the current home.

---

## 1. The four layers

```
Batman(AC|AK).exe                       <- UE3 game, x86 (AC) / x64 (AK)
 +- dinput8.dll                         <- Ultimate ASI Loader (third-party, ThirteenAG)
     +- plugins\BmSDK.Host.asi          <- native host (C++20, ~371 lines total)
         +- sdk\BmSDK.dll               <- the actual SDK (C#, net10.0-windows7.0, self-contained)
             +- Generated\**            <- 4,472 (AC) / 5,467 (AK) classes mirroring UnrealScript
             +- Framework\**            <- hand-written runtime (redirects, components, marshalling)
             +- BmGame\Scripts\*.cs     <- YOUR MOD, compiled at runtime by Roslyn
```

The important structural fact: **the native host does almost nothing**. It detours exactly one function
to get a foothold and boots CoreCLR. Every other hook, every UE3 global, every game call is implemented
in **managed C#** that P/Invokes Microsoft Detours. That is unusual, and it is good news for us — the
whole hooking surface is readable, debuggable C#, not opaque native code.

## 2. Boot chain (exact order)

1. Game starts. Windows loads `dinput8.dll` from `Binaries\Win32` (AC) / `Win64` (AK) — the **Ultimate
   ASI Loader** proxy, fetched at package time from `ThirteenAG/Ultimate-ASI-Loader` releases
   (`.github/workflows/build-release.yml`).
2. The ASI loader loads `plugins\BmSDK.Host.asi`. `DllMain(DLL_PROCESS_ATTACH)` sets
   `offsets::BaseAddress = GetModuleHandle(NULL)`, calls `DetourRestoreAfterWith()`, and registers
   **one** detour (`src/BmSDK.Host/dllmain.cpp`).
3. That one detour is on **`FEngineLoop::PreInit`** (`Framework/detour_manager.cpp`). On its first
   invocation it calls `runtime::load_dll()` and *then* the original `PreInit`.
   Why `PreInit` and not `DllMain`: the CLR must start after the process image is fully mapped but
   before engine init, and `LoadLibrary(hostfxr)` inside `DllMain` would deadlock on the loader lock.
4. `runtime::load_dll()` (`src/BmSDK.Host/runtime.cpp`) resolves **hostfxr via `nethost`**, passing
   `get_hostfxr_parameters{ assembly_path = <game>\...\sdk\BmSDK.dll }` so the *app-local* runtime in
   `sdk\` wins over any machine-wide .NET. It then uses the **command-line** init path
   (`hostfxr_initialize_for_dotnet_command_line`, i.e. `BmSDK.runtimeconfig.json`), requests
   `hdt_load_assembly_and_get_function_pointer`, and calls
   `BmSDK.Framework.Loader, BmSDK` -> `GuardedDllMain` (`UNMANAGEDCALLERSONLY_METHOD`).
5. `Loader.GuardedDllMain` -> `Loader.DllMain` (`src/BmSDK/Loader.cs`), still on the engine thread:
   - `EngineSynchronizationContext.InitOnThread()` — captures this thread as the **main thread**.
   - `FileUtils.Init()` — caches the CWD *early*, with an explicit code comment that CWD "gets
     unreliable once we start running code in detours".
   - `StaticInit` (class-path -> managed `Type` map), `ScriptManager.Init()` (find + Roslyn-compile
     mods), register redirectors, install the remaining detours.

**No function-pointer table is handed native->managed.** `GuardedDllMain` is `void()`. The managed side
finds everything itself from `Process.GetCurrentProcess().MainModule.BaseAddress`.

## 3. The hooks (all six installed from C#)

Installed by `Loader.cs` through `Framework/DetourUtil.cs` (a CsWin32 binding of
`DetourTransactionBegin/UpdateThread/Attach/Commit`; the delegates are rooted in a static list so the GC
cannot collect them).

| Hooked engine function | Offset constant (`FrameworkInternal/GameInfo.cs`) | What BmSDK does with it |
|---|---|---|
| `FEngineLoop::Tick` | `EngineTick` | Drains the main-thread work queue (`EngineSynchronizationContext`) |
| **`UObject::ProcessInternal`** | `ProcessInternal` | **Central dispatcher** — script lifecycle events + all UnrealScript redirects |
| `FWindowsViewport::ProcessDeferredMessage` | `ProcessDeferredMessage` | Raw `WM_KEYDOWN`/`WM_KEYUP` (+ SYS variants) -> `InputManager` |
| `UObject::AddObject` | `AddObject` | Auto-attach `[ScriptComponent(AutoAttach=true)]` to newly created objects |
| `UObject::ConditionalPostLoad` | `ConditionalPostLoad` | Same, for objects arriving from package load; also arms queued redirects |
| `UObject::ConditionalDestroy` | `ConditionalDestroy` | Tear down the managed wrapper for a dying `UObject` |

**Not hooked at all:** `ProcessEvent`, `CallFunction`, `StaticConstructObject`, D3D `Present`. No vtable
swapping, no IAT patching, no hand-rolled trampolines. If you want a per-frame *render* hook (e.g. to
draw an overlay), there is none out of the box — you get `RGameInfo:Tick` and `FEngineLoop::Tick`, both
logic-side. (For accessibility that is fine; we render nothing.)

### 3.1 How `ProcessInternal` becomes the dispatcher

UE3 routes a `UFunction` call through `UObject::ProcessInternal` only when the function is flagged as
having script bytecode. BmSDK exploits this: when you register a redirect it sets
`FunctionFlags |= FUNC_Defined` on the target `UFunction`
(`FrameworkInternal/Redirection/RedirectManager.cs`), forcing UE3 to enter `ProcessInternal` — where
BmSDK's detour is waiting. The detour then matches the function's **full path string** and dispatches.

Four paths are hard-wired as the script lifecycle (`Loader.cs`):

| Script callback | AC trigger path | AK trigger path |
|---|---|---|
| `Script.Main()` | `Engine.GameInfo:InitGame` | same |
| `Script.OnEnterMenu()` | `GFxUI.GFxMoviePlayer:Init` | `RSeqAct_GFxStartFrontend:Activated` |
| `Script.OnEnterGame()` | `BmGame.RPlayerController:ClientReady` | `RPlayerStartInLevel:PreloadLevelsForEntering` |
| `Script.OnTick()` (+ every attached `ScriptComponent.OnTick()`) | `BmGame.RGameInfo:Tick` | same |

The differing trigger paths are internal to the SDK — the **public callback API is identical on both
games**, so mod code doesn't branch. But note that `OnEnterMenu` means something slightly different on
each title (first Scaleform movie vs. the frontend Kismet action), so don't treat it as "a menu opened";
treat it as "the UI layer now exists" and do per-menu detection yourself.

Consequence of the design: **the dispatcher is string-keyed**, not per-function-detoured. A redirect
costs work inside `ProcessInternal` only for functions carrying `FUNC_Defined`, not for every script call
in the game.

## 4. Address resolution — the single biggest fragility

**Everything is hardcoded RVAs added to the module base. There is no signature scanning, no export
lookup, and no version check anywhere in either layer.**

- Native: `src/BmSDK.Host/Framework/offsets.h` is 8 lines and holds exactly one RVA —
  `EngineLoopPreInit` (`0x7DB210` on AC, `0x5B7220` on AK).
- Managed: `src/BmSDK/FrameworkInternal/GameInfo.cs` holds the rest —
  - `GlobalOffsets`: `GNames`, `GObjObjects`, `GError`.
  - `FuncOffsets`: `ProcessEvent`, `ProcessInternal`, `CallFunction`, `FindFunction`,
    `StaticFindObject`, `StaticConstructObject`, `GetDefaultObject`, `SpawnActor`, `LoadPackage`,
    `CollectGarbage`, `AppRealloc`, `AppFree`, `NameInit`, `StringCtor`, plus the hooked six.
  - `MemberOffsets`: `Object__ObjectInternalInteger`, `Object__Outer`, `Object__Name`, `Object__Class`,
    `Class__ClassFlags` (values differ AC vs AK — 8-byte pointers on x64).

Design around it:

- A **game patch = total breakage**, with no graceful failure path. There is no `FileVersionInfo`/hash
  check; `Guard`/`assert` only validate Detours' return codes, never address validity.
- The offsets are pinned to **one specific executable build**. The project's answer is social, not
  technical: Steam/GOG owners apply a **`CompatibilityPatch.zip` that swaps `BatmanAC.exe`** for the
  build the offsets were derived from, and may have to launch `Binaries\Win32\BatmanAC.exe` directly.
- **Our mod must never add hardcoded offsets of its own.** Everything we need comes through the
  generated API or `Game.*` helpers, so when BmSDK is re-pinned to a new build our mod needs no changes.
  This is the playbook's no-magic-numbers rule with teeth — BmSDK already spent the entire
  magic-number budget for this platform.

## 5. UE3 data structures the SDK relies on

The host declares no UE3 types at all. The managed side declares the minimum:

- `GObjects` is read as a `TArray<IntPtr>`; `GNames` is walked as `FNameEntry***` with the classic UE3
  entry shape (`ulong Flags; int Index; FNameEntry* HashNext; fixed char UniName[128]`).
- `FFrame` is an explicit-layout struct (`Node`, `Object`, `Code`, `Locals`, `PreviousFrame`,
  `OutParms`) — this is how a redirect reads the caller's arguments out of the UnrealScript stack frame.
- **There is no `GEngine`/`GWorld` global.** Everything is reached from `StaticFindObject` and the
  object graph (see `Game.*` in [mod authoring](bmsdk-mod-authoring.md)).
- The full C++ UE3 layouts (`UObject 44`, `UField 48`, `UStruct 108`, `UState 180`, `UEnum 60`,
  `UProperty 88`, `FName 8`, `TArray 12`, `#pragma pack(4)`) live only in the **generator**, a separate
  one-shot ASI — see [generator & API](bmsdk-api-and-generator.md).

## 6. Threading model

- **Everything gameplay-facing runs on the engine (main) thread.** The CLR is booted on it, all six
  detours fire on it, and `Script`/`ScriptComponent` callbacks are invoked from inside those detours.
- `Framework/EngineSynchronizationContext` captures that thread's id. `Post()` runs inline when already
  on the main thread, otherwise queues; the queue is drained inside the `FEngineLoop::Tick` detour. So
  `async`/`await` in a mod resumes on the engine thread — safe by construction.
- Off-thread work (the hot-reload `FileSystemWatcher`, thread-pool callbacks) must come back through that
  context before touching game objects. The SDK does this itself for mod activation.
- **Implication for accessibility:** a screen-reader call is a blocking native call on that same thread.
  See the speech-queue rule in [accessibility patterns](bmsdk-accessibility-patterns.md).

## 7. Failure semantics — read this before writing a detour

`Loader.RunGuarded` wraps every detour body. On an unhandled exception it logs and then calls
**`Environment.Exit(1)`** — a deliberate hard kill, justified in a code comment as better than letting
UE3 die with "more-or-less useless DBGHELP messages".

So: **any unhandled exception anywhere in our mod takes the whole game down.** Every accessibility entry
point (tick handler, redirect, component callback) must be individually try/catch'd and degrade locally.
This is the playbook's fault-isolation rule, except here it is not a nicety — it is the difference
between "one menu doesn't read" and "the game closed".

Native-side failures are `assert()` only. `Release|Win32` is built with `_DEBUG` and the debug CRT
(see [install & build](bmsdk-install-and-build.md)), so asserts do survive in the shipped host — but in a
genuinely optimized build a hostfxr failure would fall through silently.

## 8. Logging

`Debug.Log/LogWarning/LogError` -> `Trace.WriteLine` + colored `Console`. **Nothing calls
`AllocConsole`** — there is no console window by default, so output goes to an attached debugger (the
Visual Studio output pane, which is why the docs tell you to launch with F5). Errors additionally render
to an in-game `ErrorOverlay`. An open issue (AC #36) asks for `Debug.Log` to reach the `-log` window.

For our mod: don't rely on a console. Run under the VS debugger, watch it with DebugView (`Trace` is
`OutputDebugString`-visible), or write our own log file from managed code — the last option is the one
that survives a blind-user bug report.

## 9. AC vs AK — the native diff is three lines

| | Arkham City | Arkham Knight |
|---|---|---|
| Bitness | **x86** (`Win32`, vcpkg `x86-windows`, `win-x86` RID) | **x64** (`Win64`, `x64-windows`) |
| `EngineLoopPreInit` RVA | `0x7DB210` | `0x5B7220` |
| PreInit signature | 3-arg `__fastcall` (x86 `__thiscall` shape) | 2-arg `__fastcall` (x64 shape) |
| ASI loader | `dinput8-Win32` | `dinput8-Win64` |
| Online package | `OnlineSubsystemPC` | `OnlineSubsystemSteamworks` |
| Stores supported | Steam / Epic / GOG (patch needed off-Epic) | Steam only |

Everything else — printer, framework, marshalling, attributes — is shared source. Porting the host to
another UE3 title is a one-offset job; all remaining fragility lives in `GameInfo.cs` on the managed side.

> **The x86 fact is the one that decides our architecture.** Arkham City runs 32-bit, and the PRISM
> prebuilt release ships **x64 and arm64 only**. See the transport decision in
> [accessibility patterns](bmsdk-accessibility-patterns.md) §1.
