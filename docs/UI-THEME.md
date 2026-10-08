# Desktop theme

Claudgar uses a dark charcoal palette, antique-gold accents, Cambria character headings, and Segoe UI controls. The main view puts ChatGPT and Claude at the center, with open space, restrained surfaces, and a compact saved-character banner. Tables and navigation use opaque surfaces for contrast inside the character-data view.

`BrowserTheme` owns colors and shared control styles. `AssistantHome` is the default content area, offering ChatGPT, Claude, and three useful question suggestions. `CharacterBanner` owns the artwork, a minimal saved-character summary, and the action to reveal character data. `ThemeTabs` owns responsive tab navigation with keyboard-accessible buttons and Ctrl+Tab within that data view. `SectionBrowser` retains generic access to every exported field, including JSON and coverage details, and hosts optional `ISectionView` presentations. Returning home keeps the selected character and last data section.

Suggestion actions copy a question with the selected character's name and realm for the user to paste into an assistant. They exclude opaque character IDs, local account metadata, and installation paths. Assistant actions prefer a discovered installed application. A browser fallback explains that the local character connection requires the desktop client. Launch selection and snapshot summaries are separate from the visual controls and are verified without opening external applications.

`InventoryView` opens inventory as four-column bag grids with quality borders, stack counts, local mnemonic tiles, search dimming, and selected-item details, with a **JSON tree** mode. `TalentsView` gives all saved trees the full talent area, with group headers, points, rank badges, and hover details. It has no heading, selectors, toolbar, view tabs, zoom controls, or details sidebar. Healthy talent coverage is hidden; stale or incomplete coverage still appears. `TalentTreeCanvas` automatically fits and centers the tree on resize using the whole viewport, without a scale cap or a second DPI multiplier. Selection and hover use the same transform as drawing.

`TalentPresentation` reads saved metadata; `TalentTreeLayout` preserves global coordinates and uniform scale for ordinary nodes in each native tree, including cross-group edges. Named group backgrounds appear when visible membership and horizontal ranges permit separation. Unknown, shared, or unassigned visible membership keeps the complete tree together. `TalentLayoutOutliers` detects only distant duplicate talents with equivalent spells, ranks, and overlapping saved group membership. Such duplicates remain visible in their group's small **Outside saved layout** footer with an explanatory tooltip; the export and original positions are unchanged. Unique distant talents keep their saved positions. Missing or overlapping visible positions fall back to a complete list. `QuestNames` presents saved completed titles while leaving their underlying numeric IDs intact.

Talents and equipment use generic, locally drawn text placeholders. Talent and item tiles show name initials; empty equipment slots show slot labels. No game artwork, icon index, or icon downloader is packaged or used, and the views do not read previously cached icons. Saved icon IDs remain character metadata. Group colors and borders remain Claudgar theme elements; talent layout comes from the selected character's Forever export.

`EquipmentView` gives the Equipment tab directly to a fitted paper doll, with the Forever character-sheet slot order, weapons below, quality borders, durability bars, and full saved item details on hover or selection. It has no extra selectors, view tabs, or zoom controls. Empty slots use locally drawn slot labels. Missing equipment is marked unknown, and conflicting or unrecognized records remain inspectable below the sheet. Slot names are authoritative; numeric-only records are not assigned using guessed inventory constants.

The central silhouette is drawn locally and represents no particular character appearance. The export contains no rendered 3D character model. `EquipmentStatDisplay` shows recorded character totals from the optional `character.data.stats` map, including attributes, maximum health/power, armor, and combat values. Older exports show explicitly labelled recorded item bonuses with a refresh explanation. Item bonuses never substitute for character totals. Healthy equipment coverage is hidden; incomplete or stale coverage still appears. Character totals refresh independently of equipment changes.

The banner is embedded as `Artwork/stormwind-dusk.png`, so the portable executable needs no loose image files or image service at runtime.

## Artwork provenance

The ChatGPT card uses OpenAI's ChatGPT Blossom compound vector path, converted directly into a local `GraphicsPath` from the [official SVG pinned to commit 4a85c3018d20ceef48bf7549450c567896501bf9](https://github.com/openai/openai-cookbook/blob/4a85c3018d20ceef48bf7549450c567896501bf9/examples/voice_solutions/realtime_translation_guide/livekit-translation-demo/public/brand/chatgpt-blossom-black.svg). The shape is preserved and tinted with the interface's foreground color; it needs no runtime network access. The ChatGPT mark belongs to OpenAI; see [OpenAI brand guidance](https://openai.com/brand/). The Claude card's starburst is drawn locally. `AssistantMark` owns both card marks.

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

The header keeps a compact service indicator and a settings action, with **Update available** appearing only when needed. **Launch on startup**, both assistant download links, phone help, and About/license are available from the settings menu. The startup checkbox still reflects effective registration after setup and preserves explicit choices; `ReleaseControls` shows **Install update** there when a verified update is ready. The old `AppGuidance` instruction banner no longer occupies the main view. Setup status and the detailed export footer remain in character data. Setup status identifies each client separately and distinguishes local-service contact from a completed character query. `ExportStatusPresentation` explains invalid exports and recovery without hiding usable characters from other accounts.

The character banner and app header stay compact so the assistant choices receive most of the available space. The home shows a concise save-status summary; expanded coverage, realm, faction, location, and raw fields remain available in character data. Question suggestions use the exact text **Which areas should I quest next?**, **What quests do I need for Deadmines?**, and **How can I improve my gear?**. Layout containers shrink around visible controls; empty status labels reserve no space. Fictional previews verify the home, first-run setup, hidden data views, compact windows, and explicit doubled control geometry. `ControlScaleState` keeps explicit `Control.Scale` geometry separate from native monitor DPI and consumes native DPI transitions once. Native font rendering and Windows DPI-change behavior still require a real-monitor acceptance check; the preview stays at a native 96-DPI baseline.

Saved exports refresh on timestamp/size/path changes, checked every three seconds and on window focus. Setup health is checked read-only on focus and every thirty seconds. Corrective actions appear only for detected issues. Minimising hides the app in its tray; reopening restores its previous window state. Windows startup opens quietly in the tray; manual launch shows the window.

The addon minimap uses `addon/Claudgar/Textures/ClaudgarPortrait.tga`, a 64×64 uncompressed 32-bit BGRA texture with transparent corners, packaged from the same pixel portrait. `tools/Claudgar.AddonTextureBuilder` uses nearest-neighbor sampling, verifies every packaged pixel, and can render a 20px preview. It creates no new artwork. The game button retains its existing border, highlight, tooltip, secure click/combat guards, and saved drag position. The texture is embedded with the addon and delivered by owned-file setup/version updates.

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
