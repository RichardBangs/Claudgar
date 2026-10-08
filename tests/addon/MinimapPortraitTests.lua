return function(MockGame, Test, Equal)
    Test("minimap uses the bundled portrait with the existing border and highlight", function()
        local game = MockGame.New()
        local createFrame = CreateFrame
        CreateFrame = function(...)
            local frame = createFrame(...)
            frame.textures = {}
            local createTexture = frame.CreateTexture
            frame.CreateTexture = function(self, ...)
                local texture = createTexture(self, ...)
                table.insert(self.textures, texture)
                return texture
            end
            return frame
        end
        game:Login()
        local button = game.ns.MinimapButton.frame
        Equal(button.textures[1].texture, "Interface\\AddOns\\Claudgar\\Textures\\ClaudgarPortrait")
        Equal(button.textures[1].width, 20)
        Equal(button.textures[1].height, 20)
        Equal(button.textures[2].texture, "Interface\\Minimap\\MiniMap-TrackingBorder")
        Equal(button.highlight, "Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight")
        local asset = assert(io.open(ADDON_ROOT .. "/Textures/ClaudgarPortrait.tga", "rb"))
        local header = asset:read(18)
        local pixels = asset:read("*a")
        asset:close()
        Equal(header:byte(3), 2)
        Equal(header:byte(13), 64)
        Equal(header:byte(14), 0)
        Equal(header:byte(15), 64)
        Equal(header:byte(16), 0)
        Equal(header:byte(17), 32)
        Equal(header:byte(18), 8)
        Equal(#pixels, 64 * 64 * 4)
    end)
end
