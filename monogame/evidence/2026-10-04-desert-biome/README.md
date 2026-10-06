# Natural desert terrain and wind — 2026-10-04

Implemented the requested softer desert boundaries and ambient biome effects.
The previously corrected corner directions remain the starting footprint;
the presentation now rounds and weathers that footprint across the entire map.

- Sand patches, long borders and concave turns have gentle contours and a
  scattered dry-grass fringe instead of hard straight cuts. A cached texture
  reuses the existing sand artwork and palette. Both play and area maps use it.
- Faint warm dust puffs and trailing motes drift over clear sand, below actors.
- Up to two tiny tumbleweeds roll, rotate and bounce along continuous sand lanes,
  with quiet intervals between gusts. Lanes exclude water, plants, buildings and
  fixed props, and include the complete sprite/shadow footprint.
- Palm crowns and dry bushes sway by at most one pixel while their roots remain
  fixed. Existing sprite artwork is reused.

Wind advances at eight Hz and vegetation at six Hz, driven only by scene time.
Pausing and map/menu overlays freeze it. The map omits airborne weather for
readability. Effects do not consume gameplay RNG, change collision, create
persistent gameplay entities or alter the save format. No bitmap assets changed.

## Visual evidence

`map-001/002/005/008/014/030-before.png` and matching `-after.png` show large desert,
islands, lake boundaries, settlements, concave turns and a small isolated patch.
Four `wind-*.png` frames sample the moving effects at two-second intervals.
[Eight-second animation](desert-wind-preview.mp4) shows a 16:9 crop of the native
world render, enlarged with point sampling. This diagnostic preview omits the
normal miniature/scanline post-processing so the animation can be inspected.

The preview is encoded from 96 real renderer frames at 12 fps; no gameplay state
was advanced between them. The complete disposable frames are under the run's
`desert-wind/` directory. The actor's diagnostic position is not an earned route.

## Verification

- Checked Release build: **zero warnings/errors** (`build.log`).
- **657 graphics checks**, all 115 maps and 360 tour frames (`graphics.log`).
- Nine new checks verify fixed-time weather, drift/dust, clipping, rolling weed
  silhouettes, full gameplay snapshot/RNG isolation, stable terrain, biome scope,
  cache invalidation after same-map tile changes, and anchored palm roots.
- Exactly **18 desert map captures** changed; **187** other comparable captures
  are byte-identical (`capture-comparison.json`). Village, cave and UI captures
  retain their previous output. Extra animation frames have no baseline match.
- All **26 gameplay-core source files** retain their prior hashes
  (`unchanged-core.json`); all **four personal save/profile files** are unchanged
  (`user-save-preservation.json`). Test saves are isolated.
- Before/after terrain and sampled animation frames were visually inspected.
  Hardware-specific performance review remains P5/P6; this is desktop evidence.

Run: `build/graphics-checks/yipe-graphics-20261004-180001-5da11dcf`.
Baseline: `build/graphics-checks/yipe-graphics-20261004-173709-a98c14f1`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The implementation follows the existing `WorldWater` cache and scene-time
rendering approach. `WorldDesert` contains only the terrain cache, clear-sand
mask and bounded ambient drawing. There is no particle framework, timer service,
new simulation state or external asset dependency. The final simplicity review
found no remaining actionable issues in the production diff. Source fingerprints
are in `source-inputs.json`.

Feature complete. Resume the remaining P5/P6 checklist in `monogame/PORTING_PLAN.md`.
