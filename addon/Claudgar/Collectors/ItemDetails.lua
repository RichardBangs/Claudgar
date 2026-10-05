local _, ns = ...
local Api = ns.Api
local requestedItemIds = {}
ns.ItemDetails = {}

function ns.ItemDetails.Fill(context, item)
    local query = item.link or item.itemId
    if not query then
        Api.Warn(context, "An item identity is not available.")
        return item
    end
    local name, returnedLink, quality, itemLevel, requiredLevel, className, subclassName,
        maxStackCount, equipLocation, iconFileId, sellPrice, classId, subclassId,
        bindType, expansionId, setId, isCraftingReagent = Api.Call(context, "C_Item.GetItemInfo", query)
    if not name then
        Api.Warn(context, "Some item details are still loading.")
        if item.itemId and not requestedItemIds[item.itemId] and Api.Function("C_Item.RequestLoadItemDataByID") then
            requestedItemIds[item.itemId] = true
            Api.Call(context, "C_Item.RequestLoadItemDataByID", item.itemId)
        end
        return item
    end
    if item.itemId then requestedItemIds[item.itemId] = nil end
    item.name = name
    item.quality = quality
    item.itemLevel = itemLevel
    item.requiredLevel = requiredLevel
    item.className = className
    item.subclassName = subclassName
    item.classId = classId
    item.subclassId = subclassId
    item.maxStackCount = maxStackCount
    item.equipLocation = equipLocation
    item.iconFileId = iconFileId
    item.sellPrice = sellPrice
    item.isCraftingReagent = isCraftingReagent
    if item.link then
        local actualLevel = Api.Call(context, "C_Item.GetDetailedItemLevelInfo", item.link)
        if actualLevel ~= nil then item.itemLevel = actualLevel end
        local rawStats = Api.Call(context, "C_Item.GetItemStats", item.link)
        if type(rawStats) == "table" then
            item.stats = {}
            for token, value in pairs(rawStats) do
                if type(token) == "string" and type(value) == "number" then item.stats[token] = value end
            end
        end
    else
        Api.Warn(context, "An item's full variant link is not available.")
    end
    return item
end
