local _, ns = ...
local Api = ns.Api
ns.QuestNames = {}

-- Titles already observed in the quest log remain useful after turn-in and
-- across reloads. Missing historical titles are optional display metadata, not
-- missing completion records. Never request quest data from the server.
local retryAfter = {}
local lookupLimit, retryDelay = 50, 60

local function Remember(titles, records)
    if type(records) ~= "table" then return end
    for _, quest in ipairs(records) do
        if type(quest) == "table" and type(quest.questId) == "number"
            and type(quest.title) == "string" and quest.title ~= "" then
            titles[quest.questId] = quest.title
        end
    end
end

function ns.QuestNames.Completed(context, data)
    local titles, result = {}, {}
    local previous = context.previousData
    if type(previous) == "table" then
        Remember(titles, previous.completedDetails)
        Remember(titles, previous.active)
    end
    Remember(titles, data.active)
    local now, lookups = GetTime(), 0
    local canLookup = Api.Function("C_QuestLog.GetTitleForQuestID") ~= nil
    for _, questId in ipairs(data.completed) do
        local title = titles[questId]
        if not title and canLookup and lookups < lookupLimit
            and (not retryAfter[questId] or now >= retryAfter[questId]) then
            lookups = lookups + 1
            retryAfter[questId] = now + retryDelay
            local cached = Api.Call(context, "C_QuestLog.GetTitleForQuestID", questId)
            if type(cached) == "string" and cached ~= "" then title = cached end
        end
        if title then result[#result + 1] = { questId = questId, title = title } end
    end
    return result
end
