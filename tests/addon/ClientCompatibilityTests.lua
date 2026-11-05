-- Verify release metadata through the actual addon load and save lifecycle.
-- API behavior remains simulated; these checks never launch a game client.
return function(MockGame, Test, Equal, LiveSections)
    Test("Forever release client loads and exports all sections with its actual build metadata", function()
        -- Blizzard-authored UI mirror version.txt, reviewed 5 November 2026:
        -- Gethe/wow-ui-source commit eac30af597e5aaa43a60f6675ef5fdcea100375f.
        local game = MockGame.New(nil, { version = "1.60.1", build = "70790", interface = 16001 })
        game:Login()
        Equal(game.ns.enabled, true)
        LiveSections(game)
        Equal(ClaudgarDB.client.flavor, "forever")
        Equal(ClaudgarDB.client.version, "1.60.1")
        Equal(ClaudgarDB.client.build, "70790")
        Equal(ClaudgarDB.client.interface, 16001)
        Equal(game:Record().sections.talents.data.activeConfigId, 101)
    end)

    Test("later Forever patch builds are not pinned to the launch build", function()
        local game = MockGame.New(nil, { version = "1.60.2", build = "99999", interface = 16002 })
        game:Login()
        LiveSections(game)
        Equal(ClaudgarDB.client.version, "1.60.2")
        Equal(ClaudgarDB.client.build, "99999")
        Equal(ClaudgarDB.client.interface, 16002)
    end)

    Test("Retail and other Classic clients cannot collect or overwrite a saved export", function()
        for _, client in ipairs({
            { version = "12.1.5", build = "70790", interface = 120105 },
            { version = "1.15.9", build = "70790", interface = 11509 },
            { version = "2.5.6", build = "70790", interface = 20506 },
            { version = "5.5.4", build = "70790", interface = 50504 },
            { version = "1.60.1", build = "70790", interface = 120105 },
            { version = "12.1.5", build = "70790", interface = 16001 },
            { version = "1.60.1", build = "70790", interface = "16001" },
        }) do
            local saved = { schemaVersion = 1, characters = {}, marker = "preserve" }
            local game = MockGame.New(saved, client)
            game:Login()
            Equal(game.ns.enabled, false, client.version .. " client guard")
            Equal(game.ns.Snapshot.Initialize(), false)
            Equal(game.reads, 0, "No character API reads on unsupported clients")
            Equal(#game.frames, 0, "No event handlers or minimap button on unsupported clients")
            Equal(SlashCmdList.CLAUDGAR, nil)
            Equal(ClaudgarDB, saved)
            Equal(ClaudgarDB.client, nil)
            Equal(ClaudgarDB.marker, "preserve")
        end
    end)
end
