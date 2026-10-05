-- Representative account-wide export. This fixture is data only, never executable code.
ClaudgarDB = {
    ["schemaVersion"] = 1,
    ["client"] = {
        ["flavor"] = "forever", ["version"] = "1.60.1", ["build"] = "example",
        ["interface"] = 16001, ["locale"] = "enGB",
    },
    ["characters"] = {
        ["Player-1-00001234"] = {
            ["characterKey"] = "Player-1-00001234",
            ["sections"] = {
                ["character"] = {
                    ["status"] = "complete", ["observedAt"] = 1791198000,
                    ["data"] = {
                        ["guid"] = "Player-1-00001234", ["name"] = "Arwyn", ["realm"] = "Forever Beta",
                        ["level"] = 20, ["classId"] = 1, ["className"] = "Warrior", ["classToken"] = "WARRIOR",
                        ["raceId"] = 1, ["raceName"] = "Human", ["raceToken"] = "Human", ["faction"] = "Alliance",
                        ["locale"] = "enGB", ["zone"] = "Westfall", ["subZone"] = "Sentinel Hill", ["mapId"] = 52,
                        ["money"] = 12345, ["xp"] = 200, ["maxXp"] = 2000, ["restedXp"] = 100,
                    }, ["warnings"] = {},
                },
                ["quests"] = {
                    ["status"] = "complete", ["observedAt"] = 1791198001,
                    ["data"] = {
                        ["active"] = {
                            { ["questId"] = 123, ["title"] = "Example Quest", ["level"] = 20,
                              ["suggestedGroup"] = 0, ["frequency"] = 0, ["isComplete"] = false,
                              ["isFailed"] = false, ["isTask"] = false, ["isHidden"] = false, ["requiredMoney"] = 0,
                              ["objectives"] = {
                                  { ["text"] = "Example enemies slain: 2/8", ["type"] = "monster", ["finished"] = false,
                                    ["numFulfilled"] = 2, ["numRequired"] = 8, ["objectiveType"] = 0 },
                              },
                            },
                        }, ["completed"] = { 7, 33, 100 },
                    }, ["warnings"] = {},
                },
                ["talents"] = {
                    ["status"] = "complete", ["observedAt"] = 1791198002,
                    ["data"] = {
                        ["mode"] = "traits", ["activeConfigId"] = 1, ["activeSpecGroup"] = 1,
                        ["configName"] = "Arms", ["hasStagedChanges"] = false,
                        ["trees"] = {
                            { ["treeId"] = 1000, ["nodes"] = {
                                { ["nodeId"] = 1001, ["activeEntryId"] = 1002, ["activeRank"] = 1,
                                  ["currentRank"] = 1, ["ranksPurchased"] = 1, ["maxRanks"] = 3,
                                  ["isAvailable"] = true, ["isVisible"] = true, ["posX"] = 0.5, ["posY"] = 0.2,
                                  ["entries"] = {
                                      { ["entryId"] = 1002, ["definitionId"] = 1003, ["spellId"] = 1004,
                                        ["name"] = "Example Talent", ["rank"] = 1, ["maxRanks"] = 3, ["isActive"] = true },
                                  },
                                },
                              }, ["currencies"] = { { ["currencyId"] = 1, ["quantity"] = 2, ["maxQuantity"] = 5, ["spent"] = 3 } },
                              ["groups"] = { { ["groupId"] = 1, ["name"] = "Arms", ["iconFileId"] = 1234, ["currencies"] = {} } },
                            },
                        },
                    }, ["warnings"] = {},
                },
                ["inventory"] = {
                    ["status"] = "complete", ["observedAt"] = 1791198003,
                    ["data"] = { ["bags"] = {
                        { ["bagId"] = 0, ["name"] = "Backpack", ["slotCount"] = 16, ["freeSlots"] = 15, ["bagFamily"] = 0,
                          ["items"] = { { ["slot"] = 1, ["itemId"] = 6948, ["link"] = "item:6948:0:0:0:0:0:0:0",
                              ["name"] = "Hearthstone", ["count"] = 1, ["locked"] = false, ["bound"] = true,
                              ["quality"] = 1, ["itemLevel"] = 1, ["requiredLevel"] = 0, ["classId"] = 15,
                              ["subclassId"] = 0, ["className"] = "Miscellaneous", ["subclassName"] = "Junk",
                              ["equipLocation"] = "", ["iconFileId"] = 134414, ["maxStackCount"] = 1,
                              ["sellPrice"] = 0, ["isCraftingReagent"] = false, ["stats"] = {},
                          } },
                        },
                    } }, ["warnings"] = {},
                },
                ["equipment"] = {
                    ["status"] = "complete", ["observedAt"] = 1791198004,
                    ["data"] = { ["items"] = {
                        { ["slot"] = 16, ["slotName"] = "MainHandSlot", ["empty"] = false,
                          ["itemId"] = 25, ["link"] = "item:25:0:0:0:0:0:0:0", ["name"] = "Worn Shortsword",
                          ["count"] = 1, ["quality"] = 1, ["itemLevel"] = 2, ["requiredLevel"] = 1,
                          ["classId"] = 2, ["subclassId"] = 7, ["className"] = "Weapon", ["subclassName"] = "One-Handed Swords",
                          ["equipLocation"] = "INVTYPE_WEAPON", ["iconFileId"] = 135274, ["maxStackCount"] = 1,
                          ["sellPrice"] = 7, ["isCraftingReagent"] = false, ["durability"] = 10, ["maxDurability"] = 20,
                          ["stats"] = { ["ITEM_MOD_STRENGTH_SHORT"] = 1 },
                        }, { ["slot"] = 17, ["slotName"] = "SecondaryHandSlot", ["empty"] = true },
                    } }, ["warnings"] = {},
                },
            },
        },
    },
}
