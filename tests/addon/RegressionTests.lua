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
    Equal(sections.inventory.data.bags[1].freeSlots, 3)
    Equal(sections.inventory.data.bags[1].items[1].itemId, 1003)
    Equal(sections.inventory.data.bags[1].items[1].count, 7)
    Equal(sections.talents.data.activeConfigId, 101)
    Equal(sections.talents.data.trees[1].nodes[1].activeRank, 2)
    Equal(sections.talents.data.trees[1].nodes[1].entries[1].name, "Arcane Focus")
    Equal(sections.talents.data.trees[1].nodes[1].entries[1].iconFileId, 135892)
    Equal(sections.talents.data.trees[1].nodes[1].posX, 10)
    Equal(sections.talents.data.trees[1].nodes[1].posY, 20)
    Equal(sections.talents.data.trees[1].nodes[1].groupIds[1], 77)
    Equal(sections.talents.data.trees[1].nodes[1].visibleEdges[1].targetNodeId, 501)
    Equal(sections.talents.data.trees[1].nodes[1].visibleEdges[1].edgeType, 1)
    Equal(sections.talents.data.trees[1].nodes[1].visibleEdges[1].isActive, true)
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

Test("completed quest names are optional cached metadata with unchanged completion IDs", function()
    local game = MockGame.New()
    game:Login()
    local quests = game:Record().sections.quests
    Equal(quests.status, "complete")
    Equal(quests.data.completed[1], 5)
    Equal(quests.data.completedDetails[1].questId, 5)
    Equal(quests.data.completedDetails[1].title, "Jitters' Growling Gut")
    Equal(quests.data.completedDetails[2].title, "Ursangous's Paw")
    Equal(game.questRequests, 0)
    C_QuestLog.GetTitleForQuestID = nil
    game.ns.Snapshot.Capture()
    Equal(game:Record().sections.quests.status, "complete")
    Equal(game:Record().sections.quests.data.completedDetails[1].title, "Jitters' Growling Gut")
end)

Test("quest names observed before turn-in survive removal and reload", function()
    local game = MockGame.New()
    game:Login()
    C_QuestLog.GetNumQuestLogEntries = function() return 1, 0 end
    game.completed = { 5, 23, 36 }
    C_QuestLog.GetTitleForQuestID = nil
    game.ns.Snapshot.Capture()
    Equal(#game:Record().sections.quests.data.active, 0)
    Equal(game:Record().sections.quests.data.completedDetails[3].title, "Westfall Stew")
    local nextSession = MockGame.New(ClaudgarDB)
    nextSession.completed = { 5, 23, 36 }
    C_QuestLog.GetTitleForQuestID = nil
    nextSession:Login()
    Equal(nextSession:Record().sections.quests.data.completedDetails[3].title, "Westfall Stew")
    Equal(nextSession.questRequests, 0)
end)

Test("missing historical quest names use bounded reads and back off without requesting data", function()
    local game = MockGame.New()
    game.questTitles, game.completed = {}, {}
    for questId = 1, 140 do game.completed[#game.completed + 1] = questId end
    game:Login()
    Equal(game.titleReads, 50)
    game.ns.Snapshot.Capture()
    Equal(game.titleReads, 100)
    game.ns.Snapshot.Capture()
    Equal(game.titleReads, 139) -- Active quest 36 already supplies its title.
    game.ns.Snapshot.Capture()
    Equal(game.titleReads, 139)
    Equal(game:Record().sections.quests.status, "complete")
    game.now = game.now + 61
    game.ns.Snapshot.Capture()
    Equal(game.titleReads, 189)
    Equal(game.questRequests, 0)
end)

Test("older beta talent metadata stays complete and override icons take precedence", function()
    local game = MockGame.New()
    local readNode = C_Traits.GetNodeInfo
    C_Traits.GetNodeInfo = function(...)
        local node = readNode(...)
        node.groupIDs, node.visibleEdges = nil, nil
        return node
    end
    C_Traits.GetDefinitionInfo = function() return { spellID = 800, overrideIcon = 123456 } end
    game:Login()
    local talents = game:Record().sections.talents
    Equal(talents.status, "complete")
    Equal(#talents.data.trees[1].nodes[1].visibleEdges, 0)
    Equal(#talents.data.trees[1].nodes[1].groupIds, 0)
    Equal(talents.data.trees[1].nodes[1].entries[1].iconFileId, 123456)
end)

Test("zero and negative talent override icons fall back to spell icons", function()
    for _, overrideIcon in ipairs({ 0, -1 }) do
        local game = MockGame.New()
        C_Traits.GetDefinitionInfo = function()
            return { spellID = 800, overrideName = "Arcane Focus", overrideIcon = overrideIcon }
        end
        local spellReads = 0
        C_Spell.GetSpellInfo = function()
            spellReads = spellReads + 1
            return { name = "Arcane Focus", iconID = 135892 }
        end
        game:Login()
        local talents = game:Record().sections.talents
        Equal(talents.status, "complete")
        Equal(talents.data.trees[1].nodes[1].entries[1].iconFileId, 135892)
        Equal(spellReads > 0, true, "An invalid override must request the spell icon even when its name is present")
    end
end)

Test("nonpositive spell icons are omitted from saved talents", function()
    for _, spellIcon in ipairs({ 0, -1 }) do
        local game = MockGame.New()
        C_Spell.GetSpellInfo = function() return { name = "Arcane Focus", iconID = spellIcon } end
        game:Login()
        Equal(game:Record().sections.talents.data.trees[1].nodes[1].entries[1].iconFileId, nil)
    end
end)

Test("a valid talent icon override avoids an unnecessary spell lookup", function()
    local game = MockGame.New()
    C_Traits.GetDefinitionInfo = function()
        return { spellID = 800, overrideName = "Arcane Focus", overrideIcon = 123456 }
    end
    C_Spell.GetSpellInfo = function() error("The valid override must take precedence") end
    game:Login()
    local talents = game:Record().sections.talents
    Equal(talents.status, "complete")
    Equal(talents.data.trees[1].nodes[1].entries[1].iconFileId, 123456)
end)

Test("restricted talent connections and cached quest titles cannot reach the export", function()
    local game = MockGame.New()
    game.questTitles[5] = game.secret
    local readNode = C_Traits.GetNodeInfo
    C_Traits.GetNodeInfo = function(...)
        local node = readNode(...)
        node.visibleEdges[1].targetNode = game.secret
        node.groupIDs = { game.secret, 78 }
        return node
    end
    game:Login()
    local sections = game:Record().sections
    Equal(sections.quests.status, "partial")
    Equal(#sections.quests.data.completedDetails, 1)
    Equal(sections.talents.status, "partial")
    Equal(#sections.talents.data.trees[1].nodes[1].visibleEdges, 0)
    Equal(sections.talents.data.trees[1].nodes[1].groupIds[1], 78)
end)

Test("minimap click captures before the secure player-triggered reload", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    Equal(button.template, "SecureActionButtonTemplate")
    Equal(button.shown, true)
    Equal(button.width, 32)
    Equal(game.reloads, 0)
    game.money, game.now = 999, game.now + 10
    game:ClickMinimap(true)
    Equal(game.reloads, 0)
    game:ClickMinimap()
    Equal(game.reloads, 1)
    Equal(game.savedOnReload.sections.character.data.money, 999)
    Equal(game:Record(), game.savedOnReload)
end)

Test("combat minimap click never reloads later and requires another player click", function()
    local game = MockGame.New()
    game:Login()
    -- Even a previously armed secure action must stop before /reload in combat.
    game.ns.MinimapButton.frame:SetAttribute("type1", "macro")
    local record, reads = game:Record(), game.reads
    game.combat, game.money = true, 777
    game:ClickMinimap()
    Equal(game.reloads, 0)
    Equal(game:Record(), record)
    Equal(game.reads, reads)
    game.combat = false
    game:Fire("PLAYER_REGEN_ENABLED")
    game:Advance(0.6)
    Equal(game.reloads, 0)
    game:ClickMinimap()
    Equal(game.reloads, 1)
    Equal(game.savedOnReload.sections.character.data.money, 777)
end)

Test("an unsupported saved schema is preserved without creating minimap preferences", function()
    local saved = { schemaVersion = 999, characters = {} }
    local game = MockGame.New(saved)
    game:Login()
    Equal(ClaudgarDB, saved)
    Equal(ClaudgarDB.ui, nil)
    Equal(game.ns.MinimapButton.frame, nil)
    Equal(game.reloads, 0)
end)

Test("failed minimap capture cannot reload an older snapshot", function()
    local game = MockGame.New()
    game:Login()
    local record = game:Record()
    game:LeaveWorld()
    game:ClickMinimap()
    Equal(game.reloads, 0)
    Equal(game:Record(), record)
    Equal(game.ns.MinimapButton.frame.attributes.type1, nil)
end)

Test("minimap registers both drag buttons and hides its tooltip when dragging", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    Equal(button.clicks[1], "LeftButtonUp")
    Equal(button.drag[1], "LeftButton")
    Equal(button.drag[2], "RightButton")
    local tooltip = { lines = {} }
    function tooltip:SetOwner(owner, anchor) self.owner, self.anchor = owner, anchor end
    function tooltip:SetText(text) self.title = text end
    function tooltip:AddLine(text) self.lines[#self.lines + 1] = text end
    function tooltip:Show() self.shown = true end
    function tooltip:Hide() self.shown = false end
    GameTooltip = tooltip
    button.scripts.OnEnter(button)
    Equal(tooltip.owner, button)
    Equal(tooltip.shown, true)
    game:MouseDownMinimap()
    button.scripts.OnDragStart(button, "LeftButton")
    Equal(tooltip.shown, false)
    button.scripts.OnDragStop(button)
    GameTooltip = nil
end)

Test("left minimap drag persists its angle without collecting or reloading", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    local record, reads = game:Record(), game.reads
    game.cursorX, game.cursorY = 200, 100
    game:MouseDownMinimap()
    button.scripts.OnDragStart(button, "LeftButton")
    button.scripts.OnUpdate(button)
    button.scripts.OnDragStop(button)
    game:ClickMinimap(false, true)
    Equal(ClaudgarDB.ui.minimapAngle, 0)
    Equal(button.point[4], 78)
    Equal(button.point[5], 0)
    Equal(game:Record(), record)
    Equal(game.reads, reads)
    Equal(game.reloads, 0)
    local nextSession = MockGame.New(ClaudgarDB)
    nextSession:Login()
    Equal(nextSession.ns.MinimapButton.frame.point[4], 78)
    Equal(nextSession.ns.MinimapButton.frame.point[5], 0)
end)

for _, stopBeforeClick in ipairs({ true, false }) do
    local ordering = stopBeforeClick and "before" or "after"
    Test("left drag release cannot use stale reload when drag-stop runs " .. ordering .. " the click", function()
        local game = MockGame.New()
        game:Login()
        local button = game.ns.MinimapButton.frame
        local record, reads = game:Record(), game.reads
        button:SetAttribute("type1", "macro")
        game:MouseDownMinimap()
        Equal(button.attributes.type1, nil)
        button.scripts.OnDragStart(button, "LeftButton")
        -- Also exercise the release guard against a previously armed action.
        button:SetAttribute("type1", "macro")
        if stopBeforeClick then button.scripts.OnDragStop(button) end
        game:ClickMinimap(false, true)
        if not stopBeforeClick then button.scripts.OnDragStop(button) end
        Equal(button.attributes.type1, nil)
        Equal(game:Record(), record)
        Equal(game.reads, reads)
        Equal(game.reloads, 0)
        game.money = 777
        game:ClickMinimap()
        Equal(game.reloads, 1)
        Equal(game.savedOnReload.sections.character.data.money, 777)
    end)
end

Test("a normal click still refreshes after a left drag that emits no release click", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    local reads = game.reads
    button:SetAttribute("type1", "macro")
    game:MouseDownMinimap()
    button.scripts.OnDragStart(button, "LeftButton")
    button.scripts.OnDragStop(button)
    Equal(button.attributes.type1, nil)
    Equal(game.reads, reads)
    Equal(game.reloads, 0)
    game.money = 888
    game:ClickMinimap()
    Equal(game.reloads, 1)
    Equal(game.savedOnReload.sections.character.data.money, 888)
end)

Test("right minimap dragging still moves without collecting or reloading", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    local record, reads = game:Record(), game.reads
    game.cursorX, game.cursorY = 200, 100
    game:MouseDownMinimap("RightButton")
    button.scripts.OnDragStart(button, "RightButton")
    button.scripts.OnUpdate(button)
    button.scripts.OnDragStop(button)
    Equal(ClaudgarDB.ui.minimapAngle, 0)
    Equal(button.point[4], 78)
    Equal(button.point[5], 0)
    Equal(game:Record(), record)
    Equal(game.reads, reads)
    Equal(game.reloads, 0)
    game:ClickMinimap()
    Equal(game.reloads, 1)
end)

for _, releaseInCombat in ipairs({ true, false }) do
    local timing = releaseInCombat and "during combat" or "after combat"
    Test("combat interrupts left drag without refreshing on release " .. timing, function()
        local game = MockGame.New()
        game:Login()
        local button = game.ns.MinimapButton.frame
        local record, reads = game:Record(), game.reads
        game.cursorX, game.cursorY = 200, 100
        game:MouseDownMinimap()
        button.scripts.OnDragStart(button, "LeftButton")
        button.scripts.OnUpdate(button)
        game.combat, game.cursorX, game.cursorY = true, 100, 200
        button.scripts.OnUpdate(button)
        Equal(ClaudgarDB.ui.minimapAngle, 0)
        button.scripts.OnDragStop(button)
        if not releaseInCombat then game.combat = false end
        game:ClickMinimap(false, true)
        game.combat = false
        button.scripts.OnUpdate(button)
        Equal(ClaudgarDB.ui.minimapAngle, 0)
        Equal(game:Record(), record)
        Equal(game.reads, reads)
        Equal(game.reloads, 0)
        game:ClickMinimap()
        Equal(game.reloads, 1)
    end)
end

Test("a left drag started in combat cannot reload when released after combat", function()
    local game = MockGame.New()
    game:Login()
    local button = game.ns.MinimapButton.frame
    local record, reads = game:Record(), game.reads
    local x, y, angle = button.point[4], button.point[5], ClaudgarDB.ui.minimapAngle
    button:SetAttribute("type1", "macro")
    game.combat, game.cursorX, game.cursorY = true, 200, 100
    game:MouseDownMinimap()
    button.scripts.OnDragStart(button, "LeftButton")
    button.scripts.OnUpdate(button)
    Equal(button.attributes.type1, "macro")
    Equal(button.point[4], x)
    Equal(button.point[5], y)
    Equal(ClaudgarDB.ui.minimapAngle, angle)
    game.combat = false
    button.scripts.OnDragStop(button)
    game:ClickMinimap(false, true)
    Equal(button.attributes.type1, nil)
    Equal(game:Record(), record)
    Equal(game.reads, reads)
    Equal(game.reloads, 0)
    game:ClickMinimap()
    Equal(game.reloads, 1)
end)

dofile(TEST_ROOT .. "/CharacterStatsTests.lua")(MockGame, Test, Equal, LiveSections)
dofile(TEST_ROOT .. "/MinimapPortraitTests.lua")(MockGame, Test, Equal)

print(string.format("Addon regression tests: %d passed, %d failed", passed, failed))
assert(failed == 0, "Addon regression suite failed")
