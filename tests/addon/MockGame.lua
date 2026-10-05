-- Deterministic WoW API/session fixture. The regression suite loads the actual
-- addon source, then delivers real lifecycle and update events to its frame.
local MockGame = {}

local slotNames = {
    "HeadSlot", "NeckSlot", "ShoulderSlot", "ShirtSlot", "ChestSlot", "WaistSlot",
    "LegsSlot", "FeetSlot", "WristSlot", "HandsSlot", "Finger0Slot", "Finger1Slot",
    "Trinket0Slot", "Trinket1Slot", "BackSlot", "MainHandSlot", "SecondaryHandSlot",
    "RangedSlot", "AmmoSlot", "TabardSlot",
}
local slots = {}
for index, name in ipairs(slotNames) do slots[name] = index end

function MockGame.New(saved)
    local game = {
        now = 0, serverTime = 1750000000, inWorld = false, live = false,
        combat = false, reads = 0, guid = "Player-1-100", name = "Tester",
        realm = "Forever", money = 123456, xp = 321, maxXp = 1700,
        completed = { 23, 5 }, equipment = { [1] = 1001, [16] = 1002 },
        bagSlots = { [0] = 4, [1] = 2, [2] = 0, [3] = 0, [4] = 0 },
        bagItems = { [0] = { [1] = { itemID = 1003, stackCount = 7 } } },
        talentReady = true, classConfig = 999, combatConfig = 101, frames = {},
        secret = {},
    }
    local function Read(value, teardownValue)
        game.reads = game.reads + 1
        if game.live then return value end
        return teardownValue
    end
    ClaudgarDB = saved
    SlashCmdList = {}
    issecretvalue = function(value) return value == game.secret end
    GetBuildInfo = function() return "1.60.0", "65000", "Oct 1 2026", 16001 end
    GetLocale = function() return "enUS" end
    GetTime = function() return game.now end
    GetServerTime = function() return Read(game.serverTime + math.floor(game.now), game.serverTime) end
    IsPlayerInWorld = function() return game.inWorld end
    InCombatLockdown = function() return game.combat end
    UnitGUID = function() return Read(game.guid, game.guid) end
    UnitName = function() return Read(game.name, game.name) end
    GetRealmName = function() return Read(game.realm, game.realm) end
    UnitLevel = function() return Read(12, 12) end
    UnitClass = function() return "Mage", "MAGE", 8 end
    UnitRace = function() return "Human", "Human", 1 end
    UnitFactionGroup = function() return "Alliance" end
    GetZoneText = function() return "Westfall" end
    GetSubZoneText = function() return "Sentinel Hill" end
    C_Map = { GetBestMapForUnit = function() return 1436 end }
    GetMoney = function() return Read(game.money, 0) end
    UnitXP = function() return Read(game.xp, 0) end
    UnitXPMax = function() return Read(game.maxXp, 0) end
    GetXPExhaustion = function() return Read(50, 0) end
    C_QuestLog = {
        GetNumQuestLogEntries = function() return 2, 1 end,
        GetInfo = function(index)
            if index == 1 then return { title = "Westfall", isHeader = true } end
            return { questID = 36, title = "Westfall Stew", level = 10, isHeader = false,
                suggestedGroup = 0, frequency = 0, isTask = false, isHidden = false }
        end,
        IsComplete = function() return false end,
        IsFailed = function() return false end,
        GetRequiredMoney = function() return 0 end,
        GetQuestObjectives = function()
            return { { text = "Goretusk Snout: 2/3", type = "item", finished = false,
                numFulfilled = 2, numRequired = 3, objectiveType = 1 } }
        end,
        GetNumQuestObjectives = function() return 1 end,
        GetAllCompletedQuestIDs = function() return Read(game.completed, {}) end,
    }
    C_PaperDollInfo = { GetInventorySlotInfo = function(name) return slots[name] end }
    GetInventoryItemID = function(_, slot) return Read(game.equipment[slot], nil) end
    GetInventoryItemLink = function(_, slot)
        local id = game.equipment[slot]
        if id == game.secret then return Read(game.secret, nil) end
        return Read(id and "item:" .. id, nil)
    end
    GetInventoryItemCount = function() return Read(1, 0) end
    GetInventoryItemDurability = function() return 22, 30 end
    NUM_TOTAL_EQUIPPED_BAG_SLOTS, NUM_BAG_SLOTS = 4, 4
    KEYRING_CONTAINER = -2
    C_Container = {
        GetContainerNumSlots = function(bag) return Read(game.bagSlots[bag], 0) end,
        GetContainerNumFreeSlots = function(bag) return game.bagSlots[bag], 0 end,
        GetBagName = function(bag) return bag == 0 and "Backpack" or "Bag " .. bag end,
        GetContainerItemInfo = function(bag, slot)
            local item = game.bagItems[bag] and game.bagItems[bag][slot]
            if not item then return Read(nil, nil) end
            return Read({ itemID = item.itemID, stackCount = item.stackCount,
                hyperlink = "item:" .. item.itemID, itemName = "Fixture Item",
                isLocked = false, isBound = false, quality = 2, iconFileID = 123 }, nil)
        end,
        GetContainerItemDurability = function() return nil, nil end,
    }
    C_ActionBar = { ShouldShowKeyring = function() return false end }
    GetKeyRingSize = function() return 0 end
    C_Item = {
        GetItemInfo = function(query)
            return "Fixture Item", tostring(query), 2, 14, 10, "Armor", "Cloth",
                20, "INVTYPE_HEAD", 123, 75, 4, 1, 1, 0, 0, false
        end,
        GetDetailedItemLevelInfo = function() return 15 end,
        GetItemStats = function() return { ITEM_MOD_INTELLECT_SHORT = 3 } end,
        RequestLoadItemDataByID = function() end,
    }
    C_ClassTalents = {
        GetActiveConfigID = function() return Read(game.classConfig, nil) end,
    }
    C_SpecializationInfo = {
        GetActiveSpecGroup = function() return Read(1, nil) end,
        GetCombatConfigIDForSpecGroup = function(group)
            assert(group == 1, "Expected active spec-group mapping")
            return Read(game.combatConfig, nil)
        end,
    }
    C_Traits = {
        GetConfigInfo = function(config)
            if game.live and game.talentReady and config == 101 then
                return { name = "Arcane", treeIDs = { 55 } }
            end
        end,
        ConfigHasStagedChanges = function() return false end,
        GetTreeNodes = function() return { 500 } end,
        GetNodeInfo = function(_, node)
            return { ID = node, activeRank = 2, currentRank = 2, ranksPurchased = 2,
                maxRanks = 5, isAvailable = true, isVisible = true, posX = 10, posY = 20,
                activeEntry = { entryID = 600 }, entryIDs = { 600 } }
        end,
        GetEntryInfo = function() return { definitionID = 700, maxRanks = 5 } end,
        GetDefinitionInfo = function() return { spellID = 800 } end,
        GetTreeCurrencyInfo = function()
            return { { traitCurrencyID = 900, quantity = 1, maxQuantity = 3, spent = 2 } }
        end,
        GetGroupDisplayInfoByTreeID = function() return {} end,
        GetGroupCurrencyInfo = function() return {} end,
    }
    C_Spell = { GetSpellInfo = function() return { name = "Arcane Focus" } end }
    CreateFrame = function()
        local frame = { scripts = {}, events = {} }
        function frame:RegisterEvent(event) self.events[event] = true end
        function frame:SetScript(script, callback) self.scripts[script] = callback end
        game.frames[#game.frames + 1] = frame
        return frame
    end

    -- Read TOC order instead of maintaining a second copy of the module list.
    game.ns = {}
    for line in io.lines(ADDON_ROOT .. "/Claudgar_Camelot.toc") do
        local module = line:match("^([^#].-%.lua)%s*$")
        if module then
            local chunk = assert(loadfile(ADDON_ROOT .. "/" .. module:gsub("\\", "/")))
            chunk("Claudgar", game.ns)
        end
    end
    function game:Fire(event, argument)
        for _, frame in ipairs(self.frames) do
            if frame.events[event] and frame.scripts.OnEvent then
                frame.scripts.OnEvent(frame, event, argument)
            end
        end
    end
    function game:Advance(seconds)
        self.now = self.now + seconds
        for _, frame in ipairs(self.frames) do
            if frame.scripts.OnUpdate then frame.scripts.OnUpdate(frame, seconds) end
        end
    end
    function game:EnterWorld()
        self.inWorld, self.live = true, true
        self:Fire("PLAYER_ENTERING_WORLD", true)
    end
    function game:LeaveWorld()
        self.inWorld, self.live = false, false
        self:Fire("PLAYER_LEAVING_WORLD")
    end
    function game:Login()
        self:Fire("ADDON_LOADED", "Claudgar")
        self:Fire("PLAYER_LOGIN")
        self:EnterWorld()
        self:Advance(1.1)
    end
    function game:Record(guid)
        return ClaudgarDB.characters[self.realm .. ":" .. (guid or self.guid)]
    end
    return game
end

return MockGame
