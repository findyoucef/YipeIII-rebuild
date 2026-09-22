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

The version-4 catalog also extracts the compact event records initialized at
`0x0041ec40`: 20 location messages, 10 overlay triggers, 20 princesses, 26 depth
portals, eight first linked records, six second linked records, 21 fixed
map-authored pursuers, and one level-dependent pursuer. The extractor validates
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
render the three runtime-generated maps. All six extracted event-record fields
have a movement or interaction path. The ten overlay records allow movement
through their underlying `0x0d` tile, but the overlay is not yet drawn and the
original's post-combat overlay relocation remains follow-up work.

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
| `0x16` | Resolve the Yipe Hell Gatekeeper when present, then return to the starting surface location |
| `0x17` | Two-roll, nonlethal fire-wall damage |

The SDL dispatcher exposes the recovered entrance identities and implements the
restoration, depth, surface-return, key-message, fire-wall, and King-dialog effects.
It also displays the 20 placed location-message events using the exact 21-string
resource range 651–671 (one recovered string is not placed by the static catalog).
Ctrl+R rereads a placed message when the player is standing on its tile; it
does nothing away from a sign. The accelerator is present in menu resource 128.
Combat defeat selects one of resources 701–720, increments the death count,
leaves the player at one HP, and relocates to map `0x75` at `(6,10)`. Dialog 387
offers the Gatekeeper's difficulty-based fee or a type-41 fight. Payment fully
heals the player; victory permanently removes this Gatekeeper; either result
opens the tile-`0x55` exit on the next attempt and returns to map `0x11` at
`(4,5)`.
The King selector preserves all 34 message/footer pairs, greeting time-of-day
handling, quest flags, hint rolls, and gold/equipment/Lucky Rabbit Foot rewards.
The 20 princess records retain position, one of three bitmap variants, and their
one-time rescue index. The SDL view renders bitmaps 391–393 and removes a rescued
princess on later map loads. Dialog 394 offers Gold, Experience, Kiss, and Mystery;
the core preserves all formulas, healing/stat/item outcomes, two-roll Mystery
selection, its high-threshold Rainbow Key override, and the serialized counter at
player offset `+0x682`. Returning to map `0x11` after the keyed Gatekeeper victory
with exactly 20 positive signed rescue values across all 30 slots runs the
reconstructed ending-state transition (`0x0040e2d0` / `0x00446c10`). The shared
`Player::rescued_princess_count` supplies this count, Special statistics, and
high scores. Zero and negative signed slot values do not count, and more than
20 does not satisfy the original equality check. The King's `princesses_found`
quest counter remains separate.

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
door and linked-mechanism tiles; current version 16 retains it, the recovered
King quest state, the princess Mystery counter, permanent monster defeats, the
saved surface-return region, encounter history, death/Hell recovery state, and
Other-action treasure/water/snow state.

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

## Other action

The tile switch at `0x00416440` is exposed through Ctrl+Shift+O in exploration, without
changing the 13 recovered key bindings. It does not advance roaming or pursuing
monsters. Resource IDs and random-call order are preserved; SDL message boxes
adapt the original message area. Water, stone-circle, mountain, and coin-pile
movement hints identify the new control. The complete map-specific hint selector
is documented below.

| Tile / map | Effect |
| --- | --- |
| `0x0b` / `0x21` | Once-only 600 gold and 20–29 experience from the bridge |
| `0x26` / `6` | Heal `level + rand()%10`, plus intelligence with the bonus-healing ring; roll dryness first against `difficulty + 40` percent |
| `0x26` / other maps | Drinking message and sound, without healing |
| `0x35` / `0x12` | Lower mountain tiles `(3,7)` and `(4,7)` to `0x46` for the current map instance |
| `0x42` | Queue relocation 3: secret Hell entry at `(6,10)`, map `0x75`, without death or HP changes |
| `0x46` / `3` | Queue relocation 4: map `0x15`, position `(6,8)`, region `(-2,-1)`, surface and ordinary map set |
| `0x47` / `0x45` | Once-only 180 gold from the cave floor |
| `0x69` | First search gives 401–410 gold, second 101–110; later searches alternate a warning and nonlethal 5–19 damage |

Remaining tile branches provide terrain descriptions; the bee branch on tile
`0x34` consumes one roll only above three HP, stings for two HP on rolls 80–99,
and never kills. The water hole reports dry at full HP without actually consuming
its availability or a random roll. `0x0040de80` refills the healing-water flag
on nonzero map loads and enables the stone-circle tile `(4,7)` after five stamps.
Secret entry shares only the Hell destination/Gatekeeper setup with combat death.

The mountain action's player `+0x06c` flag requests a redraw, which
`0x0040d610` clears; it is not a permanent unlock. The mountain opening is not
added to native map-tile changes, so reloading restores the original mountains.
Bridge treasure, cave coins, and snow search progress are player state and do
survive loading. Native version 13 also stores water availability, although the
subsequent map initialization refills it just as ordinary map entry does.

## Movement hints and entry-time terrain

The action-3 movement branch at `0x0040e880` handles water holes, climbable
trees (including the existing secret-tree puzzle), and the Hell tunnel before
selecting a map-specific hint through `0x004121c0`. SDL retains this precedence
and appends the Ctrl+Shift+O control to the three hints that suggest using Other.

| Map | String resource | Hint / condition |
| --- | --- | --- |
| `3` | 641 | Glowing stones |
| `0x12` | 639 | Strange flowers and mountains |
| `0x2c` | 635 | A space where a wall once stood |
| `0x2d` | 636 | A pushable wall, with insufficient-strength text |
| `0x39` | 631 | A crack to the north |
| `0x45` | 632 | A coin pile, only before collection |
| `0x48` | 633 | Dungeon danger, only at level 10 or below |
| `0x4a` | 634 | A faint cry, only while rescue slot 11 is zero |
| `0x57` | 637 | Six tally marks |
| `0x5a` | 640 | A wall message about a group of stones |
| `0x6d` | 638 | A giant monster, only before a Wormoid defeat |

The last condition intentionally follows the original's defeat-counter index
34, corresponding to monster type 35 (Wormoid), not type 40 (Earth Giant).
`has_defeated_wormoid` records the zero/nonzero condition when an actual kill
is processed through ordinary attacks, Backstab, or damaging spells. It is
independent of encounter history and full-group victory; Frighten does not
record a kill. Ordinary attacks that end in a fatal counterattack return before
processing that pending kill, as in `0x00403a80`/`0x00405040`.

After loading terrain, `0x0040de80` applies these entry-time rules through the
same initialization path for starting/resumed games, transitions, relocations,
combat recovery, and flee restoration:

| Condition | Effect |
| --- | --- |
| Map `0x2d`, strength at least 40 | Change `(11,9)` from hint tile `0x49` to linked-wall action `0x5f` |
| Map `0x75`, global quest Gatekeeper state at least 1 | Open `(12,1)` with tile `0x27`; local Hell payment/defeat is not this condition |
| Map `3`, at least five stamps | Enable the stone circle at `(4,7)` with tile `0x46` |
| Occupied entry tile is `0x25` | Replace only that cell with floor `0x53` |
| Nonzero map ID | Refill healing-water availability |

Gatekeeper/stamp thresholds retain the original signed 16-bit comparisons.
The pushable-wall text itself has no strength condition: the entry patch changes
its movement action, so a strong player activates the linked wall instead of
seeing that hint. These initialization patches do not add persistent tile-change
records; linked mechanisms continue to use their existing persistence path.
The earlier entry checks in `0x0040de80` are registration checks, not additional
gameplay prerequisites. They cover descending from maps `0x0d` and `4` (action
`0x0e`), deeper entry from maps `0x42` and `0x2a` (action `0x10`), and descending
from map `0x15` through `0x00412f30`. The helpers call the original application's
registration/profile routines, with a registration dialog on failure. This
checkpoint only identifies those checks: it does not alter the original,
reproduce registration, or change the rebuild's existing content access.
Registration policy remains an explicit open difference, not a missing strength,
stamp, key, or princess gate.

## Defeat statistics

The original player has 50 signed 32-bit counters at `+0x584`, indexed by monster
type minus one. Combat writes are distinct from encounter-history flags:

| Event | Counter behavior |
| --- | --- |
| Process a defeated target other than type 40/41 (`0x00405040`) | Increment that target's counter immediately |
| Finish an Earth Giant or Gatekeeper primary group (`0x00405210`) | Increment the primary type's counter once, after all opponents are gone |
| Frighten removes the third opponent (`0x00406fe0`) | Do not count the removed opponent; a resulting group victory still counts the primary boss |
| Flee after processing a kill | Retain that kill, without counting surviving opponents |
| Fatal ordinary counterattack | Return before processing the pending target kill |

Attacks, Backstab, and damaging spells share the defeated-target path. Ordinary
and Frighten-ending victories now share completion handling, including the
previously omitted stamp/global-Gatekeeper updates after a final Frighten.
Counter increments and aggregate sums preserve the original 32-bit wrapping
without C++ signed-overflow behavior. `quest_progress_total` advances with each
recorded kill, retaining any unattributed aggregate loaded from an older native
save; the King's greater-than-50 equipment hint is now reachable through new
combat progress. The Wormoid hint predicate stays synchronized with index 34,
while retaining the historical version-14 flag when counts are unavailable.

Ctrl+T's Kills view adapts `0x0044ca90`/`0x0044cb20`/`0x0044cdc0`: five pages of
nine rows, with five rows on the last page, display the 41 known monster types
and the death count. The dialog's total sums only indices 0–40; the King's
aggregate sums all 50. Reserved entries 41–49 survive import and native saves
but are not displayed. Names are revealed only for positive counts, not merely
encountered monsters. This contradicts help string 806; the implemented rule
follows the dialog's machine code. Previous/Next navigation does not wrap, and
opening status ignores repeated keydown events. Older native saves visibly
mark the displayed counters as partial; new games and legacy imports have the
complete table. High scores and the remaining statistics panels are separate
follow-up work.

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
saves so it remains removed after map changes and restart. A
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

Combat construction at `0x004025e0` records every opponent type in the original
50-byte history array at `+0x552`. Roaming selection reads entries 36–40 to
expand the eligible type range for environments 2–6 after those monster types
have appeared. The rebuild records the same three combat slots, imports entries
0–41 from legacy saves, and persists them in native save version 11. The two
registration checks used to cap the default environment at types 20, 24, or 33
remain represented by the explicit roaming-access input; the initial rebuild
scope enables the full supplied content.

The separate map-authored pursuer at offsets `+0x394`–`+0x3a4` is initialized
by `0x0041d840`. Twenty-one maps use fixed monster types and origins; map
`0x10` selects type 1 below level 6 and type 6 thereafter. On each eligible
movement turn, `0x00411b10` moves a visible pursuer randomly 55 percent of the
time and otherwise steps directly toward the player. It makes at most five
attempts, remains within two tiles of its original position, and avoids static
placements and the random roaming monster. Contact starts the same size-zero
group combat through the original `-2` encounter slot. Victory removes the
pursuer for the current map instance, fleeing leaves it in place, and loading
the map again restores it at its origin. The SDL renderer and movement loop
preserve that lifecycle.

Maps below ID `0x29` begin fully visible. Later maps initialize hidden and use
the original eight-connected room flood from `0x0041cc00`/`0x0041cc50`,
revealing opaque boundary tiles without propagating through them. Visibility is
map-instance state and is not written to native saves.

The map renderer uses terrain bitmap resources 133–147, 153, 377, 418, and 460;
monster resources 174–214; princess resources 391–393; and resource 406 for the
initial type-1 character image.
Tile-to-source-rectangle mappings come from the draw switch at `0x0040cc10`, and
bitmap loading comes from `0x0040b8a0`.
