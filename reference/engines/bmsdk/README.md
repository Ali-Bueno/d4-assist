# BmSDK — Batman: Arkham City & Arkham Knight (Unreal Engine 3, C# scripting)

> Platform knowledge for building an accessibility mod on **BmSDK**, the C# script hook / modding SDK for
> Rocksteady's Arkham City and Arkham Knight. Read this page first, then the doc that matches what you're
> doing.

| Field | Value |
|---|---|
| Games | Batman: Arkham City, Batman: Arkham Knight |
| Engine | **Unreal Engine 3** (UnrealScript VM, Scaleform GFx UI, Wwise audio) |
| Framework | **BmSDK** — *not* UE4SS, *not* BepInEx |
| Injection | Ultimate ASI Loader (`dinput8.dll`) → `BmSDK.Host.asi` → hostfxr → `BmSDK.dll` |
| Mod language | **C# 14 / .NET 10**, self-contained runtime shipped with the SDK |
| Mod format | **loose `.cs` source files**, compiled at runtime by Roslyn — no build, no packaging |
| Iteration | **hot reload** — save the file, the running game picks it up (~500 ms) |
| Bitness | AC **x86**, AK **x64** |
| Speech transport | AK: `[DllImport("prism.dll")]` from the mod. **AC: unresolved — PRISM has no x86 prebuilt** |
| Repos | [BmSDK-AC](https://github.com/Team-BmSDK/BmSDK-AC), [BmSDK-AK](https://github.com/Team-BmSDK/BmSDK-AK) · docs at [bmsdk.dev](https://bmsdk.dev/) |
| Local clones | `D:\code\unreal\bmsdk\` |

## The docs

| Doc | Read it when |
|---|---|
| [architecture](bmsdk-architecture.md) | once, before anything — boot chain, the six hooks, offsets, threading, crash semantics |
| [mod authoring](bmsdk-mod-authoring.md) | writing mod code — `[Script]`, `[Redirect]`, `ScriptComponent<T>`, object model, input, config |
| [generator & API](bmsdk-api-and-generator.md) | looking for a game class or function — includes the **exploration cookbook** |
| [performance](bmsdk-performance.md) | before writing anything that runs per tick |
| [accessibility patterns](bmsdk-accessibility-patterns.md) | the applied doc — UI/menus, speech, cues, navigation, build order |
| [install & build](bmsdk-install-and-build.md) | setting up, shipping, or debugging a compatibility problem |

## Why this platform is unusually pleasant

- **Mods are text files.** A user installs the mod by copying `.cs` files. No compiler on their machine,
  no DLL to sign or version.
- **Hot reload.** Edit → hear the change without restarting the game. Better than BepInEx or UE4SS C++.
- **The full UnrealScript API is exposed as ordinary C#** — 4,472 classes (AC) / 5,467 (AK), with
  IntelliSense, including *private and protected* properties and native functions.
- **The hooking layer is readable C#**, not opaque native code: the native host is ~371 lines and only
  bootstraps the CLR.
- Positions come back as `ref System.Numerics.Vector3` — our navigation math needs no conversion layer.

## …and the three things that will hurt

1. **Arkham City is 32-bit and PRISM ships no x86 build.** This gates the whole speech path on AC; the
   options and recommendation are in [accessibility patterns](bmsdk-accessibility-patterns.md) §1. It is
   the first thing to resolve.
2. **Every engine address is a hardcoded RVA with no version check.** A game patch breaks the SDK
   silently. Our mod must add no offsets of its own so it survives an SDK re-pin unchanged.
3. **An unhandled exception in any hook calls `Environment.Exit(1)`** — it closes the game, it doesn't
   just fail. Every accessibility entry point needs its own try/catch.

Plus two design constraints worth knowing early: there is **no universal "focused UI item" accessor** in
either game (menus must be read per archetype), and **no actor-radius query exists** — UnrealScript's
`AllActors`/`ActorsInRadius` are VM opcodes, not functions, so no SDK can expose them. Navigation is
built from trace fans and the (fully exposed) navmesh API.

## Relationship to the rest of the library

- Screen-reader transport: [screen-reader-integration](../../screen-reader-integration/) —
  and the x86 precedent in [source-l4d2](../source-l4d2/).
- Menus/inventories behaviour: [ui-accessibility](../../ui-accessibility/).
- Navigation styles: [audio-navigation](../../audio-navigation/), especially
  [wall-sonification](../../audio-navigation/wall-sonification/) — the right fit here, since actor
  enumeration is impossible and trace fans are the available primitive.
