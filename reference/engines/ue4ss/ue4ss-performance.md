# UE4SS performance — the frame budget playbook

**The canonical cost reference for UE4SS mods.** Upstream publishes almost no numbers: a full-text
search of both docs tracks (`docs.ue4ss.com` and `/dev`) returns **zero** FPS figures, millisecond
costs or memory numbers for any feature. Every quantified value below is either **measured on our own
shipped mods** or **read out of the UE4SS source**, and is tagged accordingly.

Companion docs — this one does not repeat them:
[`ue4ss-mod-architecture.md`](ue4ss-mod-architecture.md) (the *resolve, don't scan* doctrine and the
decision ladder), [`ue4ss-api-reference.md`](ue4ss-api-reference.md) (API surface),
[`accessibility-patterns.md`](accessibility-patterns.md) (the code patterns),
[`ue4ss-settings-reference.md`](ue4ss-settings-reference.md) (every ini key).

Tags used below: **[MEASURED]** = timed on Sparking ZERO / Kakarot · **[SOURCE]** = read in the
RE-UE4SS source at a named file · **[DOC]** = stated in the official docs · **[UNVERIFIED]** = design
implies it but neither docs nor public source confirm it (UE4SS's object-lookup internals live in the
private `Re-UE4SS/UEPseudo` submodule).

**Verification note (2026-07-29):** every claim below was re-verified against the RE-UE4SS checkout at
`D:\code\unreal\ue4ss-cppmods\RE-UE4SS`, pinned to tag **v3.0.1** (commit `d935b5b`, 14 Feb 2024).
Several original `**[SOURCE]**` citations were wrong and have been corrected in place (see the `__index`
citation in §2 and the flag-order line numbers in §3.1). This document's **target version is 3.0.x**;
a few hazards it originally described (`CreateInvalidObject()` in §4.2, some of the UEHelpers getters in
§4.3, `--disable-ue4ss` in §8) are **main-branch** behavior that does not exist at v3.0.1 — each is now
called out explicitly where it occurs. Where a claim could not be confirmed at v3.0.1 (`FindProperty`'s
own body — the `deps/first/Unreal` submodule is uninitialised in this checkout and its upstream has been
deleted), that is stated as a caveat rather than presented as fact.

---

## 1. The only rule that matters

**Every cost your mod incurs lands on the game thread.** Hook callbacks run *inside* the engine call
that fired them. `ExecuteInGameThread` work runs *inside* the frame. A `LoopAsync` body runs on a
worker thread, but the moment it touches a UObject it must hop to the game thread — so its real cost
is game-thread cost too.

At 60 fps a frame is **16.7 ms**. Budget your per-tick work at **≤ 1 ms** to be invisible. A single
unbudgeted `FindAllOf` is 65–115 ms **[MEASURED]** — four to seven dropped frames, from one line of
Lua.

---

## 2. The cost ladder

| Operation | Cost | Source |
|---|---|---|
| `FindAllOf` / `FindFirstOf` / unbounded `FindObjects` / `ForEachUObject` | **65–115 ms** per call | **[MEASURED]** SZ ~115 ms fixed; Kakarot ~65 ms avg, 272 ms peak |
| Three `FindAllOf` in one tick | **153 ms** spike | **[MEASURED]** Kakarot `ui_core.lua:605` |
| `StaticFindObject` by full path | **~6 ms** | **[MEASURED]** SZ, single call |
| `Object.PropName` (one property read) | one `FindProperty(name)` **per access**, never memoised | **[SOURCE]** `LuaUObject.hpp:370-381` (`__index`/`__newindex`), `:487-497` (`GetPropertyValue`/`SetPropertyValue`), `:644-681` (`prepare_to_handle`) |
| Pointer-hop work on an object you hold (`GetClass():GetFName()`) | **~0.003 ms** | **[MEASURED]** SZ |
| 1843 × `HasKeyboardFocus` on held pointers | **19 ms total** | **[MEASURED]** SZ |

**`FindFirstOf` is not a cheap `FindAllOf`.** It cost 114 ms returning exactly one object
**[MEASURED]**. The cost is the *walk*, not the result count — sorting the finders by "how much do
they return" is the wrong mental model.

### 2.1 The chain rule (new)

`__index` does a **fresh `FindProperty(name)` on every single property access** — no cache anywhere in
the path shared by `__index`, `__newindex`, `GetPropertyValue`, `SetPropertyValue` and
`Reflection():GetProperty()` **[SOURCE]**:

- `__index`/`__newindex` metamethods (`LuaUObject.hpp:370-381`) both just call
  `prepare_to_handle(Operation::Get/Set, lua)`.
- `GetPropertyValue`/`SetPropertyValue` (same file, `:487-497`) funnel into the same call.
- The lookup itself lives in `prepare_to_handle` (`LuaUObject.hpp:644-681`): line 668 constructs
  `FName property_name = FName(member_name)`, line 670 does a linear
  `LuaCustomProperty::StaticStorage::property_list.find_or_nullptr(...)`, and line 680 does
  `obj_as_struct->FindProperty(property_name)`. There is genuinely no memoisation at any step.

`FindProperty`'s own body is a separate question from the funnel above, and it is **not verifiable at
v3.0.1**: the `deps/first/Unreal` submodule is uninitialised in this checkout and its upstream
(UEPseudo) has been deleted. The 2022 lineage of that code was a plain linear walk of the whole
property chain (`UStruct.cpp` `ForEachPropertyInChain`); **current** UEPseudo memoises it in a
`static std::unordered_map` keyed by `(UStruct*, FName)`. So "never memoised" is a statement about
**this doc's target, the 2024 v3.0.1 build, only** — it is outright false of UE4SS today.

Consequence: **every dot is a name lookup.**

```lua
-- 4 name lookups per tick, every tick
local hp = mgr.BattleState.PlayerData.Health.Current

-- 1 lookup per tick once the chain is hoisted; re-hoist only when the root dies
self.health = self.health or mgr.BattleState.PlayerData.Health
local hp = self.health.Current
```

Cache the **deepest stable object**, not the root. This is the cheapest optimisation available in a
UE4SS mod and it costs nothing but a local.

**Cost calibration.** One property access costs: a Lua metamethod dispatch, a UTF8→UTF16 heap
`std::wstring` conversion, an `FName(str, FNAME_Add)` insertion into the game's name table, a linear
scan of the registered custom-property list (`UE4SS/src/LuaType/LuaCustomProperty.cpp:29-69`, which
takes the name by value and does a `ForEachSuperStruct()` walk per registered entry), `FindProperty`
itself, a pusher hash lookup, and — for object results — a fresh Lua userdata. That is **roughly a
microsecond**, i.e. **four to five orders of magnitude cheaper** than a `FindAllOf` object-array walk
(65–115 ms, §2 table). Practical consequence: hoist chains where it is free (a local inside one
function body), but **never restructure a loop or add a cross-tick cache on the strength of
property-lookup cost alone** — measure first, and expect the walk, not the reads, to be the cost
centre.

### 2.2 The property "fast path" is a mirage — do not chase it

The obvious optimisation — resolve an `FProperty` once with `Reflection():GetProperty(name)` and reuse
it via `Property:ContainerPtrToValuePtr(container, idx)` — **does not work from Lua**:

- `ContainerPtrToValuePtr` pushes a **LightUserdata** and nothing more (`LuaXProperty.cpp:215`
  **[SOURCE]**; also stated as the return type in the docs **[DOC]**). Lua cannot dereference
  lightuserdata — it is opaque by Lua's own semantics. The only real use of it in the whole UE4SS tree
  is round-tripping it straight back into `Property:ImportText(...)` to *write* a value.
- `Reflection():GetProperty()` performs the **same fresh `FindProperty`** as plain `Object.PropName`
  (`LuaUObject.hpp:609` **[SOURCE]**), so it is not even a cheaper lookup.

Net: the fast path is strictly *worse* than normal property access — same lookup cost, unreadable
result. Hoisting the object (§2.1) is the real answer.

One genuinely useful thing a resolved `FProperty` **is** good for: `Property:GetOffset_Internal()` is
exposed (`LuaXProperty.cpp:134` **[SOURCE]**). Read the offset once, then read through your own guarded
memory bridge — that's the one durable win from resolving the property at all.

---

## 3. Finding objects: the hierarchy

Stop at the first rung that answers the question. (This is the API-cost view; the *architectural* view
— why you should not be finding anything at steady state — is the decision ladder in
[`ue4ss-mod-architecture.md`](ue4ss-mod-architecture.md) §2.)

1. **You already hold the pointer.** Free. Always the answer at steady state.
2. **`StaticFindObject(fullPath)`** — name/path lookup, ~6 ms **[MEASURED]**. The documented
   recommendation for non-instances (`UClass`, `UFunction`) **[DOC]**. Resolve once at init, cache
   forever: classes and functions do not die.
3. **`FindObjects(N, class, name, RequiredFlags, BannedFlags, exact)`** with a **bounded N** — prefer
   over `FindAllOf` when you need up to N. Whether it truly early-outs at N is **[UNVERIFIED]** (the
   loop lives in the private submodule), so treat it as "no worse than `FindAllOf`, probably better",
   not as a licence to call it per frame.
4. **`FindAllOf`** — full sweep, no flags parameter, builds a Lua table of every match. On demand only.
5. **`ForEachUObject`** — walks the entire `GUObjectArray`. Explicit-dump use only.

### 3.1 Parameter order in `FindObject` / `FindObjects`

Verified against the actual overload declarations in `LuaMod.cpp:2703` (`required_flags` as the 3rd
arg) and `:2720` (`banned_flags` as the 4th arg), and the shipped `Mods/shared/Types.lua:516-519`
**[SOURCE]** — all three agree, and so does the official documentation
(`docs/lua-api.md:297` and `:305` state RequiredFlags before BannedFlags **[DOC]**). The real order
is:

```
FindObject(className, objectShortName, RequiredFlags, BannedFlags)
FindObjects(numToFind, className, objectShortName, RequiredFlags, BannedFlags, bExactClass)
                                    ^^^^^^^^^^^^^^  ^^^^^^^^^^^^
                                    must HAVE       must NOT have
```

```lua
-- first 4 live instances, CDOs excluded native-side (no Lua-side filtering loop)
local widgets = FindObjects(4, "UMG_MainMenu_C", nil,
                            EObjectFlags.RF_NoFlags, EObjectFlags.RF_ClassDefaultObject, true)
```

`RF_ClassDefaultObject = 0x00000010` **[DOC]**.

### 3.2 Do not filter in Lua what the engine can filter for you

Both our mods walk every `FindAllOf` result calling `GetFullName()` to skip CDOs / pick the top-level
instance (Kakarot `ui_core.lua:568` `Core.first_live`, `ui_directory.lua:82-90` `first_instance`).
That is an O(n) **string-building** loop on top of an already expensive scan, and `GetFullName()` walks
the whole outer chain per object.

Two levers, in order:
- `FindFirstOf`/`FindAllOf` are documented to return **non-default instances only** **[DOC]** — i.e.
  the CDO-skipping half of those loops may already be redundant. The literal flag check is
  **[UNVERIFIED]** in public source, so confirm empirically on the target game before deleting the
  loop.
- Where a flag filter *is* what you want, use `FindObjects` with `BannedFlags` (§3.1) and let the
  native side do it.

---

## 4. Holding a pointer across frames

### 4.1 The portable idiom (works on 3.0.x)

```lua
local cache = nil
local function GetThing()
    if cache and cache:IsValid() then return cache end
    cache = FindFirstOf("Thing")          -- the expensive path, now rare
    return cache
end
```

### 4.2 `CreateInvalidObject()` is **not** available on 3.0.x

The idiom the current docs teach —

```lua
local EngineCache = CreateInvalidObject()
function GetEngine()
    if EngineCache:IsValid() then return EngineCache end
    EngineCache = FindFirstOf("Engine")
    return EngineCache
end
```

— relies on a global that is **absent from the v3.0.0 and v3.0.1 tags** (0 matches in `LuaMod.cpp` at
both tags; its doc page 404s on the stable site) and exists only on `experimental-latest`/main, heading
for 4.x **[SOURCE]**. On a 3.0.x target use §4.1 instead. Both are the same pattern; §4.2 just avoids
the nil-check.

### 4.3 UEHelpers is not the caching layer you think it is — but that description is main-branch, not 3.0.x

The underlying hazard is real, but as originally written this section described
`Mods/shared/UEHelpers/UEHelpers.lua` from the **main branch**, not from the v3.0.1 tag this doc
targets. **At v3.0.1**, that file is only **103 lines** **[SOURCE]**:

| Function | Cached? |
|---|---|
| `GetPlayerController` | **yes** — cached in a module-local (`UEHelpers.lua:33-52`) |
| `GetWorld` | yes — rides the same `GetPlayerController` cache |
| `GetPlayer`, `GetPersistentLevel`, `GetGameModeBase`, `GetGameStateBase`, `GetAllPlayers`, `GetEngine`, `GetGameInstance` | **do not exist at v3.0.1 at all** — main-branch-only additions |

So on a 3.0.x target, the specific failure mode this section warns about — an uncached getter re-running
`FindAllOf` on every call, 65–115 ms each **[MEASURED]** — does not apply to `GetPlayerController` or
`GetWorld` (already cached) and cannot apply to the other names (they aren't present to call). Treat the
hazard as a property of **whichever UEHelpers your target UE4SS build ships**, not a fact about 3.0.x
specifically: newer UEHelpers exports more getters, and there is no guarantee all of them stay cached.

The defensive practice still stands regardless of version: **wrap every UEHelpers getter you call in
your own §4.1 cache**, and check the `UEHelpers.lua` actually shipped with your target build rather than
assuming its caching from memory or from the latest docs. (Sparking ZERO's `battle.lua:10-11` records
exactly this bug in the mod it replaced: the original re-found the controller with a `FindAllOf` on
every 100 ms tick.)

### 4.4 What invalidates a handle

- Always `:IsValid()` before use — the engine can free the object at any time (actor destruction, GC,
  level transition).
- **Never keep a hooked UFunction's parameter object past the callback.** Hook params come back as
  `RemoteUnrealParam`, a live pointer into the parameter frame (`LuaUObject.cpp:1629-1657`
  **[SOURCE]**); after the call returns, `:Get()` reads reused memory. Plain `Object.PropName` FName
  reads *are* real copies — the docs' "all FNames returned by `__index` are returned by reference"
  warning **[DOC]** is really about the hook-param path.
- Comparing names in a hot loop: use `FName:GetComparisonIndex()` (an integer) **[SOURCE]** rather than
  `ToString()`, which allocates a Lua string every call.

---

## 5. Threads: where your code actually runs

**Game thread (safe to touch UObjects directly)** — `RegisterHook` callbacks, `RegisterBeginPlay*Hook`,
`RegisterInitGameState*Hook`, `RegisterProcessConsoleExec*Hook`,
`RegisterCallFunctionByNameWithArguments*Hook`, `RegisterULocalPlayerExec*Hook`, `RegisterCustomEvent`,
`RegisterConsoleCommandHandler` **[DOC]**.

**Not the game thread** — `LoopAsync`, `ExecuteAsync`, `ExecuteWithDelay` bodies. `RegisterKeyBind`
callbacks are not guaranteed either: the official "Creating a Lua Mod" tutorial wraps its state read in
`ExecuteInGameThread` *inside* the keybind callback **[DOC]**. `NotifyOnNewObject` arrives on UE4SS's
async loading thread on both our games **[MEASURED]** — one of the two reasons both mods dropped it.

**`ExecuteInGameThread(cb)` queues; it does not block.** On 3.0.x it dispatches through `ProcessEvent`.
Newer builds add an explicit choice **[DOC]**:

| Method | Frequency | Use when |
|---|---|---|
| `EngineTick` | once per frame | frame-synchronised work, predictable cadence, fewest firings — **the default to want** |
| `ProcessEvent` | many times per frame | you need the lowest possible latency |

Key name trap: the shipped ini key is **`DefaultExecuteInGameThreadMethod`**; the docs' prose calls it
`DefaultGameThreadExecutionMethod`, which does not exist. Neither key is present on 3.0.1 at all — see
[`ue4ss-settings-reference.md`](ue4ss-settings-reference.md).

### 5.1 The busy-guard is mandatory

`LoopAsync` sleeps its worker thread and fires again **without waiting** for the work it queued onto
the game thread. Under load the queue grows and you get burst stalls. Both shipped mods needed the same
guard (SZ `app.lua:142-147`, Kakarot `ui_core.lua:1567-1591`): if the previous dispatch has not
completed, **skip this beat entirely**.

---

## 6. Hooks: free at rest, not free when they fire

- `RegisterHook` needs the target UFunction **already resident in memory** **[DOC]**.
- Only Blueprint-dispatched calls go through the hookable path. **Both our games' UI is C++-direct**:
  Sparking ZERO installed 6 UMG hooks and observed **zero** firings **[MEASURED]**. Test this before
  designing around hooks.
- `RegisterBeginPlayPostHook` fires for **every actor in the game** — projectiles, effects, everything.
  Sparking ZERO's users felt it during matches until an early-out was added as the callback's *first*
  statement (`engine.lua:534,540`). Filter and return before reading a single property.
- **Never call `GetFullName()` inside a hook** — the outer-chain walk aborts on half-built widgets
  (Sparking ZERO `hookprobe.lua:53`).
- `NotifyOnNewObject`: the docs carry an explicit perf warning about the same class being registered by
  multiple mods **[DOC]** — *"which has been seen in the wild"*. We drop it entirely for other reasons
  (mid-construction objects, wrong thread).
- **Hot reload may leak registrations.** Nothing documents the classic Ctrl+R path as hook-safe, and 4.x
  adds `UninstallCurrentMod()` explicitly documented to *"destroy the Lua state and remove all hooks and
  keybinds"* **[DOC]** — which implies the old path did not. Make registration idempotent
  (`IsKeyBindRegistered`, a registered-flag table) and unregister in your own teardown.

> **Known gap in our own numbers:** we have measured scan cost, property cost and pointer-hop cost, but
> we have **never measured the overhead of a hook firing**. "Prefer hooks over polling" is an
> architectural claim here, not a benchmarked one. If a future session profiles it, put the number in
> the §2 table.

---

## 7. Settings that cost frames

Full key reference in [`ue4ss-settings-reference.md`](ue4ss-settings-reference.md); these are the ones
that matter for performance:

| Key | Ship as | Why |
|---|---|---|
| `[Debug] ConsoleEnabled`, `GuiConsoleEnabled`, `GuiConsoleVisible` | **0** | The ImGui debug GUI is the largest optional runtime subsystem UE4SS has. The docs never publish a cost, but the end-user install profile turns all three off and the *developer* profile is the one that turns them on **[DOC]** |
| `[Debug] RenderMode` | `ExternalThread` (default) | Keeps GUI rendering off the game's own tick when the GUI must stay on for an internal build. Relative cost of the three modes is undocumented |
| `[General] bUseUObjectArrayCache` | `true` (default) | Backs the finders with UE4SS's own cache instead of raw `GUObjectArray` iteration. Set `false` **only** as a startup-crash workaround **[DOC]** |
| `[General] EnableAutoReloadingLuaMods` | **0** (default) | Continuous filesystem watching while enabled |
| `[ObjectDumper] LoadAllAssetsBeforeDumpingObjects`, `[CXXHeaderGenerator] LoadAllAssetsBeforeGeneratingCXXHeaders` | **0** in any shipped config | *"Can require multiple gigabytes of extra memory"* and *"will crash the game if you load past the main menu after dumping"* **[DOC]**. Enable once, at the main menu, for a one-shot dump, then revert |
| `[Threads] SigScanner*` | defaults | **Startup only** — the one-time AOB scan. Not a steady-state dial |

`[Hooks]` is a **feature** section, not a performance section: the docs never present disabling a hook
as an optimisation, and turning one off silently breaks whatever API depends on it. Leave defaults.
(Note our own finding that `HookProcessInternal=0` does not reliably stop the detour anyway — any mod
calling `RegisterHook` installs it on demand.)

---

## 8. How to measure

1. **A/B the whole framework.** Launch with **`--disable-ue4ss`** **[DOC]** (proxy-DLL installs only) to
   get a clean baseline, then compare. This separates "my mod is slow" from "UE4SS is slow on this
   game" in one launch. **Version caveat:** this flag does **not exist at v3.0.1** (zero occurrences in
   the whole v3.0.1 tree) — it is generated into the proxy stub only on main
   (`UE4SS/proxy_generator/main.cpp:273`) **[SOURCE]**. On a 3.0.x target the flag is silently ignored;
   don't spend a session chasing it — A/B by removing the UE4SS files instead.
2. **Time the suspicious call**, don't guess. A `timed_findall()` wrapper (Kakarot `ui_core.lua:539`)
   is how every number in §2 was obtained.
3. **Count, don't just time.** The metric that actually tracked user-perceived smoothness on both mods
   was **scans per session**, not ms per scan: Sparking ZERO went 46 → **6 scans in a whole session,
   all inside the first 90 s of boot, zero during a match**.
4. `jsbLuaProfilerMod` for Lua-side hot spots before optimising a per-tick read.

---

## 9. Pre-ship checklist

- [ ] Zero `FindAllOf` / `FindFirstOf` / `ForEachUObject` in any steady-state path (log a counter and
      verify it is flat during gameplay).
- [ ] Every `UEHelpers` getter you call wrapped in a local cache (§4.3) — don't assume caching from
      memory or from the latest docs; check the file your target build actually ships.
- [ ] Deep property chains hoisted to the deepest stable object (§2.1).
- [ ] Every `LoopAsync` has a busy-guard (§5.1).
- [ ] Every hook callback early-outs before its first property read (§6).
- [ ] No `GetFullName()` in any hook or per-tick loop.
- [ ] Loop periods justified: the fastest loop in either shipped mod is 20 ms (gamepad polling only,
      and it relaxes to every 5th tick during cutscenes/loads); UI polling is 100 ms; quest/battle
      monitors 250–300 ms.
- [ ] Release `UE4SS-settings.ini` profile applied (§7) — console trio off, `LoadAllAssetsBefore*` back
      to 0.
- [ ] Dev-only probes stripped from the release package (Kakarot strips `discover.lua` in
      `package.ps1`).
