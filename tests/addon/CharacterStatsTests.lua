-- Exercises the real collectors with deterministic APIs, never a game client.
return function(MockGame, Test, Equal, LiveSections)
    Test("paper-doll stats use effective attributes and native totals", function()
        local game = MockGame.New()
        game:Login()
        local stats = LiveSections(game).character.data.stats
        Equal(stats.strength, 25)
        Equal(stats.agility, 26)
        Equal(stats.stamina, 27)
        Equal(stats.intellect, 28)
        Equal(stats.spirit, 29)
        Equal(stats.armor, 55)
        Equal(stats.maxHealth, 470)
        Equal(stats.health, nil, "Current health is not queried")
        Equal(stats.powerType, 0)
        Equal(stats.power, 450)
        Equal(stats.maxPower, 650)
        Equal(stats.mana, 450)
        Equal(stats.maxMana, 650)
        Equal(stats.meleeDamageMin, 8.5)
        Equal(stats.meleeDamageMax, 12.2)
        Equal(stats.offhandDamageMin, 2)
        Equal(stats.offhandDamageMax, 4)
        Equal(stats.meleeAttackSpeed, 2)
        Equal(stats.offhandAttackSpeed, 1.5)
        Equal(stats.rangedAttackSpeed, 2.6)
        Equal(stats.rangedDamageMin, 7.5)
        Equal(stats.rangedDamageMax, 11.3)
        Equal(stats.meleeAttackPower, 65)
        Equal(stats.rangedAttackPower, 68)
        Equal(stats.meleeCritChance, 1.2)
        Equal(stats.rangedCritChance, 2.3)
        Equal(stats.spellCritChance, 3.4)
        Equal(stats.spellPowerHoly, 17)
        Equal(stats.spellPowerArcane, 18)
        Equal(stats.spellPower, 17)
        Equal(stats.spellHealing, 25)
        Equal(stats.manaRegen, 5)
        Equal(stats.combatManaRegen, 2)
        Equal(LiveSections(game).equipment.data.items[1].stats.ITEM_MOD_INTELLECT_SHORT, 3,
            "Item modifiers remain separate")
    end)

    Test("unsupported optional stat APIs preserve complete identity coverage", function()
        local game = MockGame.New()
        for _, name in ipairs({ "UnitStat", "UnitArmor", "UnitHealthMax", "UnitPowerType", "UnitPower",
            "UnitPowerMax", "UnitDamage", "UnitAttackSpeed", "UnitRangedDamage", "UnitAttackPower",
            "UnitRangedAttackPower", "GetCritChance", "GetRangedCritChance", "GetSpellCritChance",
            "GetSpellBonusDamage", "GetSpellBonusHealing", "GetManaRegen" }) do
            _G[name] = nil
        end
        game:Login()
        Equal(LiveSections(game).character.data.stats, nil)
    end)

    Test("restricted stat components are omitted before arithmetic", function()
        local game = MockGame.New()
        game.characterStats.attributes[1][2] = game.secret
        game.characterStats.armor[2] = game.secret
        game.characterStats.attackPower[2] = game.secret
        game.characterStats.damage[1] = game.secret
        game.characterStats.spellPower[4] = game.secret
        game.characterStats.maxHealth = math.huge
        game:Login()
        local character = game:Record().sections.character
        Equal(character.status, "partial")
        local stats = character.data.stats
        Equal(stats.strength, nil)
        Equal(stats.agility, 26)
        Equal(stats.armor, nil)
        Equal(stats.maxHealth, nil)
        Equal(stats.meleeAttackPower, nil)
        Equal(stats.rangedAttackPower, 68)
        Equal(stats.meleeDamageMin, nil)
        Equal(stats.meleeDamageMax, 12.2)
        Equal(stats.spellPowerNature, nil)
        Equal(stats.spellPower, nil, "An unreadable school must not become a guessed general total")
        Equal(stats.spellPowerHoly, 17)
    end)

    Test("readable zero resources and stat values remain observed zeroes", function()
        local game = MockGame.New()
        game.characterStats.power, game.characterStats.mana = 0, 0
        game.characterStats.armor[2], game.characterStats.meleeCrit = 0, 0
        game.characterStats.manaRegen = { 0, 0 }
        game:Login()
        local stats = LiveSections(game).character.data.stats
        Equal(stats.power, 0)
        Equal(stats.mana, 0)
        Equal(stats.armor, 0)
        Equal(stats.meleeCritChance, 0)
        Equal(stats.manaRegen, 0)
        Equal(stats.combatManaRegen, 0)
    end)

    Test("alternate mana is distinct from the active resource", function()
        local game = MockGame.New()
        game.characterStats.powerType = 1
        game.characterStats.power, game.characterStats.maxPower = 30, 100
        game:Login()
        local stats = LiveSections(game).character.data.stats
        Equal(stats.powerType, 1)
        Equal(stats.power, 30)
        Equal(stats.maxPower, 100)
        Equal(stats.mana, 450)
        Equal(stats.maxMana, 650)
    end)

    for _, event in ipairs({ "PLAYER_EQUIPMENT_CHANGED", "TRAIT_CONFIG_UPDATED", "UNIT_STATS", "UNIT_AURA" }) do
        Test(event .. " refreshes the saved player stats", function()
            local game = MockGame.New()
            game:Login()
            game.characterStats.attributes[4][2] = 42
            game:Fire(event, "player")
            game:Advance(0.8)
            Equal(game:Record().sections.character.data.stats.intellect, 42)
        end)
    end

    Test("stat unit events ignore other units and restricted unit tokens", function()
        local game = MockGame.New()
        game:Login()
        local before, reads = game:Record(), game.reads
        game.characterStats.attributes[4][2] = 42
        game:Fire("UNIT_STATS", "target")
        game:Fire("UNIT_AURA", game.secret)
        game:Advance(0.8)
        Equal(game:Record(), before)
        Equal(game.reads, reads)
    end)

    Test("stat updates defer during combat and refresh on exit", function()
        local game = MockGame.New()
        game:Login()
        local before, reads = game:Record(), game.reads
        game.combat = true
        game.characterStats.attributes[4][2] = 42
        game:Fire("UNIT_STATS", "player")
        game:Advance(0.8)
        Equal(game:Record(), before)
        Equal(game.reads, reads)
        game.combat = false
        game:Fire("PLAYER_REGEN_ENABLED")
        game:Advance(0.6)
        Equal(game:Record().sections.character.data.stats.intellect, 42)
    end)

    Test("equipment exports empty-slot textures separately from item icons", function()
        local game = MockGame.New()
        game:Login()
        local items = LiveSections(game).equipment.data.items
        Equal(items[1].slotIconFileId, 10001)
        Equal(items[1].iconFileId, 123)
        Equal(items[2].slotIconFileId, 10002)
        Equal(items[2].iconFileId, nil)
        Equal(items[2].empty, true)
        local ammo
        for _, item in ipairs(items) do if item.slotName == "AmmoSlot" then ammo = item end end
        Equal(ammo.slot, 0)
        Equal(ammo.slotIconFileId, 10000)
    end)

    Test("unavailable slot textures do not invent an empty-slot icon", function()
        local game = MockGame.New()
        local slots = C_PaperDollInfo.GetInventorySlotInfo
        -- Return only the ID, as earlier supported snapshots did.
        C_PaperDollInfo.GetInventorySlotInfo = function(name)
            local slot = slots(name)
            return slot
        end
        game:Login()
        Equal(LiveSections(game).equipment.data.items[2].slotIconFileId, nil)
    end)

    Test("restricted slot textures are omitted without losing item identity", function()
        local game = MockGame.New()
        local slots = C_PaperDollInfo.GetInventorySlotInfo
        C_PaperDollInfo.GetInventorySlotInfo = function(name)
            local slot = slots(name)
            return slot, game.secret
        end
        game:Login()
        local equipment = game:Record().sections.equipment
        Equal(equipment.status, "partial")
        Equal(equipment.data.items[1].slotIconFileId, nil)
        Equal(equipment.data.items[1].itemId, 1001)
        Equal(equipment.data.items[1].iconFileId, 123)
    end)
end
