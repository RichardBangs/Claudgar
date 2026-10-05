local _, ns = ...
local Api = ns.Api

-- Inventory slot tokens documented by the client; resolve numeric IDs through
-- the API instead of assuming Retail's active equipment slots (Forever has a
-- ranged slot). Include shirt/tabard as well as stat-bearing equipment.
local slotNames = {
    "HeadSlot", "NeckSlot", "ShoulderSlot", "ShirtSlot", "ChestSlot", "WaistSlot",
    "LegsSlot", "FeetSlot", "WristSlot", "HandsSlot", "Finger0Slot", "Finger1Slot",
    "Trinket0Slot", "Trinket1Slot", "BackSlot", "MainHandSlot", "SecondaryHandSlot",
    "RangedSlot", "AmmoSlot", "TabardSlot",
}

function ns.Collectors.equipment(context)
    local data = ns.Schema.EmptyData("equipment")
    if not Api.Require(context, { "C_PaperDollInfo.GetInventorySlotInfo", "GetInventoryItemID", "GetInventoryItemLink", "C_Item.GetItemInfo" }) then return data end
    for _, slotName in ipairs(slotNames) do
        local slot = Api.Call(context, "C_PaperDollInfo.GetInventorySlotInfo", slotName)
        if type(slot) == "number" then
            local item = { slot = slot, slotName = slotName }
            local issueCount = context.issueCount
            item.itemId = Api.Call(context, "GetInventoryItemID", "player", slot)
            item.link = Api.Call(context, "GetInventoryItemLink", "player", slot)
            if item.itemId or item.link then
                item.empty = false
                item.count = Api.Call(context, "GetInventoryItemCount", "player", slot)
                item.durability, item.maxDurability = Api.Call(context, "GetInventoryItemDurability", slot)
                item.itemId = Api.Typed(context, item.itemId, "number", "Equipment item ID", true)
                item.link = Api.Typed(context, item.link, "string", "Equipment item link", true)
                ns.ItemDetails.Fill(context, item)
            elseif context.issueCount == issueCount then
                item.empty = true
            end
            data.items[#data.items + 1] = item
        else
            Api.Warn(context, slotName .. " could not be resolved in this Forever build.")
        end
    end
    return data
end
