# BmSDK — install, build, and compatibility

> Everything operational: what a player installs, what a developer builds, how releases are made, and
> the compatibility rules that will bite. Architecture is in [architecture](bmsdk-architecture.md).

---

## 1. Requirements

| | Arkham City | Arkham Knight |
|---|---|---|
| Architecture | **x86 (32-bit)** | **x64** |
| .NET | **.NET 10**, shipped **self-contained** in the release — no machine-wide install needed | same |
| Stores | Steam, Epic, GOG | **Steam only** (Epic/GOG deferred) |
| Extra steps | Steam/GOG need the **compatibility patch** (an EXE swap); may need launching `Binaries\Win32\BatmanAC.exe` directly | ships `steam_appid.txt` (`208650`) so it can run outside Steam |
| Loader | Ultimate ASI Loader `dinput8.dll` (x86) | same (x64 build) |

## 2. What a player installs

The release zip contains two folders that are copied into the game install root
(e.g. `C:\Program Files\Epic Games\BatmanArkhamCity`):

```
Binaries\Win32\               (AK: Win64\)
  dinput8.dll                 Ultimate ASI Loader (the injector)
  plugins\BmSDK.Host.asi      native host
  sdk\BmSDK.dll + ~70 MB      the SDK + a self-contained .NET 10 runtime
BmGame\
  Scripts\                    <- players drop .cs mods here
  Mods\ModTemplate\           example mod (mod.toml + scripts\)
  ScriptsDev\                 Visual Studio project for editing/debugging scripts
    ScriptsDev.slnx, ScriptsDev.csproj
```

**Upgrade rule:** replace `sdk\` wholesale. A stale `sdk\` folder from an older release breaks hostfxr
discovery — merging over it is the classic failure.

**Distribution format for our mod:** a folder of `.cs` files (+ `mod.toml`), dropped into
`BmGame\Mods\ArkhamAccess\`. No compiled artifact, no packaging. The only binary we ship is the native
speech DLL, which must sit where the process can `LoadLibrary` it — next to the executable
(`Binaries\Win32\`) or in `sdk\`, and must match the process bitness (see
[accessibility patterns](bmsdk-accessibility-patterns.md) §1).

## 3. Building the SDK from source

Only needed for nightly builds or contributing — mod authors use a release.

**Prerequisites:** Visual Studio (the docs specify **VS 2026**; the projects use toolset **v145**) with
both the **.NET Desktop development** and **Desktop development with C++** workloads, the .NET 10 SDK,
and a vanilla game build (Epic preferred; Steam users apply the compat patch first).

```bash
git clone https://github.com/Team-BmSDK/BmSDK-AC.git      # or -AK
```

Then, per the official guide:

1. **Import the game into the solution.** Copy the *game installation contents* into the repo's `bin\`
   directory (so `bin\` contains `Binaries`, `Engine`, `BmGame`…), and put the Ultimate ASI Loader
   `dinput8.dll` in `bin\Binaries\Win32`.
2. Open `BmSDK.slnx`, then in the VS terminal: `vcpkg integrate install`
   (and if NuGet is unresolved: `dotnet nuget add source "https://api.nuget.org/v3/index.json" --name "nuget.org"`).
3. **Generate the SDK** (once, and again whenever the generator changes):
   build `BmSDK.Generator` → run `bin\Binaries\Win32\BatmanAC.exe` → wait for full startup →
   press **`P`** with the window focused → the game closes itself. This writes `src\BmSDK\Generated\`.
   *(`Generated/` is not in the repo — it's large and deterministic.)*
4. **Build `BmSDK.Host`** — first delete `bin\Binaries\Win32\plugins\BmGame.Generator.*` so the generator
   doesn't run again.
5. **Build `BmSDK`.**

Or use the script:

```powershell
./Build.ps1 Prebuild [Debug|Release]   # generator only
./Build.ps1 Build    [Debug|Release]   # generator + BmSDK + Host
./Build.ps1 Clean
```

`Build.ps1` locates Visual Studio via `vswhere.exe`, enters the dev shell forcing the right arch
(`-arch=x86` for AC / x64 for AK), and MSBuilds `BmSDK.slnx` with `-p:Platform=x86|x64`, emitting to
`bin\Binaries\Win32|Win64\{plugins,sdk}\`.

**Project settings worth knowing** (they're what a mod is compiled against):
`net10.0-windows7.0`, `RuntimeIdentifier win-x86` (AC) / `win-x64` (AK), `PublishSelfContained`,
`EnableDynamicLoading`, `AllowUnsafeBlocks`, `LangVersion 14`; native side C++20, Unicode, static CRT,
vcpkg `detours` static-linked; formatting enforced by **CSharpier 1.2.6** (`dotnet-tools.json`).
Note that `Release|Win32` for the host is configured with `UseDebugLibraries`, `_DEBUG` and the debug CRT
— asserts survive into shipped builds.

## 4. Developing a mod (the normal path)

1. Install a release into the game.
2. Open `<GameDir>\BmGame\ScriptsDev\ScriptsDev.slnx` in Visual Studio.
3. Write `.cs` files under `Scripts\` or `Mods\<name>\scripts\`.
4. **F5** launches the game with scripts loaded and the debugger attached (breakpoints + `Debug.Log`).
5. Edit and save while it runs — hot reload picks it up in ~500 ms.

The project globs `Scripts/**/*.cs` and `Mods/**/*.cs`, targets `net10.0-windows7.0` self-contained,
C# 14, implicit usings, nullable, unsafe allowed, and auto-imports `BmSDK.Framework` and `Tomlyn.Model`.

> **Reference-set constraint:** at runtime, mods are compiled by Roslyn against
> `Basic.Reference.Assemblies.Net100` + `BmSDK` + `MoreLinq` + `Tomlyn` **only**. You cannot add a NuGet
> package to a mod. Extra capability must come through P/Invoke or a fork of the SDK.

## 5. Release process (for reference)

GitHub Actions (`.github/workflows/build-release.yml`), version managed by release-please:

- `build-sdk`: `dotnet publish` (Release, self-contained, x86/x64) → `publish/Binaries/Win32|64/sdk/`
- `build-host`: MSBuild the `.vcxproj` (vcpkg, C++20) → `publish/Binaries/Win32|64/plugins/*.asi`
- `create-package`: merge artifacts, download the matching Ultimate ASI Loader `dinput8.dll` from
  ThirteenAG's releases, add `README.md`, `LICENSE.md`, example scripts, the ScriptsDev project and the
  mod template, then zip.

Releases are drafted automatically; `CHANGELOG.md` is maintained by release-please.

## 6. Compatibility — the rules that will bite

1. **The SDK is pinned to one exact game executable build.** All offsets are hardcoded RVAs with no
   signature scanning and no version check ([architecture](bmsdk-architecture.md) §4). A game patch
   breaks it silently and violently.
2. **The compatibility patch replaces `BatmanAC.exe`.** Steam/GOG owners must apply it; this also means a
   Steam update can revert it. Any install guide we write for blind users must cover re-applying it.
3. **AC and AK SDKs are source-compatible, never binary-compatible** — different offsets in the generated
   code ([generator & API](bmsdk-api-and-generator.md) §4). Build per game.
4. **Regenerating the SDK requires running the game**, so contributors need the same pinned build.
5. **Anti-cheat/DRM is not a factor** — these are UE3 titles with no Denuvo/EAC in the supported builds
   (GFWL was stripped from the GOTY/Epic AC builds).

## 7. Known issues (as of the studied commit)

**Arkham City** (3 open):
- Character transitions are broken (#14, bug)
- Request to wrap `GWorld->SingleLineCheck` / `MultiLineCheck` (#55, enhancement) — relevant to us: these
  are the lower-level trace primitives a navigation feature would want
- `Debug.Log` should output to the `-log` window (#36, feature) — relevant to us: logging is
  debugger-only today

**Arkham Knight**: no open issues; Epic/GOG support explicitly deferred.

Recent notable fixes (v0.17.2): scripts not loading after a host update; `OnKeyDown` not firing when
launched through Steam — worth remembering when our hotkeys mysteriously don't fire.

## 8. Legal

MIT (© 2026 Eli Kramer). The project carries an explicit notice that it is not associated with Warner
Bros. Games or Rocksteady Studios. Our mod, being source `.cs` files distributed separately, adds no
game assets and ships no game binaries — keep it that way (playbook §10: don't commit unnecessary
binaries).

## 9. Where things live

| Thing | Location |
|---|---|
| Docs site | <https://bmsdk.dev/> (Introduction, First mod, Redirecting functions, Script components, Building from source) |
| AC repo | <https://github.com/Team-BmSDK/BmSDK-AC> |
| AK repo | <https://github.com/Team-BmSDK/BmSDK-AK> |
| `etkramer/BmSDK` | the **same repository** as BmSDK-AC (identical commit) — the personal/original remote |
| Community | Arkham Workshop Discord (linked from the docs site) |
| Local clones | `D:\code\unreal\bmsdk\BmSDK-AC`, `D:\code\unreal\bmsdk\BmSDK-AK` |
| Asset explorers mentioned by the docs | UPK Explorer, UE Explorer (for looking inside `.upk` packages) |
