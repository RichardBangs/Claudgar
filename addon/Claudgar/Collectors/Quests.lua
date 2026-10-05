local _, ns = ...
local Api = ns.Api

local questFields = {
    questId = { "questID", "number", true }, title = { "title", "string", true },
    level = { "level", "number" }, suggestedGroup = { "suggestedGroup", "number" },
    frequency = { "frequency", "number" }, isTask = { "isTask", "boolean" },
    isHidden = { "isHidden", "boolean" },
}
local objectiveFields = {
    text = { "text", "string", true }, type = { "type", "string", true },
    finished = { "finished", "boolean", true }, numFulfilled = { "numFulfilled", "number", true },
    numRequired = { "numRequired", "number", true }, objectiveType = { "objectiveType", "number" },
}

function ns.Collectors.quests(context)
    local data = ns.Schema.EmptyData("quests")
    if not Api.Require(context, { "C_QuestLog.GetNumQuestLogEntries", "C_QuestLog.GetInfo" }) then return data end
    local shown, questCount = Api.Call(context, "C_QuestLog.GetNumQuestLogEntries")
    shown = Api.Typed(context, shown, "number", "Quest log entry count", true)
    local count = shown and math.min(math.max(0, math.floor(shown)), 10000) or 0
    local seen = {}
    for index = 1, count do
        local info = Api.Call(context, "C_QuestLog.GetInfo", index)
        if type(info) ~= "table" then
            Api.Warn(context, "A quest log entry is not available.")
        elseif info.isHeader ~= true then
            local quest = Api.Fields(context, info, questFields)
            if quest.questId and quest.questId > 0 and not seen[quest.questId] then
                seen[quest.questId] = true
                quest.isComplete = Api.Call(context, "C_QuestLog.IsComplete", quest.questId)
                quest.isFailed = Api.Call(context, "C_QuestLog.IsFailed", quest.questId)
                quest.requiredMoney = Api.Call(context, "C_QuestLog.GetRequiredMoney", quest.questId)
                local objectives = Api.Call(context, "C_QuestLog.GetQuestObjectives", quest.questId)
                quest.objectives = {}
                -- MayReturnNothing is documented. A zero objective count really
                -- means an empty list; otherwise nil means missing data.
                if objectives == nil then
                    local objectiveCount = Api.Call(context, "C_QuestLog.GetNumQuestObjectives", quest.questId)
                    if objectiveCount ~= 0 then Api.Warn(context, "Quest objectives are not available.") end
                else
                    for _, objective in ipairs(Api.Array(context, objectives, "Quest objectives")) do
                        if type(objective) == "table" then
                            quest.objectives[#quest.objectives + 1] = Api.Fields(context, objective, objectiveFields)
                        else
                            Api.Warn(context, "A quest objective is not available.")
                        end
                    end
                end
                data.active[#data.active + 1] = quest
            end
        end
    end
    if type(questCount) == "number" and #data.active < questCount then
        -- Do not expand/collapse the user's quest UI to make a snapshot.
        Api.Warn(context, "Some active quests are hidden by the current quest log state.")
    end
    local completed = Api.Call(context, "C_QuestLog.GetAllCompletedQuestIDs")
    local completedSet = {}
    for _, questId in ipairs(Api.Array(context, completed, "Completed quest IDs")) do
        if type(questId) == "number" and questId > 0 and not completedSet[questId] then
            completedSet[questId] = true
            data.completed[#data.completed + 1] = questId
        else
            Api.Warn(context, "A completed quest ID is not valid.")
        end
    end
    table.sort(data.completed)
    return data
end
