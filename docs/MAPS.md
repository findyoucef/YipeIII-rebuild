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

The version-3 catalog also extracts the compact event records initialized at
`0x0041ec40`: 20 location messages, 10 overlay triggers, 20 princesses, 26 depth
portals, eight first linked records, and six second linked records. The extractor validates
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
| `0x15` | King's dynamic quest dialog, hints, and one-time rewards (resource 295) |
| `0x16` | Return to the starting surface location |
| `0x17` | Two-roll, nonlethal fire-wall damage |

The SDL dispatcher exposes the recovered entrance identities and implements the
restoration, depth, surface-return, key-message, fire-wall, and King-dialog effects.
The King selector preserves all 34 message/footer pairs, greeting time-of-day
handling, quest flags, hint rolls, and gold/equipment/Lucky Rabbit Foot rewards.
The 20 princess records retain position, one of three bitmap variants, and their
one-time rescue index. The SDL view renders bitmaps 391–393 and removes a rescued
princess on later map loads. Dialog 394 offers Gold, Experience, Kiss, and Mystery;
the core preserves all formulas, healing/stat/item outcomes, two-roll Mystery
selection, its high-threshold Rainbow Key override, and the serialized counter at
player offset `+0x682`. Returning to map `0x11` after the keyed Gatekeeper victory
with all 20 rescue values runs the reconstructed ending-state transition.

The core also preserves the waterproof shoe spray, tree-climbing shoes, and key flags.
The spray opens stream tiles `0x58`/`0x59`, while the shoes open tree tiles
`0x56`/`0x57`. Generated event records
now drive hidden overlays (`0x18`), ordinary-tile depth portals, door replacement
(`0x12`), linked-tile swaps (`0x1a`), and teleport destinations (`0x1b`). The
adapted renderer always redraws the complete map, covering the original
visibility refresh performed by `0x19`. Return-to-Surface items and spells use
the entrance map and region saved by the `0x0e` descent path, then
`0x0041e090` places the player immediately below the final `0x36`/`0x39`
entrance on that map. Native format version 5 introduced persistence for changed
door and linked-mechanism tiles; current version 10 retains it, the recovered
King quest state, the princess Mystery counter, permanent monster defeats, and
the saved surface-return region.

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
with up to five slots per map. The generated catalogs assign each record a
stable index and preserve its monster type, tile coordinate, slot, encounter
size, and original `+0x36c` and `+0x371` flags. Difficulty changes some group
sizes and late-game monster types; player level changes the selections on maps
`0x39` and `0x75`. Maps `0x76`
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
`companion_type_2`. The separate byte at `+0x371` marks a 64×64 monster that
occupies the placement tile plus its east, south, and southeast neighbors.
The byte at `+0x36c` makes a defeated placement return the next time its map is
loaded; the other placements remain absent through the per-map player flags at
`+0x34e`.

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
defense, and damage branches are also implemented. The King dialog consumes the
Gatekeeper state for its hints and rewards, and the recovered boss victories
advance stamp and Gatekeeper state when combat is started with the current map
environment.

The SDL application materializes every placement with the player's current
difficulty and level, renders its definition's bitmap resource 174–214, and
starts the recovered group setup when the player enters any tile in its
one-tile or 2×2 footprint. It exposes
target cycling, items, spells, trained special abilities, and fleeing for every
group. A victory stores a non-respawning placement's stable index in native
save version 10 so it remains removed after map changes and restart. A
respawning placement is hidden only on the current map instance and returns
after a map reload, matching the `+0x36c` load-time bypass. Version 9 defeats
with this flag migrate to transient state, while versions 2–8 migrate their
historical Giant Beetle bit to placement index 5. Fleeing leaves the
placement active and returns the player to the previous location, including
across map boundaries.

Random roaming encounters follow the movement-time path at `0x00410ed0` and
`0x0041d920`. Each eligible movement attempt compares one random roll with the
saved encounter threshold after applying terrain, map-depth, and critical-health
adjustments. At most one transient roaming monster is then selected through the
map environment's compatibility table and placed on an unoccupied revealed
cell. It pursues the player diagonally on later movement attempts, occasionally
chooses one of eight random neighboring cells, and starts the same size-zero
group combat used by the original `-1` encounter slot. Victory and fleeing both
clear it, while loading another map resets all transient roaming state.

Maps below ID `0x29` begin fully visible. Later maps initialize hidden and use
the original eight-connected room flood from `0x0041cc00`/`0x0041cc50`,
revealing opaque boundary tiles without propagating through them. Visibility is
map-instance state and is not written to native saves.

The map renderer uses terrain bitmap resources 133–147, 153, 377, 418, and 460;
monster resources 174–214; princess resources 391–393; and resource 406 for the
initial type-1 character image.
Tile-to-source-rectangle mappings come from the draw switch at `0x0040cc10`, and
bitmap loading comes from `0x0040b8a0`.
