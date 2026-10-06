---
name: locate-map-region
description: Resolve Yipe III locations from world-map placement, region labels, local tile coordinates, and landmarks. Use when the user refers to places such as D4 at 5,6, the region east of town, or a monster beside a wall, before diagnosing or editing the area.
---

# Locate a map region

Use the game's map to identify the requested location before drawing conclusions
about its artwork or behavior. Region labels repeat across map layers. Resolve
the user's location independently of the previous topic or currently open scene.

## Resolve the location

1. Separate the **world region**, **map layer**, **local tile**, and **landmark**.
   In `D4 / 5,6`, `D4` names a world region; `5,6` is a tile within it. A monster
   "on the right" can be beside that tile, rather than occupying the tile itself.
2. Read the map mapping below and look up the candidate map IDs. For a relative
   description, inspect the world-map placement and known travel links first.
   Check the referenced region's terrain, events, and neighboring tiles.
3. Inspect an actual map render or matching gameplay capture. Match the described
   wall, road, monster, building, or other landmark. Code coordinates alone do
   not establish that you are looking at the right visual issue.
4. If the layer is unstated, compare candidate maps against the user's landmarks
   and any explicit layer information. Do not silently select Hell because the
   last task involved Hell. Ask one concise clarification only if the available
   maps and context still leave multiple plausible matches.
5. Briefly identify the resolved region, layer and local position in the progress
   update. Use that same location for the reproduction and before/after evidence.

## Coordinate mapping

The current UI has six columns, **A–F left to right**, and six rows,
**1–6 top to bottom**. Let `column` and `row` be their zero-based indices:

```text
cell = row * 6 + column
RegionX = column - 4
RegionY = 2 - row
```

| UI layer | Candidate map ID | D4 example |
| --- | --- | --- |
| Surface | `cell + 1` | 22 |
| Upper caves | `cell + 41` | 62 |
| Deep caves | `cell + 81` | 102 |
| Yipe Hell / alternate map set | `cell + 99` | 120 |

Check that the candidate exists in `MapCatalog`; not every layer/grid cell is a
playable map. The alternate UI layer includes other alternate regions beyond
the Hell biome itself. Hell rendering applies specifically to maps 117–120.
The main Hell entrance is **A4, map 117**. Surface **D4 is map 22**; the reported
monster/wall spacing at `5,6` was there, not in Hell.

Local tiles are **zero-based**: `x = 0..12`, `y = 0..11`; x increases rightward
and y downward. Keep the UI's local coordinates unchanged. Static JSON uses
`tiles_by_x[x][y]`; flat tile/journal arrays use `x * 12 + y`. World terrain tiles
are 32 native pixels, so tile `(x,y)` starts at `(x * 32, y * 32)` before camera
offset and display scaling.

## Repository evidence

These links are relative to this skill; other paths below are repository-relative.
Treat current source as authoritative if the mapping changes:

- [MapJournal.cs](../../../monogame/Yipe.Desktop/MapJournal.cs): `MappedRegion.Layer`,
  `Cell`, `Coordinate`, names, discovered regions, and travel links.
- [TownMapOverlay.cs](../../../monogame/Yipe.Desktop/TownMapOverlay.cs): world-map
  layout, layer labels, area-map display, and the player's local coordinates.
- [WorldLocation.cs](../../../monogame/Yipe.Core/WorldLocation.cs): internal region
  coordinates, depth changes, and edge wrapping. Account for wrapping when the
  user describes traveling east/west/north/south across a world boundary.
- [MapCatalog.cs](../../../monogame/Yipe.Core/MapCatalog.cs) and
  [original-map-tables.json](../../../assets/maps/original-map-tables.json): existing
  map IDs, static terrain, and authored events.
- [MonsterPlacements.cs](../../../monogame/Yipe.Core/MonsterPlacements.cs): static
  encounter positions and two-by-two footprints. Roamers and pursuers may differ
  from those authored positions.
- `build/graphics-checks/yipe-graphics-*/maps/map-NNN.png`: full region renders.
  Choose a verified run matching the current renderer; check its log/source
  evidence rather than assuming any old screenshot represents the current build.
  `monogame/evidence/` contains retained comparisons and contextual gameplay views.

Prefer these read-only sources and isolated rendering fixtures for static-map
inspection. Do not unpause, navigate, reload, or overwrite the user's live game
just to identify a region. Maps **118–120 regenerate**: an arbitrary seed or a
reloaded save may not reproduce the user's current layout. For those maps, obtain
the current visible map/layout or a matching fixture before claiming an exact
reproduction. A discovery journal can be partial or stale; it is not automatically
the complete live map.

Locating an area does not authorize changing its terrain or encounters. Keep the
subsequent explanation or fix within the user's request.
