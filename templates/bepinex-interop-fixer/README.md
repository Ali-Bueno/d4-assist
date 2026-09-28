# bepinex-interop-fixer

Repairs the **corrupt IL2CPP interop assemblies** that Il2CppInterop generates for **Unity 6** games, so
BepInEx can load plugins at all. Game-agnostic: point it at any `BepInEx\interop` folder.

**Symptom it fixes** — BepInEx starts, generates interop, then:

```
[Error : BepInEx] Unable to execute IL2CPP chainloader, no plugins will be loaded
System.BadImageFormatException: Duplicate type with name '<>O'
System.IO.FileNotFoundException: ... UnityEngine.CoreModule ... at SetupUnityLogging
```

**Cause** — some TypeDef rows carry a *nested* visibility flag but have no `NestedClass` table entry.
The CLR treats them as top-level; several are compiler-generated `<>O` closure caches, so their full
names collide and the **whole assembly** is rejected. Not fixed upstream as of BepInEx `6.0.0-be.785` /
Il2CppInterop `1.5.3`.

**What it does** — for each orphan row: clears the visibility bits (→ top-level `NotPublic`) and
repoints its `Namespace` string index at a different existing `#Strings` offset so the full name is
unique. **Metadata tokens are never modified**, so every existing reference stays valid.

## Usage

```bash
dotnet run -- "D:\games\steam\steamapps\common\<Game>\BepInEx\interop"            # dry run: report only
dotnet run -- "D:\games\steam\steamapps\common\<Game>\BepInEx\interop" --apply    # patch in place
```

A clean install reports `Done. files patched: 0, rows patched: 0` — use the dry run to check whether a
new target is affected before assuming it is.

**Run it after every interop regeneration** (game update, deleted `interop/` folder). Requires .NET 6+
SDK; only dependency is `Mono.Cecil` (metadata reading is `System.Reflection.Metadata`).

Reference measurement (DRG Survivor, Unity 6000.4.7f1): 19 orphan rows across 6 of 162 DLLs —
`CoreModule`, `MarshallingModule`, `ParticleSystemModule`, `Physics2DModule`, `PhysicsModule`,
`UIElementsModule`.

Docs: [`reference/engines/bepinex/bepinex-il2cpp-interop.md`](../../reference/engines/bepinex/bepinex-il2cpp-interop.md)
· original in the DRG Survivor mod repo at `tools/InteropFixer/`.
