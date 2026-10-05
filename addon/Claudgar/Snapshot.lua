local _, ns = ...
local Api = ns.Api
ns.Snapshot = { ready = false, inWorld = false, lastCharacterKey = nil, message = "Waiting for character login." }

function ns.Snapshot.Initialize()
    if not ns.enabled then return false end
    if ClaudgarDB == nil then
        ClaudgarDB = { schemaVersion = ns.Schema.version, client = ns.Client.info, characters = {} }
    end
    if type(ClaudgarDB) ~= "table" or ClaudgarDB.schemaVersion ~= ns.Schema.version
        or type(ClaudgarDB.characters) ~= "table" then
        ns.Snapshot.message = "The saved export has an unsupported schema. Its contents have been preserved."
        return false
    end
    ClaudgarDB.client = ns.Client.info
    ns.Snapshot.ready = true
    return true
end

function ns.Snapshot.SetSessionReadable(readable)
    ns.Snapshot.inWorld = readable == true
    if not ns.Snapshot.inWorld then
        ns.Snapshot.message = "Collection is paused while the character leaves the world. Earlier observation times are retained."
    end
end

function ns.Snapshot.IsRestricted()
    if not ns.Snapshot.inWorld then return true end
    if type(InCombatLockdown) ~= "function" then return true end
    local ok, restricted = pcall(InCombatLockdown)
    return not ok or restricted ~= false
end

function ns.Snapshot.RetrySections()
    local character = ns.Snapshot.lastCharacterKey and ClaudgarDB.characters[ns.Snapshot.lastCharacterKey]
    if type(character) ~= "table" or type(character.sections) ~= "table" then return ns.Schema.sectionNames end
    local names = {}
    for _, name in ipairs(ns.Schema.sectionNames) do
        local section = character.sections[name]
        if type(section) ~= "table" or section.status ~= "complete" then names[#names + 1] = name end
    end
    return names
end

local function Collect(name, observedAt, previous)
    local context = Api.Context()
    context.previousData = type(previous) == "table" and previous.data or nil
    local ok, data = pcall(ns.Collectors[name], context)
    if not ok or type(data) ~= "table" then
        return ns.Schema.Section(ns.Schema.EmptyData(name), "failed", observedAt,
            { "The section collector failed; other sections were still collected." },
            "Collector failed in the current game state.")
    end
    local normalized, normalizedData = pcall(ns.Schema.NormalizeData, context, name, data)
    if not normalized or type(normalizedData) ~= "table" then
        return ns.Schema.Section(ns.Schema.EmptyData(name), "failed", observedAt,
            { "The section did not match the export prototype." }, "Section normalization failed.")
    end
    data = normalizedData
    for _, warning in ipairs((ns.eventWarnings and ns.eventWarnings[name]) or {}) do
        Api.Warn(context, warning)
    end
    return ns.Schema.Section(data, Api.Status(context), observedAt, context.warnings)
end

function ns.Snapshot.Capture(dirty)
    if not ns.Snapshot.ready then return false end
    if not ns.Snapshot.inWorld then
        ns.Snapshot.message = "Collection is paused while the character leaves the world. Earlier observation times are retained."
        return false
    end
    if ns.Snapshot.IsRestricted() then
        ns.Snapshot.message = "Collection is deferred until combat ends. Earlier observation times are retained."
        return false
    end
    local clockContext = Api.Context()
    local observedAt = Api.Call(clockContext, "GetServerTime")
    if type(observedAt) ~= "number" or observedAt <= 0 then
        ns.Snapshot.message = "The game clock is unavailable; collection will retry."
        return false
    end
    local characterSection = Collect("character", observedAt)
    local identity = characterSection.data
    if type(identity.guid) ~= "string" or identity.guid == ""
        or type(identity.name) ~= "string" or identity.name == ""
        or type(identity.realm) ~= "string" or identity.realm == "" then
        ns.Snapshot.message = "Character identity is restricted or loading; collection will retry."
        return false
    end
    local key = ns.Schema.CharacterKey(identity.realm, identity.guid)
    local previous = ClaudgarDB.characters[key]
    local sections = {}
    if type(previous) == "table" and type(previous.sections) == "table" then
        for _, name in ipairs(ns.Schema.sectionNames) do sections[name] = previous.sections[name] end
    end
    sections.character = characterSection
    for _, name in ipairs(ns.Schema.sectionNames) do
        if name ~= "character" and (not dirty or dirty[name] or not sections[name]) then
            sections[name] = Collect(name, observedAt, sections[name])
        end
    end
    -- Publish one coherent character record only after all requested collectors
    -- finish. A failed section cannot corrupt another section or character.
    ClaudgarDB.characters[key] = { characterKey = key, sections = sections }
    ClaudgarDB.client = ns.Client.info
    ns.Snapshot.lastCharacterKey = key
    ns.Snapshot.message = "Snapshot collected. Reload or log out to save it for the companion."
    return true
end
