# Mountain border artwork correction — 2026-10-04

The MonoGame renderer incorrectly interpreted terrain IDs `0x65`–`0x68` as
purple directional arrows. The original `app/main.cpp::tile_graphic` maps them
to the four cells of bitmap 460. Inspection of that bitmap shows mountain feet
on the **right, bottom, left, top**, respectively. Original Other-action message
1035 also describes the bottom of a mountain and scattered pebbles. The earlier
rendering coverage checks ensured these tiles drew artwork but did not validate
its meaning against the original bitmap.

Replaced `DrawArrow` with `DrawMountainFoot` in the shared ground renderer.
Narrow, irregular strips reuse the neighboring mountain texture, with subdued
earth shading and three loose stones. Most of each tile remains open sand,
matching its existing walkable behavior. Both live play and the area map use
this renderer. No assets, map data, gameplay rules or save formats changed.

All **35 occurrences across six regions** are covered: maps 1 (8), 6 (9),
7 (2), 12 (12), 13 (2) and 21 (2). Inspected before/after captures for every
affected region, including isolated rocks, long walls, mixed biomes and map
boundaries. The original source bitmap is retained alongside these captures.

- Checked Release build: zero warnings/errors.
- All **657 existing graphics checks** pass, including all 115 rendered maps.
- Exactly the six affected map captures changed; **199 other captures** are
  byte-identical to the desert-biome baseline. The separate desert-wind animation
  frames were excluded from this static comparison because they show map 1.
- Simplicity review: one production file changed; existing texture, palette,
  ground-drawing path and sprite batching reused. No new cache or asset loader.

Current run: `build/graphics-checks/yipe-graphics-20261004-181347-429970ce`.
Baseline: `build/graphics-checks/yipe-graphics-20261004-180001-5da11dcf`.
Build/check logs, capture comparison and source fingerprints are retained here.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Correction complete. Continue the remaining P5/P6 work in `PORTING_PLAN.md`.
