local _, ns = ...
if not ns.enabled then return end

ns.MinimapButton = {}
local button, dragging, suppressClick
local defaultAngle = 225

local function Print(message)
    if DEFAULT_CHAT_FRAME then DEFAULT_CHAT_FRAME:AddMessage("|cff70c8ffClaudgar:|r " .. message) end
end

local function Unlocked()
    local context = ns.Api.Context()
    return ns.Api.Call(context, "InCombatLockdown") == false
end

local function Preferences()
    if type(ClaudgarDB) ~= "table" then return nil end
    if type(ClaudgarDB.ui) ~= "table" then ClaudgarDB.ui = {} end
    return ClaudgarDB.ui
end

local function Position(angle)
    local radians = math.rad(angle)
    local radiusX, radiusY = Minimap:GetWidth() / 2 + 8, Minimap:GetHeight() / 2 + 8
    button:ClearAllPoints()
    button:SetPoint("CENTER", Minimap, "CENTER", math.cos(radians) * radiusX, math.sin(radians) * radiusY)
end

local function BeforeClick(self, mouseButton, down)
    if mouseButton ~= "LeftButton" or down then return end
    -- Drag-stop can precede the release click. Keep this guard until the next
    -- left-button press so moving the icon never refreshes or reloads the UI.
    if suppressClick then
        if Unlocked() then self:SetAttribute("type1", nil) end
        return
    end
    -- /reload is a protected action on Forever. The secure template executes
    -- the prepared macro from this real player click, after capture finishes.
    -- Never change secure attributes in combat, or reload later from an event.
    if not Unlocked() then
        ns.Events.Schedule()
        Print("Refresh is deferred during combat. Click again after combat to save the export.")
        return
    end
    self:SetAttribute("type1", nil)
    if ns.Snapshot.Capture() then
        self:SetAttribute("type1", "macro")
    else
        ns.Events.Schedule()
        Print(ns.Snapshot.message)
    end
end

function ns.MinimapButton.Initialize()
    if button or not ns.Snapshot.ready or not Minimap or not Unlocked() then return end
    local created, result = pcall(CreateFrame, "Button", "ClaudgarMinimapButton", Minimap, "SecureActionButtonTemplate")
    if not created or not result then
        Print("The minimap button is unavailable in this build. Use /claudgar snapshot, then /reload.")
        return
    end
    button = result
    ns.MinimapButton.frame = button
    button:SetSize(32, 32)
    button:SetFrameStrata("MEDIUM")
    button:SetFrameLevel(Minimap:GetFrameLevel() + 5)
    button:SetHighlightTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight")
    local icon = button:CreateTexture(nil, "ARTWORK")
    icon:SetTexture("Interface\\AddOns\\Claudgar\\Textures\\ClaudgarPortrait")
    icon:SetSize(20, 20)
    icon:SetPoint("TOPLEFT", button, "TOPLEFT", 7, -5)
    local border = button:CreateTexture(nil, "OVERLAY")
    border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder")
    border:SetSize(54, 54)
    border:SetPoint("TOPLEFT", button, "TOPLEFT", 0, 0)
    button:RegisterForClicks("LeftButtonUp")
    button:RegisterForDrag("LeftButton", "RightButton")
    button:SetAttribute("useOnKeyDown", false)
    button:SetAttribute("macrotext1", "/stopmacro [combat]\n/reload")
    button:SetScript("OnMouseDown", function(self, mouseButton)
        if mouseButton ~= "LeftButton" then return end
        suppressClick = false
        -- Disarm a previous click even when dragging does not emit PreClick.
        if Unlocked() then self:SetAttribute("type1", nil) end
    end)
    button:SetScript("PreClick", BeforeClick)
    button:SetScript("OnEnter", function(self)
        if not GameTooltip then return end
        GameTooltip:SetOwner(self, "ANCHOR_LEFT")
        GameTooltip:SetText("Claudgar")
        GameTooltip:AddLine("Left-click: refresh export and reload UI.", 1, 1, 1)
        GameTooltip:AddLine("Left-drag or right-drag: move around the minimap.", 0.7, 0.7, 0.7)
        GameTooltip:Show()
    end)
    button:SetScript("OnLeave", function() if GameTooltip then GameTooltip:Hide() end end)
    button:SetScript("OnDragStart", function(self, mouseButton)
        if mouseButton == "LeftButton" then suppressClick = true end
        if Unlocked() then
            dragging = true
            if suppressClick then self:SetAttribute("type1", nil) end
        end
        if GameTooltip then GameTooltip:Hide() end
    end)
    button:SetScript("OnDragStop", function() dragging = false end)
    button:SetScript("OnUpdate", function()
        if not dragging then return end
        if not Unlocked() then dragging = false; return end
        local x, y = GetCursorPosition()
        local centerX, centerY = Minimap:GetCenter()
        if not centerX or not centerY then return end
        local scale = Minimap:GetEffectiveScale()
        local angle = math.deg(math.atan2(y / scale - centerY, x / scale - centerX))
        Position(angle)
        local preferences = Preferences()
        if preferences then preferences.minimapAngle = angle end
    end)
    local preferences = Preferences()
    local angle = preferences and preferences.minimapAngle
    if type(angle) ~= "number" or angle ~= angle or angle == math.huge or angle == -math.huge then angle = defaultAngle end
    Position(angle)
    button:Show()
end

local events = CreateFrame("Frame")
events:RegisterEvent("PLAYER_LOGIN")
events:RegisterEvent("PLAYER_REGEN_ENABLED")
events:SetScript("OnEvent", ns.MinimapButton.Initialize)
