# Hell path readability and connected basalt walls — 2026-10-04

The user rejected the previous smaller-rock/dark-floor version: the wall looked
out of place and the paving was difficult to see. This revision supersedes that
presentation; the prior implementation is not an approved visual baseline.

## Changes

- Restored a moderate charcoal floor (base RGB 43/42/49), with restrained ember
  fissures instead of bright outlines around almost-black stone plates.
- Rebuilt the Gatekeeper path with brighter cool-gray paving, staggered joints,
  edge stones and subtle top highlights. It draws after the floor cracks. A local
  paving mask reduces warm spill to 18% over the route, keeping its contrast.
- Replaced all repeated freestanding wall rocks with connected basalt terrain
  from the existing volcanic wall-top tile in `World/terrain.png` (cell 5).
  Shared edges stay joined; exposed edges are irregular and beveled, and south
  faces are shaded. Lava animation follows the red fissures in this artwork.
- Kept the original fire-wall cells, hazard flames, landmarks, door behavior,
  Gatekeeper, haze, embers and heat distortion. The corrected A4 arch boundary
  remains. No gameplay or collision edits.
- Removed the unused fractional sprite-size machinery. No bitmap edits, new
  assets, textures, shaders or persistent renderer state. A temporary paving mask
  is compiled with the existing map cache.

## Visual review

- [Path with default settings](path-default-settings.png): scanlines on and
  Strong miniature at 1280×720.
- [A4 entrance with default settings](a4-default-settings.png).
- [Path before](path-before.png) / [after](path-after.png): matching diagnostic
  camera, time and settings, scanlines off.
- [Generated wall before](generated-wall-before.png) /
  [after](generated-wall-after.png).
- [Six-second animation](hell-readable-preview.mp4): 144 frames at 24 fps,
  Strong miniature and diagnostic scanlines off.
- Before/after raw captures of all four Hell regions.

The frontend check fixture now retains both default-settings views on every
run. Review covered the path, entrance, all four regions and alternate generated
layouts. These are isolated diagnostic scenes, not edits to the user's adventure.

## Validation

Checked Release build: zero warnings/errors. All **743 existing graphics checks**
pass, including ten Hell layouts, lava containment, fog, doors, animated/frozen
frames, UI composition and gameplay/RNG isolation. Only the four Hell map
captures changed; 111 other maps and five reflection captures are identical.
All 26 core files and four personal save/profile files retained their pre-work
hashes. Tests use isolated saves. Logs and fingerprints are retained here.

Simplicity review covered the complete renderer and capture-fixture diff. The
existing terrain asset, cache, clock and visibility contracts are reused; no new
abstractions were added. The new mask is local to cache compilation and preserves
the path during the later light pass.

Run: `build/graphics-checks/yipe-graphics-20261005-005813-2d45b546` (UTC).
Baseline: `build/graphics-checks/yipe-graphics-20261005-004607-224e459f`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The prior game session closed during work; the revised build was launched.
User acceptance of this new presentation is pending. Resume other port work
from `PORTING_PLAN.md`.
