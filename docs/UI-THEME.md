# Desktop theme

Claudgar uses a dark charcoal palette, antique-gold accents, Cambria character headings, and Segoe UI controls. The character banner shows identity, realm, faction, and location from the selected saved export. Tables and navigation use opaque surfaces for contrast.

`BrowserTheme` owns colors and shared control styles. `ThemeTabs` owns responsive tab navigation with keyboard-accessible buttons and Ctrl+Tab. `CharacterBanner` owns the artwork and saved-character summary. `SectionBrowser` retains generic access to every exported field, including JSON and coverage details.

The banner is embedded as `Artwork/stormwind-dusk.png`, so the portable executable needs no loose image files or image service at runtime.

## Artwork provenance

Generated using the built-in ImageGen tool. Asset: `src/Claudgar.App/Resources/Artwork/stormwind-dusk.png`. It is a fantasy illustration inspired by the requested World of Warcraft theme; it is not an extracted game asset.

Final generation prompt:

```text
Use case: stylized-concept
Asset type: production background artwork for the header of Claudgar, a professional World of Warcraft themed desktop companion app.
Primary request: an exquisite atmospheric painted panorama of Stormwind-inspired human Alliance architecture: monumental pale stone keep and blue-roofed towers, a subtle blue-and-gold banner, distant mountains, soft golden dusk lights. Sophisticated Blizzard-style hand-painted fantasy environment, beautifully detailed, majestic and calm.
Composition/framing: very wide panoramic landscape, 3:1 aspect ratio. Architecture concentrated in the right half. The left half is subdued dark blue atmospheric mist with very little detail, suitable for legible live interface text over it. Tall towers and castle remain recognizable when the artwork is cropped to a shallow banner. Keep important architecture near the vertical center; edges blend toward deep charcoal navy.
Color palette: deep desaturated midnight navy, slate blue, antique gold light, muted ivory stone. Restrained luminous details, rich painterly textures.
Constraints: background artwork ONLY. No interface, no panels, no borders, no typography, no lettering, no words, no logos, no watermark, no characters. This will be placed behind real controls in a desktop app.
```

## Visual verification

The pixel archmage portrait appears beside the app title and is packaged into the executable, window, and tray icons. Source: `src/Claudgar.App/Resources/Artwork/claudgar-portrait.png`; Windows icon: `src/Claudgar.App/Resources/Artwork/claudgar.ico` with 16, 20, 24, 32, 40, 48, 64, 128, and 256px entries. `tools/Claudgar.IconBuilder` performs only nearest-neighbor size/ICO packaging; it is not part of runtime startup.

The header has no persistent action toolbar. Saved exports refresh on timestamp/size/path changes, checked every three seconds and on window focus. Setup health is checked read-only on focus and every thirty seconds. Corrective actions appear only for detected issues. Minimising hides the app in its tray; reopening restores its previous window state.

Pixel portrait generation used the built-in ImageGen tool with this final prompt:

```text
Use case: logo-brand
Asset type: square pixel-art character portrait for Claudgar, a World of Warcraft themed desktop companion, also used unchanged as the basis for its Windows app icon and tiny system tray icon.
Primary request: a superb readable retro pixel-art portrait of a wise, friendly human archmage inspired by Warcraft's Khadgar: strong face, swept silver hair, short square silver beard, thick brows, calm blue eyes, deep midnight-blue mage mantle with a small antique-gold collar. Head and shoulders, face fills most of the icon. Give him an unmistakable silhouette and confident, approachable expression.
Style: authentic crisp 32-by-32 sprite aesthetic enlarged cleanly, deliberately chunky square pixels, restrained 12-color palette, strong contiguous clusters, simple facial features recognizable at 16 and 32 pixels. No soft edges, gradients, painted texture, fine linework, or tiny ornament.
Composition: centered bust inside a simple dark-navy square portrait tile with subtly clipped pixel corners and a thin antique-gold pixel border. Tile almost fills the square canvas. Outside the clipped corners is truly transparent. No drop shadow or glow outside the tile.
Palette: slate navy, warm ivory/silver, muted skin tones, antique gold, a small sapphire blue accent; high contrast on dark and light desktop backgrounds.
Constraints: one single finished icon, no mockup, no alternate versions, no labels, no letters, no text, no watermark. Strong face silhouette with minimal empty space. Preserve genuine alpha transparency outside the tile.
```

`tools/Claudgar.Preview` renders fictional character data without showing the app or invoking its setup/startup workflow. See its README for screenshot commands. This provides repeatable layout and browsing checks without changing the live game installation or starting a second local service.
