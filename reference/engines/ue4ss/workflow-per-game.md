# Workflow: Making a New Game Accessible with UE4SS

Step-by-step guide for each new Unreal Engine game.

> **Read [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md) alongside this.** This file is *what* to do
> in order; that one is *how to do it without restarting the game* — which is where most of the time
> goes if you get it wrong.

---

## Phase 1: Setup & Discovery

### 1.1 Install UE4SS
1. Download the **zDEV** version (developer install)
2. Find the game's main `.exe` in `GameName/Binaries/Win64/`
3. Extract UE4SS there
4. Edit `UE4SS-settings.ini` — **this is the step that decides whether you spend the project restarting**:
   ```ini
   [Debug]
   ConsoleEnabled = 1
   GuiConsoleEnabled = 1
   GuiConsoleVisible = 1

   [General]
   EnableHotReloadSystem = 1
   ```
   Keep this as a **dev** ini and ship a separate release one (consoles off) — do not develop against
   the file your packaging script sends to users.
5. Launch game, verify UE4SS loads (check `UE4SS.log`)
6. Install the dev inspector: copy [`templates/ue4ss-inspector/`](../../../templates/ue4ss-inspector/) to
   `Mods/Inspector/` and add `Inspector : 1` to `mods.txt`.

### 1.2 One offline catalogue, then stop dumping
1. **Once**, with `[CXXHeaderGenerator] LoadAllAssetsBeforeGeneratingCXXHeaders = 1`, press **Ctrl+H**
   (or call `GenerateSDK()`) → `CXXHeaderDump/` is your searchable catalogue of every class's real
   UPROPERTY/UFUNCTION list. Restart after this dump — load-all-assets makes the session unstable.
2. Optional but worth it: `GenerateLuaTypes()` (GUI: *Dump Lua Bindings*) → `Mods/shared/types/`, which
   gives your editor autocompletion for the game's own classes. Never `require()` those files.
3. Search the headers for: widget/UI classes, PlayerController subclasses, GameState/GameMode, HUD,
   menu/inventory classes.
4. **Do not** make `Ctrl+J` a habit. A full object dump is hundreds of MB and answers less than
   `dumpclass` does in place — use it only when you need the instantiated-container names.

### 1.3 Explore the running game  ← before writing any reflection code
Full details in [ue4ss-live-workflow](ue4ss-live-workflow.md) and
[ue4ss-reflection-cookbook](ue4ss-reflection-cookbook.md).
1. `find <substr>` / `findall UserWidget` → locate the live object for the screen you're on.
2. `dumpclass <Class>` → its whole chain: every property with its type, every function. No dump file.
3. `probe <Class>` → baseline, move the cursor in-game, `probe` again: **the property that stepped is
   your selection index.** If nothing changes, the state is not reflected — go to
   [discovery-tools §4](ue4ss-discovery-tools.md) (memory diff / `RegisterCustomProperty`).
4. `watch <Class> <Prop>` to confirm it tracks, then write the reader.
5. Sighted alternative to 2–4: the GUI Live View with watches and *Find functions*. It is ImGui, so it
   is **not screen-reader accessible** — the commands above are the equivalent.

---

## Phase 2: Create the Mod

### 2.1 Copy Template
```
Copy: UE4ss study/template/scripts/
To:   GameDir/Binaries/Win64/Mods/GameAccessibility/scripts/
```

### 2.2 Enable the Mod
Add to `Mods/mods.txt`:
```
GameAccessibility : 1
```

### 2.3 Test Basic Loading
Launch game, check console for:
```
[GameAccessibility] v1.0.0 loading...
[ScreenReader] ...
[KeybindManager] Default keybinds registered
[GameAccessibility] Fully loaded
```

---

## Phase 3: Game-Specific Customization

### 3.1 Identify Key Classes
Ask the running game — console, command file, or keybind (see the inspector template):
```
findall UserWidget          -- every live widget
find Pause                  -- narrow by name
dumpclass PlayerController  -- properties + types + functions, whole chain
props PlayerController      -- their current values
```
No relaunch, no dump file. The equivalent raw calls, if you'd rather inline them, are in
[ue4ss-reflection-cookbook](ue4ss-reflection-cookbook.md).

### 3.2 Customize game_state_tracker.lua
Add detection logic in `OnActorBeginPlay`:
```lua
function GameStateTracker.OnActorBeginPlay(className, actor)
    if className == "BP_MainMenu_C" then
        GameStateTracker.SetState(GameStateTracker.STATES.MAIN_MENU)
    elseif className == "BP_HUD_C" then
        GameStateTracker.SetState(GameStateTracker.STATES.GAMEPLAY)
    end
end
```

### 3.3 Customize ui_reader.lua
Add widget reading for the game's specific UI:
```lua
function UIReader.OnNewWidget(widget)
    local className = widget:GetClass():GetFName():ToString()

    if className == "WBP_DialogueBox_C" then
        local text = UIReader.ReadWidgetText(widget, "DialogueText")
        if text then announcer.Speak(text, false) end
    end
end
```

### 3.4 Add Game-Specific Hooks
In `main.lua`, add hooks for important game functions:
```lua
-- Example: Hook dialogue system
RegisterHook("/Script/GameName.DialogueManager:ShowDialogue", function(self, Text)
    local dialogueText = Text:Get():ToString()
    Announcer.Speak(dialogueText, true)
end)
```

### 3.5 Add Game-Specific Keybinds
In `keybind_manager.lua`:
```lua
-- Example: Read inventory
RegisterKeyBind(Key.I, {ModifierKey.CONTROL}, function()
    ExecuteInGameThread(function()
        -- Read inventory contents
    end)
end)
```

---

## Phase 4: Testing & Iteration

### 4.1 Hot Reload Cycle
1. Edit Lua files
2. Press **Ctrl+R** in game (or "Restart All Mods" in the GUI)
3. Changes take effect immediately
4. Check the console/log for errors

**Reload does not unregister anything.** Hooks and keybinds registered again on each reload stack up —
announcements start firing twice, then three times. Keep the `preId, postId` from `RegisterHook` and
`UnregisterHook` on reload, or guard registration behind a flag in `Mod:SetSharedVariable` (it survives
reloads by design); guard keybinds with `IsKeyBindRegistered`.

Adding a **new** mod folder still needs "Restart All Mods" or a relaunch; editing an existing one doesn't.

### 4.2 Test with Screen Reader
1. Ensure NVDA/JAWS is running
2. Ensure `prism.dll` (and the `prism_bridge.dll` Lua C module) are in place — see
   [screen-reader-integration](../../screen-reader-integration/README.md). PRISM only; there is no Tolk
   fallback.
3. Navigate through all game screens
4. Verify all announcements are clear and timely

### 4.3 Common Issues
- **Widget not found**: class name might differ — `find <substr>` / `findall UserWidget` to discover.
- **Hook not firing**: the path may be wrong (check the CXX header), **or the UFunction did not exist in
  memory when you registered it** — that is a hard limitation of `RegisterHook`. Register it after the
  class loads (`NotifyOnNewObject`, `RegisterBeginPlayPostHook`) or fall back to
  `RegisterCallFunctionByNameWithArgumentsPreHook`.
- **Crash on property access**: wrap every read in `pcall` + `IsValid`; avoid nested-struct and
  fixed-array reads, which can abort uncatchably.
- **Text is empty**: the property may be `FText`, not `FString` — check the type with `dumpclass`
  instead of guessing (`Prop:GetClass():GetFName():ToString()`).

---

## Phase 5: Polish

1. Adjust announcement timing (anti-spam intervals)
2. Add audio cues for spatial/contextual info
3. Ensure all menus are navigable
4. Test full game flow: main menu → gameplay → all features → end
5. Document which features are accessible
