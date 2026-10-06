# Archived illustrated Maynard artwork

The active game now uses the [retro artwork](Retro/README.md). These earlier
source files are preserved for reference and are not packaged. The descriptions
below record how the previous renderer used them.

Generated with the built-in image generation tool on 2026-10-02. These are working game assets for the desktop visual milestone, not a claim that final production art is complete.

The game reads the PNGs directly. Preserve their alpha and cell layout when replacing them. Sprite regions are selected at runtime; the source images have not been resampled or manually painted over.

| File | Layout | Contents |
| --- | --- | --- |
| hero-walk.png | 6 columns, 4 rows | Six walk poses each for down, left, right, up |
| hero-idle.png | 2 columns, 2 rows | Standing poses for down, left, right, up |
| terrain.png | 2 columns, 2 rows | Grass, path, water, rock |
| town.png | 3 columns, 3 rows | General store, healer, smith, armorer, magic shop, castle, cave, bridge, tree |

The standing poses receive a small breathing motion. Character cutouts are cropped at load time and anchored at their feet with a common height, compensating for the sheets' different transparent padding. The tree is available in the atlas but is not placed as an obstacle in the town. Building footprints and map coordinates remain those of the original map. New animation does not advance gameplay turns.

## Art direction

Colorful, readable top-down fantasy: warm stone and timber, muted meadow greens, teal water, and a silver-armored hero in blue with a red plume. Keep silhouettes clear at game size. Ground textures should stay quieter than characters and buildings. Future frames should keep the helmet, plume, cape, scale, and foot anchor consistent.

## Exact generation prompts

### Hero base

Use case: stylized-concept.
Asset type: production PNG sprite sheet for a colorful top-down 2D fantasy game.
Primary request: create a complete six-frame walking cycle in FOUR directions for one friendly knight adventurer, on a genuinely transparent background.
Composition: landscape 3:2 canvas, EXACTLY 6 equally sized columns and 4 equally sized rows (24 cells total). Each cell contains exactly one full-body character. No borders or labels. Equal spacing. Feet at the same baseline in every cell, character centered horizontally with generous clear margin. Every sprite identical size and proportions, occupying about 60% of the cell width and 80% of the cell height.
Rows from top to bottom: facing DOWN toward viewer; facing LEFT; facing RIGHT; facing UP away from viewer. All six sprites in each row face ONLY that row's direction.
Columns: six sequential walk poses, left foot forward, weight transfer, passing pose, right foot forward, weight transfer, passing pose. Opposite arms swing naturally. Same character and same outfit in every cell. Clear leg changes for actual animation, not six duplicates.
Design: silver open-face helmet, small red plume, blue tunic over light silver armor, short blue cape, brown gloves and boots, small gold belt buckle. No weapon or shield in hand, no extra characters.
Style: carefully drawn modern pixel art with coherent pixel clusters and crisp silhouettes, warm storybook fantasy, restrained detail legible at about 48 pixels tall. Soft warm sunlight from upper left, dark blue-gray outlines, no gradients or painted blur. Compact body with expressive proportions. Pure sprites only, no ground, no cast shadows, no scenery, no lettering, no watermark. Transparent alpha must remain transparent between every sprite.

### Hero walk-cycle revision (selected)

Use case: precise-object-edit. Edit this sprite sheet only to correct the walking cycle. Preserve the same knight character, equipment, colors, style, scale, six-column/four-row layout and TRANSPARENT background. Row directions must stay down, left, right, up. Each row must show a clear six-frame walk cycle, not repetitions of one stride. In columns 1 and 4 the legs must be opposite: column 1 left leg forward / right leg back; column 4 right leg forward / left leg back. In columns 3 and 6 the feet pass almost together under the body, one knee lifted, while the other leg bears weight. Columns 2 and 5 are the in-between weight transfer poses. Opposite arm swing. Keep the head and torso nearly stable, with subtle natural cape movement. Full body contained in each original cell, feet aligned consistently, no ground or shadows. Keep transparent pixels actually transparent. No new content, no text, no effects.

### Hero idle poses

Use case: identity-preserve. Asset type: transparent idle sprite atlas for the same playable knight shown in the reference walking sheet. Create exactly FOUR full-body standing sprites in an evenly spaced 2 by 2 grid on a square transparent image. Top-left faces down toward viewer; top-right faces left; bottom-left faces right; bottom-right faces up away from viewer. Preserve this exact character's silver helmet, red plume, blue tunic and cape, brown gloves and boots, small gold belt, face and pixel-art treatment. Every pose is NEUTRAL STANDING: both feet planted at the same ground level directly below the hips, legs straight and close together, arms relaxed at sides, no raised knee, no stride, no walking or running. Full body completely within its cell, centered horizontally with the same body scale and ground baseline, approximately 80% cell height. Pure cutout sprites with actual transparent alpha, no scenery, no floor, no glow, no cast shadow, no text or borders.

### Terrain base

Use case: stylized-concept.
Asset type: opaque terrain texture atlas for a playable top-down 2D fantasy game.
Primary request: one square image split into EXACTLY 2 columns and 2 rows of equally sized square terrain swatches. The swatches touch with NO padding, NO borders, NO labels.
Top left: calm meadow grass, muted fresh green base with restrained small hand-placed darker and lighter tufts, occasional very tiny leaf clusters, no flowers, no objects. Quiet enough for a walking character to read clearly.
Top right: warm pale sandstone cobblestone path, small irregular flat stones packed together with thin mossy seams, no large border stones or grass surround.
Bottom left: rich blue-teal river surface, calm water with sparse short light turquoise horizontal ripple shapes, entirely water with no shore.
Bottom right: cool slate-gray stone/cliff texture, chunky angular rock faces with muted blue and moss accents, flat overhead material texture, no scenery.
Style: polished modern pixel art, coherent crisp pixel clusters, a warm colorful storybook fantasy RPG palette. No photographic noise, no blur or gradients. Restrained variation and consistent scale across all four textures. Each quadrant is an independently seamless repeatable texture extending fully to its edges, matching its own opposite edges. Even overhead daylight, no directional edge shadow or vignette. No characters, buildings, props, text, grid lines, watermark or transparency.

### Terrain grass revision

Use case: precise-object-edit. This is a four-quadrant terrain atlas for an actual game. Keep its square dimensions and exact 2 by 2 division. Change ONLY the upper-left grass quadrant: make it a much quieter, less repetitive meadow texture, with about 80 percent fewer large grass tufts. Mostly calm muted mid-green patches and small groups of crisp pixel clusters, a few scattered tiny tufts, no flowers or objects. Keep all four edges of the grass quadrant a consistent green so that it tiles seamlessly with itself without borders. Preserve the warm green palette and pixel-art technique. Leave the upper-right stone path, lower-left water and lower-right rock quadrants unchanged. No lines, borders, labels, text, vignettes, transparency, or extra objects.

### Town objects

Use case: stylized-concept.
Asset type: transparent environment sprite atlas for a playable top-down 2D fantasy town.
Primary request: a square image laid out in EXACTLY 3 columns by 3 rows, nine equal square cells. One complete isolated object per cell. No text, labels, grid lines or frame borders. Genuinely transparent background throughout.
Style: cohesive polished modern pixel art, warm colorful storybook fantasy RPG, crisp carefully placed pixel clusters and dark blue-gray outlines. Three-quarter TOP-DOWN camera looking at the roof and front facade, front doors face the bottom of the image; orthographic, no isometric diagonal orientation. Soft warm light from upper left. Cream plaster, warm sandstone, timber, rich slate or terracotta roofs and restrained ivy. All buildings have consistent scale, occupy about 85% width and 85% height of their cell, centered horizontally with ground line at 90% cell height. Transparent padding on all sides. No baked shadows, bases or ground patches.
Cells row-major:
Row 1 left: cozy general store with a red terracotta roof, blue awning, wooden door and a small round blank hanging sign.
Row 1 middle: healing cottage with a moss green roof, herb planter and a little heart emblem on its hanging sign.
Row 1 right: weapon smithy with charcoal slate roof, small chimney, sword silhouette sign, no fire or smoke baked into the sprite.
Row 2 left: armor shop with a muted blue roof, shield emblem sign and sturdy timber beams.
Row 2 middle: magic shop with a plum-purple peaked roof, gold star ornament and a round amber window.
Row 2 right: small town castle gatehouse of warm gray stone, two compact turrets, blue roof caps and open arched entrance.
Row 3 left: low stone cave entrance, dark open tunnel within a rocky mound, a little moss, no door.
Row 3 middle: wooden footbridge running LEFT TO RIGHT, horizontal walkable plank deck, simple railings along the upper and lower edges. Standalone bridge, no river or ground.
Row 3 right: one small leafy deciduous tree, rounded canopy with muted fresh green clusters, visible brown trunk.
Constraints: exactly nine objects, consistent palette and pixel scale, every object fully within its own cell, actual transparent alpha around every silhouette, no additional scenery, no letters, no watermark.
