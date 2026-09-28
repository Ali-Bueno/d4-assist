--[[
    Keybind Manager
    ================
    Registers accessibility hotkeys for on-demand information.

    Default keybinds:
    - F1: Announce current game state
    - F2: Read player status (health, position, etc.)
    - F3: Read current UI / focused element
    - F4: Toggle verbose mode
    - F8: Speech pipeline test (says a fixed line + the active backend)
    - Ctrl+Escape: Silence screen reader

    CUSTOMIZE: Add game-specific keybinds as needed.
]]

local KeybindManager = {}

local screenReader = nil
local gameState = nil
local uiReader = nil
local announcer = nil

local verbose = false

function KeybindManager.Init(sr, gs, ui, ann)
    screenReader = sr
    gameState = gs
    uiReader = ui
    announcer = ann

    KeybindManager.RegisterDefaults()
end

function KeybindManager.RegisterDefaults()
    -- F1: Current game state
    RegisterKeyBind(Key.F1, function()
        if announcer then
            local state = gameState and gameState.GetState() or "unknown"
            announcer.SpeakInterrupt("State: " .. state)
        end
    end)

    -- F2: Player status
    RegisterKeyBind(Key.F2, function()
        ExecuteInGameThread(function()
            KeybindManager.AnnouncePlayerStatus()
        end)
    end)

    -- F3: Read current UI
    RegisterKeyBind(Key.F3, function()
        ExecuteInGameThread(function()
            if uiReader then
                uiReader.AnnounceAllVisibleText()
            end
        end)
    end)

    -- F4: Toggle verbose mode
    RegisterKeyBind(Key.F4, function()
        verbose = not verbose
        if announcer then
            announcer.SpeakInterrupt("Verbose mode: " .. (verbose and "on" or "off"))
        end
    end)

    -- F8: Speech pipeline test (validates screen reader end-to-end)
    RegisterKeyBind(Key.F8, function()
        if screenReader then
            screenReader.Output("Screen reader online. Backend: " .. screenReader.BackendName(), true)
        end
    end)

    -- Ctrl+Escape: Silence
    RegisterKeyBind(Key.ESCAPE, {ModifierKey.CONTROL}, function()
        if announcer then
            announcer.Silence()
        end
    end)

    print("[KeybindManager] Default keybinds registered\n")
end

function KeybindManager.AnnouncePlayerStatus()
    -- CUSTOMIZE: Read game-specific player data
    local UEHelpers = require("UEHelpers")
    local PC = UEHelpers:GetPlayerController()

    if not PC or not PC:IsValid() then
        if announcer then
            announcer.SpeakInterrupt("No player controller found")
        end
        return
    end

    local Pawn = PC.Pawn
    if not Pawn or not Pawn:IsValid() then
        if announcer then
            announcer.SpeakInterrupt("No pawn found")
        end
        return
    end

    -- Example: read location
    local success, result = pcall(function()
        local loc = Pawn:K2_GetActorLocation()
        return string.format("Position: X %.0f, Y %.0f, Z %.0f", loc.X, loc.Y, loc.Z)
    end)

    if success and result then
        if announcer then
            announcer.SpeakInterrupt(result)
        end
    else
        if announcer then
            announcer.SpeakInterrupt("Could not read player status")
        end
    end
end

function KeybindManager.IsVerbose()
    return verbose
end

return KeybindManager
