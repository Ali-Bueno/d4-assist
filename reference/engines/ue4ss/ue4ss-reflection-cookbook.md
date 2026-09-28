# UE4SS runtime reflection cookbook (non-visual class discovery)

**What this replaces:** the "launch → press Ctrl+J → close the game → read the dump → launch again" loop.
Everything here runs **while the game is running**, prints to the console/log or to a file you can read
with a screen reader in another window, and survives a hot reload (`Ctrl+R`).

> **Why this doc exists at all.** UE4SS's Live Property Viewer is an **ImGui GUI** — it is not
> screen-reader accessible. For a blind developer the equivalent power has to come from **text**:
> reflection queries driven from console commands, printed to the console and written to a file.
> That is what this cookbook is. (Live View is still documented in
> [`ue4ss-discovery-tools.md`](ue4ss-discovery-tools.md) for sighted collaborators and for
> screenshots.)

Related: [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) (the zero-restart loop),
[`ue4ss-api-reference.md`](ue4ss-api-reference.md) (finding objects, hooks, console),
[`../../../templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/) (a ready-to-drop mod that
implements every recipe below as a console command).

---

## 1. The reflection API surface

Everything below is UE4SS's own Lua API. Signatures are from the official docs; anything the docs do
**not** guarantee is flagged.

### Identity / navigation — `UObject`
| Method | Returns |
|---|---|
| `GetFullName()` | `string` — `Class Package.Outer.Name` |
| `GetFName()` | `FName` (a **copy**; `__index` access returns FNames **by reference**) |
| `GetClass()` | `UClass` |
| `GetOuter()` | `UObject` |
| `GetWorld()` | `UWorld` |
| `GetAddress()` | `integer` (64-bit) — the object's address |
| `IsA(UClass)` / `IsA(string FullClassName)` | `bool` |
| `IsClass()` / `IsAnyClass()` | `bool` |
| `HasAnyFlags(EObjectFlags)` / `HasAllFlags(...)` / `HasAnyInternalFlags(EInternalObjectFlags)` | `bool` |
| `GetPropertyValue(name)` / `SetPropertyValue(name, v)` | reflected member access by string |
| `CallFunction(UFunction, params...)` | invoke |
| `Reflection()` | `UObjectReflection` (only documented method: `GetProperty(name) -> Property`) |
| `type()` | `string` — UE4SS's type name (Lua's global `type()` returns `"userdata"` for everything) |
| `IsValid()` | `bool` — inherited from `RemoteObject`. **Check it before every deref.** |

### Enumeration — `UStruct` (and therefore `UClass`)
```lua
Struct:ForEachProperty(function(Property) --[[ return true to stop ]] end)
Struct:ForEachFunction(function(Function) --[[ return true to stop ]] end)
Struct:GetSuperStruct() --> UClass (may be invalid — check IsValid())
```
`UClass` adds `GetCDO() -> UClass` and `IsChildOf(UClass) -> bool`.

**Walking the whole inheritance chain** is just `GetSuperStruct()` in a loop — that is how you get the
complete property list of a Blueprint class *including* everything it inherits.

### A property's type, as a string
There is **no** `GetType()` / `GetFQN()` on `Property`. Two supported ways:
```lua
local typeName = Prop:GetClass():GetFName():ToString()   -- "BoolProperty", "ObjectProperty", ...
local isBool   = Prop:IsA(PropertyTypes.BoolProperty)    -- exact comparison
```
`Prop:GetClass()` returns a `FieldClass`, whose `GetFName():ToString()` matches the `PropertyTypes`
table entries: `ObjectProperty, Int8Property, Int16Property, IntProperty, Int64Property, NameProperty,
FloatProperty, StrProperty, ByteProperty, BoolProperty, ArrayProperty, MapProperty, StructProperty,
ClassProperty, WeakObjectProperty, EnumProperty, TextProperty`.

### Drilling into a typed property
| Type | Method |
|---|---|
| `ObjectProperty` | `GetPropertyClass() -> UClass` (class of the referenced object) |
| `StructProperty` | `GetStruct() -> UScriptStruct` |
| `ArrayProperty` | `GetInner() -> Property` (element type) |
| `BoolProperty` | `GetByteMask()`, `GetByteOffset()`, `GetFieldMask()`, `GetFieldSize()` — packed bitfields |
| any | `ContainerPtrToValuePtr(Container, ArrayIndex) -> LightUserdata`, `ImportText(Buffer, Data, PortFlags, OwnerObject)` |

### Values
- `UEnum`: `GetNameByValue(v) -> FName`, `ForEachName(cb(Name, Value))`, `GetEnumNameByIndex(i) -> FName, integer`.
- `TArray`: `#arr`, `arr[i]`, `GetArrayNum()`, `GetArrayMax()`, `GetArrayAddress()`, `GetArrayDataAddress()`,
  `ForEach(cb(index, elem))` where `elem:get()` / `elem:set()`.
- `FString` / `FName` / `FText`: `ToString()`. `FName` also `GetComparisonIndex()`.
- `FWeakObjectPtr:Get()` — **the result can be invalid**; `IsValid()` it before use.
- `UScriptStruct` is dual: type metadata *and* a value wrapper (`__index`, `GetBaseAddress()`,
  `GetStructAddress()`, `GetPropertyAddress()`, `IsMappedToObject()`, `IsMappedToProperty()`).

### Gotchas
1. **`IsValid()` before every deref** — especially after `GetSuperStruct()` and `FWeakObjectPtr:Get()`.
2. **`UFunction` parameters are not officially enumerable.** The docs type `UFunction : UObject` and list
   only `__call`, `GetFunctionFlags()`, `SetFunctionFlags()`. In the real engine `UFunction : UStruct`,
   so `Func:ForEachProperty(...)` *should* list its parameters — **verify empirically per game before
   relying on it** (guard it in `pcall`).
3. `IsA` is overloaded differently per class: `UObject:IsA` takes a `UClass` **or** a full class-name
   string; `Property:IsA` takes a `PropertyTypes` entry.
4. Some reads **abort uncatchably** (not a Lua error you can `pcall`): nested struct reads,
   `RenderTransform`, and array methods on a collapsed fixed-array property. See §5 and
   [`accessibility-patterns.md`](accessibility-patterns.md).
5. `UE4SS.GetVersion() -> major, minor, hotfix` only exists on UE4SS ≥ 1.1 (`UE4SS` is `nil` below that).

---

## 2. Recipe: dump one class, fully, in place

The single most useful command. Prints a class, its parents, every property with its type, and every
function — with **no dump file, no restart**.

```lua
local function DumpStruct(Struct, out)
    out(("== %s"):format(Struct:GetFullName()))
    Struct:ForEachProperty(function(Prop)
        out(("  %-40s : %s"):format(Prop:GetFName():ToString(),
                                    Prop:GetClass():GetFName():ToString()))
    end)
    Struct:ForEachFunction(function(Fn)
        out(("  fn %s()"):format(Fn:GetFName():ToString()))
    end)
end

local function DumpClassChain(Object, out)
    local Class = Object:IsClass() and Object or Object:GetClass()
    while Class and Class:IsValid() do
        DumpStruct(Class, out)
        Class = Class:GetSuperStruct()
    end
end
```
`out` is deliberately injected: pass `print` for the console, or a file writer (§6) so you can read the
result with a screen reader **while the game keeps running**.

## 3. Recipe: what is on screen right now

```lua
-- every live UserWidget, cheap enough for an on-demand key/command (never per frame)
local widgets = FindAllOf("UserWidget") or {}
for _, w in ipairs(widgets) do
    if w:IsValid() then
        local visible = w.bIsVisible          -- guard: not every game exposes it
        out(("%s  visible=%s"):format(w:GetFullName(), tostring(visible)))
    end
end
```
Same shape works for `FindAllOf("Actor")`, `FindAllOf("PlayerController")`, `FindFirstOf("HUD")`.
Cost rules live in [`ue4ss-api-reference.md`](ue4ss-api-reference.md): on-demand only.

## 4. Recipe: find the property that changes (the text-mode Live View watch)

This is the non-visual replacement for "add a watch and see which value flips". Snapshot the scalar
properties of an object, do the thing in-game, snapshot again, print the diff.

```lua
local snap = {}
local function Snapshot(Object)
    local t, Class = {}, Object:GetClass()
    while Class and Class:IsValid() do
        Class:ForEachProperty(function(Prop)
            local name = Prop:GetFName():ToString()
            local kind = Prop:GetClass():GetFName():ToString()
            -- scalars only: struct/array reads can abort uncatchably
            if kind:find("Int") or kind == "FloatProperty" or kind == "BoolProperty"
               or kind == "ByteProperty" or kind == "EnumProperty" then
                local ok, v = pcall(function() return Object:GetPropertyValue(name) end)
                if ok then t[name] = v end
            end
        end)
        Class = Class:GetSuperStruct()
    end
    return t
end

-- bind to a key: 1st press = baseline, 2nd press = diff
local function Probe(Object, out)
    local now = Snapshot(Object)
    if not snap[Object:GetAddress()] then
        snap[Object:GetAddress()] = now; out("baseline taken")
    else
        local before = snap[Object:GetAddress()]
        for k, v in pairs(now) do
            if before[k] ~= v then out(("%s: %s -> %s"):format(k, tostring(before[k]), tostring(v))) end
        end
        snap[Object:GetAddress()] = now
    end
end
```
Open the menu → press the key → move the cursor → press again → the property whose value stepped
`0 → 1 → 2` is your selection index. **Scalars only on purpose**: see gotcha 4.

If *nothing* flips, the state is not reflected at all — go to §5.

## 5. When reflection genuinely cannot see it

Two proven escapes, both documented in detail in
[`ue4ss-discovery-tools.md` §4](ue4ss-discovery-tools.md):

- **Native memory diff** (`mem_bridge.dll`, SEH-guarded reads at `obj:GetAddress() + off`) — recovers
  private C++ selection/page indices that were never `UPROPERTY`. Same diff idea as §4, one level lower.
- **`RegisterCustomProperty`** — re-exposes elements of a fixed C array that UE4SS reflection collapses
  to element 0. Note the playbook rule: any offset you feed it must be **derived and documented**, never
  guessed (PRINCIPLES §4).

## 6. Read the output without closing the game

Lua in UE4SS is real **PUC Lua 5.4**, so `io` works. Write the dump next to `UE4SS.log` and open it in
your editor / screen reader while the game keeps running:

```lua
local function FileOut(path)
    local f = assert(io.open(path, "w"))
    return function(line) f:write(line, "\n"); f:flush() end, function() f:close() end
end

local out, close = FileOut("dump_class.txt")
DumpClassChain(FindFirstOf("PlayerController"), out)
close()
```
`f:flush()` per line matters: you can tail the file live. **This alone removes most of the
"close the game to read the dump" cycle.**

## 7. Drive it from the console, not from edits

Register the recipes as console commands once, and from then on inspect by typing:

```lua
RegisterConsoleCommandGlobalHandler("dumpclass", function(_, Params, Ar)
    local target = Params[2]                       -- Params[1] is the command name
    local obj = FindFirstOf(target) or StaticFindObject(target)
    if not obj or not obj:IsValid() then Ar:Log("not found: " .. tostring(target)); return true end
    local out, close = FileOut(("dump_%s.txt"):format(target))
    DumpClassChain(obj, function(l) out(l); Ar:Log(l) end)
    close(); return true
end)
```
`Ar:Log(...)` echoes into the in-game console (`@` / `F10`), so the answer arrives without alt-tabbing.
The ready-made mod with `dumpclass`, `props`, `funcs`, `findall`, `probe` and dumper triggers lives in
[`templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/).

## 8. Autocomplete for the game's own classes

The GUI's **Dumpers → Dump Lua Bindings** writes LuaCATS/EmmyLua type annotations for the *game's*
classes into `Mods/shared/types`. With the `sumneko.lua` extension you then get real autocompletion:
```lua
---@class ITM_MisSel_Biome_C
local biome = FindFirstOf("ITM_MisSel_Biome_C")
---@type int
local numMissions = biome.NumMissions
```
**Never `require()` those files.** Official warning: including them overrides UE4SS's globals and breaks
everything. They are for the language server only (a `.luarc.json` helps performance on big libraries).
