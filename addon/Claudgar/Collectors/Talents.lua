local _, ns = ...
local Api = ns.Api

local currencyFields = {
    currencyId = { "traitCurrencyID", "number", true }, quantity = { "quantity", "number", true },
    maxQuantity = { "maxQuantity", "number" }, spent = { "spent", "number", true },
}
local nodeFields = {
    nodeId = { "ID", "number", true }, activeRank = { "activeRank", "number", true },
    currentRank = { "currentRank", "number", true }, ranksPurchased = { "ranksPurchased", "number", true },
    maxRanks = { "maxRanks", "number", true }, isAvailable = { "isAvailable", "boolean", true },
    isVisible = { "isVisible", "boolean", true }, posX = { "posX", "number" }, posY = { "posY", "number" },
}

local function Currencies(context, raw)
    local result = {}
    for _, info in ipairs(Api.Array(context, raw, "Talent currencies")) do
        if type(info) == "table" then
            result[#result + 1] = Api.Fields(context, info, currencyFields)
        else
            Api.Warn(context, "A talent currency is not available.")
        end
    end
    return result
end

local function Entries(context, configId, info, node)
    local result = {}
    if type(info.activeEntry) == "table" then
        node.activeEntryId = Api.Typed(context, info.activeEntry.entryID, "number", "Active talent entry", true)
    end
    for _, entryId in ipairs(Api.Array(context, info.entryIDs, "Talent entry IDs")) do
        if type(entryId) == "number" then
            local entryInfo = Api.Call(context, "C_Traits.GetEntryInfo", configId, entryId)
            if type(entryInfo) == "table" then
                local entry = {
                    entryId = entryId,
                    definitionId = entryInfo.definitionID,
                    maxRanks = entryInfo.maxRanks,
                    isActive = node.activeEntryId == entryId,
                }
                if node.activeEntryId then
                    entry.rank = entry.isActive and node.activeRank or 0
                elseif node.activeRank == 0 then
                    entry.rank = 0
                end
                if type(entry.definitionId) == "number" then
                    local definition = Api.Call(context, "C_Traits.GetDefinitionInfo", entry.definitionId)
                    if type(definition) == "table" then
                        entry.spellId = Api.Typed(context, definition.spellID, "number", "Talent spell ID")
                        entry.name = Api.Typed(context, definition.overrideName, "string", "Talent name")
                        if not entry.name and entry.spellId then
                            local spell = Api.Call(context, "C_Spell.GetSpellInfo", entry.spellId)
                            if type(spell) == "table" then entry.name = spell.name end
                        end
                        if not entry.name then Api.Warn(context, "A talent name is not available.") end
                    else
                        Api.Warn(context, "A talent definition is not available.")
                    end
                end
                result[#result + 1] = entry
            else
                Api.Warn(context, "A talent entry is not available.")
            end
        else
            Api.Warn(context, "A talent entry ID is not valid.")
        end
    end
    return result
end

local function Groups(context, configId, treeId)
    local result, groupIds = {}, {}
    local displays = Api.Call(context, "C_Traits.GetGroupDisplayInfoByTreeID", treeId)
    for _, display in ipairs(Api.Array(context, displays, "Talent groups")) do
        if type(display) == "table" and type(display.groupID) == "number" then
            groupIds[#groupIds + 1] = display.groupID
            result[#result + 1] = {
                groupId = display.groupID, name = display.displayName,
                iconFileId = display.icon, currencies = {},
            }
        else
            Api.Warn(context, "A talent group is not available.")
        end
    end
    if #groupIds > 0 then
        local byGroupId = {}
        local groupCurrency = Api.Call(context, "C_Traits.GetGroupCurrencyInfo", configId, groupIds)
        for _, info in ipairs(Api.Array(context, groupCurrency, "Talent group currencies")) do
            if type(info) == "table" and type(info.traitNodeGroupID) == "number" then
                byGroupId[info.traitNodeGroupID] = info.currencyInfos
            end
        end
        for _, group in ipairs(result) do
            group.currencies = Currencies(context, byGroupId[group.groupId])
        end
    end
    return result
end

function ns.Collectors.talents(context)
    local data = ns.Schema.EmptyData("talents")
    if not Api.Require(context, {
        "C_Traits.GetConfigInfo", "C_Traits.GetTreeNodes",
        "C_Traits.GetNodeInfo", "C_Traits.GetEntryInfo", "C_Traits.GetDefinitionInfo",
    }) then return data end
    if Api.Function("C_SpecializationInfo.GetActiveSpecGroup") then
        data.activeSpecGroup = Api.Call(context, "C_SpecializationInfo.GetActiveSpecGroup")
    end
    -- Forever's Camelot UI chooses configurations through its spec-group tabs.
    -- Prefer that route; the shared class-talents helper can be nil on Forever.
    if type(data.activeSpecGroup) == "number" and data.activeSpecGroup > 0
        and Api.Function("C_SpecializationInfo.GetCombatConfigIDForSpecGroup") then
        data.activeConfigId = Api.Call(context, "C_SpecializationInfo.GetCombatConfigIDForSpecGroup", data.activeSpecGroup)
    end
    if (type(data.activeConfigId) ~= "number" or data.activeConfigId <= 0)
        and Api.Function("C_ClassTalents.GetActiveConfigID") then
        data.activeConfigId = Api.Call(context, "C_ClassTalents.GetActiveConfigID")
    end
    if type(data.activeConfigId) ~= "number" or data.activeConfigId <= 0 then
        Api.Warn(context, "The active talent configuration is not available yet.")
        return data
    end
    local config = Api.Call(context, "C_Traits.GetConfigInfo", data.activeConfigId)
    if type(config) ~= "table" then
        Api.Warn(context, "The active talent configuration is not available yet.")
        return data
    end
    data.configName = config.name
    data.hasStagedChanges = Api.Call(context, "C_Traits.ConfigHasStagedChanges", data.activeConfigId)
    if data.hasStagedChanges then
        Api.Warn(context, "The talent UI has unapplied changes; currentRank/ranksPurchased may include them. activeRank describes active ranks.")
    end
    for _, treeId in ipairs(Api.Array(context, config.treeIDs, "Talent tree IDs")) do
        if type(treeId) == "number" then
            local tree = { treeId = treeId, nodes = {}, currencies = {}, groups = {} }
            local nodeIds = Api.Call(context, "C_Traits.GetTreeNodes", treeId)
            for _, nodeId in ipairs(Api.Array(context, nodeIds, "Talent node IDs")) do
                if type(nodeId) == "number" then
                    local info = Api.Call(context, "C_Traits.GetNodeInfo", data.activeConfigId, nodeId)
                    if type(info) == "table" then
                        local node = Api.Fields(context, info, nodeFields)
                        node.entries = Entries(context, data.activeConfigId, info, node)
                        tree.nodes[#tree.nodes + 1] = node
                    else
                        Api.Warn(context, "A talent node is not available.")
                    end
                else
                    Api.Warn(context, "A talent node ID is not valid.")
                end
            end
            tree.currencies = Currencies(context, Api.Call(context, "C_Traits.GetTreeCurrencyInfo", data.activeConfigId, treeId, true))
            tree.groups = Groups(context, data.activeConfigId, treeId)
            data.trees[#data.trees + 1] = tree
        else
            Api.Warn(context, "A talent tree ID is not valid.")
        end
    end
    return data
end
