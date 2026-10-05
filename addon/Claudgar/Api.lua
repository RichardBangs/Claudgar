local _, ns = ...
local Api = {}
ns.Api = Api

local function Pack(...)
    return { n = select("#", ...), ... }
end

function Api.Context()
    return { warnings = {}, warningSet = {}, issueCount = 0, partial = false, unavailable = false }
end

function Api.Warn(context, message)
    context.partial = true
    context.issueCount = context.issueCount + 1
    if not context.warningSet[message] and #context.warnings < 40 then
        context.warningSet[message] = true
        context.warnings[#context.warnings + 1] = message
    end
end

function Api.Function(name)
    local value = _G
    for part in name:gmatch("[^.]+") do
        if type(value) ~= "table" then return nil end
        value = value[part]
    end
    if type(value) == "function" then return value end
    return nil
end

function Api.Require(context, names)
    local available = type(issecretvalue) == "function"
    if not available then Api.Warn(context, "Secret-value detection is unavailable.") end
    for _, name in ipairs(names) do
        if not Api.Function(name) then
            Api.Warn(context, name .. " is unavailable in this Forever build.")
            available = false
        end
    end
    if not available then context.unavailable = true end
    return available
end

-- Reject secrets before truthiness, equality, concatenation, numeric operations,
-- or table access. A pcall failure is not a way to reveal a secret value.
local function Sanitize(context, value, label, depth, visited, budget)
    if type(issecretvalue) ~= "function" then
        Api.Warn(context, "Secret-value detection is unavailable.")
        return nil
    end
    local checked, secret = pcall(issecretvalue, value)
    if not checked or secret then
        Api.Warn(context, label .. " contains a restricted value; it was omitted.")
        return nil
    end
    local valueType = type(value)
    if valueType == "nil" then return nil end
    if valueType == "string" or valueType == "boolean" then return value end
    if valueType == "number" then
        if value == value and value ~= math.huge and value ~= -math.huge then return value end
        Api.Warn(context, label .. " returned a non-finite number.")
        return nil
    end
    if valueType ~= "table" then
        -- API mixins/functions/userdata are never persisted.
        return nil
    end
    if depth > 12 or visited[value] then
        Api.Warn(context, label .. " returned unsupported nested data.")
        return nil
    end
    visited[value] = true
    local copy = {}
    local copied = pcall(function()
        for key, child in pairs(value) do
            budget.count = budget.count + 1
            if budget.count > 100000 then error("collection limit") end
            local safeKey = Sanitize(context, key, label, depth + 1, visited, budget)
            if type(safeKey) == "string" or type(safeKey) == "number" then
                copy[safeKey] = Sanitize(context, child, label, depth + 1, visited, budget)
            end
        end
    end)
    visited[value] = nil
    if not copied then
        Api.Warn(context, label .. " could not be safely read.")
        return nil
    end
    return copy
end

function Api.Call(context, name, ...)
    local fn = Api.Function(name)
    if not fn then
        Api.Warn(context, name .. " is unavailable in this Forever build.")
        return nil
    end
    local result = Pack(pcall(fn, ...))
    if not result[1] then
        -- Raw exception strings can contain restricted values. Export only the
        -- known API name, never stringify or persist an API's error object.
        Api.Warn(context, name .. " could not be read in the current game state.")
        return nil
    end
    local safe = { n = result.n - 1 }
    for index = 2, result.n do
        safe[index - 1] = Sanitize(context, result[index], name, 0, {}, { count = 0 })
    end
    return unpack(safe, 1, safe.n)
end

function Api.Typed(context, value, expectedType, label, required)
    if type(value) == expectedType then return value end
    if required or value ~= nil then Api.Warn(context, label .. " is not available.") end
    return nil
end

function Api.Array(context, value, label)
    if type(value) ~= "table" then
        Api.Warn(context, label .. " is not available.")
        return {}
    end
    -- Sanitization may remove a restricted element from the middle. Preserve
    -- remaining elements instead of letting ipairs stop at the first hole.
    local keys, result = {}, {}
    for key in pairs(value) do
        if type(key) == "number" and key >= 1 and key == math.floor(key) then
            keys[#keys + 1] = key
        end
    end
    table.sort(keys)
    for _, key in ipairs(keys) do result[#result + 1] = value[key] end
    return result
end

function Api.Fields(context, source, definitions)
    local result = {}
    for target, definition in pairs(definitions) do
        local sourceName = definition[1]
        result[target] = Api.Typed(context, source[sourceName], definition[2], sourceName, definition[3])
    end
    return result
end

function Api.Status(context)
    if context.unavailable then return "unavailable" end
    if context.partial then return "partial" end
    return "complete"
end
