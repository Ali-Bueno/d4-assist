--[[
    Utilities
    =========
    Shared helper functions for accessibility mods.
]]

local Utils = {}

-- Safe property read (returns nil on failure instead of crashing)
function Utils.SafeGet(object, propertyName)
    if not object or not object.IsValid or not object:IsValid() then
        return nil
    end
    local success, result = pcall(function()
        return object[propertyName]
    end)
    if success then return result end
    return nil
end

-- Safe method call (returns nil on failure)
function Utils.SafeCall(object, methodName, ...)
    if not object or not object.IsValid or not object:IsValid() then
        return nil
    end
    local success, result = pcall(function()
        return object[methodName](object, ...)
    end)
    if success then return result end
    return nil
end

-- Get class name of a UObject
function Utils.GetClassName(object)
    if not object or not object.IsValid or not object:IsValid() then
        return "invalid"
    end
    local success, result = pcall(function()
        return object:GetClass():GetFName():ToString()
    end)
    if success then return result end
    return "unknown"
end

-- Convert FText/FString/FName to Lua string safely
function Utils.ToStr(value)
    if not value then return "" end
    if type(value) == "string" then return value end
    local success, result = pcall(function()
        return value:ToString()
    end)
    if success then return result or "" end
    return ""
end

-- Iterate properties of a UObject (for discovery/debugging)
function Utils.DumpProperties(object)
    if not object or not object.IsValid or not object:IsValid() then
        print("[Utils] Invalid object\n")
        return
    end

    local class = object:GetClass()
    print("[Utils] Properties of " .. object:GetFullName() .. ":\n")

    class:ForEachProperty(function(prop)
        local name = prop:GetFName():ToString()
        local propClass = prop:GetClass():GetFName():ToString()
        print("  " .. name .. " (" .. propClass .. ")\n")
    end)
end

-- Iterate functions of a UObject (for discovery/debugging)
function Utils.DumpFunctions(object)
    if not object or not object.IsValid or not object:IsValid() then
        print("[Utils] Invalid object\n")
        return
    end

    local class = object:GetClass()
    print("[Utils] Functions of " .. object:GetFullName() .. ":\n")

    class:ForEachFunction(function(func)
        local name = func:GetFName():ToString()
        print("  " .. name .. "\n")
    end)
end

-- Find and list all instances of a class (for discovery)
function Utils.ListInstances(className)
    local objects = FindAllOf(className)
    if not objects then
        print("[Utils] No instances of " .. className .. " found\n")
        return {}
    end

    print("[Utils] Found " .. #objects .. " instances of " .. className .. ":\n")
    for i, obj in pairs(objects) do
        print("  [" .. i .. "] " .. obj:GetFullName() .. "\n")
    end
    return objects
end

return Utils
