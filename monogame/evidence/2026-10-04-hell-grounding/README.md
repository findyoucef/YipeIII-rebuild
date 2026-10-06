# Hell terrain grounding correction — 2026-10-04

The user found the new rocks and fire-wall areas disconnected from the floor.
The previous renderer changed the floor abruptly to a black rectangle at every
fire-wall tile, then aligned all rock bases to the bottom of that rectangle.

`WorldHell.cs` now carries the same cracked stone underneath both areas. A
continuous, weathered soot fringe crosses the terrain boundary, with angular
loose stones. Molten pockets have wider irregular banks; cooling crust uses the
underlying ground color and lava uses the rocks' warmer orange/brown palette.
Rock positions vary slightly within their cells. Small contact shadows and
overlapping chips settle their feet into the ground. Existing art is unchanged.

This is a localized renderer correction: no additional assets, persistent state,
render targets or abstractions. Terrain compilation reuses the existing map
cache. Contact shadows use 21 short scanline draws per prop, rather than
individual pixel draws. Original collision, damage and all gameplay stay intact.

## Verification

- Checked Release build: zero warnings/errors.
- All **743 existing graphics checks** pass, including ten Hell layouts,
  lava containment, fog masking, door updates, animation/pause and state/RNG
  preservation. No new tests were needed for this visual adjustment.
- Only the four Hell map captures changed. The 111 other map renders and five
  reflection captures in the same directory remain identical to the previous run.
- All 26 gameplay core files and four personal save/profile files retain their
  pre-work hashes. The diagnostic runner uses isolated saves.
- Inspected all four Hell regions, alternate generated layouts, the actual
  16:9 frontend and the Gatekeeper entrance. Before/after images, logs and hash
  comparisons are retained here. These are diagnostic scenes, not earned routes.
- Simplicity review: production changes are confined to the existing terrain,
  sprite and placement methods in `WorldHell.cs`; no new types or state fields.

[Before](hell-play-before.png) · [After](hell-play-after.png)

Run: `build/graphics-checks/yipe-graphics-20261005-002127-16737949` (UTC).
Baseline: `build/graphics-checks/yipe-graphics-20261005-000339-c19d2cbd`.
The run also retains animation frames and generated seeds 1, 7 and 99.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Correction complete. Resume the remaining P5/P6 work from `PORTING_PLAN.md`;
the prior six-part Hell goal is still complete. User acceptance of this visual
revision is pending.
