--[[
    UI Reader
    =========
    Reads text from UMG widgets and announces changes.

    CUSTOMIZE PER GAME:
    - Identify specific widget class names (use Ctrl+J dump)
    - Hook game-specific UI update functions
    - Add ReadWidget implementations for each UI type
]]

local UIReader = {}

local announcer = nil

-- Track known widgets to avoid re-reading
local knownWidgets = {}

function UIReader.Init(ann)
    announcer = ann
end

-- Called when a new UserWidget appears
-- CUSTOMIZE: Filter and read relevant widgets
function UIReader.OnNewWidget(widget)
    if not widget:IsValid() then return end

    local className = widget:GetClass():GetFName():ToString()
    local fullName = widget:GetFullName()

    -- Avoid processing the same widget twice
    local addr = widget:GetAddress()
    if knownWidgets[addr] then return end
    knownWidgets[addr] = true

    print("[UIReader] New widget: " .. className .. "\n")

    -- CUSTOMIZE: Add game-specific widget reading here
    -- Example:
    -- if className == "MainMenuWidget_C" then
    --     UIReader.ReadMainMenu(widget)
    -- elseif className == "DialogueWidget_C" then
    --     UIReader.ReadDialogue(widget)
    -- end
end

-- Read all TextBlock children of a widget (generic approach)
function UIReader.ReadAllTextBlocks()
    local textBlocks = FindAllOf("TextBlock")
    if not textBlocks then return {} end

    local texts = {}
    for _, tb in pairs(textBlocks) do
        if tb:IsValid() then
            local success, text = pcall(function()
                return tb.Text:ToString()
            end)
            if success and text and text ~= "" then
                table.insert(texts, text)
            end
        end
    end
    return texts
end

-- Read a specific widget's text property
function UIReader.ReadWidgetText(widget, propertyName)
    if not widget:IsValid() then return nil end

    local success, text = pcall(function()
        local prop = widget[propertyName or "Text"]
        if prop then
            return prop:ToString()
        end
        return nil
    end)

    if success then return text end
    return nil
end

-- Announce all visible text (debug/discovery function)
function UIReader.AnnounceAllVisibleText()
    local texts = UIReader.ReadAllTextBlocks()
    if #texts > 0 then
        local combined = table.concat(texts, ". ")
        if announcer then
            announcer.Speak(combined, true)
        end
    end
end

-- Clean up references to destroyed widgets
function UIReader.Cleanup()
    local toRemove = {}
    for addr, _ in pairs(knownWidgets) do
        -- Periodically clean stale references
        toRemove[addr] = true
    end
    -- Note: Full cleanup would need IsValid checks on stored references
    -- For now, clear all on map change
    knownWidgets = {}
end

return UIReader
