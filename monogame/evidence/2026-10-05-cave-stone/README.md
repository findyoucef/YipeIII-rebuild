# Cave and mountain stone - 2026-10-05

The user preferred the cave entrance's darker gray and requested that the mountain
wall blend into it. The shared native art import now replaces the mountain's
green-gray fill with the cave's slate `(64,73,87)`, retaining brighter cores of the
original rock clusters. The cave's exterior two-pixel shadow band uses that same
slate. Internal arch shadows, the dark tunnel, threshold and all alpha values stay
unchanged. Shapes and pixel scale are preserved; there is no blur or new art style.

This applies to town, world and area-map rendering, as well as isolated and
connected desert foothills. Interior cave and Hell artwork is unaffected.

Primary references resolved from the surface map: Maynard **E3 / 1,1**, map 17;
**F3 / 7,4**, map 18, the cave in the region containing the reported F3 / 2,6 ridge.

## Comparisons

- [Maynard cave close-up](cave-comparison.png), before left and after right.
- [Full F3 region](f3-comparison.png), before left and after right.
- `after/` contains current GPU map captures for Maynard, F3 and desert F1, plus
  the current native terrain and town atlases.

The before sides reuse the prior style correction's CPU composites; the after
sides are fresh GPU captures. Scaling is nearest-neighbor. The later completion
audit viewed these images directly, together with the original F3 GPU baseline,
current area maps 17/18/23, desert F1 and the village gameplay camera. The cave
comparison has a player overlapping the new entrance; `after/area-017.png` shows
the entrance unobstructed, and `after/yipe-village-focus-4-1.png` shows it in play.
These confirm the connected contours, darker fronts and retained native detail;
the cave rim shares the wall's slate and its opening remains clearly visible.
User feedback can guide further visual refinements.

## Current evidence

The graphics runner successfully created a device in this session. The earlier
OpenGL startup blocker no longer applies. Run:

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true --verbosity quiet
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The checked Release build has zero warnings/errors. **1,203 visual integration
checks pass**, rendering 360 gameplay tour frames in
`build/graphics-checks/yipe-graphics-20261005-223853-8405419a`.
The five added checks cover slate color/brightness, restrained highlights, matching
cave/rock brightness, identical town/world cave art, exterior-only rim changes,
unchanged silhouettes/other sprites, and the dark tunnel. Existing coverage also
passes for all 36 surface mountain layouts, 40,960 matching overlap pixels,
contours, lighter tops/darker fronts, secrets, mutations, fog, effects and saves/RNG.

`pixels.log` records an independent before/after comparison of the actual native
atlases: 93 cave exterior shadow pixels change to slate; all 143 connected dark
tunnel pixels remain identical. Cave alpha and every other sprite cell remain
identical. Terrain changes are confined to its mountain cell, which now contains
557 dark slate, 380 mid-gray and 87 bright highlight pixels.

The isolated runner exited normally. No user game was navigated or restarted,
and no gameplay, collision, encounters, map data or save-format code changed.
Production changes are confined to two small native art processing methods in
`RetroArt.cs`; the existing graphics runner contains the regression coverage.
Historical CPU harnesses import older native art and do not cover this correction.

## Completion audit

| Requirement | Inspected evidence |
| --- | --- |
| Connected irregular mountain masses, with F3 / 2,6 as reference | Original `mountain-form/before/map-018.png`, current `after/area-018.png`, contour and 40,960 overlap-pixel checks. |
| Distinct faces and original pixel-art style | F3, F1, E4 and village captures; native atlas, binary-alpha and top/front contrast checks. |
| Blend cave entrances into darker slate walls | Unobstructed village and F3 area captures, gameplay close-up, five cave/stone checks and native pixel comparison. |
| Routes, secrets and saves preserved | All 36 surface maps' open-cell/blocked-core checks, secret and cache checks, full save/RNG comparisons; production edits are rendering-only. |
| Town, world and map consistency | Shared renderer/import source, identical town/world cave and terrain checks, fog checks and captured area/gameplay views. |
| Durable progress and verification | Updated `PORTING_PLAN.md`, retained build and 1,203-check graphics logs, screenshots and completion checksums. |

The completion audit verified every production/test source and art asset listed
in `source.sha256` against the current worktree. Only its plan-document checksum
differs after later documentation updates. `completion.sha256` records the final
documentation and reviewed evidence alongside the unchanged tested sources.
