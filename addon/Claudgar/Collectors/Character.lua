local _, ns = ...
local Api = ns.Api

function ns.Collectors.character(context)
    if not Api.Require(context, { "UnitGUID", "UnitName", "GetRealmName", "UnitLevel", "UnitClass", "UnitRace" }) then
        return {}
    end
    local data = {}
    data.guid = Api.Typed(context, Api.Call(context, "UnitGUID", "player"), "string", "GUID", true)
    data.name = Api.Typed(context, Api.Call(context, "UnitName", "player"), "string", "Character name", true)
    data.realm = Api.Typed(context, Api.Call(context, "GetRealmName"), "string", "Realm", true)
    data.level = Api.Typed(context, Api.Call(context, "UnitLevel", "player"), "number", "Level", true)
    data.className, data.classToken, data.classId = Api.Call(context, "UnitClass", "player")
    data.raceName, data.raceToken, data.raceId = Api.Call(context, "UnitRace", "player")
    data.faction = Api.Call(context, "UnitFactionGroup", "player")
    data.locale = ns.Client.info.locale
    data.zone = Api.Call(context, "GetZoneText")
    data.subZone = Api.Call(context, "GetSubZoneText")
    data.mapId = Api.Call(context, "C_Map.GetBestMapForUnit", "player")
    data.money = Api.Call(context, "GetMoney")
    data.xp = Api.Call(context, "UnitXP", "player")
    data.maxXp = Api.Call(context, "UnitXPMax", "player")
    data.restedXp = Api.Call(context, "GetXPExhaustion")
    for _, name in ipairs({ "className", "classToken", "raceName", "raceToken", "faction", "zone" }) do
        data[name] = Api.Typed(context, data[name], "string", name, true)
    end
    for _, name in ipairs({ "classId", "raceId", "money", "xp", "maxXp" }) do
        data[name] = Api.Typed(context, data[name], "number", name, true)
    end
    if data.mapId == nil then Api.Warn(context, "The current map is not available.") end
    return data
end
