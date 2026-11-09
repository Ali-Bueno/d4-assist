# PRISM — consuming the prebuilt release (verified, DO NOT re-investigate)

> **Read this when:** you're wiring PRISM into a mod for the first time (any engine) and need the
> concrete facts about the release layout, exports, call sequence and what to ship. One-time-per-mod
> reference — not needed once the adapter is working. For *how the call physically reaches PRISM on
> each engine* (the transport), see [the integration index](README.md).
>
> Checked against **PRISM v0.16.7** by downloading the asset and reading the binary/headers;
> release layout re-verified against **v0.17.3** (2026-07-20).
> **No need to compile the PRISM repo or use CMake/vcpkg** to consume it: the release is enough.

- **Per-platform releases** on GitHub (`ethindp/prism`, assets of the latest release). For Windows:
  **`prism-windows-x64.zip`** and `prism-windows-arm64.zip`. **There is no x86 (32-bit) build** → if the
  game/host is 32-bit, the PRISM prebuilt is NOT usable (you'd have to compile). Most modern x64 games →
  use `x64`.
- **Stable C API**: `include/prism.h` is wrapped in `extern "C"`, pure C types (`char*`, `float`,
  `size_t`, opaque pointers `PrismContext*`/`PrismBackend*`), `__cdecl` convention, `PRISM_API` macro
  (`__declspec(dllimport/dllexport)`). **No C++ type crosses the boundary** → a DLL built with **MinGW or
  MSVC** can consume `prism.dll` (which is built with MSVC) **with no ABI problems**.
- On **x64 the exports are NOT decorated** (`prism_init`, `prism_backend_speak`, …) → the most robust
  path is **dynamic loading**: `LoadLibrary("prism.dll")` + `GetProcAddress` by name. Zero link config,
  zero import lib, and you control when it loads (key if it's an injected DLL: load outside `DllMain`).
- **Windows zip contents**: `include/prism.h`; `dynamic/release/bin/prism.dll` (~1.1 MB, **the ONLY
  file you distribute**) + `tolk.dll` (~525 KB, optional Tolk-compat backend — do **not** ship it:
  `prism.dll` does not import it, and NVDA/JAWS/SAPI support is built in); `dynamic/release/lib/prism.lib`
  (MSVC import lib, optional); a CMake package (target **`prism::prism`**). It also includes `static/`
  with a ~79 MB `prism.lib` → **it's MSVC C++: NOT linkable from MinGW** (MSVC only, yields a single
  self-contained DLL).
- **`prism.dll` runtime dependencies**: only system DLLs + UCRT (`KERNEL32`, `USER32`, `ole32`,
  `OLEAUT32`, `RPCRT4`, `UIAutomationCore`, `api-ms-win-core-*`). **Does NOT depend on
  `vcruntime140`/`msvcp140`** → **no VC++ Redistributable required**. Asian-reader backends
  (`PCTKUSR.dll`, `ZDSRAPI_x64.dll`, Orca/SpeechDispatcher bridges) load **on demand**; for NVDA/JAWS/SAPI
  **you don't have to distribute them**.
- **Minimal sequence**: `prism_config_init` → `prism_init(&cfg)` → `prism_registry_acquire_best(ctx)` →
  `prism_backend_initialize(be)` → `prism_backend_speak(be, txt, interrupt)` → … → `prism_backend_free` +
  `prism_shutdown`. Use `interrupt=true` when switching context; log the chosen backend with
  `prism_registry_name`. Others: `prism_backend_braille`, `prism_backend_is_speaking`, `set_rate/volume/pitch`.
- **Static linking (source-level ports only)**: injected/plugin hosts dynamic-load `prism.dll`, but a mod
  compiled *into* the game (decomp / libultraship PC ports) can link `prism.lib` and call the API directly.
  The static `prism.lib` is **MSVC C++** → MSVC toolchain only (not MinGW). Still copy `prism.dll` next to the exe as a POST_BUILD step.
- **Version note — use PRISM ≥ v0.17.0.** Releases v0.16.2–v0.16.7 had a regression
  ([issue #49](https://github.com/ethindp/prism/issues/49)) where the **NVDA backend's `initialize()`
  falsely succeeded when NVDA wasn't running, shadowing JAWS/other readers** — so `acquire_best` could
  pick a dead backend. **Fixed in v0.17.0** (2026-07-07); `acquire_best` is correct there. Only if you're
  pinned to an affected version, the workaround is to enumerate the registry and pick the highest-priority
  backend reporting `IS_SUPPORTED_AT_RUNTIME` instead of `acquire_best` — and retire that workaround once
  you upgrade to ≥ v0.17.0.
- **Wine / Proton: NVDA works only on cores before v0.16.2 (verified in source 2026-11-08).** From v0.16.2 (`83f73a8`)
  the NVDA backend also requires `RpcMgmtInqIfIds` to list the NvdaController interface (v0.18.3
  `source/backends/nvda.cpp:51-72, 164-169`). In Wine that call is a stub that always returns `RPC_S_INVALID_BINDING`
  (`dlls/rpcrt4/rpc_server.c:1650`), so NVDA never initializes there, even against a working substitute: Cthulhu's
  "Wine Access" (`cthulhu-wine-access.exe`) serves the real `NvdaCtlr.<session>.<desktop>` RPC endpoint in the prefix.
  v0.12.2 checks `testIfRunning` only, so D4 Assist 0.1.32 (Prismatoid 0.3.0 bundles v0.12.2 byte for byte) spoke through
  Wine Access under Proton. From v0.18.3 the Wine bridges (Orca, Speech Dispatcher, Spiel) are the way instead: placeholder
  DLLs from `prism-linux-x64.zip` `dynamic/release/wine/placeholders/` found by plain `LoadLibrary` (so beside the exe),
  else in `prism.dll`'s own folder (`source/optional_library.cpp:56-64`), and the `.dll.so` halves in a folder listed in
  `WINEDLLPATH` when the process starts (upstream `doc/src/wine-bridges.md`). The same tester's Orca bridge reached
  Cthulhu in Grimdark and in the D2R mod (1.0.12). **Under Wine, ask for the bridged backends by id, highest priority
  first, never `acquire_best`:** Proton's SAPI (`protontts` over eSpeak-NG, whose data the Steam runtime lacks) reports a
  voice and stays mute, and v0.18.3 ranks it 97 like Speech Dispatcher (Orca 100), so `acquire_best` returns SAPI
  whenever Orca's bridge can't load (D2R mod, 2026-11-08/09). Prismatoid: `AvailableBackends` + `AcquireBackend(id)`;
  PRISM's log at debug (`prism_set_log_handler`/`prism_set_log_level`, P/Invoke in Prismatoid 0.3.0) says why a bridge
  failed. `protontricks-launch` does not pass `WINEDLLPATH` on (same prefix: through it SAPI, through `proton` directly
  Orca): start the process with Proton itself. Orca/Cthulhu is the route heard so far; the Speech Dispatcher bridge also
  needs `libspeechd.so.2`, which SteamLinuxRuntime_4 lacks. D4 Assist ships v0.18.3 this way from 2026-11-08
  (`third_party/prism/README.md`, `PrismSpeechSink`); the D2R mod's `docs/proton.md` is the worked example for an
  injected mod.
- **Prismatoid vs. newer cores:** 0.3.0 passes `PrismConfig { version = 2 }`, which v0.18.3's `prism_init` accepts
  (versions 1-4). 0.4.0 (core v0.17.3) marshals its own 48-byte config from `prism_config_init`, which in v0.18.3
  (`PRISM_CONFIG_VERSION` 4) writes 56 bytes, so 0.4.0 must not drive a v0.18.3 `prism.dll`.
- **Official bindings — "Prismatoid"** (an in-progress effort upstream): **.NET** — `Prismatoid` on
  NuGet (v0.3.0 as of 2026-07, MPL-2.0) targets **.NET 10** → fine for standalone tools, NOT loadable
  inside BepInEx (net472/net6) or other injected .NET hosts, which keep the P/Invoke dynamic-load path
  above; **Python** — `Prismatoid` on PyPI (tooling/prototyping); **Godot** — in-tree in the prism
  repo. The C API above remains the universal path for injected mods.
- **Deploy**: copy `prism.dll` next to the mod DLL — nothing else (`tolk.dll` from the zip is NOT
  needed). That's all.
