--[[
    Announcer
    =========
    Anti-spam layer for screen reader output.
    Prevents duplicate messages and enforces minimum intervals.
]]

local Announcer = {}

local screenReader = nil
local lastMessage = ""
local lastTime = 0
local minInterval = 200  -- ms between identical messages

function Announcer.Init(sr)
    screenReader = sr
end

function Announcer.Speak(message, interrupt)
    if not message or message == "" then return end
    if not screenReader then return end

    local now = os.clock() * 1000
    if message == lastMessage and (now - lastTime) < minInterval then
        return
    end

    lastMessage = message
    lastTime = now
    screenReader.Output(message, interrupt or false)
end

function Announcer.SpeakUnique(message)
    if message and message ~= lastMessage then
        Announcer.Speak(message, false)
    end
end

function Announcer.SpeakInterrupt(message)
    Announcer.Speak(message, true)
end

function Announcer.Silence()
    if screenReader then
        screenReader.Silence()
    end
end

function Announcer.SetMinInterval(ms)
    minInterval = ms
end

function Announcer.GetLastMessage()
    return lastMessage
end

return Announcer
