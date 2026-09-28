# UE4SS Lua API reference — globals, hooks & console

Quick-lookup reference for the **global** UE4SS Lua API surface: object discovery, async/threading,
input, hooks, and the console. Companion docs:
[discovery-tools](ue4ss-discovery-tools.md) (dumpers + Live View),
[settings-reference](ue4ss-settings-reference.md), [accessibility-patterns](accessibility-patterns.md),
[compatibility-and-crashes](ue4ss-compatibility-and-crashes.md).

**Not in this doc:** the class/property reflection API (`UStruct:ForEachProperty`,
`UStruct:ForEachFunction`, `UClass:GetCDO`, `TArray`, `FName`/`FString`/`FText`, `UEnum`, full
`EObjectFlags`/`PropertyTypes` enumerations, etc.) — that lives in
[`ue4ss-reflection-cookbook.md`](ue4ss-reflection-cookbook.md). The **cost model** behind every
recommendation here (measured numbers, caching idioms, the frame budget, how to measure) lives in
[`ue4ss-performance.md`](ue4ss-performance.md).

> **Doc caveat:** the official API list is "mostly complete" but not fully updated since 2.5.2 — verify
> against your build. Lua runtime is **PUC Lua 5.4** (real Lua), so `require` of a native Lua C module
> works (the PRISM/Tolk bridge path). Where a signature says a thread/execution context is **not
> documented**, that's a gap in the upstream docs, not an omission here — verify empirically before
> relying on it.

---

## 1. Object discovery & iteration

```
FindObject(className: string|FName|nil, objectName: string|FName|nil, requiredFlags: EObjectFlags, bannedFlags: EObjectFlags) -> UObject
FindObject(class: UClass, outer: UObject, objectPath: string, exactMatch: bool) -> UObject
```
Two overloads:
- **Overload 1** — by **short class name** and/or **short object name** (at least one of the two must be
  given). `requiredFlags`/`bannedFlags` are `EObjectFlags`, OR-able with `|`. This is a **name lookup**,
  not a linear scan — cheaper than `ForEachUObject`/`FindAllOf`.

> **Parameter-order trap.** The official docs' prose tables have these two **swapped** (they say
> param 3 = "cannot have", param 4 = "must have"). The real order — verified against the overload
> strings in `LuaMod.cpp:3271-3276,3439-3440` and the shipped `Mods/shared/Types.lua:743,763` — is
> **param 3 = required, param 4 = banned**. Same trap in `FindObjects` below.
- **Overload 2** — mirrors UE's native `FindObject`, relative to an `outer` (or all packages if `nil`),
  by path; `exactMatch` controls whether subclasses are accepted.
```lua
FindObject("SceneComponent", "TransformComponent0")
FindObject("FirstPersonCharacter_C", "FirstPersonCharacter_C_0", EObjectFlags.RF_NoFlags, EObjectFlags.RF_ClassDefaultObject)
```

```
FindObjects(count: integer, className, objectName, requiredFlags, bannedFlags, exactMatch: bool) -> table<UObject>
```
`count = 0` or `nil` → **all** matches (expensive). Includes subclasses by default; includes CDOs unless
banned via flags. Prefer this over `FindAllOf` when you only need up to N results — and let the flags do
the filtering instead of walking the results in Lua calling `GetFullName()`:
```lua
FindObjects(4, "UMG_MainMenu_C", nil, EObjectFlags.RF_NoFlags, EObjectFlags.RF_ClassDefaultObject, true)
```
Whether it genuinely early-outs at `count` is **not confirmed** (that loop lives in UE4SS's private
`UEPseudo` submodule) — treat it as "no worse than `FindAllOf`, probably better", not as free.

```
StaticFindObject(fullName: string) -> UObject|UClass|AActor
StaticFindObject(objectClass: UClass, outer: UObject, name: string, requireExactMatch: bool) -> ...
```
Looks up by **full path**, no type prefix. The recommended finder for non-instances (`UClass`,
`UFunction`). Only returns what's **already resident in memory** — it does not load anything.
```lua
StaticFindObject("/Script/Engine.Character")
```

```
FindFirstOf(className: string) -> UObject|UClass|AActor
```
First **non-default** instance of the class (short name). Never returns a CDO or a non-instance.

```
FindAllOf(className: string) -> nil | table<number, UObject>
```
**All** non-default instances, including subclasses. Returns `nil` if none exist. No flags parameter.

```
ForEachUObject(callback)   -- callback(object: UObject, chunkIndex: integer, objectIndex: integer)
```
Walks **every live UObject** in the `GUObjectArray` (a chunked array) — the most expensive finder of all.
The official docs literally warn: *"This will take quite a while to finish executing due to all of the
'print' calls"*. Explicit-dump use only, never in a refresh loop.

```
NotifyOnNewObject(className: string, callback) -> UObject   -- callback(constructedObject: UObject)
```
`className` is the **full path**, no type prefix (e.g. `"/Script/Engine.Actor"`). This is a **hook** on
`StaticConstructObject_Internal`: it fires when an instance of that class **or a derived class** is
constructed. It doesn't scan anything — near-zero cost at rest, making it the cheap way to watch a class
continuously. The docs warn that registering the same `className` from multiple mods duplicates work and
can degrade performance. Execution thread: **not documented**.

```
StaticConstructObject(uclass, outer, name?, flags?, internalFlags?, bCopyTransientsFromClassDefaults?, bAssumeTemplateOwnershipOfObject?, templateObject?, param9/10/11?) -> UObject
```
**Creates** an instance (mirrors `StaticConstructObject_Internal`). Always check `:IsValid()` on the
result.

```
LoadAsset(assetPath: string)
```
The docs are explicit: *"It must only be called from within the game thread. For example, from within a
UFunction hook or `RegisterConsoleCommandHandler` callback."*

```
IterateGameDirectories() -> table
```
Filesystem tree of game directories, navigable with dot notation (`GameDirectory.Binaries.Win64`), plus
`.__name` / `.__absolute_path` / `.__files` keys. Filesystem only — not a UObject API.

### Performance (READ THIS)
- **`FindAllOf`/`FindObjects` (unbounded)/`ForEachUObject` scan the whole `GUObjectArray` every call**
  (tens of thousands of objects). Calling one every tick per adapter stalls the game thread → input lag.
  **Cache**: find a container once, keep the ref (pooled UI widgets persist for the session — closing
  only collapses them), and re-scan only when the cached ref goes invalid. Cache list results too,
  refreshing every few seconds. See the caching pattern in
  [accessibility-patterns](accessibility-patterns.md).
- `bUseUObjectArrayCache=true` makes UE4SS keep its own fast object cache (backs these finders) — but the
  Lua call still materializes/filters a table each time; caching in your mod is still needed.
- `FindObject`/`StaticFindObject` are **name/path lookups**, not scans — much cheaper than
  `FindAllOf`/`ForEachUObject`, but still not free enough to call every tick without a reason.
- `FindAllOf` is a full sweep every call: **on-demand only** (a key/command), **never per frame**.
- **Every `Object.PropName` access is a fresh `FindProperty(name)`** — nothing memoises it
  (`LuaUObject.hpp:817-833`). A 4-deep property chain read per tick is 4 name lookups per tick: hoist
  the deepest stable object into a local. The apparent `Reflection():GetProperty()` +
  `ContainerPtrToValuePtr()` fast path does **not** work from Lua (opaque LightUserdata) — see
  [ue4ss-performance.md](ue4ss-performance.md) §2.
- Prefer **event-driven** acquisition over polling — but where that worked, it meant
  `RegisterBeginPlayPostHook`, **not** `NotifyOnNewObject`, which both games had to drop outright (see
  the sharp edge below). Only Sparking ZERO actually ships BeginPlay-hook acquisition; Kakarot uses a
  persistent-root directory plus budgeted scans and has **not** tested the hook (its ini ships with
  BeginPlay hooking off). Measured per-game scan costs and the full decision ladder:
  [ue4ss-mod-architecture.md](ue4ss-mod-architecture.md).
- `UEHelpers`' `GetPlayerController` / `GetPlayer` / `GetWorld` and friends are **not cached** — they
  re-run `FindAllOf` on every call. Wrap them. See [ue4ss-performance.md](ue4ss-performance.md) §4.3.

---

## 2. Async, threading & input

```
ExecuteInGameThread(callback)
```
Queues `callback` onto the **game thread**. On **3.0.x it dispatches through `ProcessEvent`** (fires
many times per frame); newer builds add an explicit `EGameThreadMethod` argument and the ini key
`DefaultExecuteInGameThreadMethod` (`EngineTick` = once per frame, predictable — the one to want;
`ProcessEvent` = lower latency, more firings). Beware: the docs' prose calls that key
`DefaultGameThreadExecutionMethod`, which does not exist — see the settings reference.
**Required** for any UObject read/write:
UObject APIs are not thread-safe off the game thread and touching one there is undefined
behavior/crash. Timing is non-deterministic — the docs describe it as "as soon as the game has time to
execute it."

```
ExecuteWithDelay(delayMs: integer, callback)
```
Runs `callback` once, after `delayMs`. Execution thread: **not documented** — if the callback touches
UObjects, nest it inside `ExecuteInGameThread`.

```
ExecuteAsync(callback)
```
Fire-and-forget, no artificial delay. Execution thread: **not documented**.

```
LoopAsync(ms: integer, callback)
```
Repeating loop; `callback` returns `true` to stop, `false` to continue. Useful as a "tick" decoupled from
the render frame. Runs on a **worker thread** — do the actual UObject work inside a nested
`ExecuteInGameThread`, and **guard against backlog** (only queue the next step once the previous one
finished) so a busy game thread doesn't pile up late steps.

```
RegisterKeyBind(key: table, callback)
RegisterKeyBind(key: integer, modifierKeys: table, callback)
```
Only fires if the game or the debug console has focus. Uses the global `Key` and `ModifierKey`
(`SHIFT`, `CONTROL`, `ALT`) tables.

```
IsKeyBindRegistered(key) -> boolean
IsKeyBindRegistered(key, modifierKeys) -> boolean
```
Check before registering — useful on hot-reload so you don't stack duplicate binds (hooks aren't
auto-cleared on reload either, see §5).

```
print(string)
```
Does **not** append `\n` and does **not** format — use `string.format` first.

---

## 3. Hooks & event-driven detection

```
RegisterHook(functionPath: string, preCallback, postCallback?) -> preId, postId
```
`functionPath` is the **full path of a UFunction**; the type prefix doesn't matter (`/Script/...` and
`/Game/...` both work). Fires for **all instances** of that class and its subclasses (inheritance
respected) — `self` in the callback is the specific instance.
```lua
local pre, post = RegisterHook("/Script/Engine.Actor:BeginPlay", function(self, ...)
    print("BeginPlay: "..self:GetFullName().."\n")
end)
```
- **Callback signature:** `self` first, then one argument per UFunction parameter.
- **Key limitation (verbatim from the docs):** *"Any UFunction that you attempt to register with
  RegisterHook must already exist in memory when you register it."* You cannot hook a class that hasn't
  loaded yet — wait until it exists (`NotifyOnNewObject`, or one of the lifecycle hooks below).
- **Parameter convention, repeated throughout the API:** *"Parameters (except strings & bools &
  FOutputDevice) must be retrieved via `Param:Get()` and set via `Param:Set()`."* This `:Get()/:Set()`
  rule is for **hook params only** — normal `obj.Prop` access on a UObject elsewhere doesn't need it.
- **Pre vs. post:** one call registers both; return a value to override, `nil` to pass through. Whether
  the post-hook's `ReturnValue` reflects the pre-hook's override is **not explicitly documented** — treat
  as unconfirmed, verify empirically.
- **Crash gotcha:** on some games UE4SS can't safely install the dispatch detour, and calling
  `RegisterHook` can crash at boot. This is downstream of address-resolution failures — fix the
  engine-version override first (see the compatibility doc). A mod calling `RegisterHook` installs the
  `ProcessInternal` detour **on demand**, regardless of the `[Hooks]` settings flags.
```
UnregisterHook(functionPath, preId, postId)
```
Pass both ids.

```
RegisterCallFunctionByNameWithArgumentsPreHook(callback)
RegisterCallFunctionByNameWithArgumentsPostHook(callback)
```
`callback(Context, Str, Ar, Executor, bForceCallWithNonExec)`; return `nil` to use the original value,
`true`/`false` to override it. A **global** hook on the call-by-name mechanism — the catch-all net when
you can't anchor a hook to one specific function.

```
RegisterInitGameStatePreHook(callback(GameState))
RegisterInitGameStatePostHook(callback(GameState))
```
Around `AGameModeBase::InitGameState`.

```
RegisterBeginPlayPreHook(callback(Actor))
RegisterBeginPlayPostHook(callback(Actor))
```
Around `AActor::BeginPlay`.

These four (plus `RegisterLoadMapPreHook`/`PostHook(callback(...))` for level changes) are **global
base-class hooks** — they fire for *any* actor/game mode, unlike `RegisterHook`'s "must already be in
memory" requirement, so they're a guaranteed early entry point. Use `LoadMap`/`BeginPlay` to detect
level/state changes cheaply, without polling.

```
RegisterCustomEvent(eventName: string, callback)
```
Fires when a Blueprint function/event with that name is invoked, without needing its full path.

```
RegisterCustomProperty({Name, Type = PropertyTypes.X, BelongsToClass = "/Script/Engine.Character", OffsetInternal = 0xF40})
```
Exposes a raw memory offset as a reflected property. **This is exactly the kind of magic offset the
playbook prohibits hardcoding undocumented** (PRINCIPLES §4) — if you use it, record where the offset
came from (dumper output, RE session, etc.) next to the call. `PropertyTypes` full enumeration lives in
the reflection cookbook.

### NotifyOnNewObject sharp edge (firehose crash)
It fires **by inheritance**, so a base class (`UserWidget`, `TextBlock`) runs your callback for **every
widget in the game** — hundreds in a burst when a menu opens — and touching them mid-construction
hard-aborts the process; `pcall` cannot catch it. **Both mods measured for this reference dropped
`NotifyOnNewObject` entirely, for two independent root causes** (mid-construction access on one game,
callbacks arriving on the async loading thread on the other).

→ Canonical rule: [ue4ss-mod-architecture.md §4](ue4ss-mod-architecture.md). Field notes and the
narrow-registration variant that does work: [accessibility-patterns §9](accessibility-patterns.md).

---

## 4. Console

```
RegisterConsoleCommandHandler(commandName, callback) -> nil
```
`callback(FullCommand: string, Parameters: table, OutputDevice: FOutputDevice) -> bool`. `Parameters` is
1-indexed. Returning `true` stops other handlers from processing the command.

```
RegisterConsoleCommandGlobalHandler(commandName, callback)
```
Same signature as above. Verbatim from the docs: *"Unlike RegisterConsoleCommandHandler, this global
variant runs the callback for all contexts."* This is the correct variant for your own inspection
commands.

```
RegisterProcessConsoleExecPreHook(callback(Context, Command, CommandParts, Ar, Executor))
RegisterProcessConsoleExecPostHook(callback(Context, Command, CommandParts, Ar, Executor))
```
Intercepts **any** console command. Return `nil` to keep the original value.

```
RegisterULocalPlayerExecPreHook(callback(Context, InWorld, Command, Ar))
RegisterULocalPlayerExecPostHook(callback(Context, InWorld, Command, Ar))
```
Returns **two** values: (1) override of the Exec return value (`nil` = original), (2) `true`/`nil` lets
the original Exec run, `false` cancels it.

**Specific → generic hierarchy:** `RegisterConsoleCommand(Global)Handler` → `RegisterULocalPlayerExec*` →
`RegisterProcessConsoleExec*`. Pick the most specific one that covers your case; fall back to the console
exec hooks only when you need to intercept commands you don't own.

`FOutputDevice` (the `Ar` parameter above) is a console-output-sink type passed directly into callbacks —
no `:Get()`/`:Set()` needed, unlike wrapped hook params.

---

## 5. Mod file layout & hot reload

- Layout: `Mods/<ModName>/scripts/main.lua` — the `scripts` folder and `main.lua` entry point are
  **mandatory** — plus a `<ModName> : 1` line in `Mods/mods.txt` (`1` = enabled, `0` = disabled; file
  order = load order). A non-recommended alternative is an empty `enabled.txt` inside the mod folder,
  but that doesn't let you control load order.
- Hot reload: the "Restart All Mods" button in the GUI's Console tab, or Ctrl+R with
  `EnableHotReloadSystem=1` (see [settings-reference](ue4ss-settings-reference.md)).
- **Gotcha to design around:** hooks are **not** cleared automatically on reload. Either register hooks
  idempotently (guard against double-registration) or keep the returned ids and call `UnregisterHook`
  before re-registering, or duplicate callbacks accumulate on every reload.

---

## Cost matrix

| Function | Full GUObjectArray sweep | Event-driven | Filter by | Use for |
|---|---|---|---|---|
| `FindObject` | No (name/path lookup) | No | Short name or path | One-off, targeted lookup |
| `FindObjects` | Partial, capped by `count` | No | Short name + flags | Bounded listing |
| `StaticFindObject` | No (path lookup) | No | Full path | Resolving classes and UFunctions |
| `FindFirstOf` | Yes, up to the 1st match | No | Short name | "Give me one instance" |
| `FindAllOf` | Yes, full sweep | No | Short name | On-demand only, never per tick |
| `ForEachUObject` | Yes, callback per object | No | None | Explicit dump only |
| `NotifyOnNewObject` | No | Yes | Full path + subclasses | Cheap continuous watch — **but read §3 first** |

**Golden rule:** discovery is on-demand (a key/command); continuous tracking is a cached ref kept
current by an **actor-lifecycle hook** (`RegisterBeginPlayPostHook`); never call a `Find*` function
inside a per-tick/per-frame loop. `NotifyOnNewObject` is the classic answer to that last part and is
listed above for completeness — but see
[ue4ss-mod-architecture.md §4](ue4ss-mod-architecture.md) before reaching for it.
