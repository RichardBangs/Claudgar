local _, ns = ...
local Api = ns.Api
ns.CharacterStats = {}

-- These are current player totals from Forever's paper-doll APIs. Item stat
-- modifiers cannot replace them: equipment, talents, forms and buffs contribute.
local attributes = {
    { "strength", "LE_UNIT_STAT_STRENGTH" }, { "agility", "LE_UNIT_STAT_AGILITY" },
    { "stamina", "LE_UNIT_STAT_STAMINA" }, { "intellect", "LE_UNIT_STAT_INTELLECT" },
    { "spirit", "LE_UNIT_STAT_SPIRIT" },
}
local schools = { "Holy", "Fire", "Nature", "Frost", "Shadow", "Arcane" }

local function Call(context, name, ...)
    -- Optional capabilities vary across Forever builds. Missing functions are
    -- omissions; failures or restricted returns still use normal API warnings.
    if not Api.Function(name) then return nil end
    return Api.Call(context, name, ...)
end

local function Put(context, stats, name, value)
    value = Api.Typed(context, value, "number", "Character stat " .. name)
    if value ~= nil then
        if value == value and value ~= math.huge and value ~= -math.huge then
            stats[name] = value
        else
            Api.Warn(context, "Character stat " .. name .. " is not finite.")
        end
    end
end

local function AttackPower(context, stats, name, api)
    local base, positive, negative = Call(context, api, "player")
    -- Native paper-doll total: base + positive + negative. Never substitute zero
    -- for a missing/restricted component or operate on unsanitized API returns.
    if type(base) == "number" and type(positive) == "number" and type(negative) == "number" then
        Put(context, stats, name, base + positive + negative)
    end
end

function ns.CharacterStats.Collect(context)
    local stats = {}
    for _, attribute in ipairs(attributes) do
        local index = _G[attribute[2]]
        if type(index) == "number" then
            local _, effective = Call(context, "UnitStat", "player", index)
            Put(context, stats, attribute[1], effective)
        end
    end
    local _, armor = Call(context, "UnitArmor", "player")
    Put(context, stats, "armor", armor)
    -- The native health stat is maximum health. UnitHealth is a secret-return
    -- API in Forever; no current-health approximation or alternate lookup.
    Put(context, stats, "maxHealth", Call(context, "UnitHealthMax", "player"))
    Put(context, stats, "powerType", Call(context, "UnitPowerType", "player"))
    Put(context, stats, "power", Call(context, "UnitPower", "player"))
    Put(context, stats, "maxPower", Call(context, "UnitPowerMax", "player"))
    -- Power type 0 is mana, including a druid's alternate mana while shapeshifted.
    Put(context, stats, "mana", Call(context, "UnitPower", "player", 0))
    Put(context, stats, "maxMana", Call(context, "UnitPowerMax", "player", 0))

    local minimum, maximum, offMinimum, offMaximum = Call(context, "UnitDamage", "player")
    Put(context, stats, "meleeDamageMin", minimum)
    Put(context, stats, "meleeDamageMax", maximum)
    Put(context, stats, "offhandDamageMin", offMinimum)
    Put(context, stats, "offhandDamageMax", offMaximum)
    local mainSpeed, offSpeed = Call(context, "UnitAttackSpeed", "player")
    Put(context, stats, "meleeAttackSpeed", mainSpeed)
    Put(context, stats, "offhandAttackSpeed", offSpeed)
    local rangedSpeed, rangedMinimum, rangedMaximum = Call(context, "UnitRangedDamage", "player")
    Put(context, stats, "rangedAttackSpeed", rangedSpeed)
    Put(context, stats, "rangedDamageMin", rangedMinimum)
    Put(context, stats, "rangedDamageMax", rangedMaximum)
    AttackPower(context, stats, "meleeAttackPower", "UnitAttackPower")
    AttackPower(context, stats, "rangedAttackPower", "UnitRangedAttackPower")
    Put(context, stats, "meleeCritChance", Call(context, "GetCritChance"))
    Put(context, stats, "rangedCritChance", Call(context, "GetRangedCritChance"))
    -- Forever's spell crit function has no school argument.
    Put(context, stats, "spellCritChance", Call(context, "GetSpellCritChance"))

    local minimumBonus, complete = nil, true
    for index, school in ipairs(schools) do
        local bonus = Call(context, "GetSpellBonusDamage", index + 1)
        Put(context, stats, "spellPower" .. school, bonus)
        if type(bonus) == "number" then
            minimumBonus = minimumBonus and math.min(minimumBonus, bonus) or bonus
        else
            complete = false
        end
    end
    -- Native general spell-power display is the lowest magical-school bonus.
    if complete then Put(context, stats, "spellPower", minimumBonus) end
    Put(context, stats, "spellHealing", Call(context, "GetSpellBonusHealing"))
    local regen, combatRegen = Call(context, "GetManaRegen")
    Put(context, stats, "manaRegen", regen)
    Put(context, stats, "combatManaRegen", combatRegen)
    if next(stats) then return stats end
    return nil
end
