# IL2CPP interop — generation, the Unity 6 corruption bug, and the fixer

IL2CPP compiles the game's C# to native code; there are no managed assemblies to reference. BepInEx 6
rebuilds fake ones: **Cpp2IL** reads `global-metadata.dat` + `GameAssembly.dll` and reconstructs the
type graph, then **Il2CppInterop** emits *interop assemblies* — managed stubs whose methods marshal
into native IL2CPP. Those stubs in `BepInEx/interop/` are what you `<Reference>` and what Harmony
patches.

Everything in this doc is about that generated layer failing.

---

## 1. What generation produces

| Path | What it is |
|---|---|
| `BepInEx/unity-libs/` | Unity's own managed assemblies, the base Il2CppInterop generates against. Ships with BepInEx. |
| `BepInEx/interop/` | The generated stubs: `Assembly-CSharp.dll`, `UnityEngine*.dll`, `Il2Cppmscorlib.dll`, plus every third-party assembly the game uses. Older bleeding-edge builds called this folder `unhollowed/`. |
| `BepInEx/cache/` | Generation cache. Delete it together with `interop/` whenever the game updates. |

Name mangling to expect: types that would collide with a real BCL type get an `Il2Cpp` prefix
(`Il2Cppmscorlib`, `Il2CppSystem.Collections.Generic.List<T>`), and game assemblies keep their name
(`Assembly-CSharp.dll`, `FundayFactory.UI.dll`). Generic collections you get back from game code are
`Il2CppSystem.*`, **not** `System.*` — they don't implement the BCL interfaces, so LINQ over them won't
compile.

---

## 2. Reading the game's code

The generated `interop/Assembly-CSharp.dll` is a normal managed assembly, so decompile it:

```bash
ilspycmd "<game>\BepInEx\interop\Assembly-CSharp.dll" -o "<somewhere>\game code" -p
```

Method **bodies are empty stubs** (they just call into native), but everything you need for modding is
there: class names, inheritance, field names/types, method signatures, enums, attributes. That's what
you patch against. When you need actual logic, that's a Ghidra job on `GameAssembly.dll`, or
`Il2CppDumper` + `cpp2il` for a symbol map.

Keep the decompiled tree next to the mod repo (git-ignored — it's the game's code) and regenerate it
after every game update.

---

## 3. The Unity 6 corruption bug (and the fix)

**Symptom.** BepInEx starts, generates everything, and then:

```
[Error : BepInEx] Unable to execute IL2CPP chainloader, no plugins will be loaded
System.BadImageFormatException: Duplicate type with name '<>O'
System.IO.FileNotFoundException: ... UnityEngine.CoreModule ... at SetupUnityLogging
```

No plugin ever loads. The game itself plays normally, which makes it look like *your* mod is broken.

**Cause.** On Unity 6, Il2CppInterop emits TypeDef rows with a *nested* visibility flag
(`NestedPublic`…) but **no matching `NestedClass` table entry**. The CLR then treats those rows as
top-level types. Several are compiler-generated closure caches named `<>O`, so as top-level types they
collide — and the CLR rejects the **entire assembly**. One bad row kills `UnityEngine.CoreModule.dll`,
and with it everything downstream.

Measured on DRG Survivor (Unity 6000.4.7f1): **19 orphan rows across 6 of 162 interop DLLs** —
`CoreModule`, `MarshallingModule`, `ParticleSystemModule`, `Physics2DModule`, `PhysicsModule`,
`UIElementsModule`. Not fixed upstream as of BepInEx be.785 / Il2CppInterop 1.5.3.

**Fix.** Binary-patch each orphan row in place:

1. Clear the visibility bits (`flags & ~0x7`) → the row becomes top-level `NotPublic`, which is
   consistent with having no declaring type.
2. Repoint its `Namespace` string index at a *different existing* offset in the `#Strings` heap, so
   each `<>O` ends up with a unique full name.

Metadata **tokens are never touched**, so every existing reference stays valid — this is the whole
reason it's safe. It rewrites bytes in the TypeDef table, nothing else.

**The tool:** [`templates/bepinex-interop-fixer/`](../../../templates/bepinex-interop-fixer/)
(~140 lines, `System.Reflection.Metadata` + raw byte writes; original lives in the DRG repo at
`tools/InteropFixer/`). It is game-agnostic — point it at any interop folder.

```bash
cd templates/bepinex-interop-fixer
dotnet run -- "<game>\BepInEx\interop"            # dry run: lists offending files/rows
dotnet run -- "<game>\BepInEx\interop" --apply    # patch in place
```

Dry-run output on a clean install is `Done. files patched: 0, rows patched: 0` — that's how you check
whether a *new* game is affected at all before assuming it is.

**Run it after every regeneration.** Deleting `interop/` for a game update means running the fixer
again, every time.

---

## 4. Order of operations, end to end

```
game update
  └─ check metadata version → upgrade BepInEx/core if needed   (versions-and-install §2)
     └─ delete BepInEx/interop + BepInEx/cache
        └─ steam_appid.txt → launch once → wait → close → delete steam_appid.txt
           └─ InteropFixer --apply
              └─ ilspycmd on interop/Assembly-CSharp.dll → re-read changed signatures
                 └─ dotnet build the mod
```

Skipping the fixer step is the failure that costs a whole afternoon, because the error message points
at Unity, not at the generator.

---

## 5. Interop assemblies are not normal assemblies

Things that hold for the whole generated layer, and that shape how you write patches:

- **Reflection over interop types is unreliable.** Names may be mangled, and generated members exist
  that have no IL2CPP counterpart. Prefer the strongly-typed generated API over
  `Type.GetType("…")`/string lookups.
- **`Il2CppSystem.*` ≠ `System.*`.** Convert explicitly at the boundary; don't LINQ over game
  collections.
- **Casting between interop types uses `TryCast<T>()`/`Cast<T>()`**, not C# `as`/`(T)`. Both call into
  native IL2CPP — read the crash rules in
  [bepinex-harmony-il2cpp-rules.md](bepinex-harmony-il2cpp-rules.md) before using them inside a patch.
- **Your own `MonoBehaviour`s must be registered** with `ClassInjector.RegisterTypeInIl2Cpp<T>()`
  before you `AddComponent<T>()`, and they need the IL2CPP constructor
  (`public T(IntPtr ptr) : base(ptr) { }`).
- **Some interop APIs crash natively and uncatchably.** Verified: `UnityEngine.AI.NavMesh`
  (`SamplePosition` / `CalculatePath`) on the Unity 6 build kills the process instantly — no exception,
  the log just stops mid-line. A native AccessViolation in IL2CPP cannot be caught by .NET, so
  "wrap it in try/catch" is not a mitigation; the only fix is not calling it (we replaced it with a
  managed A* over the game's own block grid). Treat an unexplained silent process death as
  *"the last interop call I added is native-fatal"*.

---

Related: [bepinex-versions-and-install.md](bepinex-versions-and-install.md) ·
[bepinex-harmony-il2cpp-rules.md](bepinex-harmony-il2cpp-rules.md) · [README](README.md)
