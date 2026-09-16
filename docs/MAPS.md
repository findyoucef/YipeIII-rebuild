# Map recovery

The original `CMap` object is constructed at `0x0041cac0` and loaded at
`0x0041cb30`. Each map is 13 tiles wide and 12 tiles high. Its 156 signed
16-bit tile values begin at object offset `+0x04` and use column-major indexing:

```text
index = x * 12 + y
```

The checked getter at `0x0041cd80` establishes the bounds and indexing. The
setter at `0x0041cdb0` uses the same formula. Three 156-byte state grids follow
the tiles at offsets `+0x13c`, `+0x1d8`, and `+0x274`; their exact gameplay names
remain unresolved.

## Static maps

The dispatcher at `0x0041e110` selects 112 static loader functions. Each loader
initializes 156 local 16-bit constants and copies them into the map. The generated
catalog at [`assets/maps/original-map-tables.json`](../assets/maps/original-map-tables.json)
records every dispatched map ID, loader address, environment value, and tile grid.

Regenerate it from a current Ghidra C export:

```bash
./tools/extract_map_tables.py \
  --decompiled-dir /Users/baouchiy/Downloads/YipeIII-analysis/decompiled \
  --output assets/maps/original-map-tables.json \
  --cpp-output src/generated/original_maps.cpp
```

The version-2 catalog also extracts the compact event records initialized at
`0x0041ec40`: 20 location messages, 10 overlay triggers, 26 depth portals, eight
first linked records, and six second linked records. The extractor validates
those totals and every coordinate. It requires every loader to produce all 156
tile values and stops on an incomplete table. Map IDs `0x76` through `0x78` are
generated at runtime by `0x0041e0f0` instead of appearing in the static
dispatcher, so they are outside this catalog. The core reconstructs their
underlying generator at `0x0043fd80`: it creates four to six random rooms,
connects them in order to the west and east anchors and current entry tile,
then applies the original diagonal cleanup and fixed boundary writes. Seeded
tests fix both the resulting tile hashes and random-call counts, including the
original boundary overwrite that can cut a short seeded corridor. The generated
C++ copy lets the core load static tables without a JSON dependency at runtime.

Surface-map transitions use player offsets `+0x46` and `+0x48` as wrapped region
coordinates. `0x00444b50` moves a player arriving at an outer border back to the
opposite inner border, and `0x00444ad0` calculates the next map as
`region_x - 6 * region_y + 0x11` for layer zero. The core preserves the original
region-coordinate wrapping and the additional layer and alternate-map offsets.
The SDL application follows these exits and can render every static table. Its
headless smoke test renders all 112 maps, exercising the recovered tile-to-bitmap
switch and every required terrain sheet. The same loader and smoke test now
render the three runtime-generated maps. Dynamic monster-overlay tiles remain
separate follow-up work.

## Starting area

Player initialization selects map ID `0x11` and position `(5,5)`. Its loader is
`0x00420e60`, and the dispatcher assigns environment `0`. `yipe::starting_map()`
contains that exact grid. Core tests check tiles at the origin, starting position,
central entrance, and far edge to catch orientation or truncation mistakes.

Movement is reconstructed from the switch at `0x00444c10`. `yipe::try_move()`
preserves its numeric result codes, whether each tile updates the player position,
edge behavior, and the four unresolved player flags used by conditional tiles.
The SDL application renders the starting area with the original 32-pixel tile
art and the type-1 character bitmap. Arrow keys or WASD move the character across
ordinary walkable tiles. Static depth entrances now select the matching map on
the next layer, and leaving the original layer range returns to map `0x11` at
`(4,5)`, as in `0x00444ad0` and `0x00444f40`.

The action dispatcher at `0x0040e880` establishes these special movement paths:

| Result | Reconstructed behavior |
| --- | --- |
| `0x05`–`0x09` | Weapon, armor, healing, miscellaneous, and magic-shop entrances |
| `0x0c` | Difficulty-limited full restoration of HP, SP, and AP |
| `0x0d` | Casino entrance |
| `0x0e`, `0x0f`, `0x10` | Descend, ascend, or alternate descend between map layers |
| `0x11` | Special-ability training entrance |
| `0x13`, `0x14` | Rainbow-key and underworld-key locked-door messages |
| `0x15` | Dynamic quest/information dialog (resource 295) |
| `0x16` | Return to the starting surface location |
| `0x17` | Two-roll, nonlethal fire-wall damage |

The SDL dispatcher exposes the recovered entrance identities and implements the
restoration, depth, surface-return, key-message, and fire-wall effects. The core
also preserves the tree-climbing-shoes and key flags. Generated event records
now drive hidden overlays (`0x18`), ordinary-tile depth portals, door replacement
(`0x12`), linked-tile swaps (`0x1a`), and teleport destinations (`0x1b`). The
adapted renderer always redraws the complete map, covering the original
visibility refresh performed by `0x19`. Native format version 5 introduced
persistence for changed door and linked-mechanism tiles; current version 6
retains it.

The secret-tree puzzle is a separate branch inside ordinary movement result
`3`, not result `0x15`. Entering tile `0x56` or `0x57` first shows the tree-climb
message. On map 1, if the bonus-healing ring has not already been obtained, the
game opens dialog 459 and compares its edit text exactly with `hhhhhh` at
`0x00448180`. Success awards 1500 gold and 250 experience, decrements a positive
restoration-use counter, and sets the persisted ring flag. Wrong input changes
nothing, and the ring flag prevents the puzzle from reopening. The SDL build
accepts the same case-sensitive code through an adapted title-bar text-entry
prompt; core tests cover failure, success, and repeated attempts.

The central tile with ID `1` returns action code `8`. The action dispatcher at
`0x0040e880` opens miscellaneous-shop dialog resource 213. Because the current
map ID is `0x11`, `0x004402b0` selects the title resource “Fred's Miscellaneous
Shop,” stock builder `0x00440e50`, and the map-specific price slice in
`0x00441010`. The SDL build displays all 11 recovered stock rows and prices when
the player enters that tile. Its purchase path follows `0x00441250`: seven
consumables use the first free slot among the 13 carried-item slots, while the
four equipment rows update their matching slot only when the purchased tier is
an improvement. Failed purchases do not deduct gold.

## Monster catalogs

The event initializer at `0x0041ec40` creates 186 monster placements on 84 maps,
with up to five slots per map. The generated catalogs preserve each monster
type, tile coordinate, slot, encounter size, and the original `+0x36c` and
`+0x371` flags. Difficulty changes some group sizes and late-game monster types;
player level changes the selections on maps `0x39` and `0x75`. Maps `0x76`
through `0x78` receive types 32, 33, and 34 even though their terrain is generated
separately.

Regenerate the placement catalog with:

```bash
./tools/extract_monster_placements.py \
  --source /Users/baouchiy/Downloads/YipeIII-analysis/decompiled/functions/0041ec40.c \
  --output assets/maps/original-monster-placements.json \
  --cpp-output src/generated/original_monsters.cpp
```

Sixteen records use the extended writer at `0x0041d780`. Its final two
arguments become the companion monster types held at generated-map offsets
`+0x38c` and `+0x390`; the combat constructor replaces slots 1 and 2 with
those types for primary monsters above type 34. The generated catalog exposes
the fixed and difficulty-derived values as `companion_type_1` and
`companion_type_2`. The separate `+0x371` placement flag remains unresolved.

The setup table at `0x00405640` and attack table at `0x004042e0` provide all 41
monster definitions. The generated definition catalog joins their names from
string resources 230–270, bitmaps 174–214, base health, health random range,
attack, defense, special rating, and ordinary minimum/maximum damage. Combat
setup can materialize every definition. The core supports fixed groups of up to
three same-type opponents, including target selection, slot-order
counterattacks, fleeing, and deferred group rewards. It also preserves the
constructor's automatic group-size selection and adjacent-rank variants used by
the three size-zero placements on map `0x14`. The four player special abilities
and their Invisibility/Enrage combat statuses are implemented from the static
branches. High-rank placements now override their generated companions and
scale primary health by player level. The Gatekeeper's empowered setup, attack,
defense, and damage branches are also implemented; its surrounding quest state
and the remaining monster-specific branches still need reconstruction before
the application enables all encounters.

The first reachable static encounter is the Giant Beetle on map `0x0b` at
`(2,8)`. The route from the north-edge entry at `(5,10)` is covered by the
playable-flow test. Bitmap resource 175 is drawn at the placement until the
encounter is won; the version-5 native save preserves its defeated state.

The start-area renderer currently needs bitmap resources 133, 134, 137, 139,
141, 142, 377, and 418. Resource 406 is the initial type-1 character image.
Tile-to-source-rectangle mappings come from the draw switch at `0x0040cc10`, and
bitmap loading comes from `0x0040b8a0`.
