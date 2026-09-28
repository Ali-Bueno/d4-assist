# BepInEx — versions, builds and install

**The single most expensive mistake on a Unity target is picking the wrong BepInEx build.** A wrong
build does not produce a helpful error: the game either starts with no log at all, or logs
*"Unable to execute IL2CPP chainloader, no plugins will be loaded"* and runs normally without your mod.
Get this page right before writing a line of C#.

---

## 1. Which BepInEx do I need?

| Game runtime | BepInEx | Plugin base class | Notes |
|---|---|---|---|
| Unity **Mono** (`Managed/Assembly-CSharp.dll` exists) | **BepInEx 5** (stable) or 6 Mono | `BaseUnityPlugin` | Managed assemblies are directly referenceable/decompilable. The easy case. |
| Unity **IL2CPP** (`GameAssembly.dll` + `global-metadata.dat` exist) | **BepInEx 6 bleeding edge** (`BepInEx.Unity.IL2CPP`) | `BasePlugin` | Needs generated *interop* assemblies. Everything below is about this case. |

Tell them apart in 5 seconds: if the game folder has `GameAssembly.dll`, it is IL2CPP. If it has
`<Game>_Data\Managed\Assembly-CSharp.dll`, it is Mono.

> There is no stable BepInEx 6 release. IL2CPP work **always** means a bleeding-edge build. That is
> normal, not a red flag.

---

## 2. The IL2CPP metadata version is what actually decides the build

BepInEx 6 does not read IL2CPP itself — it bundles **Cpp2IL**, which parses `global-metadata.dat`.
Each Cpp2IL version supports a range of *metadata versions*. If the game's metadata version is newer
than the bundled Cpp2IL, BepInEx cannot generate anything and the chainloader dies.

**Verified data points** (do not extrapolate a table you haven't checked):

| Metadata | Unity | Needs | Bundled Cpp2IL |
|---|---|---|---|
| v23–v31 | up to Unity 2022 / 2023 | BepInEx **6.0.0-be.738** works | (be.738's bundled Cpp2IL) |
| **v39** | **Unity 6000.x** (verified on 6000.4.7f1) | BepInEx **6.0.0-be.785** or newer | `2022.1.0-development.1452` |

**Read the game's metadata version yourself instead of guessing** — `BepInEx/global-metadata.dat`'s
sibling, `<Game>_Data/il2cpp_data/Metadata/global-metadata.dat`, starts with:

- bytes `0..3`: magic `AF 1B B1 FA` (`0xFAB11BAF`)
- bytes `4..7`: **metadata version** as a little-endian int32

```powershell
$b = [IO.File]::ReadAllBytes("<game>\<Game>_Data\il2cpp_data\Metadata\global-metadata.dat")[0..7]
'magic=0x{0:X8} version={1}' -f [BitConverter]::ToUInt32($b,0), [BitConverter]::ToInt32($b,4)
```

If the version is above anything a released build handles, take the **newest** bleeding-edge build and
check its Cpp2IL. Rule of thumb: newest game → newest build, no exceptions.

---

## 3. Where to download bleeding-edge builds

Bleeding-edge builds are **not** on GitHub Releases. They live on the build server:

- **https://builds.bepinex.dev/projects/bepinex_be** → pick the build number → artifact
  `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.<N>+<shorthash>.zip`

Pick the artifact that matches **runtime + architecture**: `Unity.IL2CPP` vs `Unity.Mono`,
`win-x64` vs `win-x86`. A x86 game with an x64 BepInEx simply does nothing.

### The build we validated (Unity 6)

| Component | Version |
|---|---|
| BepInEx | `6.0.0-be.785` (commit `6abdba47eeebe08552282e7a58ef0f4a9ab60b62`) |
| Cpp2IL | `2022.1.0-development.1452` |
| Il2CppInterop | `1.5.3` |
| Verified against | DRG Survivor, Unity `6000.4.7f1`, metadata v39 |

Shortcut when the build server is down or the build got pruned: copy `BepInEx/core/` from a known-good
install. Ours lives in the DRG mod repo's shipped package
(`DRGAccess release full/BepInEx/`) and in the game folder
`D:\games\steam\steamapps\common\Deep Rock Survivor\BepInEx\`.

**Check what you actually have** (never trust the folder name):

```powershell
(Get-Item "<game>\BepInEx\core\BepInEx.Core.dll").VersionInfo.ProductVersion   # 6.0.0-be.785+6abdba4...
(Get-Item "<game>\BepInEx\core\Cpp2IL.Core.dll").VersionInfo.ProductVersion    # 2022.1.0-development.1452
```

---

## 4. Install layout (IL2CPP, Windows)

Unzip into the **game root** (next to the exe). You get:

```
<game>/
├── winhttp.dll             ← the proxy DLL Doorstop injects through
├── doorstop_config.ini     ← Doorstop settings (enabled=true, target_assembly=…BepInEx.Unity.IL2CPP.dll)
├── dotnet/                 ← the .NET runtime BepInEx 6 IL2CPP runs on (must ship with it)
├── .doorstop_version       ← (some builds)
└── BepInEx/
    ├── core/               ← BepInEx + Cpp2IL + Il2CppInterop + Harmony
    ├── plugins/            ← your mod goes here (own subfolder per mod)
    ├── patchers/
    ├── config/             ← BepInEx.cfg, per-plugin .cfg
    ├── unity-libs/         ← Unity managed assemblies used as the base for generation
    ├── interop/            ← GENERATED on first launch (older builds: unhollowed/)
    ├── cache/              ← GENERATED
    └── LogOutput.log       ← your first stop, always
```

Everything except `plugins/`, `interop/` and `cache/` comes from the zip.

### First launch

Launch once and **wait** — first-run interop generation takes minutes on a big game and looks like a
hang. Then confirm the header of `BepInEx/LogOutput.log`:

```
[Message: Preloader] BepInEx 6.0.0-be.785 - DRG Survivor (05/06/2026 4:18:04)
[Info   :   BepInEx] Running under Unity 6000.4.7f1
[Info   :   BepInEx] Runtime version: 6.0.7
```

If the log doesn't exist at all: Doorstop never loaded → wrong architecture, an anticheat/launcher
wrapper, or `winhttp.dll` blocked. If the log exists but there's a `Cpp2IL` / metadata error → wrong
build (§2). If it ends in `BadImageFormatException` / *"no plugins will be loaded"* → corrupt interop,
see [bepinex-il2cpp-interop.md](bepinex-il2cpp-interop.md).

### Steam relaunch race (do this before first launch)

Steam games relaunch themselves through Steam, so the process you started dies and a second one starts
— while interop generation is mid-write. That corrupts the output or half-generates it. Put a
`steam_appid.txt` containing just the appid next to the exe, launch **directly from the exe**, let
generation finish, close, then **delete the file** (it changes how the game reports ownership/overlay).

---

## 5. Updating BepInEx / after a game update

A game update can change the metadata version, the Unity version, and every class you patched.

1. Read the new Unity/metadata version (§2). Upgrade `BepInEx/core/` if needed.
2. **Delete `BepInEx/interop/` and `BepInEx/cache/`** — they are stale and BepInEx will not always
   notice.
3. `steam_appid.txt` → launch once → wait for regeneration → close → delete `steam_appid.txt`.
4. Repair the interop if the game is Unity 6 ([bepinex-il2cpp-interop.md](bepinex-il2cpp-interop.md) §3).
5. Re-decompile the interop `Assembly-CSharp.dll` and re-check every patched signature — Unity 6
   silently widened types (`int`→`long`, `float`→`double`) and moved UI fields between classes, which
   compiles fine and fails at runtime.
6. Rebuild the mod.

**Ship the whole thing.** If you distribute the mod, an end user whose BepInEx regenerates interop will
regenerate the *corrupt* one. For a Unity 6 target, the "full" package must contain the validated
`BepInEx/core/` **and** the repaired `interop/` — a plugin-only zip is not enough.

---

Related: [bepinex-il2cpp-interop.md](bepinex-il2cpp-interop.md) ·
[bepinex-project-setup.md](bepinex-project-setup.md) · [README](README.md)
