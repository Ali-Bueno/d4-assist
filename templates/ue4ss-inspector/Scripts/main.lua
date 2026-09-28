--[[
  UE4SS Inspector — non-visual, zero-restart class/object discovery.

  Drop this folder in  <Game>/Binaries/Win64/Mods/Inspector/  and add  "Inspector : 1"  to Mods/mods.txt.

  Three ways to drive it, all while the game keeps running:
    1. Console commands   (needs the in-game console: @ / F10, or the UE4SS GUI console)
    2. A COMMAND FILE     (no console needed at all — the accessible path):
         write a line into   inspector_cmd.txt   next to UE4SS.log, save it,
         read the answer in  inspector_out.txt   with your screen reader.
    3. Keybinds           (F-keys, only registered if free — see BINDS at the bottom)

  Commands:
    help                         list commands
    findall <Class>              every live instance of Class (short name)
    find <substr>                live UserWidgets/Actors whose full name contains substr
    dumpclass <Class|/Path>      full class chain: every property + type, every function
    props <Class> [n]            current scalar property values of instance n (default 1)
    funcs <Class>                functions of the class chain
    outer <Class>                the Outer chain of instance 1
    probe <Class>                1st call = baseline, next = diff (finds the selection index)
    watch <Class> <Prop> [ms]    poll one property, log every change (watch off = stop)
    dump <objects|cxx|uht|usmap|actors|meshes|luatypes>   trigger a built-in dumper in place

  Everything prints to the console AND appends to inspector_out.txt.
]]

local M = {}

local OUT_PATH = "inspector_out.txt"   -- relative to the game's working dir (next to UE4SS.log)
local CMD_PATH = "inspector_cmd.txt"
local POLL_MS  = 500

--------------------------------------------------------------------------------
-- output
--------------------------------------------------------------------------------

local echo_device = nil    -- FOutputDevice of the current console command, if any

local function out(line)
    line = tostring(line)
    print("[Inspector] " .. line .. "\n")
    local f = io.open(OUT_PATH, "a")
    if f then f:write(line, "\n"); f:close() end
    if echo_device then pcall(function() echo_device:Log(line) end) end
end

local function header(title)
    out("")
    out("======== " .. title .. " ========")
end

-- every game read goes through this: a bad deref must never take the game down
local function safe(fn, fallback)
    local ok, v = pcall(fn)
    if ok then return v end
    return fallback
end

--------------------------------------------------------------------------------
-- lookup helpers
--------------------------------------------------------------------------------

local function resolve(name, index)
    if not name then return nil end
    if name:find("/") then                       -- full path -> StaticFindObject
        local o = safe(function() return StaticFindObject(name) end)
        if o and o:IsValid() then return o end
        return nil
    end
    local all = safe(function() return FindAllOf(name) end)
    if all then
        local o = all[index or 1]
        if o and o:IsValid() then return o end
    end
    local o = safe(function() return FindFirstOf(name) end)
    if o and o:IsValid() then return o end
    return nil
end

local function class_of(obj)
    if safe(function() return obj:IsClass() end, false) then return obj end
    return safe(function() return obj:GetClass() end)
end

local function prop_type(prop)
    return safe(function() return prop:GetClass():GetFName():ToString() end, "?")
end

local SCALAR = {
    IntProperty = true, Int8Property = true, Int16Property = true, Int64Property = true,
    FloatProperty = true, DoubleProperty = true, BoolProperty = true,
    ByteProperty = true, EnumProperty = true,
}

--------------------------------------------------------------------------------
-- commands
--------------------------------------------------------------------------------

function M.help()
    header("commands")
    for _, l in ipairs({
        "findall <Class>", "find <substr>", "dumpclass <Class|/Path>", "props <Class> [n]",
        "funcs <Class>", "outer <Class>", "probe <Class>", "watch <Class> <Prop> [ms] | watch off",
        "dump <objects|cxx|uht|usmap|actors|meshes|luatypes>",
    }) do out("  " .. l) end
end

function M.findall(name)
    header("findall " .. tostring(name))
    local all = safe(function() return FindAllOf(name) end)
    if not all then return out("no instances") end
    for i, o in ipairs(all) do
        if o:IsValid() then
            out(("[%d] %s  @0x%X"):format(i, safe(function() return o:GetFullName() end, "?"),
                                          safe(function() return o:GetAddress() end, 0)))
        end
    end
end

function M.find(substr)
    header("find " .. tostring(substr))
    local needle = tostring(substr):lower()
    for _, root in ipairs({ "UserWidget", "Actor" }) do
        for _, o in ipairs(safe(function() return FindAllOf(root) end) or {}) do
            if o:IsValid() then
                local n = safe(function() return o:GetFullName() end, "")
                if n:lower():find(needle, 1, true) then out(n) end
            end
        end
    end
end

local function each_class(obj, fn)
    local c = class_of(obj)
    while c and safe(function() return c:IsValid() end, false) do
        fn(c)
        c = safe(function() return c:GetSuperStruct() end)
    end
end

function M.dumpclass(name)
    local obj = resolve(name)
    if not obj then return out("not found: " .. tostring(name)) end
    header("dumpclass " .. safe(function() return obj:GetFullName() end, tostring(name)))
    each_class(obj, function(c)
        out("-- " .. safe(function() return c:GetFullName() end, "?"))
        safe(function()
            c:ForEachProperty(function(p)
                out(("   %-44s %s"):format(safe(function() return p:GetFName():ToString() end, "?"), prop_type(p)))
            end)
        end)
        safe(function()
            c:ForEachFunction(function(f)
                out("   fn " .. safe(function() return f:GetFName():ToString() end, "?") .. "()")
            end)
        end)
    end)
end

function M.funcs(name)
    local obj = resolve(name)
    if not obj then return out("not found: " .. tostring(name)) end
    header("funcs " .. tostring(name))
    each_class(obj, function(c)
        safe(function()
            c:ForEachFunction(function(f) out(safe(function() return f:GetFName():ToString() end, "?")) end)
        end)
    end)
end

-- scalars only on purpose: struct/array reads can abort uncatchably (see the reflection cookbook)
local function scalars(obj)
    local t = {}
    each_class(obj, function(c)
        safe(function()
            c:ForEachProperty(function(p)
                local n, k = safe(function() return p:GetFName():ToString() end), prop_type(p)
                if n and SCALAR[k] then
                    local v = safe(function() return obj:GetPropertyValue(n) end)
                    if v ~= nil then t[n] = v end
                end
            end)
        end)
    end)
    return t
end

function M.props(name, n)
    local obj = resolve(name, tonumber(n) or 1)
    if not obj then return out("not found: " .. tostring(name)) end
    header("props " .. safe(function() return obj:GetFullName() end, tostring(name)))
    local t = scalars(obj)
    local keys = {}
    for k in pairs(t) do keys[#keys + 1] = k end
    table.sort(keys)
    for _, k in ipairs(keys) do out(("   %-44s %s"):format(k, tostring(t[k]))) end
end

function M.outer(name)
    local obj = resolve(name)
    if not obj then return out("not found: " .. tostring(name)) end
    header("outer chain")
    local o, depth = obj, 0
    while o and safe(function() return o:IsValid() end, false) and depth < 16 do
        out(("%s%s"):format(string.rep("  ", depth), safe(function() return o:GetFullName() end, "?")))
        o = safe(function() return o:GetOuter() end)
        depth = depth + 1
    end
end

local baselines = {}

function M.probe(name)
    local obj = resolve(name)
    if not obj then return out("not found: " .. tostring(name)) end
    local key = safe(function() return obj:GetAddress() end, 0)
    local now = scalars(obj)
    local before = baselines[key]
    baselines[key] = now
    if not before then
        return out(("probe baseline: %s (%d scalars). Move the cursor, then run probe again.")
                   :format(tostring(name), (function() local n = 0; for _ in pairs(now) do n = n + 1 end; return n end)()))
    end
    header("probe diff " .. tostring(name))
    local changed = false
    for k, v in pairs(now) do
        if before[k] ~= v then
            changed = true
            out(("   %-44s %s -> %s"):format(k, tostring(before[k]), tostring(v)))
        end
    end
    if not changed then out("   nothing changed (state is not reflected — see cookbook §5)") end
end

local watching = nil

function M.watch(name, prop, ms)
    if name == "off" then watching = nil; return out("watch off") end
    if not prop then return out("usage: watch <Class> <Prop> [ms]") end
    watching = { name = name, prop = prop, last = nil }
    local period = tonumber(ms) or 250
    out(("watching %s.%s every %dms"):format(name, prop, period))
    LoopAsync(period, function()
        local w = watching
        if not w or w.name ~= name or w.prop ~= prop then return true end   -- superseded -> stop
        ExecuteInGameThread(function()
            local obj = resolve(w.name)
            if not obj then return end
            local v = safe(function() return obj:GetPropertyValue(w.prop) end)
            if v ~= w.last then
                out(("%s.%s = %s"):format(w.name, w.prop, tostring(v)))
                w.last = v
            end
        end)
        return false
    end)
end

-- built-in dumpers, triggered in place. Names differ between UE4SS builds, so probe before calling.
local DUMPERS = {
    objects  = { "DumpAllObjects" },
    cxx      = { "GenerateSDK", "DumpCXXHeaders" },
    uht      = { "GenerateUHTCompatibleHeaders" },
    usmap    = { "DumpUSMAP" },
    actors   = { "DumpAllActors" },
    meshes   = { "DumpStaticMeshes" },
    luatypes = { "GenerateLuaTypes", "DumpLuaBindings" },
}

function M.dump(what)
    local names = DUMPERS[tostring(what)]
    if not names then return out("usage: dump <objects|cxx|uht|usmap|actors|meshes|luatypes>") end
    for _, fname in ipairs(names) do
        if type(_G[fname]) == "function" then
            out("running " .. fname .. "() ...")
            ExecuteInGameThread(function()
                local ok, err = pcall(_G[fname])
                out(ok and (fname .. "() done") or (fname .. "() failed: " .. tostring(err)))
            end)
            return
        end
    end
    out(("no dumper global found for '%s' in this UE4SS build (tried: %s). Use the keybind instead.")
        :format(tostring(what), table.concat(names, ", ")))
end

--------------------------------------------------------------------------------
-- dispatch
--------------------------------------------------------------------------------

local function dispatch(line, device)
    local args = {}
    for w in tostring(line):gmatch("%S+") do args[#args + 1] = w end
    local cmd = table.remove(args, 1)
    if not cmd then return end
    local fn = M[cmd:lower()]
    echo_device = device
    if fn then
        local ok, err = pcall(fn, table.unpack(args))
        if not ok then out("error: " .. tostring(err)) end
    else
        out("unknown command: " .. cmd .. " (try 'help')")
    end
    echo_device = nil
end

-- 1. console commands.
--    "inspect <cmd> ..." always works; the distinctive command names are also registered directly.
--    help / find / dump are NOT registered on their own: too likely to collide with a game command
--    (reach them as "inspect help", "inspect find Pause", "inspect dump cxx").
for _, prefix in ipairs({ "inspect", "ins" }) do
    RegisterConsoleCommandGlobalHandler(prefix, function(_, params, device)
        dispatch(table.concat(params, " ", 2), device); return true
    end)
end
for _, name in ipairs({ "dumpclass", "findall", "props", "funcs", "outer", "probe", "watch" }) do
    RegisterConsoleCommandGlobalHandler(name, function(_, params, device)
        dispatch(table.concat(params, " "), device); return true
    end)
end

-- 2. command file: write a line into inspector_cmd.txt, save, read inspector_out.txt
local last_cmd = nil
LoopAsync(POLL_MS, function()
    local f = io.open(CMD_PATH, "r")
    if f then
        local line = f:read("*l")
        f:close()
        if line and line ~= "" and line ~= last_cmd then
            last_cmd = line
            ExecuteInGameThread(function() dispatch(line, nil) end)
        end
    end
    return false
end)

-- 3. optional keybinds — only claimed if the game's own mod hasn't taken them
local BINDS = {
    -- { Key.F10, {ModifierKey.CONTROL}, function() M.probe("YourWidget_C") end },
}
for _, b in ipairs(BINDS) do
    if not IsKeyBindRegistered(b[1], b[2]) then RegisterKeyBind(b[1], b[2], b[3]) end
end

out("Inspector ready — console: 'help' | file: write a command into " .. CMD_PATH)
return M
