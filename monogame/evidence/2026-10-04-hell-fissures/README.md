# Smaller Hell formations and glowing fissures — 2026-10-04

Following feedback about gaps and oversized formations, ordinary fire-wall cells
now use the two low rock silhouettes at **62.5%** of their previous dimensions.
Every ordinary fire-wall cell gets a formation; the prior deterministic gaps
and the special A4 end-cap exception are removed. Tall columns no longer repeat
through ordinary walls. The Gatekeeper's pillars, doors and other landmarks keep
their existing size. Sprite positions snap to native pixels, and contact shadows
and loose chips scale with the rocks.

All authored fire-wall cells share a connected, irregular-edged bed of warm
crust with animated thin molten veins and occasional pools. Smaller flames
identify the hazard. The recessed arch retains its rows 0–1 foundation; the
original collision map, damage, path widths and door rules are unchanged.

The floor base changed from RGB 52/52/61 to 24/23/29, with dimmer paving and grain.
Warped stone fractures have thin orange cores, uneven heat and a soft local
halo. The halo reuses the existing cached firelight texture and its scene-clock
flicker. Broad lava ribbons were replaced by fine veins to keep the darker stone
dominant. Floor fissures are visual details, not new damaging terrain.

No source bitmap was edited or generated. Production changes are confined to
`WorldHell.cs`; no new textures, fields, render passes, shaders or state models
were introduced. The existing map-cache, clock, fog and sprite contracts are
reused. The complete diff was reviewed for simplicity and unused branches.

## Visual evidence

- [Six-second animated preview](hell-fissures-preview.mp4), 1280×720 at 24 fps,
  Strong miniature, diagnostic scanlines disabled.
- [A4 / 6,2 before](a4-6-2-before.png) and [after](a4-6-2-after.png).
- [Play before](hell-play-before.png) and [after](hell-play-after.png).
- [Generated Hell](generated-hell-play.png).
- Before/after raw captures of maps 117–120.

These use matching isolated diagnostic scenes, not the user's adventure. All
four regions, the A4 approach and generated layouts were visually reviewed.
Animation frames and all ten Hell layouts remain in the full run directory.

## Validation

- Checked Release build: **zero warnings/errors**.
- **743 existing graphics checks pass**, including ten Hell layouts, scene-time
  animation/pause, lava containment, fog, cache invalidation, doors, Gatekeeper
  visibility, sharp UI and full gameplay/RNG preservation.
- Only four Hell map captures changed; 111 other maps and all five reflection
  captures remain identical to the baseline.
- All 26 core files and four personal save/profile files retained their pre-work
  hashes. Checks use isolated saves. Logs and hashes are retained here.

Final run: `build/graphics-checks/yipe-graphics-20261005-004607-224e459f` (UTC).
Baseline: `build/graphics-checks/yipe-graphics-20261005-003412-819739e2`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The previous game session had closed by completion, so the updated build was
launched. User visual acceptance is pending. Other port work resumes from
`PORTING_PLAN.md`.
