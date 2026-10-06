# Earth Cave contrast and retro correction — 2026-10-04

The user rejected the initial Earth Cave palette: walls and ground lacked
contrast, and the smooth shading lost the game's retro character. This correction
applies to all seven Earth regions: upper E3–E6 and deep E4–E6.

`WorldEarth` now uses explicit shared-palette colors for the terrain, brighter
ochre rock facets, crisp light rims, dark wall outlines and stepped front-face
shading. Walkable earth uses a separate darker ramp with small gravel clusters.
Limited dithering joins floor patches and represents local lighting; the previous
continuous brown blends and smooth wall bands are removed. Sediment remains a
distinctive feature of upper E4. Embedded details and drip pools are also mapped
to the existing 40-color palette. Ambient animation and the approved screen
effects still run normally.

The seven room identities, rounded wall geometry, roots, fossils, masonry,
mineral seams, dust, drips and fungi remain. No terrain, encounters, movement,
visibility rules, secrets, gameplay RNG or save formats changed.

## Before and after

All gameplay comparisons use scanlines on and Strong miniature. All seven final
gameplay captures were visually reviewed. Native full-map comparisons are also
retained as `map-ID-before.png` and `map-ID-after.png`.

| Region | Before | After |
| --- | --- | --- |
| Upper E3 | [Before](earth-57-before.png) | [After](earth-57-after.png) |
| Upper E4 | [Before](earth-63-before.png) | [After](earth-63-after.png) |
| Upper E5 | [Before](earth-69-before.png) | [After](earth-69-after.png) |
| Upper E6 | [Before](earth-75-before.png) | [After](earth-75-after.png) |
| Deep E4 | [Before](earth-103-before.png) | [After](earth-103-after.png) |
| Deep E5 | [Before](earth-109-before.png) | [After](earth-109-after.png) |
| Deep E6 | [Before](earth-115-before.png) | [After](earth-115-after.png) |

## Verification

Checked Release: **zero warnings/errors; 897 graphics checks pass** across all
115 maps and ten Hell layouts, with 360 tour frames. Fourteen new assertions
verify shared-palette membership for all static terrain/detail pixels and wall
top/floor brightness separation in every Earth region. Measured separation is
48.3–62.7 on the weighted 0–255 RGB scale, against a minimum of 30. Existing
secret-passage, fog, pause, animation, cache invalidation and full-state/RNG
checks remain passing.

Of 684 paired captures, 72 Earth Cave images changed. Only the seven Earth full
maps changed; the other 108 maps and five reflection captures are byte-identical.
All 26 core source files and the personal adventure, journal and preview saves
retain their pre-work hashes. The user's live game stayed open and its profile
changed during work; that file is not claimed identical or restored. Graphics
tests use isolated save files. Hashes and logs are retained here.

Baseline: `build/graphics-checks/yipe-graphics-20261005-043518-0594a6df`.
Final run: `build/graphics-checks/yipe-graphics-20261005-044628-1a27d151`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Simplicity review: one production file changes the existing cached terrain
painting. No new fields, helper types, assets or rendering passes. Palette mapping
uses the existing detail-pixel function and atlas color-distance convention.
No remaining findings in the task diff.

The previous game process closed during verification. The updated build was
then opened for testing. User visual acceptance is pending; other port work
resumes at P5.6.
