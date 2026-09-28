# Harmony on IL2CPP — the rules that stop the crashes

Harmony works on IL2CPP through `Il2CppInterop.HarmonySupport`: it detours the **native** method behind
the interop stub. That detail is the source of every rule below. A bad patch here does not throw a
catchable exception — it takes the process down with an `AccessViolationException` that .NET 6 cannot
catch, usually *before* your log line gets flushed.

All of these were paid for in crashes on shipped mods.

---

## 1. Only patch methods **declared directly** on the target class

Patching an inherited Unity/IL2CPP base method (`Selectable.OnSelect`,
`UIFormTabbedPageAlbum.OnPageChanged`, …) makes the detour fire during early IL2CPP initialisation,
before the types involved are ready → **fatal AccessViolationException**, silent failure to open.

Check the decompiled class: if the method isn't written there, don't patch it. Patch the concrete
subclass's own declared method instead (`Setup`, `Show`, `SetupOverclock`, `AdvanceFlow`…).

Corollary: `[HarmonyPatch(typeof(X), nameof(X.SetVisibility))]` **fails to resolve** when `X` merely
inherits `SetVisibility` — Harmony's `DeclaredMethod` doesn't see inherited members. You get a silent
no-op patch, not an error.

## 2. Never use `TryCast<T>()` or `GetComponentInChildren<T>()` inside a base-class patch

Same reason: they call into native IL2CPP at a moment where the type system may not be up. Safe inside
a patch on a *specific game class*; fatal inside a patch on a base class. When you need to discriminate
in a risky context, compare `GetIl2CppType().Name` strings instead of casting.

## 3. Postfix parameter names must match the original **exactly**

Harmony binds prefix/postfix parameters by name. If the game method declares `bool value`, a postfix
with `bool toggle` won't bind (and can misbind). Capturing a *subset* of parameters is fine, as long as
each name matches. Safest of all: capture nothing and read state off `__instance`.

```csharp
[HarmonyPostfix]
static void Postfix(UISliderToggle __instance)   // read isOn from the instance
```

## 4. Overloads need `TargetMethod()`

`[HarmonyPatch(typeof(X), nameof(X.Setup))]` is ambiguous when `Setup` has two overloads. Resolve it
explicitly:

```csharp
static MethodBase TargetMethod() =>
    typeof(UILevelUpForm).GetMethods().First(m => m.Name == "Setup" && m.GetParameters().Length == 2);
```

## 5. **Native-to-native calls bypass Harmony entirely** — the big one

If game method `A` calls method `B` internally, that call happens *inside native code* and never goes
through `B`'s managed interop stub. Your patch on `B` will never fire when `A` is the caller — while
firing normally when *you* call `B`. This produces the maddening "the patch works sometimes" bug.

- Patch the **managed entry point** the engine/UI actually invokes: patch
  `StepSelectorBase.IncreaseIndex`/`DecreaseIndex`, not the `SetIndex` they call natively.
- Some things have **no** patchable entry point (e.g. a form's `Show()` invoked native-to-native). Hook
  a neighbouring event instead — we detect an overlay closing via the overlay's own `HidePopup` /
  `SetVisibility(false)` rather than the parent form's un-hookable `Show()`.
- **For mission-critical state, poll the real state as the source of truth and treat patches as
  accelerators**, with a dedup guard so patch + poll never double-announce. That architecture survived
  three separate IL2CPP surprises where the patch silently stopped firing.

## 6. Some detours corrupt state even when empty

Verified: `UISettingsPageVideo.OnToggleVsync` crashes the game with a *completely empty* postfix — the
detour itself corrupts something. There is no workaround other than not patching that method (we read
that toggle by polling `isOn` each frame instead).

If a patch crashes with an empty body, stop debugging your code: the method is unpatchable. Move on to
a different hook point.

## 7. Registering the patch class

```csharp
public override void Load()
{
    var harmony = new HarmonyLib.Harmony("com.example.mymod");
    harmony.PatchAll();                       // or PatchAll(typeof(SpecificPatch)) while bisecting
}
```

Wrap `PatchAll` per patch class while bringing a mod up: one unresolvable `[HarmonyPatch]` attribute
throws at registration and takes **all** your other patches with it. Bisecting by class is the fastest
way to find which target signature the game update changed.

Harmony is already referenced by BepInEx — do **not** add a `PackageReference` for it.

---

## 8. Debugging checklist when a patch "does nothing"

In this order — it's cheapest first, and each one has actually been the answer:

1. Is the class registration even running? (Log in `Load()`.)
2. Did `PatchAll` throw because of *another* patch in the same assembly?
3. Is the method **declared** on that type, or inherited? (§1)
4. Is it an overload you didn't disambiguate? (§4)
5. Is the real caller native → the detour never runs? (§5)
6. Did the game update change the signature? Re-decompile the interop and compare — Unity 6 widened
   many types (`int`→`long`, `float`→`double`) and moved fields between UI classes, all of which
   compile fine and misbehave at runtime.

---

Related: [bepinex-il2cpp-interop.md](bepinex-il2cpp-interop.md) ·
[bepinex-runtime-gotchas.md](bepinex-runtime-gotchas.md) · [README](README.md)
