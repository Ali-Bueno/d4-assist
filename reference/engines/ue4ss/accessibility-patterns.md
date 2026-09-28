# Accessibility Patterns for UE4SS Mods

Patterns and strategies for making Unreal Engine games accessible to blind players using UE4SS Lua mods.

---

## Architecture Overview

Each accessibility mod should follow this modular structure:

```
Mods/
  GameAccessibility/
    scripts/
      main.lua                -- Entry point, initializes everything
      screen_reader.lua       -- Tolk/TTS integration
      game_state_tracker.lua  -- Monitors game state changes
      ui_reader.lua           -- Reads UI widget text
      audio_cue_service.lua   -- Spatial audio / contextual sounds
      keybind_manager.lua     -- Accessibility hotkeys
      utils.lua               -- Shared utilities
```

---

## 1. Screen Reader Integration (Tolk)

### Challenge
Tolk is a C/C++ library. Lua mods can load C libraries (.dll) from the `scripts/` folder.

### Approach: C Wrapper DLL
Create a small C DLL that wraps Tolk and exposes Lua-callable functions:

```c
// tolk_lua.c - Compiled as tolk_lua.dll, placed in scripts/
#include <lua.h>
#include <Tolk.h>

static int l_tolk_load(lua_State *L) { Tolk_Load(); return 0; }
static int l_tolk_unload(lua_State *L) { Tolk_Unload(); return 0; }
static int l_tolk_output(lua_State *L) {
    const char *text = luaL_checkstring(L, 1);
    // Convert to wide string and call Tolk_Output
    Tolk_Output(wtext, FALSE);
    return 0;
}
static int l_tolk_speak(lua_State *L) {
    const char *text = luaL_checkstring(L, 1);
    int interrupt = lua_toboolean(L, 2);
    Tolk_Output(wtext, interrupt);
    return 0;
}
static int l_tolk_silence(lua_State *L) { Tolk_Silence(); return 0; }
static int l_tolk_is_loaded(lua_State *L) {
    lua_pushboolean(L, Tolk_IsLoaded());
    return 1;
}

static const luaL_Reg tolklib[] = {
    {"load", l_tolk_load},
    {"unload", l_tolk_unload},
    {"output", l_tolk_output},
    {"speak", l_tolk_speak},
    {"silence", l_tolk_silence},
    {"is_loaded", l_tolk_is_loaded},
    {NULL, NULL}
};

int luaopen_tolk_lua(lua_State *L) {
    luaL_newlib(L, tolklib);
    return 1;
}
```

### Usage in Lua
```lua
local tolk = require("tolk_lua")
tolk.load()
tolk.speak("Game loaded", true)  -- true = interrupt previous speech
```

### Alternative: SAPI via COM
If Tolk wrapper is too complex, use Windows SAPI directly via a similar C wrapper.

---

## 2. UI Text Reading

### Discovering Widget Classes
First, dump all objects (Ctrl+J) and search for UI-related classes:
```lua
-- Enumerate all UserWidget subclasses
local Widgets = FindAllOf("UserWidget")
if Widgets then
    for _, w in pairs(Widgets) do
        print(w:GetFullName() .. "\n")
    end
end

-- Look for TextBlock widgets specifically
local TextBlocks = FindAllOf("TextBlock")
if TextBlocks then
    for _, tb in pairs(TextBlocks) do
        local text = tb.Text  -- FText property
        if text then
            print("Text: " .. text:ToString() .. "\n")
        end
    end
end
```

### Watching for UI Changes
```lua
-- Detect when new widgets appear
NotifyOnNewObject("/Script/UMG.UserWidget", function(Widget)
    ExecuteInGameThread(function()
        -- Inspect the widget, read its text content
        local className = Widget:GetClass():GetFName():ToString()
        print("New widget: " .. className .. "\n")
    end)
end)
```

### Hooking Widget Update Functions
```lua
-- Hook specific widget functions to catch text changes
RegisterHook("/Script/UMG.TextBlock:SetText", function(self, Text)
    local newText = Text:Get():ToString()
    tolk.speak(newText, false)
end)
```

---

## 3. Game State Tracking

### State Machine Pattern
```lua
local GameState = {
    current = "unknown",
    previous = "unknown",

    STATES = {
        MAIN_MENU = "main_menu",
        GAMEPLAY = "gameplay",
        PAUSE_MENU = "pause_menu",
        INVENTORY = "inventory",
        DIALOGUE = "dialogue",
        COMBAT = "combat",
        LOADING = "loading",
    }
}

function GameState:Update(newState)
    if newState ~= self.current then
        self.previous = self.current
        self.current = newState
        self:OnStateChanged()
    end
end

function GameState:OnStateChanged()
    tolk.speak("Now in: " .. self.current, true)
end
```

### Detecting State from UE Objects
```lua
-- Hook map loading for level changes
RegisterLoadMapPostHook(function()
    GameState:Update(GameState.STATES.LOADING)
end)

-- Hook BeginPlay for when actors load
RegisterBeginPlayPostHook(function(Actor)
    local name = Actor:GetClass():GetFName():ToString()
    -- Check for menu/HUD actors to determine state
end)
```

---

## 4. Keybind System for Accessibility

```lua
-- Read current info on demand
RegisterKeyBind(Key.F1, function()
    tolk.speak("Current state: " .. GameState.current, true)
end)

RegisterKeyBind(Key.F2, function()
    -- Read player health/status
    local PC = FindFirstOf("PlayerController")
    if PC:IsValid() then
        local Pawn = PC.Pawn
        -- Read health, position, etc.
    end
end)

RegisterKeyBind(Key.F3, function()
    -- Read current menu item / focused widget
end)

-- Toggle verbosity
local verbose = false
RegisterKeyBind(Key.F4, function()
    verbose = not verbose
    tolk.speak("Verbose mode: " .. (verbose and "on" or "off"), true)
end)

-- Silence screen reader
RegisterKeyBind(Key.ESCAPE, {ModifierKey.CONTROL}, function()
    tolk.silence()
end)
```

---

## 5. Anti-Spam / Announcement Queue

```lua
local Announcer = {
    lastMessage = "",
    lastTime = 0,
    minInterval = 200,  -- ms between same messages
}

function Announcer:Speak(message, interrupt)
    local now = os.clock() * 1000
    if message == self.lastMessage and (now - self.lastTime) < self.minInterval then
        return  -- skip duplicate
    end
    self.lastMessage = message
    self.lastTime = now
    tolk.speak(message, interrupt or false)
end

function Announcer:SpeakUnique(message)
    if message ~= self.lastMessage then
        self:Speak(message, false)
    end
end
```

---

## 6. Periodic Polling (Careful Use)

```lua
local previousMenuText = ""

LoopAsync(500, function()
    ExecuteInGameThread(function()
        -- Check focused widget or menu state
        local currentText = GetCurrentFocusedText()  -- custom function
        if currentText and currentText ~= previousMenuText then
            previousMenuText = currentText
            Announcer:Speak(currentText, true)
        end
    end)
    return false  -- keep running
end)
```

**Warning:** Avoid polling every frame. 250-500ms is usually sufficient for UI changes.

---

## 7. Per-Game Discovery Workflow

1. **Install UE4SS (zDEV)** in the game
2. **Dump C++ headers** (Ctrl+H) — learn class names, properties, functions
3. **Dump all objects** (Ctrl+J) — see what's loaded at runtime
4. **Use Live Viewer** — search for UI widgets, player controllers, game state objects
5. **Identify key UFunctions** — menu navigation, text display, state changes
6. **Write hooks** — intercept relevant functions
7. **Build accessibility layer** — screen reader + audio cues
8. **Test with hot-reload** (Ctrl+R) — iterate fast

---

## 8. Detecting UI/state changes: hooks vs polling

Prefer **event-driven** over polling — it's cheaper and more responsive:

1. **Level/mode changes** → `RegisterInitGameStatePostHook` or `RegisterBeginPlayPostHook`.
   ⚠ **`RegisterLoadMapPre/PostHook` crashes some games** — both DB mods had to disable it. The
   hook-free substitute is a **world-epoch poll**: compare the address of the current world (read from
   a persistent root) once per tick and flush every cache when it changes.
2. **Actor acquisition** → `RegisterBeginPlayPostHook`: the actor is fully initialised, unlike a
   construction notify, and it costs no scan. This is the recommended default
   ([ue4ss-mod-architecture.md §2](ue4ss-mod-architecture.md)).
3. **A screen/widget appearing** → if you must watch construction, `NotifyOnNewObject` on the
   **specific** `WBP_*_C` class only, never a base class (§9). Both mods here ended up dropping it
   entirely — read [ue4ss-mod-architecture.md §4](ue4ss-mod-architecture.md) first.
4. **A value/text/selection changing** → `RegisterHook` on the game's setter (e.g. a `SetText`-equivalent
   or a cursor-move UFunction). Fires for **all instances** of the class; `self` is the instance. Read
   wrapped params with `param:Get()`.

**When you must poll** (no usable hook — many custom-engine UIs, e.g. CyberConnect2, expose no stable
setter): keep it cheap and diff-gated. See the caching + busy-guard rules below.

---

## 9. Hard-won lessons (real bugs, from a UE4.21 CC2 game)

These cost real debugging time — internalize them.

### `IsVisible()` LIES → check the ancestor chain
`UWidget:IsVisible()` reflects only a widget's **own** slate visibility. A pooled/closed window is usually
Collapsed at some **ancestor**, but each child still returns `IsVisible()==true`. So a naive
"is this on screen" check latches onto stale "Yes/No"/"Saving…" widgets and the wrong adapter wins.
**Fix — an `on_screen()` helper:** `IsVisible()` AND walk `GetParent()` up rejecting any ancestor whose
`GetVisibility()` is `Collapsed(1)` or `Hidden(2)`.
```lua
local NOT_RENDERED = { [1]=true, [2]=true }  -- ESlateVisibility Collapsed / Hidden
function on_screen(o)
    if not (o and o:IsValid() and o:IsVisible()) then return false end
    local cur, depth = o, 0
    while depth < 8 do
        local ok, p = pcall(function() return cur:GetParent() end)
        if not ok or not p or not p:IsValid() then break end
        local oe, e = pcall(function() return p:GetVisibility() end)
        if oe and NOT_RENDERED[tonumber(e) or -1] then return false end
        cur, depth = p, depth + 1
    end
    return true
end
```
`GetVisibility()` returns an ESlateVisibility (`0 Visible, 1 Collapsed, 2 Hidden, 3 HitTestInvisible,
4 SelfHitTestInvisible`). **`0 Visible` = interactive; `3 HitTestInvisible` = rendered but NOT interactive**
(e.g. a menu shown behind an intro cinematic). Gate "the user is actually here" on `enum == 0`.

### Widgets are POOLED → cache refs, never re-scan
Containers persist for the session (closing only Collapses them). So `FindAllOf` them **once**, keep the
ref, and re-scan only when the cached ref is invalid. `FindAllOf` is O(all UObjects); calling it per tick
per adapter is what causes input lag. Two caches pay off massively:
```lua
-- one live ref per class, re-found only when it dies:
function cached_live(cls)  -- pooled top-level widget persists → ~1 scan per session
  local c = LIVE[cls]; if c and c:IsValid() then return c end
  c = first_live(cls); LIVE[cls] = c; return c
end
-- a whole FindAllOf list, refreshed every ~30 ticks (pools are static):
function cached_all(cls, tick) ... end
```
Prefer the **top-level instance** (direct GameInstance child) when a class has several live copies (a real
window + a nested collapsed one) — caching the wrong one makes detection silently fail.

### Broad `NotifyOnNewObject` on a base widget class is a FIREHOSE → crash
**The rule and its two independent root causes are canonical in
[ue4ss-mod-architecture.md §4](ue4ss-mod-architecture.md)** — short version: it fires by inheritance,
a base class means every widget in the game, mid-construction access hard-aborts, and **both mods
dropped it entirely**. Field notes that live here rather than there:

- A per-tick `FindAllOf` is slow but does **NOT** crash the same way, because it only ever sees
  **already-constructed** objects. The crash is specific to the construction-time callback × volume —
  which is why "slow" and "unsafe" are different axes here.
- The narrow-registration variant genuinely works: the **DBZK_Fix** mod (same UE4 AT engine) only ever
  notifies on `LocalPlayer`, `CameraComponent`, `ATCheatManager` and specific scene classes, and
  touching the fresh object in the callback (`IsValid()` then read/write) is fine at that volume.
- If you need "any widget" (focus tracking), use a **throttled, load-gated `FindAllOf`** for that one
  path, or the engine's focused-widget accessor — never a base-class notify.
- Save/menu flows often have a clean UFunction hook point instead of watching widgets: DBZK_Fix
  triggers off `RegisterHook("/Script/AT.ATSaveManager:Load"/"Save")`. Look for the game's
  save-manager class.

### Some property reads ABORT UNCATCHABLY (pcall can't save you)
Calling a method on a null/dangling UObject, and **nested struct reads like `widget.RenderTransform
.Translation.X`**, can hard-abort the whole `ExecuteInGameThread` — pcall does NOT catch it. Guard **every
level** with `:IsValid()` first, and avoid RenderTransform/struct-field reads in throwaway dumpers. When a
dev dump keeps truncating, add a **breadcrumb + flush before** each risky read so the last line names the
culprit.

### Poll busy-guard (no backlog)
`LoopAsync` fires on a worker thread and queues `ExecuteInGameThread` **without waiting**. On a busy game
thread (loading, a movie) those pile into a backlog that runs late in bursts → huge input-to-speech lag.
**Only queue the next step once the previous finished** (a `busy` flag). Keep per-tick work cheap; compute
state once and reuse within the tick.

**Clear the flag on ENTRY to the game-thread callback, not on exit** (rule added 2026-07-29 after an audit
flagged the entry-clear as a bug; it is deliberate, and the reasoning generalizes to every UE4SS mod).
Clearing on exit looks stricter — it guarantees zero re-entrancy — but on these engines some errors are
**C++ exceptions `pcall` cannot catch**: they kill the callback mid-flight, and a `busy` left true then
silences the loop for the **rest of the session**, with nothing in the log to say why (seen live on
2026-07-04). Clearing on entry keeps the whole anti-pile-up purpose — the game thread runs the callback
atomically, so at most **one** extra step can queue while one is running — and bounds the worst case to a
single extra step instead of a permanently mute reader. Reference implementation: `Core.loop`,
`ui_core.lua:1776-1795` in the DBZ Kakarot mod.

### Debounce transitions
- **Screen-switch debounce:** require a screen to be the top active adapter for N consecutive polls before
  announcing, so a 1-tick flash (e.g. the title menu behind a boot dialog) stays quiet. Allow a per-screen
  longer confirm for "fallback" screens (the title).
- **Content stickiness:** for a paged reader, only commit new content when it's stable across 2 scans, and
  never drop the cached value to nil on a transient empty scan while still on screen — otherwise a page
  turn deactivates the screen and re-reads the old page.

### Subtree matching needle
`GetFullName()` is `"ClassName /Engine/Transient.…Path"`. To match a container's descendants, match on the
**path** (`fullname:match("%s(.+)$")`), NOT the full name — a child's name starts with ITS own class, so
the class-prefixed needle never matches (a subtle "reads nothing" bug).

### Baked-image text is unreadable
Some labels (e.g. a "Loading" word, control glyphs) are **textures**, not text nodes — nothing to read.
Read the surrounding real text (tips/recap) and map glyphs by their brush-texture token if a button name
is needed.

---

## Differences from BepInEx/Unity Approach

| Aspect | BepInEx (Unity) | UE4SS (Unreal) |
|--------|-----------------|----------------|
| Language | C# | Lua (or C++) |
| Patching | Harmony Prefix/Postfix | RegisterHook on UFunctions |
| Object finding | FindObjectOfType<T> | FindFirstOf("ClassName") |
| UI system | Unity UI / TextMeshPro | UMG (UserWidget, TextBlock) |
| Hot reload | Recompile DLL + restart | Ctrl+R (instant) |
| TTS | Tolk via NuGet/DLL ref | Tolk via C wrapper DLL |
| State detection | MonoBehaviour lifecycle | BeginPlay/EndPlay hooks |
| Performance | IL2CPP reflection cost | Direct property access on a held pointer (~0.003 ms) — but *finding* the object first is a 65–115 ms array walk, and every property access is a fresh name lookup; see [`ue4ss-performance.md`](ue4ss-performance.md) |
