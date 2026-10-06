# World art for the complete port

## Character appearances

Four native 32-pixel atlases provide all twelve original appearance slots, with down/left/right/up directions and two alternating walking poses per character. First two rows per character are stride A/B. The approved original `hero.png` remains the in-world animation for portrait 0/1; the new first knight also illustrates its creation preview. All other appearances use these sheets, the same foot anchor, 250 ms steps, four-Hz cycle, trail and bridge reflection. Original appearance slot numbers and Silly Mode (9–12) are preserved; names in the selection overlay are new descriptive labels.

All outputs used the built-in imagegen tool. Native compilation caught a figure crossing a cell boundary in the original eight-row middle sheet; it was split into two four-row sheets and repadded without clipping figures. Original and discarded outputs remain in the generated-images directory.

### heroes-01.png — eight rows; portraits 1–4

```text
Use case: stylized-concept. Asset type: transparent production sprite atlas for a top-down 16-bit fantasy RPG. EXACTLY FOUR columns and EIGHT rows of equal square 32x32 logical-pixel cells. Output a 1:2 portrait atlas. Four characters, TWO consecutive rows per character. Columns in EVERY row: facing DOWN toward viewer; facing LEFT; facing RIGHT; facing UP away from viewer. First row of each character is walking pose A (left foot forward), second row pose B (right foot forward), a modest natural two-pose walk. Full bodies, same identity, size and costume between poses and directions. Each complete sprite strictly inside the central 65% of its cell, generous transparent margin all four sides; feet centered at 80% cell height. No figure crosses rows. True coarse 16-bit pixel clusters, restrained muted slate/olive/ochre/plum/teal palette and dark charcoal outlines, upper-left light, tiny expressive chibi proportions but adult adventurers. Genuine transparent alpha, no scenery, ground rectangles, shadows, text, checkerboard, grid lines, borders or extra cells. Pixel-art asset, not painted illustration. Exactly 32 isolated sprites total. Rows 1-2: silver helmeted knight, blue tunic, small red cape. Rows 3-4: gold-armored knight, gray helmet, red cape. Rows 5-6: green-hooded ranger, olive tunic, brown boots, short sheathed dagger. Rows 7-8: rose-pink robed mage, violet pointed hat, tiny wooden staff. Each two-row pair is ONE consistent character in all eight poses.
```

### Original eight-row draft for portraits 5–8 (retained outside the project)

```text
Use case: stylized-concept. Asset type: transparent production sprite atlas for a top-down 16-bit fantasy RPG. EXACTLY FOUR columns and EIGHT rows of equal square 32x32 logical-pixel cells. Output a 1:2 portrait atlas. Four characters, TWO consecutive rows per character. Columns in EVERY row: facing DOWN toward viewer; facing LEFT; facing RIGHT; facing UP away from viewer. First row of each character is walking pose A (left foot forward), second row pose B (right foot forward), a modest natural two-pose walk. Full bodies, same identity, size and costume between poses and directions. Each complete sprite strictly inside the central 65% of its cell, generous transparent margin all four sides; feet centered at 80% cell height. No figure crosses rows. True coarse 16-bit pixel clusters, restrained muted slate/olive/ochre/plum/teal palette and dark charcoal outlines, upper-left light, tiny expressive chibi proportions but adult adventurers. Genuine transparent alpha, no scenery, ground rectangles, shadows, text, checkerboard, grid lines, borders or extra cells. Pixel-art asset, not painted illustration. Exactly 32 isolated sprites total. Rows 1-2: long dark-haired adult sorceress wearing a modest royal blue robe and blue cape. Rows 3-4: elderly wizard in a wide pointed slate-blue hat and blue robe, gray beard and short wooden staff. Rows 5-6: white-haired adult mage in dark plum robe and violet cape, short wooden staff. Rows 7-8: whimsical small upright golden-brown bear adventurer, round ears, ochre tunic and small brown backpack. Each two-row pair is ONE consistent character in all eight poses.
```

### Eight-row packing attempt (not used)

```text
Edit this character sprite atlas ONLY to repair its grid packing. Preserve all four character designs, colors, directions and alternating walking poses. Re-layout all 32 COMPLETE figures into EXACTLY FOUR equal columns and EIGHT equal rows over the FULL canvas. Columns are down, left, right, up; paired rows belong to blue sorceress, blue wizard, white-haired violet mage, golden bear, in that order. Reduce each figure to 65% of its current size and center it within its exact cell. Cell centers vertically are 6.25%,18.75%,31.25%,43.75%,56.25%,68.75%,81.25%,93.75% of total image height. Minimum 16% empty transparent margin on every edge of every cell. No feet, hats, staffs or hair may touch or cross a grid boundary. Preserve real transparent alpha; no grid lines, labels, extra poses, backgrounds or shadows. Return the full four-column eight-row atlas, 1:2 canvas.
```

### heroes-02.png — four rows; portraits 5–6

```text
Edit the referenced sprite sheet into a SQUARE atlas containing ONLY the blue sorceress in rows 1-2 and the blue wizard in rows 3-4. Exactly FOUR equal columns and FOUR equal rows. Remove the other two characters. Preserve each retained character's existing appearance, the two walking poses and all four directions. Columns: down, left, right, up. Each character uses two rows. Exactly sixteen sprites. Center each complete sprite in its cell. Keep each figure within the central 65 percent of the cell with transparent padding on EVERY edge. All 4 rows occupy exactly one quarter of canvas height; cell centers at y=12.5%,37.5%,62.5%,87.5%. No sprites touch or cross cell borders. Coarse native 32-pixel art, true transparent alpha, no text, grid lines, extra characters or shadows. Return a SQUARE image.
```

### heroes-03.png — four rows; portraits 7–8

```text
Edit the referenced sprite sheet into a SQUARE atlas containing ONLY the white-haired violet mage in rows 1-2 and the golden bear in rows 3-4. Exactly FOUR equal columns and FOUR equal rows. Remove the other two characters. Preserve each retained character's existing appearance, the two walking poses and all four directions. Columns: down, left, right, up. Each character uses two rows. Exactly sixteen sprites. Center each complete sprite in its cell. Keep each figure within the central 65 percent of the cell with transparent padding on EVERY edge. All 4 rows occupy exactly one quarter of canvas height; cell centers at y=12.5%,37.5%,62.5%,87.5%. No sprites touch or cross cell borders. Coarse native 32-pixel art, true transparent alpha, no text, grid lines, extra characters or shadows. Return a SQUARE image.
```

### Final packing edit applied to heroes-02 and heroes-03

```text
Precise sprite-atlas packing correction. Retain the same SIXTEEN complete sprites in exactly the same 4 by 4 row-major order and preserve their pixel-art appearance, costume, direction and poses. SHRINK EACH complete sprite to 65 PERCENT of its current width and height, then center it inside its ORIGINAL EQUAL SQUARE GRID CELL. Every cell must have at least 20 percent completely EMPTY TRANSPARENT margin on ALL FOUR SIDES. Especially leave a large empty horizontal band above and below each row: no feet, hats, hair, cape or staff may touch another row. Use centers x/y 12.5%, 37.5%, 62.5%, 87.5% of full canvas. Do not crop or truncate any figure. Square canvas, true transparent background, no grid, text or added content.
```

### heroes-04.png — eight rows; portraits 9–12

```text
Use case: stylized-concept. Asset type: transparent production sprite atlas for a top-down 16-bit fantasy RPG. EXACTLY FOUR columns and EIGHT rows of equal square 32x32 logical-pixel cells. Output a 1:2 portrait atlas. Four characters, TWO consecutive rows per character. Columns in EVERY row: facing DOWN toward viewer; facing LEFT; facing RIGHT; facing UP away from viewer. First row of each character is walking pose A (left foot forward), second row pose B (right foot forward), a modest natural two-pose walk. Full bodies, same identity, size and costume between poses and directions. Each complete sprite strictly inside the central 65% of its cell, generous transparent margin all four sides; feet centered at 80% cell height. No figure crosses rows. True coarse 16-bit pixel clusters, restrained muted slate/olive/ochre/plum/teal palette and dark charcoal outlines, upper-left light, tiny expressive chibi proportions but adult adventurers. Genuine transparent alpha, no scenery, ground rectangles, shadows, text, checkerboard, grid lines, borders or extra cells. Pixel-art asset, not painted illustration. Exactly 32 isolated sprites total. These four characters are the game's Silly Mode avatars. Rows 1-2: upright comic golden rooster, small red comb, russet tail, orange legs. Rows 3-4: tiny golden fairy with pink wings, modest rose tunic, yellow hair and boots, walking rather than hovering. Rows 5-6: friendly round yellow smiling face as a head, dark russet jester body, large cream ruffled collar and brown cape. Rows 7-8: whimsical red toy-like imp adventurer with a round red smiling head, small yellow eyes, tiny horns, red tunic and yellow mittens; friendly rather than frightening. Each two-row pair is ONE consistent character in all eight poses.
```


## Desert and vegetation additions

`desert.png` is a 2 × 2 opaque 32-pixel terrain atlas. `flora.png` is a 4 × 3 transparent 48-pixel object atlas. Generated and padded with the built-in imagegen tool; source outputs remain in the generated-images directory. Both compile through RetroArt into the established palette.

### Desert prompt

```text
Use case: stylized-concept. Asset type: production pixel-art RPG terrain atlas. Create a 2 by 2 grid of FOUR opaque seamless 32x32 logical-pixel tiles, enlarged to 1024x1024. No gutters, labels, frames or perspective. Authentic restrained 16-bit pixel clusters, shared muted ochre, sandstone, brown, olive and slate palette, no smooth gradients or outlines around cells. Top left: quiet warm desert sand, sparse tiny ripples and pebbles, walkable. Top right: same sand with a few low dune ridges, walkable. Bottom left: solid chunky ochre mountain rock/cliff surface filling its entire cell, unmistakably a wall, upper-left lighting. Bottom right: pale sandy ground with a few dry olive grass tufts, walkable. Each tile entirely fills its square with opaque terrain and repeats cleanly; keep detail quiet enough for tiny characters. This is functional terrain for a top-down miniature retro fantasy RPG.
```

### Vegetation prompt

```text
Use case: stylized-concept. Asset type: production transparent pixel-art RPG vegetation and small ground objects atlas. Exactly 4 columns by 3 rows, twelve equal square cells with no gutters or borders. Each is a 48x48 logical-pixel canvas, enlarged for delivery. Authentic 16-bit large deliberate pixel clusters, muted olive greens, warm brown, sandstone, slate, pale cream, soft pink and blue accents. Top-down three-quarter view matching tiny fantasy villages, light upper-left. Row 1 left to right: desert palm tree; small two-armed cactus; low dry thorn bush; spiky desert grass. Row 2: dark conifer pine; broad leafy oak tree; small cluster of white meadow flowers; small cluster of pink and blue desert flowers. Row 3: bleached small animal skull on ground; gray sitting boulder; pink lotus on flat green lily pad; blue star-shaped water flower on flat green lily pad. One isolated object per cell, full silhouette strictly INSIDE central 70 percent of its cell, at least 6 logical transparent pixels on each side; ample transparent padding especially tree branches. Center each object horizontally with base at 36/48 cell height; smaller flowers stay small. No ground rectangles, backgrounds, shadows outside objects, text, grid lines or UI. Genuine transparent alpha across the entire sheet outside sprites.
```

### Vegetation packing correction

```text
Edit this atlas only to fix sprite packing. Preserve all twelve sprites, their colors, order and pixel-art style. The output must be exactly FOUR equal columns and THREE equal rows. Cell centers are at x=12.5%,37.5%,62.5%,87.5% and y=16.6667%,50%,83.3333% of the whole canvas. Reposition and uniformly reduce EACH sprite to fit strictly within the central 60% of its own cell, with at least 20% cell-width/height transparent margin on ALL four sides. In particular the palm and pine must not extend into the next row. No object may touch a cell boundary. Preserve real transparent alpha. No outlines, text, grid marks or added ground.
```


The initial props sheet placed the portal's top into the lever cell above it.
Native atlas-border checks caught this. It was revised with this imagegen prompt:

```text
Use case: precise-object-edit. Correct ONLY layout padding in this transparent RPG map-object atlas. It must be an EXACT equally spaced 4-column by 4-row grid. Keep the same sixteen objects, same order, same design, colors, and pixel art. Reduce each complete object to 72 percent of its present size. Center each object inside its equal original cell, with its lowest visible edge at 82 percent of cell height. Keep a MINIMUM 12 percent empty transparent margin inside EVERY EDGE of EVERY CELL. No part of the row-four tree, portal, or shrine may extend upward into row three. The two lever sprites and the fountain must be fully isolated inside their cells. Preserve all flames, wooden corners, lever handles and arch tops. Exactly these sixteen objects, no extra content, backgrounds, checkerboard, labels, borders, grid lines or shadows. Real alpha transparency, 4x4 equal grid.
```

The first `monsters-03` atlas also failed the compiled sprite-border check at
Flame Beast. It was revised with this exact imagegen padding prompt:

```text
Use case: precise-object-edit. Edit this transparent 4-column by 3-row RPG character atlas. Change ONLY the layout padding. Reduce each of its TWELVE complete existing figures to 72 percent of its current size and center each within its ORIGINAL EQUAL GRID CELL. Preserve each figure's shape, design, colors, pose and coarse pixel style. Exactly the same twelve figures in the same row-major order. Lowest visible edge aligned at 85 percent cell height. At least 10 percent clear transparent padding on EVERY side of EACH cell. In particular the Flame Beast's whole horns and hands must stay well inside row 2 column 1, the Air Dragon wings inside row 2 column 2, the Earth Giant's fists inside row 2 column 4, and the Gatekeeper's key inside row 3 column 1. No anatomy touches any cell boundary. No new content, no extra objects, labels, borders, shadows, scenery, backdrop or checkerboard. Preserve real alpha transparency. Return the full 4 by 3 atlas.
```

The first `monsters-01` atlas failed the compiled sprite-border check at the bat
wing. It was revised using imagegen; the new padding is checked after native-pixel
compilation. Exact edit prompt:

```text
Use case: precise-object-edit. Edit this transparent 4 by 4 RPG monster atlas. Change ONLY the layout padding: reduce each of the sixteen COMPLETE creatures to 72 percent of its current size. Preserve every creature's existing shape, pose, colors and pixel art design. Exactly the same sixteen creatures in the same row-major order. Center each sprite in its own ORIGINAL EQUAL GRID CELL, align feet or lowest edge at 85 percent cell height, and leave at least 10 percent EMPTY TRANSPARENT margin on EVERY side of EACH cell. Especially keep both bat wings fully inside row 2 column 1, all gargoyle wings inside their respective cells, the fire beetle's legs inside row 4 column 1, the wizard staff inside row 3 column 4, and the moose antlers inside row 1 column 4. No sprite touches or crosses any cell boundary. No new creatures, no captions, labels, borders, shadows, background or checkerboard. Preserve genuine alpha transparency. Return the complete four-column four-row atlas.
```

The first `monsters-02` output was revised for atlas padding before use. The final
transparent version keeps tongues, flames and tridents inside their own cells.

Padding edit prompt:

```text
Use case: precise-object-edit. Edit this transparent 4 by 4 RPG monster atlas. Change ONLY the layout padding: reduce each of the sixteen complete creature sprites to 75 percent of its current size, keeping its original shape, pose, colors and pixel-art design. Center each sprite in its own original cell, aligning the feet/lowest edge consistently near the cell bottom, with at least 10 percent EMPTY TRANSPARENT padding on EVERY side inside EACH cell. In particular keep the toad's entire tongue inside row 2 column 3; keep the fire lord's flames inside row 1 column 4; and keep all claws and tridents inside their own cells. Exactly the same sixteen creatures in the same row-major order. No new content, no labels, no borders, no background or checkerboard. Preserve real alpha transparency. Return the complete atlas.
```

Generated with the built-in imagegen tool. Existing approved town art is preserved.
Compile these magnified source atlases to the same native pixels and palette as
the town. No gameplay RNG is used by rendering or animation.

| File | Grid | Native cell | Contents |
| --- | --- | --- | --- |
| terrain.png | 4 × 4 | 32 × 32 | Floor/wall pairs: earth, stone, fire, Zephyr, water, final cave, Hell, snow |
| monsters-01.png | 4 × 4 | 48 × 48 | Monster IDs 1–16 |
| monsters-02.png | 4 × 4 | 48 × 48 | Monster IDs 17–32 |
| monsters-03.png | 4 × 3 | 48 × 48 | Monster IDs 33–41, King, princess, teacher |
| props.png | 4 × 4 | 48 × 48 | Chests, sign, stairs, doors, fountain, fire, switches, raft, tree, portal, training shrine |

## Map objects prompt

```text
Use case: stylized-concept. Production transparent sprite atlas for a warm early-1990s 16-bit fantasy RPG. Exactly sixteen complete isolated environment objects in a precise equal 4-column by 4-row grid. Each cell represents 48x48 logical pixels. Coarse economical pixel clusters, flat warm ochre/slate/olive/plum/teal colors, dark charcoal outline, slight top-down three-quarter view. Match a whimsical medieval village RPG. Real alpha transparency. No background, checkerboard, text, labels, grid lines, gradients, antialiasing, contact shadows, or extra objects. Keep each whole object in central 75 percent of its cell, clear padding on EVERY edge. Grounded object feet end at 85 percent cell height.
Exact row-major order:
Row 1: closed small wooden treasure chest with brass latch; same chest opened with a few gold coins inside; small wooden location notice board on two short posts with tiny unreadable marks; rectangular stone stairs ascending into a dark arch, no surrounding wall.
Row 2: stone stairs descending into a dark square opening; closed plain oak dungeon door in a slender slate frame; closed magical dungeon door with a modest multicolor keyhole detail in slate frame; closed dark iron underworld door with small red keyhole detail in slate frame.
Row 3: small turquoise healing fountain in a shallow stone basin; compact low wall of muted orange-red fire; small metal lever tilted left on a stone pedestal; same lever tilted right.
Row 4: compact square wooden raft with four logs; rounded squat olive tree with brown trunk, no grass base; subtle plum magic portal inside a thin stone arch; small carved training shrine with crossed wooden practice swords.
Exactly one object per cell, total sixteen objects. ALL extremities and flames fit within own cell with generous empty padding.
```

## Monster IDs 33–41 and NPC prompt

```text
Use case: stylized-concept.
Asset type: transparent production sprite atlas for a warm, quirky early-1990s 16-bit RPG. Exactly TWELVE complete isolated sprites in a perfectly regular 4-column by 3-row grid. Each cell represents 48x48 logical pixels, total 192x144 logical pixels, enlarged with nearest-neighbor pixels. Genuine alpha transparency.
Style: economical coarse pixel clusters, solid flat colors, dark charcoal outline, shared warm ochre/slate/olive/plum/teal palette. Slight top-down three-quarter RPG perspective. Humorous fantasy, readable silhouettes, restrained detail. No smooth gradients, antialiasing, background scenery, words, labels, numbers, borders, checkerboard, or contact shadows.
CRITICAL: Each entire figure fits in the central 75 percent of its own cell, leaving at least 10 percent empty transparent margin at EVERY cell edge. Feet align at 85 percent cell height. Wings, horns and weapons NEVER reach or cross cell edges. Each sprite centered with consistent native pixel size.
Exact row-major contents:
Row 1: Muscles (absurdly muscular squat olive ogre, clenched fists, brown shorts); Axe Man (stocky hooded slate-and-brown warrior holding a modest single axe); Wormoid (large coiled russet segmented cave worm, monstrous maw, upright head); Stone King (crowned broad gray stone golem with tiny gold crown and thick rock arms).
Row 2: Flame Beast (broad horned volcanic beast with muted ember-red cracks); Air Dragon (pale slate-blue winged dragon curled compactly with airy spiral tail); Soaked Spike (bulky teal aquatic monster covered in coral-like spikes); Earth Giant (massive mossy brown rock colossus with big fists).
Row 3: Gatekeeper (broad horned underworld guardian in dark slate armor with muted ember highlights, holding a small key); King (friendly elderly human king, gold crown, crimson cape, blue tunic, full body); Princess (adult human princess in a modest plum gown with a small gold circlet, full body); Skill Teacher (adult human in muted olive robe with a wooden staff, full body).
Exactly one figure per cell, these twelve figures only. No extra decorative items.
```

## Monster IDs 17–32 prompt

```text
Use case: stylized-concept.
Asset type: production transparent monster sprite atlas for a warm, quirky early-1990s 16-bit RPG. Exactly sixteen complete isolated enemy sprites in a perfectly regular 4-column by 4-row grid. 48x48 LOGICAL pixel cells, 192x192 logical pixel atlas enlarged for delivery. Actual alpha transparency in the background. No checkerboard painted into the image.
Style: coarse economical pixel clusters, solid palette colors, dark charcoal outline, warm ochre/slate/olive/plum/teal highlights. Consistent pixel scale and slight top-down three-quarter RPG perspective. A humorous, handmade fantasy game mood. No gradients, antialiasing, glossy painting, scenery, text, numbers, labels, borders, contact shadows or weapons outside the cell.
Each creature centered in its own equal cell, full body, feet or lowest body edge on row 43 of its cell, 4 logical pixels of clear padding. Each has one readable idle/combat-ready pose facing slightly down and right. No characters overlap adjacent cells. Preserve silhouettes suitable for native 32px world icons and 48px combat sprites.
Exact row-major order:
Row 1: Venus Man Trap (upright carnivorous green plant with a toothed red mouth and root feet); Little Daemon (small red imp with horns, tail and bat wings); Snow Monster (round shaggy off-white snow creature); Fire Lord (regal red and black armored humanoid with ember crown).
Row 2: Evil Air Wave (angry slate-blue spiral of wind with a visible face); Air Thing (pale floating wind spirit with long curling arms); Tongue Toad (olive toad with a long curled red tongue); Squawker (squat odd bird, cream belly and purple crest).
Row 3: Water Ghost (translucent-looking but solid-pixel teal ghost with trailing watery body); Wisp (small pale turquoise spirit orb with a soft-shaped solid pixel flame tail); Ice Cave Crab (blue and slate crab with icy claws); Electric Eel (curved teal eel with tiny yellow electrical accents).
Row 4: Merman (green-blue fish humanoid with a small wooden trident); Hand Head (strange squat purple humanoid with a giant hand for its head); Lump (lumpy round plum-colored creature with tiny eyes and little feet); Earl Earthworm (thick brown earthworm rearing up with a comically stern face).
Exactly these sixteen, one per cell, transparent backgrounds and no decorative extras. Preserve equal cell dimensions even for airy spirits; keep ALL anatomy within each cell.
```

## Monster IDs 1–16 prompt

```text
Use case: stylized-concept.
Asset type: production transparent monster sprite atlas for a warm, quirky early-1990s 16-bit RPG. Exactly sixteen complete isolated enemy sprites in a perfectly regular 4-column by 4-row grid. 48x48 LOGICAL pixel cells, 192x192 logical pixel atlas enlarged for delivery. Actual alpha transparency in the background. No checkerboard painted into the image.
Style: coarse economical pixel clusters, solid palette colors, dark charcoal outline, warm ochre/slate/olive/plum/teal highlights. Consistent pixel scale and slight top-down three-quarter RPG perspective. A humorous, handmade fantasy game mood. No gradients, antialiasing, glossy painting, scenery, text, numbers, labels, borders, contact shadows or weapons outside the cell.
Each creature centered in its own equal cell, full body, feet or lowest body edge on row 43 of its cell, 4 logical pixels of clear padding. Each has one readable idle/combat-ready pose facing slightly down and right. No characters overlap adjacent cells. Preserve silhouettes suitable for native 32px world icons and 48px combat sprites.
Exact row-major order:
Row 1: Small Bunny (cream rabbit with upright ears); Giant Beetle (sturdy moss-green beetle with six legs and large mandibles); Attack Weasel (slim russet weasel, mischievous teeth); Mystic Moose (small purple-brown moose with antlers and mystic teal eyes).
Row 2: Rabid Bat (purple bat with open wings, visible tiny feet); Ancient Seagull (scruffy white and slate seabird); Mouth Man (round squat ochre humanoid whose enormous mouth dominates the face); Stone Golem (chunky gray animated rock figure).
Row 3: Hairy One-Eye (shaggy brown cyclops); Baby Gargoyle (small slate winged imp); Adult Gargoyle (larger charcoal gargoyle with stone wings and horns); Evil Wizard (hooded plum-robed wizard holding a little wooden staff).
Row 4: Fire Bug (red orange beetle, subtly glowing shell); Flame Flier (small winged ember creature); Cloaked Stallion (dark horse rearing slightly under a ragged plum cloak); Burning Skeleton (ivory skeleton with tiny ember flames along its shoulders).
Exactly these sixteen, one per cell, with transparent empty cell backgrounds and no decorative extras.
```

## Terrain prompt

```text
Use case: stylized-concept.
Asset type: production 2D RPG terrain texture atlas for a C# MonoGame game, matching its approved early-1990s 16-bit pixel artwork.
Primary request: exactly sixteen SQUARE terrain texture tiles in a precisely aligned 4-column by 4-row grid. Each tile is 32x32 LOGICAL pixels; the whole atlas is 128x128 logical pixels enlarged with nearest-neighbor to 1024x1024. Every logical pixel is a solid flat square. Tiles touch with NO gaps, borders, labels or grid lines. No scene, no characters, no text.
Style: economical coarse pixel clusters, restrained detail, broad quiet playable floors, top-down textures with slight upper-left light. Crisp pixel edges, no smooth gradients, no antialiasing or glossy illustration.
Palette: warm slate #252B3B #404957 #697585, dark charcoal #141924; olive #364E33 #496348 #6B8747; ochre #B78858 #D9B87C; earth #55382E #83563E; teal #24475E #2B6B83 #4A9EAA; plum #523B6B #805681; muted ember #892737 #BF5652 #E88965. Use only a restrained subset per tile.
Exact grid contents, reading left to right:
Row 1: quiet earthen cave floor (warm brown sparse pebbles); rough earthen rock wall top (raised craggy brown); quiet slate stone cave floor (subtle worn paving); solid slate stone wall top (coarse irregular rock).
Row 2: dark volcanic cave floor (cool charcoal with sparse dull red cracks); solid volcanic basalt wall top (dark jagged clusters); pale windy Zephyr cave floor (quiet desaturated blue gray); pale wind-carved stone wall top (broad curved rock clusters).
Row 3: quiet damp teal cave floor (subtle flat stone); wet blue-gray cave wall top (coarse rock, restrained teal rim); strange final-cave floor (muted plum earth, sparse speckles); strange final-cave wall top (chunky plum and slate rocks).
Row 4: quiet Hell floor (dark russet burnt earth); Hell rock wall top (red brown black rock); quiet snowy ground (off-white with slate shadows, no blue glare); frosted rock wall top (slate clusters capped with snow).
All floors fill their square edge-to-edge and tile seamlessly on all four edges. Wall TOP textures also fill the whole square edge-to-edge; no freestanding boulders, perspectives, cutouts, shadows outside a tile, or decorative objects. Tiles must be consistent native pixel size, density, and top-down view. No outlines separating the sixteen atlas cells.
```

## Princess variants and sandstone entrance

`quests.png`: 2×2, native 48-pixel cells. Princess variants 1–3 follow original bitmaps 391–393; cell 4 is the desert entrance. Compiled to the shared palette and 44-pixel foot anchor. Generated with the built-in image tool; original retained at `exec-49d4afe6-623b-4b05-8ce9-a0915c831f55.png` in the generated-images session folder. Exact prompt:

```text
Use case: stylized-concept. Create a production pixel-art sprite atlas for the established Yipe III retro RPG. Exactly 2 columns by 2 rows of equal square cells, transparent background. Native design 48x48 per cell, visibly enlarged with crisp nearest-neighbor pixels, no smooth illustration or blur. Each object entirely inside its own cell, centered horizontally, generous transparent border at least 6 native pixels on each side and top. Bottom of every object at native row 42 of its cell. Top-left: small full-body blonde princess in a pink medieval gown and tiny gold crown, hands together, facing viewer, matching original princess variant 1. Top-right: a distinct auburn-haired princess in a rose-purple gown and gold crown, facing viewer, variant 2. Bottom-left: blonde princess in a teal-blue gown with tiny gold crown, facing viewer, variant 3. These three have compact readable silhouettes, heads and bodies like the existing retro RPG character sprites, height about 30 native pixels, no background or ground shadows. Bottom-right: squat sandstone cave entrance, a black opening surrounded by angular warm ochre and tan rock, width 34 native pixels height 29, bottom flush at row 42, no vegetation, no green or gray rim, matching the warm rocky desert texture. Use a restrained palette of dark navy outlines, muted pink/rose, teal, gold, parchment and earthy sandstone shades. Consistent top/front three-quarter RPG perspective. Four sprites only, no text, labels, grid lines, tile outlines, cast shadows, decorative floor or extra objects. Preserve transparent alpha.
```

## Desert atmosphere and softened boundaries — 2026-10-04

`WorldDesert.cs` compiles a map-wide sand layer from the existing desert atlas
and authored corner directions. A separable smoothing pass, gentle spatial
variation and palette-matched dry-grass dithering replace hard geometric edges.
No bitmap assets changed. Wind is procedural native-pixel artwork: sparse soft
dust puffs/motes and tiny rotating twig silhouettes with bounce and ground shadows.
At most two continuous, obstacle-free sand lanes carry tumbleweeds. Scene time
controls the effects; no simulation RNG or collision is involved. `DrawFlora`
adds a one-pixel crown/branch sway to existing palm and dry-bush sprites, leaving
roots anchored. The original generation prompts and source assets above remain
unchanged. [Implementation evidence](../../../../../evidence/2026-10-04-desert-biome/README.md)
retains before/after renders and an eight-second animation preview.

## Hell biome — 2026-10-04

`hell.png` supplies eight new rock/ruin/ironwork sprites; see [the complete
generation prompt and cell order](HELL_ART.md). `WorldHell.cs` compiles charcoal
ground, eight-frame lava/crust and soft firelight caches, with scene-clock flame,
bubble, ember, ash and smoke drawing. Only Hell maps 117–120 use these assets.
Original fire-wall cells retain visible hot hazards and original collision.
[Completion evidence](../../../../../evidence/2026-10-04-hell-biome/README.md)
contains all four regions, generated variants and the animated preview.

The user rejected the later small-rock/dark-floor revision. Current Hell walls
use connected volcanic basalt from `terrain.png` cell 5, with exposed-edge bevels
and animated heat following the texture's painted fissures. Ordinary wall rock
props are removed. The floor is moderate charcoal with dim ember cracks, and
brighter cool-gray paving draws over those cracks with reduced firelight spill.
The path now uses irregular flat flagstones with shallow joints, and exposed
wall corners are rounded with worn perimeters; shared wall edges stay joined.
Landmarks and source art are unchanged. Default-settings captures include
scanlines and Strong miniature.
[Current visual evidence](../../../../../evidence/2026-10-04-hell-flagstones/README.md).
