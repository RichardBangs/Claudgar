local _, ns = ...
local Api = ns.Api

local function CollectBag(context, bagId, keyring)
    local slotCount
    if keyring then
        slotCount = Api.Call(context, "GetKeyRingSize")
    else
        slotCount = Api.Call(context, "C_Container.GetContainerNumSlots", bagId)
    end
    slotCount = Api.Typed(context, slotCount, "number", "Bag slot count", true)
    if not slotCount then return nil end
    if slotCount < 0 or slotCount > 1000 then
        Api.Warn(context, "A bag slot count is not valid.")
        return nil
    end
    local freeSlots, bagFamily = Api.Call(context, "C_Container.GetContainerNumFreeSlots", bagId)
    local bag = {
        bagId = bagId, slotCount = slotCount, freeSlots = freeSlots,
        bagFamily = bagFamily, name = Api.Call(context, "C_Container.GetBagName", bagId), items = {},
    }
    for slot = 1, slotCount do
        local issueCount = context.issueCount
        local info = Api.Call(context, "C_Container.GetContainerItemInfo", bagId, slot)
        if type(info) == "table" then
            local item = {
                slot = slot, itemId = info.itemID, link = info.hyperlink, name = info.itemName,
                count = info.stackCount, locked = info.isLocked, bound = info.isBound,
                quality = info.quality, iconFileId = info.iconFileID,
            }
            item.itemId = Api.Typed(context, item.itemId, "number", "Inventory item ID", true)
            item.count = Api.Typed(context, item.count, "number", "Inventory item count", true)
            item.link = Api.Typed(context, item.link, "string", "Inventory item link", true)
            item.durability, item.maxDurability = Api.Call(context, "C_Container.GetContainerItemDurability", bagId, slot)
            bag.items[#bag.items + 1] = ns.ItemDetails.Fill(context, item)
        elseif context.issueCount ~= issueCount then
            -- A missing/restricted record does not prove that a slot is empty.
            Api.Warn(context, "Some carried item slots could not be read.")
        elseif info ~= nil then
            Api.Warn(context, "An inventory item record is not valid.")
        end
    end
    return bag
end

function ns.Collectors.inventory(context)
    local data = ns.Schema.EmptyData("inventory")
    if not Api.Require(context, { "C_Container.GetContainerNumSlots", "C_Container.GetContainerItemInfo" }) then return data end
    local lastBag = NUM_TOTAL_EQUIPPED_BAG_SLOTS or NUM_BAG_SLOTS
    if type(lastBag) ~= "number" or lastBag < 0 or lastBag > 10 then
        Api.Warn(context, "The equipped bag count is unavailable in this Forever build.")
        return data
    end
    -- Backpack plus every equipped carried bag; bank/mail/auction storage is
    -- outside this first-version contract. Never enumerate bank bag IDs.
    for bagId = 0, lastBag do
        local bag = CollectBag(context, bagId)
        if bag then data.bags[#data.bags + 1] = bag end
        -- The backpack always has slots. A zero here means the container data
        -- is still loading, not that this character has an empty inventory.
        if bagId == 0 and (not bag or bag.slotCount == 0) then
            Api.Warn(context, "The backpack is not available yet; carried inventory is still loading.")
        end
    end
    -- The Forever branch's loaded ContainerFrame also includes the carried
    -- keyring when the game exposes it; it is not a bank container.
    local showKeyring = Api.Call(context, "C_ActionBar.ShouldShowKeyring")
    if showKeyring == true then
        if type(KEYRING_CONTAINER) == "number" and KEYRING_CONTAINER < 0 then
            local keyring = CollectBag(context, KEYRING_CONTAINER, true)
            if keyring then data.bags[#data.bags + 1] = keyring end
        else
            Api.Warn(context, "The carried keyring container is not available.")
        end
    end
    return data
end
