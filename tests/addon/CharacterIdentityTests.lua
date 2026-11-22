return function(MockGame, Test, Equal)
    local client = { version = "1.60.1", build = "71037", interface = 16001 }

    local function OnlyRecord(expected)
        local key, record = next(ClaudgarDB.characters)
        Equal(record, expected)
        Equal(key, expected.characterKey)
        Equal(next(ClaudgarDB.characters, key), nil, "Unexpected second character")
    end

    local function SimulatedValue(state, game)
        if state == "empty" then return "" end
        if state == "restricted" then return game.secret end
        return nil
    end

    for _, realmState in ipairs({ "empty", "nil", "restricted" }) do
        Test("minimap save with a " .. realmState .. " realm exports the current character", function()
            local game = MockGame.New(nil, client)
            game.realm = SimulatedValue(realmState, game)
            game:Login()
            game.money = 700
            game:ClickMinimap()

            Equal(game.reloads, 1)
            local record = assert(game.savedOnReload)
            OnlyRecord(record)
            Equal(ClaudgarDB.client.build, "71037")
            local sections = record.sections
            Equal(sections.character.status, realmState == "restricted" and "partial" or "complete")
            Equal(sections.character.data.guid, game.guid)
            Equal(sections.character.data.name, game.name)
            Equal(sections.character.data.realm, nil)
            Equal(sections.character.data.money, 700)
            Equal(sections.quests.data.completed[1], 5)
            Equal(sections.talents.data.activeConfigId, 101)
            Equal(sections.inventory.data.bags[1].items[1].itemId, 1003)
            Equal(sections.equipment.data.items[1].itemId, 1001)
        end)
    end

    Test("a realm-less login reuses the previous realm-qualified character key", function()
        local old = MockGame.New()
        old:Login()
        local legacyKey = old.realm .. ":" .. old.guid
        Equal(old:Record().characterKey, legacyKey)

        local game = MockGame.New(ClaudgarDB, client)
        game.realm = ""
        game:Login()
        game.money = 705
        game:ClickMinimap()
        Equal(game.reloads, 1)
        local record = assert(game.savedOnReload)
        OnlyRecord(record)
        Equal(record.characterKey, legacyKey)
        Equal(record.sections.character.data.realm, nil)
        Equal(record.sections.character.data.money, 705)
    end)

    Test("a later readable realm leaves the GUID-only character key stable", function()
        local old = MockGame.New(nil, client)
        old.realm = ""
        old:Login()
        local key = assert(old:Record()).characterKey
        Equal(key, old.guid)

        local game = MockGame.New(ClaudgarDB, client)
        game:Login()
        game:ClickMinimap()
        local record = assert(game.savedOnReload)
        OnlyRecord(record)
        Equal(record.characterKey, key)
        Equal(record.sections.character.data.realm, game.realm)
    end)

    Test("an ambiguous legacy GUID leaves both realm-qualified records intact", function()
        local old = MockGame.New()
        old:Login()
        local first = assert(old:Record())
        local saved = ClaudgarDB
        local other = MockGame.New()
        other.realm = "Other Realm"
        other:Login()
        local second = assert(other:Record())
        saved.characters[second.characterKey] = second

        local game = MockGame.New(saved, client)
        game.realm = ""
        game:Login()
        game:ClickMinimap()
        Equal(game.reloads, 1)
        Equal(game.savedOnReload.characterKey, game.guid)
        Equal(ClaudgarDB.characters[first.characterKey], first)
        Equal(ClaudgarDB.characters[second.characterKey], second)
    end)

    Test("unavailable level, class and race APIs preserve readable player identity", function()
        local game = MockGame.New(nil, client)
        UnitLevel, UnitClass, UnitRace = nil, nil, nil
        game:Login()
        game:ClickMinimap()
        Equal(game.reloads, 1)
        local character = game.savedOnReload.sections.character
        Equal(character.status, "partial")
        Equal(character.data.guid, game.guid)
        Equal(character.data.name, game.name)
        Equal(character.data.realm, game.realm)
        Equal(character.data.level, nil)
    end)

    Test("characters with the same name and no realm keep separate records", function()
        local first = MockGame.New(nil, client)
        first.realm = ""
        first:Login()
        local previous = assert(first:Record())

        local second = MockGame.New(ClaudgarDB, client)
        second.realm, second.guid = "", "Player-2-100"
        second.money, second.completed = 8, { 81 }
        second:Login()
        second:ClickMinimap()
        Equal(second.reloads, 1)
        local record = assert(second.savedOnReload)
        Equal(record.sections.character.data.name, first.name)
        Equal(record.sections.character.data.guid, second.guid)
        Equal(record.sections.quests.data.completed[1], 81)
        Equal(ClaudgarDB.characters[previous.characterKey], previous)
        Equal(previous.sections.quests.data.completed[1], 5)
        assert(record.characterKey ~= previous.characterKey)
    end)

    for _, field in ipairs({ "guid", "name" }) do
        for _, state in ipairs({ "nil", "empty", "restricted" }) do
            Test("a " .. state .. " player " .. field .. " defers save until identity recovers", function()
                local game = MockGame.New(nil, client)
                game.realm = ""
                game:Login()
                local previous = assert(game:Record())
                local original = game[field]
                game[field] = SimulatedValue(state, game)
                game.money = 900
                game:ClickMinimap()
                game:Advance(1)
                OnlyRecord(previous)
                Equal(previous.sections.character.data.money, 123456)
                Equal(game.reloads, 0)

                game[field] = original
                game:Advance(6)
                Equal(game:Record().sections.character.data.money, 900)
                Equal(game.reloads, 0, "An automatic retry must not reload")
                game:ClickMinimap()
                Equal(game.reloads, 1)
            end)
        end
    end
end
