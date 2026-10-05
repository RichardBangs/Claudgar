local _, ns = ...

-- The versioned, language-independent contract lives in docs/DATA-CONTRACT.md
-- and schemas/export.schema.json. Lua uses 1-based arrays; SavedVariables writes
-- an empty array as {}, which the companion normalizes using the schema.
ns.Schema = {
    version = 1,
    sectionNames = { "character", "quests", "talents", "inventory", "equipment" },
    statuses = { complete = true, partial = true, unavailable = true, failed = true },
}
ns.Collectors = {}

-- This is the Lua side of the shared data prototype, using the same names as
-- schemas/export.schema.json. Primitive fields are optional when an API cannot
-- supply them; named [] fields always represent ordered lists. Stats are a
-- string-to-number object. Exported objects are projected through this table so
-- client API additions cannot leak arbitrary fields into the companion.
ns.Schema.prototype = {
    character = {
        guid = "string", name = "string", realm = "string", level = "number",
        classId = "number", className = "string", classToken = "string",
        raceId = "number", raceName = "string", raceToken = "string", faction = "string",
        locale = "string", zone = "string", subZone = "string", mapId = "number",
        money = "number", xp = "number", maxXp = "number", restedXp = "number", stats = "numberMap",
    },
    quests = { active = "quest[]", completed = "number[]", completedDetails = "completedQuest[]" },
    completedQuest = { questId = "number", title = "string" },
    quest = {
        questId = "number", title = "string", level = "number", suggestedGroup = "number",
        frequency = "number", isComplete = "boolean", isFailed = "boolean",
        isTask = "boolean", isHidden = "boolean", requiredMoney = "number", objectives = "objective[]",
    },
    objective = {
        text = "string", type = "string", finished = "boolean", numFulfilled = "number",
        numRequired = "number", objectiveType = "number",
    },
    talents = {
        mode = "string", activeConfigId = "number", activeSpecGroup = "number",
        configName = "string", hasStagedChanges = "boolean", trees = "tree[]",
    },
    tree = { treeId = "number", nodes = "node[]", currencies = "currency[]", groups = "group[]" },
    node = {
        nodeId = "number", activeEntryId = "number", activeRank = "number", currentRank = "number",
        ranksPurchased = "number", maxRanks = "number", isAvailable = "boolean", isVisible = "boolean",
        posX = "number", posY = "number", entries = "entry[]",
        groupIds = "number[]", visibleEdges = "edge[]",
    },
    edge = { targetNodeId = "number", edgeType = "number", visualStyle = "number", isActive = "boolean" },
    entry = {
        entryId = "number", definitionId = "number", spellId = "number", name = "string",
        rank = "number", maxRanks = "number", isActive = "boolean", iconFileId = "number",
    },
    currency = { currencyId = "number", quantity = "number", maxQuantity = "number", spent = "number" },
    group = { groupId = "number", name = "string", iconFileId = "number", currencies = "currency[]" },
    inventory = { bags = "bag[]" },
    bag = {
        bagId = "number", name = "string", slotCount = "number", freeSlots = "number",
        bagFamily = "number", items = "inventoryItem[]",
    },
    equipment = { items = "equippedItem[]" },
    item = {
        slot = "number", itemId = "number", link = "string", name = "string", count = "number",
        quality = "number", itemLevel = "number", requiredLevel = "number", classId = "number",
        subclassId = "number", className = "string", subclassName = "string", equipLocation = "string",
        iconFileId = "number", maxStackCount = "number", sellPrice = "number", isCraftingReagent = "boolean",
        durability = "number", maxDurability = "number", stats = "numberMap",
    },
    inventoryItem = { locked = "boolean", bound = "boolean" },
    equippedItem = { slotName = "string", slotIconFileId = "number", empty = "boolean" },
}
for _, itemType in ipairs({ "inventoryItem", "equippedItem" }) do
    for name, fieldType in pairs(ns.Schema.prototype.item) do
        ns.Schema.prototype[itemType][name] = fieldType
    end
end

local function NormalizeValue(context, value, fieldType, label)
    local elementType = fieldType:match("^(.-)%[%]$")
    if elementType then
        local result = {}
        for _, element in ipairs(ns.Api.Array(context, value, label)) do
            local normalized = NormalizeValue(context, element, elementType, label)
            if normalized ~= nil then result[#result + 1] = normalized end
        end
        return result
    end
    if value == nil then return nil end
    if fieldType == "numberMap" then
        if type(value) ~= "table" then
            ns.Api.Warn(context, label .. " is not a stat map.")
            return nil
        end
        local result = {}
        for token, amount in pairs(value) do
            if type(token) == "string" and type(amount) == "number" then
                result[token] = amount
            else
                ns.Api.Warn(context, label .. " contains an unsupported stat value.")
            end
        end
        return result
    end
    local prototype = ns.Schema.prototype[fieldType]
    if prototype then
        if type(value) ~= "table" then
            ns.Api.Warn(context, label .. " is not an object.")
            return nil
        end
        local result = {}
        for name, childType in pairs(prototype) do
            result[name] = NormalizeValue(context, value[name], childType, label .. "." .. name)
        end
        return result
    end
    return ns.Api.Typed(context, value, fieldType, label)
end

function ns.Schema.NormalizeData(context, sectionName, data)
    return NormalizeValue(context, data, sectionName, sectionName)
end

function ns.Schema.EmptyData(sectionName)
    if sectionName == "quests" then
        return { active = {}, completed = {}, completedDetails = {} }
    elseif sectionName == "talents" then
        return { mode = "traits", trees = {} }
    elseif sectionName == "inventory" then
        return { bags = {} }
    elseif sectionName == "equipment" then
        return { items = {} }
    end
    return {}
end

function ns.Schema.Section(data, status, observedAt, warnings, errorMessage)
    return {
        status = status,
        observedAt = observedAt,
        data = data,
        warnings = warnings or {},
        error = errorMessage,
    }
end

function ns.Schema.CharacterKey(realm, guid)
    -- GUIDs already identify a character; realm keeps the key readable. The
    -- companion adds installation/account isolation without exporting paths.
    return realm .. ":" .. guid
end
