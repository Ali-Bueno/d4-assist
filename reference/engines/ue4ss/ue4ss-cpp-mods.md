# UE4SS C++ mods

UE4SS mods don't have to be Lua. A **C++ mod** is a DLL loaded by UE4SS itself, subclassing
`RC::CppUserModBase`, built and shipped separately from `Mods/<Name>/dlls/main.dll`. This doc covers the
base class, the build, installation, the C++ API surface relevant to accessibility, and — the operative
question for us — **when it's actually worth leaving Lua**.

> Everything else in this folder assumes Lua (the reference transport per
> [`../../screen-reader-integration/README.md`](../../screen-reader-integration/README.md)). Read this
> doc only when a specific mod section needs what C++ gives that Lua doesn't (see the final section).

---

## 1. The base class — `RC::CppUserModBase`

Header: `UE4SS/include/Mod/CppUserModBase.hpp`. Every C++ mod subclasses it and overrides the lifecycle
hooks it needs; unused ones stay as their no-op default.

```cpp
virtual auto on_program_start() -> void {}      // UE4SS is starting; nothing Unreal-side exists yet
virtual auto on_dll_load(StringViewType dll_name) -> void {}
virtual auto on_unreal_init() -> void {}        // the 'Unreal' namespace is only usable FROM HERE ON
virtual auto on_ui_init() -> void {}            // GUI ready; call UE4SS_ENABLE_IMGUI() here (see §5)
virtual auto on_update() -> void {}             // tick
virtual auto on_cpp_mods_loaded() -> void {}
virtual auto on_lua_start(StringViewType mod_name, LuaMadeSimple::Lua& lua,
                          LuaMadeSimple::Lua& main_lua, LuaMadeSimple::Lua& async_lua,
                          LuaMadeSimple::Lua* hook_lua) -> void {}
virtual auto on_lua_stop(StringViewType mod_name, LuaMadeSimple::Lua& lua,
                         LuaMadeSimple::Lua& main_lua, LuaMadeSimple::Lua& async_lua,
                         LuaMadeSimple::Lua* hook_lua) -> void {}
virtual auto render_tab() -> void {}            // LEGACY — use register_tab() (§5) instead
```

There are also deprecated overloads of `on_lua_start`/`on_lua_stop` taking
`std::vector<Lua*>& hook_luas` — don't use them, the signatures above are current.

**Ordering rule that matters for accessibility code:** don't touch `Unreal::` anything before
`on_unreal_init()` fires — object discovery, reflection, hooks all assume Unreal is up. `on_program_start`
is UE4SS-only setup (config, logging).

### Metadata and inherited helpers

Constructor fields to fill in: `ModName`, `ModVersion`, `ModDescription`, `ModAuthors`,
`ModIntendedSDKVersion`.

Protected methods inherited from `CppUserModBase`:
- `register_tab(name, RenderFunctionType)` — adds an ImGui tab, auto-cleaned on mod destruction (§5).
- `register_keydown_event(Key, callback, custom_data = 0)`.

---

## 2. Exporting the mod

Every C++ mod DLL must export exactly these two `extern "C"` entry points — UE4SS calls them to
construct/destroy the mod instance:

```cpp
#define MY_AWESOME_MOD_API __declspec(dllexport)
extern "C" {
    MY_AWESOME_MOD_API RC::CppUserModBase* start_mod() { return new MyAwesomeMod(); }
    MY_AWESOME_MOD_API void uninstall_mod(RC::CppUserModBase* mod) { delete mod; }
}
```

---

## 3. Build

### CMake (official — `UE4SS-RE/UE4SSCPPTemplate`)

Root `CMakeLists.txt` does `add_subdirectory(RE-UE4SS)` + `add_subdirectory(MyAwesomeMod)`; the mod's own
`CMakeLists.txt` is:
```cmake
add_library(TARGET SHARED dllmain.cpp)
target_link_libraries(TARGET PUBLIC UE4SS)
```

Prerequisite: `git submodule update --init --recursive` inside `RE-UE4SS` — its Unreal source submodule
needs an Epic Games account linked to the GitHub account cloning it.

```
cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=Game__Shipping__Win64
cmake --build build
```
or, with Visual Studio:
```
cmake -B build -G "Visual Studio 17 2022"
cmake --build build --config Game__Shipping__Win64
```

The template's `new_mod_setup.bat` pins the `RE-UE4SS` submodule to the commit of the **last release**,
for ABI compatibility with the UE4SS build you're targeting — don't float it to `main` casually.

### xmake (seen in real mods — shorter)

```lua
target(projectName)
add_rules("ue4ss.mod")
add_includedirs("include")
add_files("*.cpp")
```

Pick whichever your target repo already uses; both produce the same shared-library artifact.

---

## 4. Installing the built DLL

1. The build outputs `MyMods\Binaries\<Config>\MyAwesomeMod\MyAwesomeMod.dll`.
2. Copy it to `<Game>\...\Mods\MyAwesomeMod\dlls\main.dll` (UE4SS also accepts `<ModName>.dll` in that
   folder, but `main.dll` takes priority if both are present).
3. Enable it in `Mods\mods.txt`: add `MyAwesomeMod : 1` **above** the
   `; Built-in keybinds, do not move up!` / `Keybinds : 1` section. File order in `mods.txt` is load
   order.
4. Verify: launch the game and check the UE4SS console for `"MyAwesomeMod says hello"` in blue, printed
   before the object scan begins.

---

## 5. C++ API surface relevant to accessibility mods

### Finding objects (only after `on_unreal_init()`)

```cpp
UObjectGlobals::StaticFindObject<UObject*>(nullptr, nullptr, STR("/Script/CoreUObject.Object"));
```
`UObjectGlobals` also has C++ equivalents of `FindObject`/`FindObjects`/`FindFirstOf`/`FindAllOf`/
`StaticConstructObject`/`ForEachUObject` — the same operations the Lua API exposes, listed in
[`ue4ss-reflection-cookbook.md`](ue4ss-reflection-cookbook.md).

> **The official `cpp-api.html` page is marked "incomplete."** For real signatures, read the headers
> directly: `Re-UE4SS/UEPseudo`, under `include/Unreal`.

### Reading/writing properties without hardcoding structs (this is the recommended path)

Matches the "never hardcode offsets" rule (CLAUDE.md) — resolve properties by name through reflection
instead of a hand-written struct layout:

```cpp
FVector* p = Obj->GetValuePtrByPropertyNameInChain<FVector>(STR("SpawnLocation")); // searches whole hierarchy
Obj->GetValuePtrByPropertyName<T>(name);                                           // innermost class only
```

Nested structs: `GetPropertyByNameInChain` → `FStructProperty::GetStruct()` →
`ContainerPtrToValuePtr<T>(instance)`, chainable for deeper nesting. Writing is `*ptr = value;` once you
have the typed pointer.

### Hooks

```cpp
#include <Hooks.hpp>
Unreal::Hook::RegisterInitGameStatePostCallback(&cb);  // cb: auto(RC::Unreal::AGameModeBase* Context) -> void
auto hook_id = UObjectGlobals::RegisterHook(STR("/Script/Pkg.Class:Function"), PreFn, PostFn, nullptr);
auto PreFn(UnrealScriptFunctionCallableContext& Context, void* CustomData) -> void {
    auto self = Context.Context;
}
```
`Unreal::Hook::Register<Event>PreCallback`/`PostCallback` exist for the same event set the Lua API
exposes: `InitGameState`, `BeginPlay`, `ProcessConsoleExec`, `CallFunctionByNameWithArguments`,
`ULocalPlayerExec`. The C++ pre/post split gives you the params struct directly, which is the thing Lua
can't do cleanly (see §7, reason 4).

### BP macros — calling UFunctions by constructing the params struct

For calling a Blueprint/native UFunction from C++ without the game's own header for it:
`UE_BEGIN_SCRIPT_FUNCTION_BODY` / `UE_BEGIN_NATIVE_FUNCTION_BODY` (locate the UFunction by full name,
throw if missing), `UE_SET_STATIC_SELF` (static functions, passes the CDO), `UE_COPY_PROPERTY[_CUSTOM]`
(`_CUSTOM` variant when the UE property name isn't a valid C++ identifier), `UE_COPY_STRUCT_PROPERTY_BEGIN`
+ `UE_COPY_STRUCT_INNER_PROPERTY`, `UE_COPY_OUT_PROPERTY[_CUSTOM]` (wrapper must take the argument by
reference), `UE_COPY_VECTOR`, `UE_COPY_STL_VECTOR_AS_TARRAY`, `WITH_OUTER(TMap, FName, int)`,
`UE_CALL_FUNCTION` / `UE_CALL_STATIC_FUNCTION`, `UE_RETURN_PROPERTY[_CUSTOM]` / `UE_RETURN_VECTOR` /
`UE_RETURN_STRING[_CUSTOM]`. Canonical usage examples live in `AActor.cpp` in the UE4SS repo
(`K2_DestroyActor`, `K2_SetActorRotation`, `UGameplayStatics::FindNearestActor`).

### A GUI tab of your own

```cpp
auto on_ui_init() -> void override { UE4SS_ENABLE_IMGUI() }  // REQUIRED or the game crashes the instant a tab draws

MyAwesomeMod() : CppUserModBase() {
    register_tab(STR("My Test Tab"), [](CppUserModBase* instance) {
        ImGui::Text("This is the contents of the tab");
        auto mod = dynamic_cast<MyAwesomeMod*>(instance);
        if (!mod) return;
        mod->render_some_stuff(mod->m_private_number);
    });
}
```
- `register_tab` cleans itself up automatically when the mod is destroyed. The manual alternative,
  `UE4SSProgram::get_program().add_gui_tab(shared_ptr<GUI::GUITab>)` /
  `.remove_gui_tab(...)`, requires you to clean up by hand or it crashes on unload.
- ImGui ships embedded (the `UE4SS-RE/imgui` fork) — no separate dependency to add.
- **The callback's thread is not documented.** Since it's part of the GUI render loop, assume it is
  **not** the game thread: compute/cache the value in `on_update()` and only paint it inside the tab
  callback.

### DataTables — read-only, open investigation

No production-ready mutation path exists; treat this as read-only:
- **Practicable option (read-only):** `dataTable->GetRowMap()` (the reflected native `UDataTable`
  function) returns `TMap<FName, uint8*>`; iterate with `GetElementsPtr()`/`Num()` and cast each `uint8*`
  to the row struct (which inherits `FTableRowBase`). After `AddRow()`/`RemoveRow()` UE4SS's `TMap` view
  goes stale — `Num()` doesn't recompute and `GetElementsPtr()` is out of date — so **never mutate**.
- **Kismet DataTable Function Library** (`DoesDataTableRowExist`, `GetDataTableRowNames`,
  `GetDataTableRowFromName`): `GetDataTableRowFromName` relies on `CustomThunk`/
  `Stack.MostRecentProperty`, which isn't populated when called from C++ — it fails, and there is no known
  workaround.
- Open problem: FName alignment inside rows (`alignas(4)` vs `alignas(8)` depending on
  `WITH_CASE_PRESERVING_NAME`) isn't resolved either.
- **For us:** reading text/localization by iterating `GetRowMap()` without mutating is the only supported
  use case.

### Dumper headers — which one to reach for

- **CXX dump**: the official docs call the generator "shoddily made" — no UHT macros, no correct
  `#include`s, padding is **not reliable**. Treat it as a property catalogue, never as a memory map.
- **UHT dump**: feeds `UE4GameProjectGenerator` (Buckminsterfullerene02 fork; UE 4.22+ only) to produce a
  mirror `.uproject` you can actually compile against.
- **For accessibility work you almost never need the UHT project.**
  `GetValuePtrByPropertyNameInChain` + `ContainerPtrToValuePtr` (§5 above) gets you the value without
  building a mirror project, and — same as the Lua path — it never hardcodes an offset.

---

## 6. Lua vs C++

| | Lua | C++ |
|---|---|---|
| **Iteration** | Hot-reload (`Ctrl+R`), no restart — see [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) | Recompile **and** restart the game on every change. Only mitigable with `RC::await_debugger()` + attach (`Helpers/Debug.hpp`) |
| **Crash risk** | Sandboxed — a script error doesn't take the game down | A dangling cached pointer, a tab not cleaned up, or a missing `UE4SS_ENABLE_IMGUI()` crashes the **whole game** |
| **Performance** | Interpreter overhead | Native — matters only for genuinely per-frame work |
| **Own GUI** | Not possible | `register_tab` + ImGui (§5) — Lua has no equivalent |
| **PRISM path** | Needs the `prism_bridge.dll` C module (see [`../../screen-reader-integration/README.md`](../../screen-reader-integration/README.md)) | `LoadLibrary("prism.dll")` + `GetProcAddress` directly, no bridge layer |

---

## 7. When it's worth switching to C++

Worth it when at least one of these is true:
1. You want a **GUI tab of your own** as a live, in-game explorer (§5).
2. You're reading **many objects' properties every tick** — every `Object.PropName` access from Lua costs
   a fresh `FindProperty(name)` lookup, never memoised (`LuaUObject.hpp:817-833`), so a per-tick sweep over
   dozens of objects × several properties each pays that lookup cost every time. A C++ mod holds typed
   pointers and skips the Lua marshaling entirely.
3. You want to call `prism.dll` **without the Lua bridge layer** in between.
4. You need hooks with direct access to **in/out parameters** via the BP macros (§5) — the params-struct
   construction those macros do isn't available from the Lua hook API.

**Not worth it — and this is the default for our mods:** as long as the adapter is reading a handful of
states at low frequency, Lua's hot-reload iteration speed is a much bigger practical win than anything
C++ buys, and the crash blast radius is far smaller (§6). Stay in Lua unless one of the four reasons above
is concretely true for the mod section you're building.

> **Accessibility caveat on reason 1:** an ImGui tab is itself **not accessible to a blind developer** —
> it's a visual GUI, same limitation as UE4SS's own Live Property Viewer (see
> [`ue4ss-reflection-cookbook.md`](ue4ss-reflection-cookbook.md)). If the person building the mod is
> blind, the equivalent "live explorer" is the **text channel** in
> [`../../../templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/), and reason 1 no longer
> applies — that inspector is Lua, not C++.

---

Related: [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) (zero-restart Lua loop this doc's iteration
cost is measured against), [`ue4ss-reflection-cookbook.md`](ue4ss-reflection-cookbook.md) (the same
reflection operations from Lua), [`../../screen-reader-integration/README.md`](../../screen-reader-integration/README.md)
(PRISM transport per engine, including the C++-vs-bridge distinction in §6 above).
