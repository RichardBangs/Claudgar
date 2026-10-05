local MockGame = dofile(TEST_ROOT .. "/MockGame.lua")
local passed, failed = 0, 0

local function Equal(actual, expected, message)
    if actual ~= expected then
        error((message or "Unexpected value") .. ": expected " .. tostring(expected)
            .. ", got " .. tostring(actual), 2)
    end
end

local function Test(name, run)
    local ok, message = pcall(run)
    if ok then
        passed = passed + 1
        print("PASS " .. name)
    else
        failed = failed + 1
        print("FAIL " .. name .. ": " .. tostring(message))
    end
end

local function LiveSections(game)
    local record = assert(game:Record(), "No character snapshot was published")
    for _, sectionName in ipairs(game.ns.Schema.sectionNames) do
        Equal(record.sections[sectionName].status, "complete", sectionName .. " status")
    end
    return record.sections
end

Test("live snapshot includes completed quests, equipment, bags, talents and character stats", function()
    local game = MockGame.New()
    game:Login()
    local sections = LiveSections(game)
    Equal(sections.character.data.money, 123456)
    Equal(sections.character.data.xp, 321)
    Equal(sections.character.data.maxXp, 1700)
    Equal(sections.quests.data.active[1].title, "Westfall Stew")
    Equal(#sections.quests.data.completed, 2)
    Equal(sections.quests.data.completed[1], 5)
    Equal(sections.quests.data.completed[2], 23)
    Equal(sections.equipment.data.items[1].itemId, 1001)
    Equal(sections.equipment.data.items[1].stats.ITEM_MOD_INTELLECT_SHORT, 3)
    Equal(sections.equipment.data.items[2].empty, true)
    Equal(sections.inventory.data.bags[1].slotCount, 4)
    Equal(sections.inventory.data.bags[1].items[1].itemId, 1003)
    Equal(sections.inventory.data.bags[1].items[1].count, 7)
    Equal(sections.talents.data.activeConfigId, 101)
    Equal(sections.talents.data.trees[1].nodes[1].activeRank, 2)
    Equal(sections.talents.data.trees[1].nodes[1].entries[1].name, "Arcane Focus")
end)

Test("logout after API teardown preserves every live section and observation time", function()
    local game = MockGame.New()
    game:Login()
    local before = assert(game:Record())
    local reads = game.reads
    game.now = game.now + 10
    game.live, game.inWorld = false, false
    game:Fire("PLAYER_LOGOUT")
    Equal(game:Record(), before, "SavedVariables record at logout")
    Equal(game.reads, reads, "Collector API calls at logout")
    for _, sectionName in ipairs(game.ns.Schema.sectionNames) do
        Equal(game:Record().sections[sectionName].observedAt, before.sections[sectionName].observedAt)
    end
    Equal(game:Record().sections.character.data.money, 123456)
    Equal(#game:Record().sections.quests.data.completed, 2)
    Equal(game:Record().sections.equipment.data.items[1].itemId, 1001)
    Equal(game:Record().sections.inventory.data.bags[1].items[1].count, 7)
end)

Test("pending updates and manual capture stay deferred during loading screens", function()
    local game = MockGame.New()
    game:Login()
    local before = game:Record()
    game:Fire("PLAYER_MONEY")
    game:LeaveWorld()
    local reads = game.reads
    game:Advance(10)
    Equal(game.ns.Snapshot.Capture(), false)
    Equal(game.reads, reads, "Collector API calls while out of world")
    Equal(game:Record(), before)
    game.money = 700
    game:EnterWorld()
    game:Advance(1.1)
    Equal(game:Record().sections.character.data.money, 700)
end)

Test("login does not publish default API values before entering the world", function()
    local game = MockGame.New()
    game:Fire("ADDON_LOADED", "Claudgar")
    game:Fire("PLAYER_LOGIN")
    game:Advance(10)
    Equal(game:Record(), nil)
    Equal(game.reads, 0)
    game:EnterWorld()
    game:Advance(1.1)
    Equal(game:Record().sections.character.data.money, 123456)
end)

Test("combat preserves earlier observations and captures pending updates on exit", function()
    local game = MockGame.New()
    game:Login()
    local before = game:Record()
    game.combat, game.money = true, 444
    game:Fire("PLAYER_MONEY")
    local reads = game.reads
    game:Advance(20)
    Equal(game:Record(), before)
    Equal(game.reads, reads)
    game.combat = false
    game:Fire("PLAYER_REGEN_ENABLED")
    game:Advance(0.6)
    Equal(game:Record().sections.character.data.money, 444)
    assert(game:Record().sections.character.observedAt > before.sections.character.observedAt)
end)

Test("a second character never inherits the previous character sections", function()
    local first = MockGame.New()
    first:Login()
    local previous = first:Record()
    local saved = ClaudgarDB
    local second = MockGame.New(saved)
    second.guid, second.name = "Player-1-200", "Second"
    second.money, second.completed = 10, { 81 }
    second.equipment, second.bagItems = {}, {}
    second:Login()
    Equal(second:Record("Player-1-100"), previous)
    Equal(second:Record().sections.character.data.name, "Second")
    Equal(second:Record().sections.character.data.money, 10)
    Equal(#second:Record().sections.quests.data.completed, 1)
    Equal(second:Record().sections.quests.data.completed[1], 81)
    Equal(second:Record().sections.equipment.data.items[1].empty, true)
    Equal(#second:Record().sections.inventory.data.bags[1].items, 0)
end)

Test("genuine live zero values and removed items replace previous observations", function()
    local game = MockGame.New()
    game:Login()
    game.money, game.xp, game.maxXp = 0, 0, 0
    game.equipment, game.bagItems = {}, {}
    game:Fire("PLAYER_MONEY")
    game:Fire("PLAYER_XP_UPDATE", "player")
    game:Fire("PLAYER_EQUIPMENT_CHANGED", 1)
    game:Fire("BAG_UPDATE_DELAYED")
    game:Advance(0.8)
    local sections = LiveSections(game)
    Equal(sections.character.data.money, 0)
    Equal(sections.character.data.xp, 0)
    Equal(sections.character.data.maxXp, 0)
    Equal(sections.equipment.data.items[1].empty, true)
    Equal(sections.equipment.data.items[1].itemId, nil)
    Equal(#sections.inventory.data.bags[1].items, 0)
end)

Test("Camelot spec-group talents work when the class talents API is absent", function()
    local game = MockGame.New()
    C_ClassTalents.GetActiveConfigID = nil
    game:Login()
    local talents = game:Record().sections.talents
    Equal(talents.status, "complete")
    Equal(talents.data.activeConfigId, 101)
    Equal(talents.data.trees[1].nodes[1].entries[1].spellId, 800)
end)

Test("talents fall back to the class config when the spec-group route is unavailable", function()
    local game = MockGame.New()
    C_SpecializationInfo.GetActiveSpecGroup = nil
    C_SpecializationInfo.GetCombatConfigIDForSpecGroup = nil
    game.classConfig = 101
    game:Login()
    Equal(game:Record().sections.talents.data.activeConfigId, 101)
    Equal(#game:Record().sections.talents.data.trees, 1)
end)

Test("restricted values are omitted without labelling restricted equipment empty", function()
    local game = MockGame.New()
    game.money = game.secret
    game.equipment[1] = game.secret
    game.bagItems[0][1].stackCount = game.secret
    game:Login()
    local sections = game:Record().sections
    Equal(sections.character.status, "partial")
    Equal(sections.character.data.money, nil)
    Equal(sections.equipment.status, "partial")
    Equal(sections.equipment.data.items[1].itemId, nil)
    Equal(sections.equipment.data.items[1].empty, nil)
    Equal(sections.inventory.status, "partial")
    Equal(sections.inventory.data.bags[1].items[1].count, nil)
    local function Check(value)
        assert(value ~= game.secret, "A restricted value escaped into SavedVariables")
        if type(value) == "table" then
            for key, child in pairs(value) do Check(key); Check(child) end
        end
    end
    Check(ClaudgarDB)
end)

Test("early empty sections are refreshed after client data finishes loading", function()
    local game = MockGame.New()
    game.completed, game.equipment, game.bagItems = {}, {}, {}
    game.bagSlots[0], game.talentReady = 0, false
    game:Login()
    Equal(#game:Record().sections.quests.data.completed, 0)
    Equal(game:Record().sections.inventory.data.bags[1].slotCount, 0)
    Equal(#game:Record().sections.talents.data.trees, 0)
    game.completed, game.equipment = { 23 }, { [1] = 1001 }
    game.bagSlots[0], game.talentReady = 4, true
    game.bagItems = { [0] = { [1] = { itemID = 1003, stackCount = 7 } } }
    game:Advance(5)
    Equal(#game:Record().sections.quests.data.completed, 1)
    Equal(game:Record().sections.equipment.data.items[1].itemId, 1001)
    Equal(game:Record().sections.inventory.data.bags[1].items[1].count, 7)
    Equal(#game:Record().sections.talents.data.trees, 1)
end)

Test("carried and equipped item identities survive an unavailable item details API", function()
    local game = MockGame.New()
    C_Item.GetItemInfo = nil
    game:Login()
    local sections = game:Record().sections
    Equal(sections.equipment.status, "partial")
    Equal(sections.equipment.data.items[1].itemId, 1001)
    Equal(sections.equipment.data.items[1].link, "item:1001")
    Equal(sections.inventory.status, "partial")
    Equal(sections.inventory.data.bags[1].items[1].itemId, 1003)
    Equal(sections.inventory.data.bags[1].items[1].count, 7)
    Equal(sections.inventory.data.bags[1].items[1].link, "item:1003")
end)

Test("an unreadable backpack is reported as partial and can recover on a bag event", function()
    local game = MockGame.New()
    game.bagSlots[0] = 0
    game:Login()
    Equal(game:Record().sections.inventory.status, "partial")
    game.bagSlots[0] = 4
    game:Fire("BAG_UPDATE_DELAYED")
    game:Advance(0.8)
    Equal(game:Record().sections.inventory.status, "complete")
    Equal(game:Record().sections.inventory.data.bags[1].items[1].itemId, 1003)
end)

Test("late talent data is retried and completed sections do not poll indefinitely", function()
    local game = MockGame.New()
    game.talentReady = false
    game:Login()
    game:Advance(5)
    Equal(#game:Record().sections.talents.data.trees, 0)
    game.talentReady = true
    game:Advance(10)
    Equal(#game:Record().sections.talents.data.trees, 1)
    game:Advance(15)
    local reads = game.reads
    game:Advance(60)
    game:Advance(60)
    Equal(game.reads, reads, "Collector polling after the bounded warm-up")
end)

print(string.format("Addon regression tests: %d passed, %d failed", passed, failed))
assert(failed == 0, "Addon regression suite failed")
