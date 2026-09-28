# BmSDK — mod authoring: the C# API you actually write against

> Everything a mod author touches: what a mod *is* on disk, the two hook attributes, script components,
> the object model, input, config, logging, and the iteration loop. Platform internals are in
> [architecture](bmsdk-architecture.md); cost of each construct is in [performance](bmsdk-performance.md).

---

## 1. What a mod is

There are **two** shapes, both plain `.cs` **source files compiled at runtime by Roslyn** — no DLL to
build, no packaging step. Players drop text files into the game folder.

```
<GameDir>\BmGame\
  Scripts\**\*.cs                 -> the implicit "Scripts" mod (Mod.CreateDefault)
  Mods\<name>\mod.toml            -> an explicit, configurable mod
  Mods\<name>\scripts\**\*.cs
  ScriptsDev\ScriptsDev.slnx      -> the Visual Studio project used to edit + debug them
```

`ScriptManager.CompileMod` builds a `CSharpCompilation` per mod: **C# 14**, `Platform.X86` (AC),
`allowUnsafe: true`, referencing `Basic.Reference.Assemblies.Net100` + `BmSDK.dll` + `MoreLinq` +
`Tomlyn`. The result is emitted to an in-memory PE and loaded into a **collectible
`AssemblyLoadContext`** (one per mod).

Ship a mod as: **a folder of `.cs` files** (plus `mod.toml`). That is the whole distribution format.

### 1.1 Minimum script

```csharp
using BmSDK;

namespace ArkhamAccess;

[Script]
public class Boot : Script
{
    public override void Main()        { Debug.Log("Arkham Access loaded"); }   // Engine.GameInfo:InitGame
    public override void OnEnterMenu() { }                                      // GFxUI.GFxMoviePlayer:Init
    public override void OnEnterGame() { }                                      // RPlayerController:ClientReady
    public override void OnTick()      { }                                      // RGameInfo:Tick
    public override void OnKeyDown(Keys key) { }                                // raw WM_KEYDOWN
    public override void OnLoad()      { }   // hot-reload: this build activated
    public override void OnUnload()    { }   // hot-reload: this build about to be discarded
}
```

Rules enforced by the loader:
- `[Script]` on a concrete, non-abstract subclass of `Script`. Discovery is by reflection over the
  compiled assembly; the script's display name defaults from `[CallerFilePath]`.
- The instance is created with `RuntimeHelpers.GetUninitializedObject` and then its constructor is
  invoked — so don't rely on field initializers running in any exotic order; do setup in `Main()`.

### 1.2 Lifecycle in practice (accessibility framing)

| Callback | Fires on | Use it for |
|---|---|---|
| `Main()` | once, `GameInfo:InitGame` | init PRISM, load config, register redirects that must exist early |
| `OnEnterMenu()` | first Scaleform movie init | hook the UI layer, announce "main menu" |
| `OnEnterGame()` | every level load | reset per-level state, re-acquire player pawn, announce region |
| `OnTick()` | every **world tick** (not render frame) | poll player state, drive wall sonification, diff-gate speech |
| `OnKeyDown(Keys)` | raw window message | accessibility hotkeys (repeat last, read objective, toggle radar) |
| `OnLoad`/`OnUnload` | hot reload | release native handles (shut PRISM down cleanly on unload!) |

`Keys` is a verbatim copy of `System.Windows.Forms.Keys`. Input is edge-detected once per tick by
`InputManager` from a 255-slot press/prev-press array, so `OnKeyDown` fires once per physical press.

> `OnUnload` matters more than it looks. Hot reload unloads the ALC; if we `prism_init()`ed and left the
> native DLL holding callbacks into unloaded managed code, the next edit crashes the game. Shut down and
> re-init across reloads.

## 2. Hooking game functions: `[Redirect]`

A **global redirect** replaces a UnrealScript function for *all* instances of a type.

```csharp
class Redirects
{
    [Redirect(typeof(RGameRI), nameof(RGameRI.GetDifficulty))]
    static int GetDifficultyRedirect(RGameRI self)
    {
        if (Game.GetGameInfo().bStoryDLC)
            return (int)RPersistentOptions.DifficultyLevels.Difficulty_Hard;

        return self.GetDifficulty();   // <- "call the original"
    }
}
```

Contract:
- **static** method; return type and parameter types/order must match the UFunction exactly.
- For a non-static target, the instance is passed as an **artificial first parameter** (`self`).
- `AllowSubtypes` (default `true`) controls whether child classes are matched too.

### 2.1 How "call the original" really works (and why it matters)

There is **no orig-delegate**. You call the same wrapper again (`self.GetDifficulty()`), which re-enters
`ProcessInternal`. `RedirectManager.ExecuteRedirector` keeps a `Stack<RedirectCall>` and detects the
re-entry: on re-entry it dispatches the **next** registered redirect (local ones first, then global, in
registration order), and when they are exhausted it calls `RedirectCall.RunOriginal`, which copies the
frame's arguments into a fresh buffer and `ProcessEvent`s the *exact declaring* function, bypassing
overrides.

Practical consequences:
- Semantics are a **prefix chain**: each redirect wraps the next; the last one reaches the real
  implementation. Multiple mods can redirect the same function and they compose.
- **"Cancel the call" = simply never call `self.Method(...)`.** There is no `bool` return / cancel flag.
- If your redirect doesn't chain, **every downstream redirect (from other mods) is silently skipped.**
  For an accessibility mod that is a compatibility landmine: our redirects should be *observers* that
  always chain, never replacements.
- Return values are marshalled back into the frame's `Result` pointer only when the redirect's return
  type isn't `void`.

**Accessibility rule of thumb:** use `[Redirect]` as a read-only tap.

```csharp
[Redirect(typeof(SomeUiClass), nameof(SomeUiClass.SetFocusedItem))]
static void OnFocusChanged(SomeUiClass self, string label)
{
    try { Speech.Say(label, interrupt: true); } catch (Exception e) { Debug.LogError(e.ToString()); }
    self.SetFocusedItem(label);      // ALWAYS chain
}
```

Note the `try/catch`: an escaping exception here kills the process (see architecture §7).

## 3. Per-instance hooking: `ScriptComponent<T>` + `[ComponentRedirect]`

A **script component** is a C#-side object attached to a specific game object. It is *not* a UE3
`ActorComponent`: attachment is a `Dictionary<Type, List<IScriptComponent>>` on the managed
`GameObject` wrapper.

```csharp
[ScriptComponent(AutoAttach = true, AllowSubtypes = false)]
public class PlayerAccess : ScriptComponent<RPawnPlayer>
{
    public override void OnAttach() { Debug.Log("attached to player pawn"); }
    public override void OnTick()   { /* runs once per world tick */ }
    public override void OnDetach() { }

    [ComponentRedirect(nameof(RPawnPlayer.ExperienceAwarded))]
    void OnXp(int xp, int teethXp)
    {
        Owner.ExperienceAwarded(xp, teethXp);   // Owner == the RPawnPlayer, no artificial self
    }
}
```

- Generic argument = the actor type it applies to; `ScriptComponent` (non-generic) means `Actor`.
- Only **one instance of a given component type per object**.
- `[ComponentRedirect]` is an **instance** method, no `self` parameter, and only fires for objects that
  actually carry this component — that is the whole point: a *local* redirect.
- Attach manually with `actor.AttachScriptComponent<T>()` / `HasScriptComponent<T>()` /
  `DetachScriptComponent<T>()`, or automatically with `AutoAttach = true`.

> **`AutoAttach` performance warning is real and the official docs say it too**: auto-attach hooks
> `AddObject`/`ConditionalPostLoad` and walks the new object's type hierarchy on **every object
> creation**. Targeting a common type (`Actor`, `GameObject`) will hurt. Target the narrowest type you
> can — for us `RPawnPlayer`, `RPlayerController`, specific UI classes.

## 4. The object model

- Every game object is a thin wrapper: `public IntPtr Ptr { get; internal set; }` plus
  `bool IsValid => Ptr != IntPtr.Zero`. **No finalizers.**
- Lifetime follows UE3's GC, not .NET's: the wrapper is created in the `AddObject` detour and
  **zeroed** in the `ConditionalDestroy` detour. A stale reference you cached becomes `IsValid == false`
  — and most SDK code does *not* defensively re-check, so **we must**: cache game objects only across a
  frame, or re-validate before use.
- Wrapper identity is cached globally (`Dictionary<IntPtr, GameObject>`), and the concrete managed type
  is resolved by reading the native `UClass` chain and mapping the class path through
  `StaticInit.GetManagedTypeForClassPath`. So a wrapper already *is* its most-derived known type and
  casting is ordinary C#: `worldInfo.Game as RGameInfo`.
- Finding objects:
  - `Game.FindObject<T>(path)` — exact `StaticFindObject`, cheap.
  - `Game.FindObjects<T>()` — served from `FindObjectsCache`, a dictionary maintained *incrementally*
    as objects register/unregister. Fast, but its own XML doc warns: *"Can be slow, in cases where many
    objects of the given type exist. Be careful when using this with common types like `GameObject` or
    `Actor`."*
  - `GameObject.FindObjectsSlow<T>()` — `[Obsolete]`, walks the whole raw `GObjects` array. Never.
- `StaticClass()` and `DefaultObject` (the CDO) exist on every generated class — useful for reading
  class defaults without an instance.

### Marshalling types

| Type | Shape | Cost / trap |
|---|---|---|
| `FName` | 8-byte `{Index, Number}` | `ToString()` walks the native `GNames` hash table and builds a new string **every call, no cache** — never in a tick loop |
| `FString` | native `TArray<TCHAR>` layout, `IDisposable` | you must dispose / `AppFree` or you leak native memory |
| `TArray<T>` | `{Data, Num, Max}`, `unsafe class`, `IList<T>` | can own memory or wrap a native pointer; each indexer access marshals |
| `TMap<K,V>` | **placeholder** | generator emits `TMap<object,object> /* TODO */` — non-functional, don't design around it |
| structs (`Vector`, `Rotator`…) | `[StructLayout(Explicit)] record struct` | `Actor.Location` is exposed as **`ref System.Numerics.Vector3`** — direct memory, zero copy |
| delegates | `System.IntPtr` | UnrealScript delegates are not modelled; you get a raw pointer |

## 5. Engine entry points (`Framework/Game.cs`)

```csharp
Game.GetEngine()                  // GameEngine
Game.GetWorldInfo()               // WorldInfo  (Game.GetWorldInfo().Game as RGameInfo, etc.)
Game.GetGameInfo()                // RGameInfo
Game.GetGameRI()                  // RGameRI    (replication info)
Game.GetPersistentData()
Game.GetGameViewportClient()      // RGFxGameViewportClient  <- the Scaleform viewport
Game.GetConsole()
Game.GetPlayerController(int controllerId = 0)
Game.GetPlayerPawn()              // == playerController.CombatPawn  (Batman)
Game.GetCheatManager()
Game.GetDeltaTime()               // worldInfo.DeltaSeconds
Game.SpawnActor(...)
Game.LoadPackage(filename)
Game.FindObject<T>(path) / Game.FindObjects<T>()
```

There is **no event API** beyond `Script.OnTick()` / `IScriptComponent.OnTick()`, both fired from
`BmGame.RGameInfo:Tick`. That is once per **world tick**, which is our accessibility polling budget.

## 6. Config, preload and logging

- **`mod.toml`** per mod, parsed with Tomlyn, exposed as `Mod.Config` (a `TomlTable`). A `[preload]`
  section supports `packages[]` (force-load `.upk`s before world load) and `keep_alive[]` (root objects
  via `AddToRoot` so UE3's GC won't collect them), after which a full native GC pass is forced by
  `PreloadManager`.
  - For us: `keep_alive` is the sanctioned way to hold on to a sound cue or UI object we need later.
- **`Debug.Log/LogWarning/LogError`** auto-prefix a sender derived from an internal stack (per script /
  per component) or from `[CallerFilePath]`. Output goes to `Trace` + an in-game `ErrorOverlay`; there is
  no console window (architecture §8).
- **No console-command system** and **no coroutine/timer API**. You get raw `System.Threading` /
  `Task` via global usings, plus `EngineSynchronizationContext` to marshal continuations back to the
  engine thread. Build our own tiny scheduler on `OnTick` + `Game.GetDeltaTime()`.
- `global using System.Runtime.InteropServices;` is applied assembly-wide, so **`[DllImport]` works in a
  mod script with no extra ceremony** — this is how we will reach `prism.dll`.

## 7. The iteration loop (why this SDK is pleasant)

1. Edit a `.cs` under `BmGame\Scripts\` (or `Mods\<name>\scripts\`).
2. A `FileSystemWatcher` (500 ms debounce) sees it, `ReloadMod` runs: old collectible ALC `Unload()` +
   `GC.Collect()`, recompile, re-activate — **the game keeps running**.
3. Compile errors surface through `Debug.LogError` / the error overlay instead of crashing.

That is a genuinely better loop than BepInEx (restart) or UE4SS Lua (restart for C++ mods). Practical
advice for the accessibility mod:

- Keep the PRISM handle in a static that `OnUnload` tears down and `OnLoad` re-creates.
- Keep speech state (last-spoken cache, diff-gates) in statics that reset on reload, so a reload gives a
  clean slate instead of stale suppression.
- Develop with the game sitting in the target menu; edit → hear the change in ~1 second.

## 8. Debugging

Open `<GameDir>\BmGame\ScriptsDev\ScriptsDev.slnx` in Visual Studio and press **F5** — it launches the
game with scripts loaded and attaches, so breakpoints and `Debug.Log` output work directly. Target is
`net10.0-windows7.0`, self-contained, C# 14, implicit usings, nullable, unsafe allowed, with
`BmSDK.Framework` and `Tomlyn.Model` auto-imported. The project globs `Scripts/**/*.cs` and
`Mods/**/*.cs`.
