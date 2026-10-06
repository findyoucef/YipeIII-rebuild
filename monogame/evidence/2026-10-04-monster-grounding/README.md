# Surface D4 monster grounding — 2026-10-04

The user reported awkward monster/wall spacing on **surface D4 (map 22), viewed
from 5,6**. Its three authored monsters at 5,7 / 6,7 / 7,7 had visible feet 10 / 8 /
8 native pixels above the tile bottoms. Their transparent atlas padding was
being positioned as though it were part of the creatures.

`WorldRenderer` now measures each monster sprite's lowest opaque pixel once at
load time, then anchors world monsters to the bottom of their occupied tiles.
A small four-row contact shadow stays within that footprint. Authored monsters,
roamers and pursuers share this drawing path; large monsters retain their two-tile
footprint and existing integer scale. No change to logical positions, encounters,
collision, source sprites or combat portraits. The entrance Gatekeeper retains
its deliberate arch placement.

## Comparisons

- Surface D4 at 5,6: [before](surface-d4-before.png) / [after](surface-d4-after.png).
  Both use scanlines on and Strong miniature.
- Entire D4: [before](map-022-before.png) / [after](map-022-after.png).
- Large monster: [before](large-monster-before.png) / [after](large-monster-after.png).
- Hell entrance: [before](hell-entrance-before.png) / [after](hell-entrance-after.png).

Checked Release build: **zero warnings/errors; 805 graphics checks pass** across
115 maps and ten Hell layouts. The 53 added checks cover complete sprite coverage
and foot alignment for all 44 atlas frames and six large-monster variants, shadow
opacity/containment, visible D4 contact shadows and full-state/RNG preservation
for the D4 frontend capture. Visually inspected D4, a large monster and the Hell
entrance with the existing door fix retained.

89 raw map captures changed; 26 other maps and all five reflection captures are
identical. Sprite atlases and combat screenshots remain byte-identical. All 26
gameplay core files and four personal save/profile files retain their pre-work
hashes. Tests use isolated saves. Comparison hashes and logs are retained here.

Baseline: `build/graphics-checks/yipe-graphics-20261005-024844-28ac19ff`.
Passing run: `build/graphics-checks/yipe-graphics-20261005-025103-b10bd44d`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Simplicity review: one production file, one cached integer per atlas frame and one
shared world-drawing helper replace the three fixed-offset call sites. No gameplay
or asset changes, per-frame texture reads, or extra rendering passes. No remaining
findings in the task diff.

The MonoGame application was closed during work; the verified build was reopened.
The separate C++ reference app was left alone. User acceptance is pending. Resume
other port work from `PORTING_PLAN.md`.
