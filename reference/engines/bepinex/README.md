# BepInEx (Unity — Mono & IL2CPP)

Platform knowledge for modding Unity games via **BepInEx** + **Harmony** in C#. Sourced from three
shipped accessibility mods: Core Keeper Access (Unity Mono), Megabonk Access (IL2CPP, Unity 2023.2) and
DRG Survivor Access (IL2CPP, **Unity 6**).

**Key facts:**
- **The runtime decides everything.** `GameAssembly.dll` present → IL2CPP → BepInEx **6 bleeding edge**,
  `net6.0`, `BasePlugin`. `<Game>_Data\Managed\Assembly-CSharp.dll` present → Mono → BepInEx 5 (or 6
  Mono), `net472`, `BaseUnityPlugin`.
- **IL2CPP has no managed assemblies**: BepInEx generates *interop* stubs into `BepInEx/interop/` on
  first launch. Those stubs are what you reference, decompile and patch — and on Unity 6 they come out
  **corrupt** and must be repaired, or no plugin ever loads.
- **The bundled Cpp2IL version — not the BepInEx version — is what must match the game's IL2CPP
  metadata version.** Unity 6 (metadata v39) needs **be.785+**.
- Screen reader path is plain **C# P/Invoke → PRISM** (no bridge DLL needed); the `Prismatoid` NuGet
  binding targets .NET 10 and does **not** load inside BepInEx.
- Harmony works on IL2CPP by detouring **native** methods — which is why patching an inherited base
  method, or expecting a patch to fire on a native-to-native call, kills the process or silently does
  nothing.

## Docs

| File | Purpose |
|---|---|
| **`bepinex-versions-and-install.md`** | **READ FIRST — pick the right build or nothing works**: Mono vs IL2CPP, how the IL2CPP metadata version decides the build, where bleeding-edge builds live, the validated Unity 6 combo, install layout, first-launch + Steam relaunch race, what to redo after a game update |
| **`bepinex-il2cpp-interop.md`** | **The generated layer**: what interop is, decompiling it for reference, the **Unity 6 corruption bug** (`Duplicate type with name '<>O'` → "no plugins will be loaded") and the byte-patch fixer, plus how interop types differ from real ones (`Il2CppSystem`, `TryCast`, native-fatal APIs) |
| **`bepinex-harmony-il2cpp-rules.md`** | **Patch without crashing**: only patch declared methods, no `TryCast`/`GetComponentInChildren` in base-class patches, exact parameter names, overload disambiguation, **native-to-native calls bypass Harmony**, unpatchable methods, the "my patch does nothing" checklist |
| `bepinex-project-setup.md` | csproj per runtime (packages, BepInEx NuGet feed, interop references, aliases), plugin entry point, `dotnet build` auto-deploy, what file goes where at runtime |
| `bepinex-runtime-gotchas.md` | Runtime traps: never find objects by name, stale game-state objects after a retry, per-build API surface differences, obfuscated fields → Transform-based UI reading, `ClassInjector`, DI-injected non-MonoBehaviours, NAudio handle limits, stale-text speech |
| `accessibility-patterns.md` | Mod architecture for accessibility: PRISM P/Invoke + the shared adapter, folder layout, UI reading and navigation fixes, gameplay audio rules, bring-up order for a new target |

## Code templates (NOT in this folder)

| Template | What it is |
|---|---|
| [`templates/bepinex-interop-fixer/`](../../../templates/bepinex-interop-fixer/) | Game-agnostic CLI that repairs Unity 6 interop assemblies (`--apply`). Mandatory step after every interop regeneration on Unity 6. |

## Known-good combinations

| Game | Unity | Runtime | BepInEx | Notes |
|---|---|---|---|---|
| Core Keeper | Mono | Mono | `BepInEx.Unity.Mono 6.0.0-be.*`, net472 | Managed DLLs referenced from `_Data\Managed` |
| Megabonk | 2023.2.22f1 | IL2CPP | `BepInEx.Unity.IL2CPP 6.0.0-be.*`, net6.0 | Obfuscated fields; `FindObjectsOfType` unavailable |
| DRG Survivor | 6000.4.7f1 | IL2CPP (metadata v39) | **`6.0.0-be.785`** + interop fixer | NavMesh interop is native-fatal |
