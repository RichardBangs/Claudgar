local _, ns = ...
local Api = ns.Api
ns.Snapshot = { ready = false, inWorld = false, lastCharacterKey = nil, message = "Waiting for character login." }
local lastGuid, lastRealm

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

local function SavedGuid(record)
    local sections = type(record) == "table" and record.sections
    local character = type(sections) == "table" and sections.character
    local data = type(character) == "table" and character.data
    return type(data) == "table" and data.guid
end

local function CharacterKey(guid, realm)
    if guid == lastGuid and realm == lastRealm then return ns.Snapshot.lastCharacterKey end
    local records = ClaudgarDB.characters
    local preferred = realm and (realm .. ":" .. guid) or guid
    if SavedGuid(records[guid]) == guid then return guid end
    if SavedGuid(records[preferred]) == guid then return preferred end

    -- Earlier versions keyed records by realm. Keep a unique match so the
    -- companion can use the same character ID after the realm disappears.
    local match
    for key, record in pairs(records) do
        if type(key) == "string" and SavedGuid(record) == guid then
            if match then
                -- Multiple legacy records match. Start under the GUID and
                -- leave their independently observed sections intact.
                if records[guid] == nil then return guid end
                return nil
            end
            match = key
        end
    end
    if match then return match end
    if records[preferred] == nil then return preferred end
    return nil
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
    if type(identity.guid) ~= "string" or not identity.guid:find("%S")
        or type(identity.name) ~= "string" or not identity.name:find("%S") then
        ns.Snapshot.message = "The player name or GUID is restricted or still loading. Claudgar will retry."
        return false
    end
    local key = CharacterKey(identity.guid, identity.realm)
    if not key then
        ns.Snapshot.message = "The saved character key conflicts with another record. Existing snapshots were preserved."
        return false
    end
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
    lastGuid, lastRealm = identity.guid, identity.realm
    ns.Snapshot.message = "Snapshot collected. Reload or log out to save it for the companion."
    return true
end
