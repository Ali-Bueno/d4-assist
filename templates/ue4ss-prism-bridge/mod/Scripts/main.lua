--[[
    Accessibility Mod Template for UE4SS
    =====================================
    Entry point. Initializes all accessibility services.

    Usage:
    1. Copy this template folder to: GameDir/Binaries/Win64/Mods/GameAccessibility/
    2. Add "GameAccessibility : 1" to Mods/mods.txt
    3. Customize per game (see GAME-SPECIFIC SETUP section below)
]]

local UEHelpers = require("UEHelpers")

-- Load mod modules
local ScreenReader = require("screen_reader")
local GameStateTracker = require("game_state_tracker")
local UIReader = require("ui_reader")
local KeybindManager = require("keybind_manager")
local Announcer = require("announcer")

-- ============================================================
-- GAME-SPECIFIC SETUP (customize per game)
-- ============================================================

local MOD_NAME = "GameAccessibility"
local MOD_VERSION = "1.0.0"

-- ============================================================
-- INITIALIZATION
-- ============================================================

print("[" .. MOD_NAME .. "] v" .. MOD_VERSION .. " loading...\n")

-- Initialize screen reader (PRISM via prism_bridge)
local srInitialized = ScreenReader.Init()
if srInitialized then
    print("[" .. MOD_NAME .. "] Screen reader initialized\n")
else
    print("[" .. MOD_NAME .. "] WARNING: Screen reader not available\n")
end

-- Initialize announcer with screen reader
Announcer.Init(ScreenReader)

-- Initialize game state tracker
GameStateTracker.Init(Announcer)

-- Initialize UI reader
UIReader.Init(Announcer)

-- Initialize keybinds
KeybindManager.Init(ScreenReader, GameStateTracker, UIReader, Announcer)

-- ============================================================
-- GAME-SPECIFIC HOOKS (customize per game)
-- ============================================================

-- Hook map loading to detect level changes
RegisterLoadMapPostHook(function()
    GameStateTracker.OnMapLoaded()
end)

-- Hook BeginPlay to detect new actors
RegisterBeginPlayPostHook(function(Actor)
    ExecuteInGameThread(function()
        if not Actor:IsValid() then return end
        local className = Actor:GetClass():GetFName():ToString()
        GameStateTracker.OnActorBeginPlay(className, Actor)
    end)
end)

-- Watch for new UI widgets
NotifyOnNewObject("/Script/UMG.UserWidget", function(Widget)
    ExecuteInGameThread(function()
        if not Widget:IsValid() then return end
        UIReader.OnNewWidget(Widget)
    end)
end)

-- ============================================================
-- STARTUP ANNOUNCEMENT
-- ============================================================

if srInitialized then
    Announcer.Speak(MOD_NAME .. " version " .. MOD_VERSION .. " loaded", true)
end

print("[" .. MOD_NAME .. "] Fully loaded\n")
