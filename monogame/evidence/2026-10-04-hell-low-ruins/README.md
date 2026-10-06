# Hell A4 monster clearance — 2026-10-04

Resolved the reported **A4 / 6,7** through the world-map mapping, candidate
layer renders and the paused live view: this is **Yipe Hell, map 117**. The
monsters at 4,7 and 8,7 were being covered by decorative pillars drawn from
the wall cells immediately below them, at 4,8 and 8,8.

Replaced those two pillars with low, uneven pieces of broken masonry contained
inside their wall tiles. Both monsters now keep their complete silhouettes and
visible feet. Logical monster positions, encounters, collision, wall boundaries,
flames, the approved red floor and the central path are unchanged.

- View from 6,7 with scanlines on and Strong miniature:
  [before](a4-6-7-before.png) / [after](a4-6-7-after.png).
- Entire region: [before](map-117-before.png) / [after](map-117-after.png).

Checked Release build: **zero warnings/errors; 809 graphics checks pass** across
115 maps and ten Hell layouts, with 360 tour frames. Four new regression checks
verify each monster's opaque pixels survive the complete scene render and each
ruin stays below the neighboring monster's tile. The exact 6,7 frontend capture
uses an isolated, fully revealed fixture; it does not navigate the live game.
Both the full region and the default-effects gameplay view were visually reviewed.

Of 621 paired captures, only 151 Hell A4 captures changed (including 144 animation
frames). Map 117 is the only changed full-map image; the other 114 maps and five
reflection captures remain byte-identical. All 26 core source files and four
personal save/profile files retain their pre-work hashes. Logs, source hashes
and capture comparisons are retained here.

Baseline: `build/graphics-checks/yipe-graphics-20261005-034927-4660e757`.
Passing run: `build/graphics-checks/yipe-graphics-20261005-035227-36dd249a`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Simplicity review: one localized production change in `DrawHellScenery`, removing
the old pillar-only debris code from `DrawHellSprite`. No new assets, state,
helpers or rendering passes. No remaining findings in the task diff.

The user's existing MonoGame process was left open with its current progress.
Save and restart that application to load the verified build. User acceptance
is pending. Resume other port work from `PORTING_PLAN.md`.
