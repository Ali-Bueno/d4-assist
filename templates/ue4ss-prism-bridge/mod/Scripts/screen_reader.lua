--[[
    Screen Reader Service
    =====================
    TTS output via PRISM (https://github.com/ethindp/prism), routed through the
    in-process Lua C module `prism_bridge.dll`, which statically links Lua 5.4.4
    (UE4SS's exact version, so the lua_State ABI matches) and dynamically loads
    prism.dll. PRISM speaks through whatever system screen reader is running
    (NVDA/JAWS/VoiceOver/Orca/…).

    SETUP: prism_bridge.dll + prism.dll + tolk.dll must sit in this Scripts/ folder
    (build them with src/prism_bridge/build.ps1, already prebuilt in the template).
    A system screen reader must be running to hear speech.

    This is the single speech sink; everything else calls Announcer, which calls here.
    If the bridge fails to load, falls back to print-only mode so the mod still runs.
]]

local ScreenReader = {}

local prism = nil
local isLoaded = false
local fallbackMode = false

function ScreenReader.Init()
    local success, result = pcall(function()
        return require("prism_bridge")
    end)

    if success and result and result.is_ready and result.is_ready() then
        prism = result
        isLoaded = true
        print("[ScreenReader] PRISM loaded, backend: " .. tostring(prism.detect and prism.detect()) .. "\n")
        return true
    else
        fallbackMode = true
        print("[ScreenReader] PRISM not available, using fallback (print only)\n")
        print("[ScreenReader] Error: " .. tostring(result) .. "\n")
        return false
    end
end

function ScreenReader.IsLoaded()
    return isLoaded
end

-- interrupt=true cuts off current speech (use on context changes); false queues.
function ScreenReader.Output(text, interrupt)
    if not text or text == "" then return end

    if isLoaded and prism then
        prism.say(text, interrupt or false)
    end

    -- Always log to console for debugging / when in fallback mode.
    if fallbackMode then
        print("[TTS] " .. text .. "\n")
    end
end

function ScreenReader.Silence()
    if isLoaded and prism then
        prism.stop()
    end
end

-- Name of the active backend (e.g. "NVDA"), or "none".
function ScreenReader.BackendName()
    return (isLoaded and prism and prism.detect()) or "none"
end

-- Optional voice tuning (floats).
function ScreenReader.SetRate(v)   if isLoaded and prism then prism.set_rate(v) end end
function ScreenReader.SetVolume(v) if isLoaded and prism then prism.set_volume(v) end end
function ScreenReader.SetPitch(v)  if isLoaded and prism then prism.set_pitch(v) end end

function ScreenReader.Shutdown()
    -- prism_bridge has no explicit Lua-side shutdown; the process teardown frees it.
    isLoaded = false
    prism = nil
end

return ScreenReader
