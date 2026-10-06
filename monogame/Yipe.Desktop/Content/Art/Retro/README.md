# Retro town artwork

These six source atlases were generated or edited with the built-in image-generation
tool for the complete retro treatment of the playable Maynard town. Original
generated files remain in the Codex generated-images directory. Earlier
illustrated artwork is archived one directory above and is not packaged.

## Runtime art contract

The source PNGs are magnified artwork. On load, `TownRenderer` compiles them to
native-resolution textures, averaging covered source pixels, mapping them to a
shared 40-color palette, and using binary transparency. Rendering uses point
sampling, whole-pixel positions, and fixed sprite dimensions. Shop signs draw
on a 28 × 28 canvas to keep their symbols readable at a modest size, with
integer-aligned pieces for their stationary posts and gently moving boards.
The scaled lower pieces exclude transparent bottom padding so point sampling
preserves the one-pixel outline along the board's bottom edge.
The cave uses the shared slate shades over the mountain's rock terrain, with
paving only at its south-facing entrance. Its base meets the bottom of the rock
tile and uses no freestanding building shadow, avoiding a visible strip of mountain
texture beneath the entrance.

| File | Grid | Native cell | Native atlas | Contents |
| --- | --- | --- | --- | --- |
| terrain.png | 2 columns, 2 rows | 32 × 32 | 64 × 64 | Grass, path, water, rock |
| town.png | 3 columns, 3 rows | 48 × 48 | 144 × 144 | General, healer, smith; armorer, magic, castle; cave, bridge, small props |
| shops.png | 3 columns, 3 rows | 48 × 48 | 144 × 144 | First five cells replace the original shop sprites, removing their built-in hanging markers; the remaining cells are ignored |
| hero.png | 6 columns, 4 rows | 32 × 32 | 192 × 128 | Rows: down, left, right, up. Columns: four walking poses, standing, blink |
| meadow.png | 2 columns, 2 rows | 32 × 32 | 64 × 64 | Quiet grass, clover, dry flecks, leafy grass |
| details.png | 4 columns, 4 rows | 32 × 32 | 128 × 128 | Flowers, stones, grass; reeds, shrub, crates, potions; bread/potion/sword/shield signs; star sign, banner, anvil, pot |

Frames stay on fixed canvases. Load-time integer translation aligns hero boots
to row 28 and helmet centers to column 16; buildings align their bases to row 44.
The bridge is centered; decorative props align their bases to row 28.
Translation cannot resize a pose and rejects clipping. Source dimensions need
not divide evenly into cells: the importer samples the complete normalized grid.
The hero is drawn with feet at tile position + (16, 25). Walking cycles at
4 frames/second of movement, with each grid step taking 250 milliseconds;
a brief idle blink occurs every four seconds.
The world updates at 60 Hz on a 320 × 180 canvas, enlarged by whole-number
scales whenever the window is at least that size.

The five cleaned shop cells are compiled through the same palette and alignment
rules, then copied into the runtime town atlas. The original castle, cave,
bridge, and pebble sprites stay intact. Roof ornaments and architectural details
remain; shop identity comes from the separate animated pictogram signs.
The castle uses only the small blue banners built into its walls; the separate
flagpole props are not placed, keeping its entrance and silhouette uncluttered.

Water and smoke advance in discrete six-Hz steps. Shadows and sparkles use
solid pixels. Four grass variants share a quiet olive base; mirrored placement
breaks up repetition. Small plants and stones use authored world positions.
Riverbanks and southeast building shadows compile once into native textures.
Road connection masks compile into a native-pixel atlas with rounded bends,
softened junctions, and subtly uneven earth edges. Four stone orientations
preserve texture variation; edge variation repeats seamlessly across tiles.
The current follows the connected river, with foam constrained to its water mask.
Bridge reflections reuse the hero's current pose, mirrored from the boots and
compressed into 20 pixel rows. A faint blue-green tint, one-pixel ripple, and
broken scan rows soften the reflection. Animated position controls its approach
fade; the water mask clips its edges and the bridge deck draws over it. Reflection
motion shares scene time, so it freezes when paused and follows load/reset immediately.
Signs and foliage move only one pixel; their posts and roots stay fixed.
Shop signs share the facade's ground line and touch its front-right corner;
the magic shop uses its front-left corner to avoid the cliffs. Per-shop offsets
keep the boards outside the walls. Facades draw over sign supports to preserve
windows and doors, with the same character depth as their signs. Standing next to and facing an entrance
adds a small doorstep cue. Surface-aware dust or grass disturbance follows the
current walking step and has no persistent particle history.
The optional scanline setting draws a faint display-space line below each
enlarged world row; it is on by default and does not modify source artwork.
The miniature setting defaults to Strong and adds a GPU tilt-shift blur
and deeper contact shadows, retaining the art's original colors. The blur runs
at 1280 x 720 after point enlargement, with a broad falloff and fractional sample
offsets. Its focus follows the hero, while overlays remain sharp. Off preserves the original crisp
presentation; Strong increases the effect. Post-processing does not change these
source files or their native palette/alpha contract.

## Exact generation prompts

### Terrain — terrain.png

```text
Use case: stylized-concept.
Asset type: actual low-resolution pixel-art RPG terrain sheet, shown enlarged with nearest-neighbor pixels.
Create a 64 by 64 LOGICAL PIXEL image, enlarged exactly 16 times to 1024 by 1024 for delivery. Exactly four 32x32 logical-pixel square terrain tiles in a 2x2 grid, touching without gaps. Every visible logical pixel is a flat square of ONE solid color. All edges follow the same coarse pixel grid. This must look like genuine early 1990s 16-bit game graphics, extremely economical pixel clusters, NOT a detailed illustration with a pixelation filter.
Top left: quiet meadow grass, medium olive green with a few tiny two-pixel tufts. Mostly flat calm green. No large bushes or flowers.
Top right: a warm sandstone path texture with simple chunky flat stones, three ochre/tan shades and a dark brown seam color.
Bottom left: teal-blue river surface with a few short two- or three-pixel horizontal cyan ripple marks. Three blue shades, entirely water.
Bottom right: chunky blue-gray rock/cliff material, four gray shades, simple big angular shapes.
All four quadrants are independently seamless textures, matching their own opposite edges. Flat overhead game view. Palette: dark blue-gray outlines #252b3b; olive greens #496348 #6b8747 #99a957; stone #83563e #b78858 #d9b87c #f1dc9e; water #24475e #2b6b83 #4a9eaa #91c7bc; rock #404957 #697585 #a4adb0. No gradients, no antialiasing, no soft lighting, no noisy dithering, no borders or grid lines, no labels, no text, no objects or characters, no transparency.
```

### Buildings and props — town.png

```text
Use case: stylized-concept.
Asset type: native low-resolution pixel-art sprites for a 16-bit top-down fantasy RPG town, delivered as a magnified sprite atlas.
Create exactly 3 columns by 3 rows of equal square cells on a genuinely transparent background. Each cell represents a 48x48 LOGICAL PIXEL canvas, so the whole atlas is 144x144 logical pixels, shown enlarged with crisp square pixels. Strict coarse pixel grid, flat solid colors, NO antialiasing or smooth gradients. Authentic early-1990s RPG sprite economy: a few big color clusters, three or four shades per material, clear dark navy outline, simple iconic roof and door shapes. Absolutely no high-resolution painted detail or hundreds of tiny roof tiles.
Camera: orthographic top-down RPG three-quarter view, all front doors facing bottom, not isometric. All objects centered at pixel x24, ground baseline pixel y44. All buildings contained in their cell, about 40 pixels wide, with transparent margins. No baked cast shadows, ground tiles, smoke or glow. Shared colors: dark navy #252b3b, timber #55382e #83563e #b78858, cream stone #d9b87c #f1dc9e, olive greens #496348 #6b8747 #99a957, blue-gray #404957 #697585 #a4adb0, blues #24475e #326da8 #72a4c2, muted red #893747 #bf5652 #e88965, purple #523b6b #805681.
Nine cells row-major:
1 red-roof general shop, striped blue/cream awning, centered wooden door, blank little sign.
2 green-roof healing cottage, centered arched door, tiny heart symbol sign.
3 charcoal-roof blacksmith, centered door, small chimney on upper right, tiny sword sign.
4 blue-roof armor shop, centered door, tiny shield sign.
5 purple-roof magic shop, centered door, small gold star and square amber window.
6 miniature gray stone castle gate, two blue-capped turrets and central arch.
7 low rocky cave mouth, dark opening and olive moss.
8 horizontal wooden footbridge, deck runs LEFT TO RIGHT, simple railings at top and bottom. Center this bridge at x24 y24, not at ground baseline.
9 a tiny low cluster of pebbles and two grass tufts, only 16x12 logical pixels, centered near x24 y38; a ground decoration that a character could step over.
Exactly one object per cell. Actual transparent alpha between objects. No border lines, grid, labels, text, scene, floor, watermark, blur, painted textures or lighting effects.
```

### Four-direction hero — hero.png

```text
Use case: stylized-concept.
Asset type: production low-resolution RPG character sprite sheet, real coarse pixel art.
Create a 192x128 LOGICAL PIXEL sprite sheet magnified exactly 8x to a 1536x1024 PNG. This sheet has EXACTLY six equal columns and four equal rows. Every cell is exactly 32x32 logical pixels. Every visible logical pixel must be a flat solid square aligned to one consistent grid. Transparent background, no baked shadows. Early-1990s 16-bit RPG style, very simple readable pixel clusters, no antialiasing, gradients, painted texture or fine detail.
Character: the same small friendly knight in all 24 cells: silver open-faced helmet, small red plume, blue tunic/cape, brown boots and gloves, gold belt. Approximately 16 pixels wide and 26 pixels tall. Three shades per material. Strong dark navy outlines. Palette: navy #252b3b, silver #697585 #a4adb0 #d5d8cc, blue #25416d #326da8 #72a4c2, red #893747 #bf5652 #e88965, brown #55382e #83563e #b78858, skin #d99b70 #f2c68f, gold #d9ac4f.
Rows, strictly:
row 1 facing DOWN toward viewer;
row 2 facing LEFT;
row 3 facing RIGHT;
row 4 facing UP away from viewer.
In EVERY row:
column 1 WALK: left foot forward, right foot back, opposite arm forward.
column 2 WALK: passing pose, feet nearly together, one knee raised.
column 3 WALK: RIGHT foot forward, LEFT foot back, opposite arm forward. This must differ clearly from column 1.
column 4 WALK: second passing pose, opposite knee raised.
column 5 IDLE: both boots planted together, arms relaxed, eyes open.
column 6 IDLE: same standing pose, subtle blink and one-pixel cape change. Do not resize the body.
Alignment is essential: torso center at local x16 in every cell, sole ground line local y28 in every cell; consistent scale, same foot anchor across all poses. Leave transparent padding on every side. No part of a sprite can touch a neighboring cell. Pure sprites only: no labels, borders, grid lines, floor, glow, scenery or text.
```

### Meadow variants — meadow.png

```text
Use case: stylized-concept. Asset type: production pixel-art ground atlas for a gentle overhead fantasy RPG, intended to compile to four 32x32 native tiles.
Create a square 2 by 2 terrain atlas with equal square quadrants, no margins, dividers, labels or text. Each quadrant must be one complete seamless flat grass tile, with exactly the same muted olive base color #6b8747 at every tile edge. Retro 16-bit artwork: deliberately chunky logical pixels, hard edges, tiny restrained pixel clusters, no gradients, no lighting baked across the tile, no perspective or objects casting shadows. It should look like 32-pixel tiles magnified evenly.
Top left: quiet short meadow grass with just a few dark green two-pixel blades and sparse lighter clusters. Top right: short grass with a few tiny low clover shapes and open space. Bottom left: slightly dry grass with restrained straw-colored accents and two small bare earthy flecks. Bottom right: lush grass with sparse low dark green leaf clusters. All four keep the SAME background olive hue and the SAME calm average brightness, and visual detail is sparse: at least 80% open base color, no repeated dots everywhere, no large pattern or central mound. These tiles will be intermixed, so all boundaries must match. Palette limited to muted olive #6b8747, darker green #496348 and #364e33, soft straw #99a957, warm earth #83563e. No flowers, buildings, water, trees, borders, outlines around entire tiles, transparency, anti-aliasing, blur, noise, shadows, or polished vector style.
```

### Village details — details.png

```text
Use case: stylized-concept. Asset type: production transparent prop atlas for a 16-bit overhead fantasy village.
Create ONE square PNG with a strict 4 by 4 grid of 16 equally sized square cells on genuine transparent background. No grid lines, cell backgrounds, labels or text. Every cell has one simple recognizable small sprite centered on x and grounded at 3/4 of its cell height, with generous transparent padding on all four sides. Fixed consistent scale: each is a tiny 12 to 20 logical-pixel prop in a 32x32 native cell, shown magnified with chunky crisp pixel clusters. View is slightly overhead, matching classic top-down 2D RPG houses with visible front faces, never isometric diamonds. Solid limited muted forest-green, cream, ochre, warm brown, slate, muted blue and purple palette; dark outlines; sunlight upper left. Binary transparent silhouettes, no gradients, no soft edges, no shadow ellipse.
Exact row-major contents:
Row 1: a sparse low patch of three tiny cream meadow flowers; a sparse low patch of three tiny lavender meadow flowers; a compact cluster of three small gray pebbles; a low tuft of three dark-green grass blades.
Row 2: a short clump of thin river reeds; a low rounded green shrub; two small wooden produce crates with a red apple visible; two tiny teal/green potion bottles in a wooden carrier.
Row 3: a small square wooden hanging sign with a simple pale bread-loaf emblem; a small square wooden hanging sign with a simple green potion-bottle emblem; a small square wooden hanging sign with a simple silver sword emblem; a small square wooden hanging sign with a simple pale blue shield emblem.
Row 4: a small square wooden hanging sign with a simple violet star emblem; a narrow blue hanging pennant on a short wooden post; a tiny dark iron anvil; a low clay flower pot with green leaves.
Signs have clear pictograms, absolutely NO letters. All 16 objects separated, nothing touching cell borders. Same size pixel grid across the entire atlas. Avoid photorealism, rendered 3D, smooth shading, ornate detail, large decorative bases, cast shadows, sparkles or glow. Make props legible when reduced to 32x32 cells.
```

The two village-polish originals are preserved at:

- `/Users/baouchiy/.codex/generated_images/01a0fdbf-26b0-7b62-87c2-b2d5922b39b4/exec-d2cb114c-a918-4ad3-abe7-4f440826fd1d.png` (meadow)
- `/Users/baouchiy/.codex/generated_images/01a0fdbf-26b0-7b62-87c2-b2d5922b39b4/exec-865a21d1-f4b4-4099-8df3-5e6ef93f10f1.png` (details)

### Shop marker removal — shops.png

Edited from `town.png` with the built-in image-generation tool. Only the first
five cells are integrated; other town sprites continue to use `town.png`.
The original output is preserved at
`/Users/baouchiy/.codex/generated_images/01a0fdbf-26b0-7b62-87c2-b2d5922b39b4/exec-56c8b8fb-a6c6-4a4e-bdca-891a7f5d0957.png`.

```text
Use case: precise-object-edit.
Asset type: existing transparent pixel-art RPG building sprite atlas, strict 3 by 3 grid.
Image 1 is the edit target. Make only five small object removals from the supplied atlas. Remove the little wall-mounted or hanging shop sign AND its projecting bracket from each of the first five shop sprites:
- top-left red-roof shop: remove its blank tan/brown hanging board on the LEFT edge.
- top-middle green-roof healer: remove its cream banner with red heart on the RIGHT edge.
- top-right blacksmith: remove its dark wooden sword sign on the RIGHT edge.
- middle-left blue-roof armorer: remove its cream shield banner on the LEFT edge.
- middle-middle purple-roof magic shop: remove its purple hanging star banner on the RIGHT edge. KEEP the gold star built into the roof gable.
Replace only the removed marker and bracket with genuine transparency where they extend beyond the building, or seamless matching wall/trim where a bracket touches the wall. Do not put new signs, flags, plaques, symbols, props, or decorative patches in their place.
Preserve ALL other pixels, shapes, colors, texture detail, proportions and placements as closely as possible. In particular keep the red shop's striped awning; every roof, window, door, plant box and chimney; the castle and its two blue tower banners; the cave, bridge and pebble/grass cluster EXACTLY as they are. Do not change the cave moss; the game handles that separately.
Keep the exact original 3-column by 3-row layout and equal square cells, same object scale, same canvas framing, no recentering, no shifts, no resizing, no new margins. Keep the hard-edged coarse pixel-art style and original colors. Actual transparent alpha background. Do not redraw, restyle, blur, sharpen or embellish the atlas. The only visible differences should be the removal of those five projecting shop markers and their brackets. No grid lines, labels, text, background fill, glow or shadows.
```
