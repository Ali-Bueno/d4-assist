# BmSDK — the generated API and how to explore it

> The SDK's game-facing surface is machine-generated from the running game. This doc explains where it
> comes from, what the emitted C# looks like, and — most importantly — **how to find the class or
> function you need** among ~4,500–5,500 generated types. That exploration cookbook (§5) is the part
> you will reread every session.

---

## 1. Where the API comes from

`BmSDK.Generator` is a **separate native ASI plugin, injected into the actually running game**. There is
no offline dump, no `.upk` parser, no JSON intermediate. It:

1. Resolves `GObjects` / `GNames` from hardcoded RVAs (`src/BmSDK.Generator/Engine/GameOffsets.h`).
2. Waits for you to press **`P`** with the game window focused, then suspends the main thread.
3. **Force-loads every `.upk` in `BmGame\CookedPCConsole`** via the engine's own `LoadPackage`, skipping
   asset-only packages by name regex (`Anim_*`, `Bio_*`, `ShaderCache`, …). This is what makes the dump
   complete — classes living in packages that aren't resident at boot still get reflected.
4. Walks `GObjects`, keeps every object whose `Class == UClass::StaticClass()` and that isn't a CDO.
5. Wipes `src/BmSDK/Generated/` and writes one `<ManagedName>.g.cs` per class into
   `Generated/<Package>/`, plus a single `StaticInit.g.cs` holding the class-path → managed-`Type` map.
6. `exit(0)` — the game closes itself. One-shot by design.

Consequences:

- **You need the game installed and runnable to regenerate the SDK.** There is no headless path.
- `Generated/` is **not** in the repo (it is large and deterministic) — a fresh clone must run the
  generator once before `BmSDK` will compile. See [install & build](bmsdk-install-and-build.md).
- The generator carries its **own** hardcoded offsets, duplicated from the runtime's. Both are pinned to
  one exact executable build.
- Since the dump is of the *live* engine, the generated API is exactly as faithful as UE3's own
  reflection — no guessing, no heuristics.

## 2. What the emitted C# looks like

### Class

```csharp
namespace BmSDK.Engine;

public partial class Pawn : BmSDK.Engine.Actor, BmSDK.Engine.Interface_Speaker, BmSDK.IGameObject
{
    static BmSDK.Class s_staticClass = null;
    public static BmSDK.Class StaticClass() { /* StaticFindObjectChecked<Class>(…, "Engine.Pawn", …) */ }
    public static Pawn DefaultObject => (Pawn)StaticClass().DefaultObject;
    internal Pawn() { }
    protected Pawn(nint ptr) : base(ptr) { }
    // + AttachScriptComponent / HasScriptComponent / GetScriptComponent(s) / DetachScriptComponent
}
```

Non-abstract classes also get a construction path: Actors via `SpawnActorInternal(...)`, plain objects
via `ConstructObjectInternal(...)`.

### Property — a direct offset read (this is the cheap path)

```csharp
public unsafe float TimeOut
{
    get { return MarshalUtil.ToManaged<float>(Ptr + 16); }
    set { MarshalUtil.ToUnmanaged(value, Ptr + 16); }
}
```

Bools are UE3 bitmask-packed and get mask arithmetic. Struct-valued properties are emitted as
**`ref` returns over raw memory**, which is excellent for us:

```csharp
public unsafe ref System.Numerics.Vector3 Location => ref MarshalUtil.AsRef<Vector3>(Ptr + 72);
```

`System.Numerics.Vector3` for positions means our navigation math needs no conversion layer at all.

### Function — a `ProcessEvent` call with a stack-allocated parameter block (this is the expensive path)

```csharp
public unsafe override void OnSetVelocity(SeqAct_SetVelocity Action)
{
    var funcManaged = GameObject.StaticFindObjectChecked<Function>(
        Function.StaticClass(), null, "Engine.Pawn.OnSetVelocity", true);   // <-- string lookup, EVERY call
    byte* paramsPtr = stackalloc byte[4];
    MarshalUtil.ToUnmanaged(Action, paramsPtr + 0);
    GameFunctions.ProcessEvent(Ptr, funcManaged.Ptr, (nint)paramsPtr, 0);
}
```

In-params are written at their property offsets in the block; return values and out-params are read back
out of the same block after the call. For **native** functions the generator temporarily clears
`FUNC_Native` / zeroes `iNative` around the call and restores them afterwards — otherwise `ProcessEvent`
would not route a native function through script execution. So native functions *are* callable, at the
cost of a flag mutation per call.

> See [performance](bmsdk-performance.md) — the per-call string lookup is the single most important
> cost fact in this SDK.

### Struct, enum, delegate

```csharp
[StructLayout(LayoutKind.Explicit, Size = 24)]
public partial record struct FEventUploadConfig { /* props use fixed(void* p = &this) to get a Ptr */ }

public enum EDoubleClickDir : byte { DCLICK_None = 0, DCLICK_Left = 1, /* … */ }
```

- Enums are **always** emitted `: byte`, regardless of UE3's underlying storage.
- **Delegates are not modelled**: `Core.DelegateProperty` maps to a bare `System.IntPtr`. If a game
  system exposes a hook only via a script delegate, we cannot bind it directly through the generated API
  — use a `[Redirect]` on the function that invokes it instead.

## 3. Namespaces and naming

- UE3 package `Core` → the **root namespace `BmSDK`**. Every other package `X` → `BmSDK.X`, and the
  folder `Generated/X/` matches.
- Packages present (both games): `Core`, `Engine`, `BmGame`, `BmScript`, `GFxUI`, `AkAudio`, `IpDrv`,
  `WinDrv`, plus `OnlineSubsystemPC` (AC) / `OnlineSubsystemSteamworks` (AK).
- **Names are the UnrealScript names, essentially verbatim.** Game classes keep their script prefix:
  `RPawnPlayer`, `RPlayerController`, `RGFxMovie`, `RSeqAct_*`, `RHUDPrompt`. The only mangling rules:

| Rule | Example |
|---|---|
| `Core.Object` → `GameObject`, `Core.System` → `_System`, `Engine.Console` → `_Console` | BCL collision avoidance |
| UScriptStructs get an `F` prefix | `SubtitleCue` → `FSubtitleCue` |
| A field named like its outer gets a leading `_` | — |
| Overridden UFunctions reuse the **base** declaration's managed name | overrides line up across the hierarchy |
| Duplicate names within a struct/function/enum get a `_N` suffix | — |

- Scale: **AC = 4,472** classes/interfaces (43 interfaces), **AK = 5,467** (66 interfaces).
- The runtime lookup table, if you ever need it programmatically, is
  `StaticInit.g.cs`: `_classPathToManagedTypeMap["Package.Name"] = typeof(BmSDK.Package.Name)`.

## 4. AC vs AK — source-compatible, never binary-compatible

Both repos share the identical generator, printer, framework, attributes and marshalling code. Only the
`Generated/` tree and the offsets differ.

- **Same class, same C# surface, different offsets.** `Engine.Actor.Location` is `Ptr + 72` on AC and
  `Ptr + 196` on AK (`Actor` itself is 428 bytes vs 668 bytes).
- Package sets differ: ~1,200 `Engine` classes in common, 63 AC-only (older AI system: `AITree`,
  `AICommandNodeBase`), 318 AK-only (`ActorFactoryApex*`, `AkAction`, vehicle/physics factories).
- **Verdict:** one C# *source* tree can target both games — shared `.cs` files linked into two projects,
  each referencing its own game's `BmSDK`, with `#if AC` / `#if AK` for the genuine API deltas. A
  compiled assembly is **never** interchangeable.

For the accessibility mod this is the right shape anyway: our engine-adapter layer (`Speak`, `Raycast`,
`PlayerPosition`, `ReadFocusedItem`) is where the per-game branches live, and everything above it is
shared logic.

## 5. Exploration cookbook — how to find what you need

This is the practical workflow. Assume the SDK checkout is at `D:\code\unreal\bmsdk\BmSDK-AC` (or `-AK`)
and the generated tree at `src\BmSDK\Generated\`.

**1. Folder = package = namespace.** Guess the package, then look by filename:

```
find src/BmSDK/Generated -iname "RPawnPlayer.g.cs"
grep -rn "class RGFxMovieUI" src/BmSDK/Generated/BmGame/
```

Game-specific gameplay/UI classes are in `BmGame/` and almost always start with `R`.

**2. Grep the UnrealScript name, not a mangled one** — mangling is minimal (§3).

**3. The UnrealScript metadata is preserved as `///` doc comments, so it is greppable.** Every emitted
member carries its original kind and name:

```
/// Class: Pawn                     /// ABSTRACT Class: …      /// (flags = 142609079)
/// FloatProperty: TimeOut          /// Function: OnSetVelocity
/// Enum: ETravelType               /// InlineArray{Type}: Name
```

So `grep -rn "/// Function: " src/BmSDK/Generated/GFxUI/` enumerates every callable UI function, and the
raw `ClassFlags`/`PropertiesSize` are visible without touching the game.

**4. Function bodies embed the full UnrealScript path string** (`"Engine.Pawn.OnSetVelocity"`), so you
can grep a path to correlate C# ⇄ UnrealScript in either direction — and those same strings are what the
`ProcessInternal` dispatcher matches, which makes them the exact keys for `[Redirect]`.

**5. Read the class defaults without running anything**: every class has `StaticClass()` and
`DefaultObject` (the CDO).

**6. Check for a hand-written helper first.** `src/BmSDK/Classes/**/*.cs` (no `.g.` in the name) are
manually authored `partial` extensions of generated types — e.g. `Classes/Core/GameObject.Methods.cs`,
`Classes/Engine/Actor.cs`. Ergonomic wrappers may already exist.

**7. Never edit generated files.** `Generated/` is wiped wholesale on every regeneration. Extend through
`partial` classes in your own mod, or via `[Redirect]` / `ScriptComponent`.

## 6. Fidelity — what is and isn't exposed

**Exposed, perhaps surprisingly:**

- **Private and protected UProperties are all emitted `public`.** The generator has no access-level
  filtering at all. Everything UE3 reflects, you can read.
- **Private UFunctions too** — privacy only suppresses the `override`/`virtual` keyword, not the member.
- **Native functions are callable** through the `FUNC_Native` flag-flip trick described in §2.

**Not exposed:**

| Thing | Why | What to do instead |
|---|---|---|
| `FUNC_Operator` / `FUNC_Iterator` UFunctions | no sane C# call-site mapping | — |
| `foreach` iterators: `AllActors`, `CollidingActors`, `VisibleCollidingActors`, `ActorsInRadius`, `EncroachingActors` | these are **UnrealScript VM opcodes, not `UFunction`s** — no generated SDK can ever expose them | trace fans (`Trace`/`FastTrace`), `NavigationHandle.GetAllPolyCentersWithinBounds`, or `Game.FindObjects<T>()` |
| `TMap` | emitted as `TMap<object,object> /* TODO */`, non-functional (26 occurrences in AC, 36 in AK) | avoid designing around any map-typed property |
| script delegates | mapped to bare `IntPtr` | `[Redirect]` the invoking function |
| members of `CLASS_Intrinsic` classes (`Class`, `Property`, `Field`…) | their data lives in hand-written C++, not UnrealScript declarations | the hand-written `Classes/Core/*.cs` wrappers |
| UnrealScript `exec` qualifier | the generator preserves names, not qualifiers | `PlayerController.ConsoleCommand("…")` |

The "unresolvable property type" fallback (`UNKNOWN`) exists in the generator but is **never actually hit**
in either game's output — every property type maps cleanly except the delegate/map cases above.
