# BmSDK — performance model and the rules that follow from it

> An accessibility mod runs work every tick, forever, on the engine thread. This doc is the cost model:
> what is cheap, what is secretly expensive, and the rules to write against. Sources are the SDK's own
> code and its own XML-doc warnings.

---

## 1. The cost table

| Operation | Cost | Why |
|---|---|---|
| **Generated property get/set** | **cheap** — a blit at a fixed offset | `MarshalUtil.ToManaged<T>(Ptr + N)`; struct props are `ref` returns over raw memory (zero copy) |
| **Generated method call** | **expensive** | `StaticFindObjectChecked<Function>(…, "Pkg.Class.Func", …)` — **a string-path lookup on every single call** — plus `stackalloc`, param marshalling, a `FunctionFlags`/`iNative` mutation for native funcs, and `ProcessEvent` |
| `FName.ToString()` | expensive | walks the native `GNames` hash table and allocates a fresh string **every call, no cache** |
| `FString` | expensive + leaky | native `TArray<TCHAR>`; `IDisposable` — undisposed instances leak native memory |
| `Game.FindObject<T>(path)` | cheap-ish | single `StaticFindObject` |
| `Game.FindObjects<T>()` | moderate | served from an incrementally maintained `FindObjectsCache`, but its own doc warns: *"Can be slow… Be careful when using this with common types like `GameObject` or `Actor`"* |
| `GameObject.FindObjectsSlow<T>()` | **never** | `[Obsolete]`; full O(n) scan of the entire `GObjects` array per call |
| `Script.OnTick()` | one dispatch per world tick | fired from `RGameInfo:Tick` via the `ProcessInternal` detour |
| `ScriptComponent.OnTick()` | scales with **total** live components | `Loader` iterates a flat global `GameObject.AllScriptComponents.ToArray()` — cost is global, not per-actor, and the array is **copied every tick** |
| `[Redirect]` on a function | pays inside `ProcessInternal` only for functions flagged `FUNC_Defined` | but it forces that function out of its fast path forever |
| `[ScriptComponent(AutoAttach = true)]` | pays on **every object creation** in the game | hooks `AddObject`/`ConditionalPostLoad` and walks the new object's type hierarchy |
| `Trace` / `FastTrace` | one engine call each; **no batch API exists** | an N-ray radar is literally N calls |

Things the SDK already does right, so you don't have to: reflection for redirects and components is
cached per type at registration (`ScriptComponentManager.CacheRedirectors`, `LocalRedirectManager`'s
cached `MethodInvoker`), not per invocation.

## 2. The rules

**R1 — In per-tick code, read properties; do not call generated methods.**
This is the big one. A property read is a memory fetch; a method call is a string lookup plus
`ProcessEvent`. Prefer `pawn.Location`, `pawn.Health`, `controller.KismetState`, `pawn.bGliding` over any
`Get…()` function that returns the same thing.

**R2 — When a method is unavoidable per tick, hoist the `Function` lookup.**
The generated wrapper re-resolves by path every call, so either cache the `Function` object once
(`Game.FindObject<Function>("Engine.Actor.FastTrace")`) and `ProcessEvent` it yourself, or — simpler —
call it at a reduced rate (§3).

**R3 — Never call `FName.ToString()` or build an `FString` in a hot path.**
Resolve names once at load/level-start and cache the managed `string`. Dispose every `FString` you
create. A per-tick `ToString()` allocates and hash-walks forever.

**R4 — Target `AutoAttach` at the narrowest possible type.**
`ScriptComponent<RPawnPlayer>` or `<RPlayerController>` — never `<Actor>` or `<GameObject>`. The official
docs say it plainly: auto-attach on a very common type "can kill performance".

**R5 — Prefer one `Script.OnTick()` over many script components.**
Because component ticking iterates a *global* list and copies it each tick, ten components on ten actors
cost more than one central tick that reads those ten actors. Use components for their *scoping* value
(local redirects, per-actor state), not as a ticking mechanism.

**R6 — Budget your rays.** There is no multi-trace call. A wall-sonification bed at, say, 8 directions is
8 `FastTrace` calls per update; a polar scan at 36 rays is 36. `FastTrace` (bool only) is markedly
cheaper than `Trace` (which fills out hit location, normal and `FTraceHitInfo`) — use `FastTrace` for
"is something within X" and reserve `Trace` for "what and how far".

**R7 — Cache game-object wrappers for at most one frame, and check `IsValid`.**
Wrappers are pointer holders with no finalizer; UE3's GC zeroes them out from under you via the
`ConditionalDestroy` detour, and most SDK code doesn't re-check. Re-acquire the player pawn on
`OnEnterGame()` and validate before use.

**R8 — Catch every exception at every entry point.**
Not strictly performance, but the same discipline: an escaping exception in a detour calls
`Environment.Exit(1)` and closes the game.

**R9 — Never block the engine thread.**
Everything runs on it. A blocking screen-reader call inside `OnTick` becomes frame time. Queue speech and
flush it, or push it to a worker and marshal results back through `EngineSynchronizationContext`.

## 3. A tick budget that works

The accessibility workload splits cleanly into three rates. Drive them all from `Script.OnTick()` using
`Game.GetDeltaTime()` as the clock — do not use wall-clock timers, because they keep running while the
game is paused.

| Rate | What runs there | Notes |
|---|---|---|
| **Every tick** | player `Location` / `Rotation` / `KismetState` / `bGliding` property reads; diff-gating | pure property reads, effectively free |
| **~10–20 Hz** | wall-sonification ray fan, proximity cues, HUD-prompt polling | the only place `FastTrace` is allowed |
| **On change / on demand** | speech, menu reads, objective announcements, expensive `Get…()` calls | driven by diff-gates and hotkeys, never polled |

Diff-gating (playbook §9: *detect real changes before announcing*) is also the primary performance
mechanism here — it converts an expensive announcement path into a comparison of cached values.

## 4. Hot-reload cost (development only)

Each save triggers: `FileSystemWatcher` (500 ms debounce) → collectible `AssemblyLoadContext.Unload()` →
forced `GC.Collect()` → full Roslyn recompile of that mod → re-activation. It's a visible hitch, not a
crash, and it's a development-time cost only. Two practical notes:

- Keep the mod split into several files but one *mod* — reload granularity is per-mod, not per-file.
- Release native resources (the PRISM handle) in `OnUnload` and re-acquire in `OnLoad`, or reloads will
  leak handles and eventually misbehave.

## 5. Deployment footprint

The SDK publishes **self-contained .NET 10** into `Binaries\Win32|Win64\sdk\` — roughly 70 MB of runtime
files. No machine-wide .NET install is needed, but a **stale `sdk\` folder from an older release breaks
hostfxr discovery**, so upgrades must replace the folder rather than merge into it. Our mod adds only
`.cs` text files plus one native speech DLL, so it costs effectively nothing on top.
