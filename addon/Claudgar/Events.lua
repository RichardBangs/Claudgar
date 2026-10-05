local addonName, ns = ...
if not ns.enabled then return end

local frame = CreateFrame("Frame")
local pendingAt, dueAt
local dirty = {}
local warmupStartedAt, warmupIndex
local warmupSteps = { { delay = 5, all = true }, { delay = 15 }, { delay = 30 } }
ns.eventWarnings = {}
ns.Events = {}

local eventSections = {
    PLAYER_LEVEL_UP = { "character", "talents" },
    PLAYER_XP_UPDATE = { "character" }, PLAYER_MONEY = { "character" },
    UPDATE_EXHAUSTION = { "character" },
    ZONE_CHANGED = { "character" }, ZONE_CHANGED_INDOORS = { "character" },
    ZONE_CHANGED_NEW_AREA = { "character" },
    QUEST_LOG_UPDATE = { "quests" }, QUEST_ACCEPTED = { "quests" },
    QUEST_REMOVED = { "quests" }, QUEST_TURNED_IN = { "quests" },
    QUEST_WATCH_UPDATE = { "quests" }, QUEST_DATA_LOAD_RESULT = { "quests" },
    PLAYER_TALENT_UPDATE = { "talents", "character" }, ACTIVE_TALENT_GROUP_CHANGED = { "talents", "character" },
    ACTIVE_COMBAT_CONFIG_CHANGED = { "talents", "character" }, TRAIT_CONFIG_UPDATED = { "talents", "character" },
    TRAIT_CONFIG_CREATED = { "talents", "character" }, TRAIT_NODE_CHANGED = { "talents", "character" },
    TRAIT_NODE_CHANGED_PARTIAL = { "talents", "character" }, TRAIT_NODE_ENTRY_UPDATED = { "talents", "character" },
    TRAIT_TREE_CURRENCY_INFO_UPDATED = { "talents", "character" }, TRAIT_TREE_CHANGED = { "talents", "character" },
    SPELL_DATA_LOAD_RESULT = { "talents" },
    ADDON_RESTRICTION_STATE_CHANGED = { "character", "quests", "talents", "inventory", "equipment" },
    BAG_UPDATE_DELAYED = { "inventory" }, BAG_UPDATE = { "inventory" },
    PLAYER_EQUIPMENT_CHANGED = { "equipment", "inventory", "character" },
    UNIT_INVENTORY_CHANGED = { "inventory", "equipment", "character" },
    UPDATE_INVENTORY_DURABILITY = { "inventory", "equipment" },
    GET_ITEM_INFO_RECEIVED = { "inventory", "equipment" },
    ITEM_DATA_LOAD_RESULT = { "inventory", "equipment" },
}

-- Optional player-only paper-doll updates. Unsupported events do not reduce
-- identity coverage; equipment/talent changes and full captures still refresh.
local statUnitEvents = {
    UNIT_STATS = true, UNIT_RESISTANCES = true, UNIT_ATTACK_POWER = true,
    UNIT_RANGED_ATTACK_POWER = true, UNIT_DAMAGE = true, UNIT_ATTACK_SPEED = true,
    UNIT_MAXHEALTH = true, UNIT_MAXPOWER = true, UNIT_POWER_UPDATE = true,
    UNIT_DISPLAYPOWER = true, UNIT_AURA = true,
}
for event in pairs(statUnitEvents) do eventSections[event] = { "character" } end

function ns.Events.Schedule(sections, delay)
    for _, name in ipairs(sections or ns.Schema.sectionNames) do dirty[name] = true end
    local now = GetTime()
    pendingAt = pendingAt or now
    -- Debounce bursts, but continuously arriving events cannot postpone a
    -- snapshot forever. OnUpdate needs no optional timer API or library.
    dueAt = math.min(now + (delay or 0.75), pendingAt + 3)
end

local function Register(event, sections, optional)
    local ok = pcall(frame.RegisterEvent, frame, event)
    if not ok and not optional then
        for _, name in ipairs(sections or ns.Schema.sectionNames) do
            ns.eventWarnings[name] = ns.eventWarnings[name] or {}
            ns.eventWarnings[name][#ns.eventWarnings[name] + 1] =
                event .. " is unavailable; some changes may only be captured on reload/logout."
        end
    end
end

Register("ADDON_LOADED")
Register("PLAYER_LOGIN")
Register("PLAYER_ENTERING_WORLD")
Register("PLAYER_LEAVING_WORLD")
Register("PLAYER_REGEN_ENABLED")
Register("PLAYER_LOGOUT")
for event, sections in pairs(eventSections) do Register(event, sections, statUnitEvents[event]) end

frame:SetScript("OnEvent", function(_, event, argument)
    if event == "ADDON_LOADED" then
        if argument == addonName then ns.Snapshot.Initialize() end
    elseif event == "PLAYER_LOGIN" then
        ns.Events.Schedule(nil, 1)
    elseif event == "PLAYER_ENTERING_WORLD" then
        ns.Snapshot.SetSessionReadable(true)
        warmupStartedAt, warmupIndex = GetTime(), 1
        ns.Events.Schedule(nil, 1)
    elseif event == "PLAYER_LEAVING_WORLD" then
        ns.Snapshot.SetSessionReadable(false)
        warmupStartedAt, warmupIndex = nil, nil
    elseif event == "PLAYER_LOGOUT" then
        -- Forever can already return zero/empty defaults here. SavedVariables
        -- saves the latest in-session snapshot without querying torn-down APIs.
        ns.Snapshot.SetSessionReadable(false)
        warmupStartedAt, warmupIndex = nil, nil
    elseif event == "PLAYER_REGEN_ENABLED" then
        if dueAt then ns.Events.Schedule(nil, 0.5) end
    elseif ns.Snapshot.ready then
        if statUnitEvents[event] or event == "UNIT_INVENTORY_CHANGED" or event == "PLAYER_XP_UPDATE" then
            -- Unit tokens can be secret too; check before comparing.
            if type(issecretvalue) ~= "function" then return end
            local checked, secret = pcall(issecretvalue, argument)
            if not checked or secret then return end
            if argument ~= "player" then return end
        end
        ns.Events.Schedule(eventSections[event])
    end
end)

frame:SetScript("OnUpdate", function()
    if not ns.Snapshot.ready or (not dueAt and not warmupIndex) then return end
    if ns.Snapshot.IsRestricted() then return end
    local now = GetTime()
    -- Some beta APIs initialize after entering the world. Refresh once, then
    -- retry incomplete sections a bounded number of times; ordinary events keep
    -- snapshots current after this warm-up without polling all collectors forever.
    while warmupIndex and warmupSteps[warmupIndex]
        and now >= warmupStartedAt + warmupSteps[warmupIndex].delay do
        local step = warmupSteps[warmupIndex]
        local sections = step.all and ns.Schema.sectionNames or ns.Snapshot.RetrySections()
        if #sections > 0 then ns.Events.Schedule(sections, 0) end
        warmupIndex = warmupIndex + 1
    end
    if warmupIndex and not warmupSteps[warmupIndex] then
        warmupStartedAt, warmupIndex = nil, nil
    end
    if not dueAt or now < dueAt then return end
    if ns.Snapshot.Capture(dirty) then
        dirty = {}
        pendingAt, dueAt = nil, nil
    else
        -- Identity and APIs can become readable after loading screens or leaving
        -- restricted content. Keep pending sections and retry at a modest rate.
        dueAt = GetTime() + 5
    end
end)
