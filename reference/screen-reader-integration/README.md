# Screen-reader integration by engine

How our accessibility mods actually reach the OS screen reader (**PRISM** on every target — no
Tolk fallback) on each host we target. This is the **transport** layer — the plumbing that carries a
`Speak(text, interrupt)` call from mod code down to `prism.dll`. It answers, per engine:

- Can the host **load native code** at all, and how?
- Can a **Lua script** reach native functions directly, or does it need a native/host component?
- **Does the UE4SS-style Lua C-module bridge port here?** (short answer: only sometimes)
- What do we actually **ship**?

The reusable knowledge is at the bottom: [the adapter contract](#the-part-that-ports-everywhere-the-adapter-contract)
is engine-agnostic and shared across all targets; only the transport under it changes.

> Companion to [prism-prebuilt.md](prism-prebuilt.md) (consuming the PRISM prebuilt release) and
> CLAUDE.md §6 (architecture). Those tell you *how to call PRISM*; this doc tells you *how the call
> physically gets there* on each engine.

---

## The two questions that decide everything

Every host reduces to two yes/no questions:

1. **Is the host's Lua a real native Lua** (PUC Lua or LuaJIT compiled to machine code), or a
   **managed re-implementation** running on another VM (Kahlua on the JVM, MoonSharp on .NET)?
   - Managed Lua → **cannot** load DLLs, no C modules, no FFI. You *must* bridge through the host
     VM's own native-interop (JNA on Java, P/Invoke on .NET).
   - Native Lua → native code is reachable; go to question 2.

2. **Does the host let a *script* load native code** (`package.loadlib`/`require` of a Lua C module,
   or LuaJIT `ffi`)?
   - Yes → you can ship a **pure-script mod + a bundled native module** (the UE4SS model).
   - No (sandboxed / unsupported) → the script can't reach native on its own; a **native host
     plugin** must inject the functions into the Lua state.

Everything below is those two questions answered per engine.

---

## Decision matrix (our targets)

| Host / framework | Lua runtime | Script can load native? | Screen-reader transport we use | UE4SS C-module bridge ports? |
|---|---|---|---|---|
| **UE4SS** (Unreal, IL2CPP/UE4-5) | PUC **Lua 5.4** (native) | **Yes** — `require` a Lua C module | Lua C module `prism_bridge.dll` → `LoadLibrary("prism.dll")` | **This is the reference impl** |
| **REFramework** (RE Engine) | PUC **Lua 5.4.3** (native) | **No** (not reliably — see below) | **Native C++ plugin** loads PRISM, injects `prism.*` into Lua via `on_lua_state_created`; *or* keep C#/P-Invoke | **Source reusable, but not as a script `require`** — load it from a native plugin |
| **Project Zomboid** (Java) | **Kahlua** (Lua 5.1 on the JVM) | **No** (managed VM, no FFI) | Lua → Java bridge (**JNA**) → PRISM. Today via file-tail `-javaagent`; better via `@LuaMethod` | **No** — impossible; a C module can't load into a JVM Lua |
| **[BepInEx](../engines/bepinex/README.md) / Unity (Mono)** | C# (not Lua) | n/a | C# P/Invoke → PRISM | n/a |
| **[BepInEx 6](../engines/bepinex/README.md) / Unity (IL2CPP)** | C# (not Lua) | n/a | C# P/Invoke → PRISM (use correct IL2CPP interop) | n/a |
| **[BmSDK](../engines/bmsdk/README.md) / UE3 — Arkham Knight (x64)** | C# (not Lua) | n/a | C# P/Invoke → PRISM, straight from the mod's `.cs` | n/a |
| **[BmSDK](../engines/bmsdk/README.md) / UE3 — Arkham City (x86)** | C# (not Lua) | n/a | **Open** — no PRISM x86 prebuilt. Build PRISM x86 / out-of-process x64 bridge / Tolk x86 (as in L4D2). See [accessibility patterns §1](../engines/bmsdk/bmsdk-accessibility-patterns.md) | n/a |
| **Native game** (C/C++/VB6) | none | n/a | Native hook `LoadLibrary("prism.dll")` directly | n/a |

**ABI rule for Lua C modules:** a Lua C module (`luaopen_<name>`) must be compiled against the host's
**exact Lua major.minor ABI**. UE4SS and REFramework are *both* Lua **5.4**, so the UE4SS
`prism_bridge.c` **source** is ABI-compatible with REFramework — but see REFramework below for why you
still can't just `require` it from a script. Kahlua is Lua 5.1 *and* not native, so nothing compiled
loads there at all.

---

## UE4SS (Unreal) — pure Lua script + bundled C module ✅ (the reference)

**Verdict:** the cleanest case. Scripts can `require` a native Lua C module, so accessibility logic
stays in Lua and native code is a single small DLL.

- Host Lua is **PUC Lua 5.4** (native, real), and UE4SS lets mod scripts `require` C modules.
- `prism_bridge.dll` is a **Lua C module**: it statically links the exact Lua 5.4.4 UE4SS bundles
  (ABI match) and exposes `luaopen_prism_bridge`. On `require("prism_bridge")` UE4SS calls that entry
  point with its own `lua_State`.
- Inside, the module does `LoadLibrary("prism.dll")` + `GetProcAddress` (CLAUDE.md §4 dynamic-loading
  path) and exposes `say/stop/is_speaking/detect/set_rate/set_volume/set_pitch` to Lua.
- **Ship:** the Lua mod + `prism_bridge.dll` + `prism.dll` in the mod's `Scripts/`.
- **Reference implementation:** `D:\code\unreal\UE4ss study\template` —
  `src/prism_bridge/prism_bridge.c`, `mod/Scripts/screen_reader.lua`, `mod/Scripts/announcer.lua`.

This is the layout the other engines are measured against.

---

## REFramework (RE Engine) — needs a native plugin ⚠️ (no pure-Lua path)

**Verdict:** you can drive accessibility logic from Lua, but you **cannot** reach PRISM from a Lua
*script* alone. A native C++ REFramework plugin is required. Pure-Lua distribution is **not** an
option here.

Why (verified against praydog/REFramework source, high confidence):

- REFramework embeds **PUC Lua 5.4.3** — **not LuaJIT**. So **`ffi` does not exist** (it is a
  LuaJIT-only library); `ffi.load("prism.dll")` is impossible.
- The Lua state opens `base, package, string, math, table, bit32, utf8, os, coroutine, debug`
  (`os.*`/`debug.getregistry` are then nil'd). `package` and `loadlib.c` are compiled in, so
  `package.loadlib`/`require` of a C module *technically* exist — **but**:
  - a general DLL like `prism.dll` exports `prism_*`, not `luaopen_prism`, so
    `package.loadlib("prism.dll","luaopen_prism")` fails (symbol not found);
  - community reports ([REFramework #623](https://github.com/praydog/REFramework/issues/623)) show the
    script runner does not load native DLLs reliably — `require` falls through to the text loader.
  - **⚠️ Correction:** `docs/accessibility-patterns.md` still lists "write a Tolk wrapper as a native
    DLL and `require` it from Lua" as an option — that path is **not reliable** in REFramework. Prefer
    one of the two below.
- **Supported native bridge:** ship a **native C++ REFramework plugin** (`reframework/plugins/*.dll`)
  using `include/reframework/API.h`. On `on_lua_state_created` it wraps the `lua_State` with
  `sol::state_view` (under `lock_lua()/unlock_lua()`) and registers thin wrappers — `prism.speak(text,
  interrupt)`, etc. The plugin itself does `LoadLibrary("prism.dll")`. Accessibility logic can then
  live in `reframework/autorun/*.lua`.

**Two viable options — pick per project:**

| Option | Native component | Logic lives in | When to choose |
|---|---|---|---|
| **A. C# (current)** | REFramework.NET + `TolkDotNet` (P/Invoke) | C# | Working today (Re7Access, sf6Access). Keep if you don't need Lua. Migrate Tolk→PRISM to match the playbook default. |
| **B. Native plugin + Lua** | small C++ plugin loading `prism.dll`, injects `prism.*` | Lua (`autorun/`) | Choose if you want the accessibility logic in Lua and shared with UE4SS-style code. **Still ships a native DLL.** |

Since the UE4SS C module is already Lua 5.4, its `LoadLibrary`-based PRISM loading code is directly
reusable **inside** Option B's plugin — you're just registering the functions via
`on_lua_state_created` instead of via `luaopen_`.

**Bottom line on "move RE Engine to Lua":** moving *logic* to Lua is fine, but it does **not** remove
the native component. If the only motivation was "pure Lua like PZ/UE4SS," that goal isn't reachable
on REFramework — you always ship at least a thin native plugin. If the motivation is unifying the
accessibility *logic* style across games, Option B delivers that.

---

## Project Zomboid (Java / Kahlua) — must bridge through Java ⛔ (no native from Lua)

**Verdict:** the UE4SS bridge cannot port at all. PZ's Lua is **Kahlua**, a Lua 5.1 dialect running
**on the JVM** — it cannot load DLLs, has no FFI, and can't host a C module. All native work goes
through Java.

- **Current transport:** Lua writes `SR|…` lines to `Zomboid/Lua/PZAccessSpeech.txt`; an in-process
  `-javaagent` (`PZAccess.jar`) tails the file and calls PRISM via **JNA**
  (`Native.load("prism", …)` → `prism_backend_speak`). Launched by a custom `.bat` because the Steam
  launcher rejects `-javaagent`.
- **Cleaner future transport (same engine constraints):** expose a Java method to Kahlua via PZ's
  `@LuaMethod`/`LuaManager.GlobalObject` mechanism, so Lua calls Java *directly* (no file, no tail),
  and Java calls PRISM via JNA. Removes the file-polling latency; still Java-mediated.
- **Ship:** the Lua mod (`media/lua/client/*`) **plus** the Java agent JAR (with `prism.dll`
  bundled as a resource — `tolk.dll` not needed) and the launcher.
- **Reference implementation:** `D:\code\lua\project zomboid Access` — `bridge/src/pzaccess/bridge/Main.java`
  (JNA + PRISM), `PZAccess/42/media/lua/client/PZAccess_Core.lua` (Lua speech sink).

The Lua-side adapter (the `speak` sink, dedup) is shared with the others; only the transport under it
is Java.

---

## Managed C# hosts (BepInEx / Unity) and native games — for completeness

- **BepInEx / Unity (Mono or IL2CPP):** no Lua involved; call PRISM from C# via `[DllImport]`
  P/Invoke (dynamic-load path per CLAUDE.md §4). The official .NET binding (`Prismatoid` on NuGet)
  targets .NET 10 and does NOT load inside BepInEx hosts (net472/net6) — P/Invoke directly. On
  IL2CPP use correct interop (see memories) and never call PRISM per-frame from a hot path.
  Ship `prism.dll` next to the mod DLL. Platform details — build selection, interop generation and its
  Unity 6 repair, Harmony rules — are in [engines/bepinex](../engines/bepinex/README.md);
  the mod-side architecture is in
  [engines/bepinex/accessibility-patterns.md](../engines/bepinex/accessibility-patterns.md).
- **Native game (C/C++/VB6):** the native hook links/loads `prism.dll` directly
  (`LoadLibrary` + `GetProcAddress`). No intermediary.

---

## The part that ports everywhere: the adapter contract

Regardless of transport, mod code is written against **one** small interface. Only the implementation
underneath changes per engine. This is the real reusable asset — port *this*, not the compiled DLL.

```
ScreenReader
  Output(text, interrupt)      -- route to the backend; interrupt=true on context change
  Silence()                    -- stop speech
  SetRate/SetVolume/SetPitch(v)
  BackendName()                -- log the chosen PRISM backend on init

Announcer  (built on ScreenReader — engine-agnostic, pure logic, ships as-is)
  Speak(msg, interrupt)        -- dedup / anti-spam gate (e.g. 200 ms on identical msg)
  SpeakUnique(msg) / SpeakInterrupt(msg)
```

- `Announcer` (dedup/diff-gating, verbosity) and the string/localization layer contain **no native
  calls** → they copy verbatim between UE4SS, RE Engine (Lua) and PZ.
- Only `ScreenReader.Output` differs: `prism_bridge.say()` (UE4SS), `prism.speak()` from the native
  plugin (RE Engine Option B), or the Java/JNA sink (PZ).
- Keep a **single sink** so backend or transport swaps (JNA↔P/Invoke) happen in one place (CLAUDE.md §9).

---

## One-line summary per engine

- **UE4SS** — native Lua 5.4 + scripts can `require` C modules → **pure Lua mod + bundled C module**. ✅
- **REFramework / RE Engine** — native Lua 5.4 but scripts **can't** load DLLs and there's no FFI →
  **native C++ plugin required**; logic can be Lua, distribution never pure-Lua. ⚠️
- **Project Zomboid** — Lua runs on the JVM (Kahlua) → **bridge through Java (JNA)**; no C module ever
  loads. ⛔
- **Everywhere** — the `ScreenReader` + `Announcer` adapter is identical; only the transport differs.
