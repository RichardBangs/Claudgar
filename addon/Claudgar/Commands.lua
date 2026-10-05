local _, ns = ...
if not ns.enabled then return end

local function Print(message)
    if DEFAULT_CHAT_FRAME then DEFAULT_CHAT_FRAME:AddMessage("|cff70c8ffClaudgar:|r " .. message) end
end

SLASH_CLAUDGAR1 = "/claudgar"
SlashCmdList.CLAUDGAR = function(command)
    command = (command or ""):lower():match("^%s*(.-)%s*$")
    if command == "snapshot" then
        if not ns.Snapshot.Capture() then ns.Events.Schedule() end
        Print(ns.Snapshot.message)
    elseif command == "status" or command == "" then
        Print(ns.Snapshot.message)
        local key = ns.Snapshot.lastCharacterKey
        local record = key and ClaudgarDB and ClaudgarDB.characters[key]
        if record then
            for _, name in ipairs(ns.Schema.sectionNames) do
                local section = record.sections[name]
                Print(name .. ": " .. section.status .. " (observed " .. section.observedAt .. ")")
            end
        end
        Print("Click the Claudgar minimap button to refresh and save, or use /claudgar snapshot then /reload.")
    else
        Print("Commands: /claudgar status, /claudgar snapshot. Reload or log out to save.")
    end
end
