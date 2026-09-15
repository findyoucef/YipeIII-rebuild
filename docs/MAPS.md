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

The extractor requires every loader to produce all 156 values and stops on an
incomplete table. Map IDs `0x76` through `0x78` are generated at runtime by
`0x0041e0f0` instead of appearing in the static dispatcher, so they are outside
this catalog. The generated C++ copy lets the core load those same tables without
a JSON dependency at runtime.

Surface-map transitions use player offsets `+0x46` and `+0x48` as wrapped region
coordinates. `0x00444b50` moves a player arriving at an outer border back to the
opposite inner border, and `0x00444ad0` calculates the next map as
`region_x - 6 * region_y + 0x11` for layer zero. The core preserves the original
region-coordinate wrapping and the additional layer and alternate-map offsets.
The SDL application follows these exits and can render every static table. Its
headless smoke test renders all 112 maps, exercising the recovered tile-to-bitmap
switch and every required terrain sheet. Runtime-generated maps and the dynamic
monster-overlay tiles remain separate follow-up work.

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
ordinary walkable tiles. Special result codes are available to the core but their
area transitions and dialogs are still being connected.

The central tile with ID `1` returns action code `8`. The action dispatcher at
`0x0040e880` opens miscellaneous-shop dialog resource 213. Because the current
map ID is `0x11`, `0x004402b0` selects the title resource “Fred's Miscellaneous
Shop,” stock builder `0x00440e50`, and the map-specific price slice in
`0x00441010`. The SDL build now displays all 11 recovered stock rows and prices
when the player enters that tile. Purchase and equipment mutation remain part of
the economy work in P6.

The start-area renderer currently needs bitmap resources 133, 134, 137, 139,
141, 142, 377, and 418. Resource 406 is the initial type-1 character image.
Tile-to-source-rectangle mappings come from the draw switch at `0x0040cc10`, and
bitmap loading comes from `0x0040b8a0`.
