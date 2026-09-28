--[[
    Game State Tracker
    ==================
    Tracks the current game state (menu, gameplay, combat, etc.)
    and announces changes to the player.

    CUSTOMIZE PER GAME:
    - Add game-specific states
    - Add detection logic in OnActorBeginPlay
    - Add hooks for game-specific state transitions
]]

local GameStateTracker = {}

local announcer = nil

-- State constants (extend per game)
GameStateTracker.STATES = {
    UNKNOWN = "unknown",
    LOADING = "loading",
    MAIN_MENU = "main_menu",
    GAMEPLAY = "gameplay",
    PAUSE_MENU = "pause_menu",
    INVENTORY = "inventory",
    DIALOGUE = "dialogue",
    COMBAT = "combat",
    CUTSCENE = "cutscene",
}

local currentState = GameStateTracker.STATES.UNKNOWN
local previousState = GameStateTracker.STATES.UNKNOWN

function GameStateTracker.Init(ann)
    announcer = ann
end

function GameStateTracker.GetState()
    return currentState
end

function GameStateTracker.GetPreviousState()
    return previousState
end

function GameStateTracker.SetState(newState)
    if newState == currentState then return end

    previousState = currentState
    currentState = newState

    GameStateTracker.OnStateChanged(previousState, currentState)
end

function GameStateTracker.OnStateChanged(oldState, newState)
    if announcer then
        announcer.SpeakInterrupt(newState)
    end
    print("[GameState] " .. oldState .. " -> " .. newState .. "\n")
end

-- Called when a new map is loaded
function GameStateTracker.OnMapLoaded()
    GameStateTracker.SetState(GameStateTracker.STATES.LOADING)
end

-- Called when an actor begins play
-- CUSTOMIZE: Add detection logic based on actor class names
function GameStateTracker.OnActorBeginPlay(className, actor)
    -- Example detection (customize per game):
    -- if className == "MainMenuWidget_C" then
    --     GameStateTracker.SetState(GameStateTracker.STATES.MAIN_MENU)
    -- elseif className == "HUDWidget_C" then
    --     GameStateTracker.SetState(GameStateTracker.STATES.GAMEPLAY)
    -- end
end

return GameStateTracker
