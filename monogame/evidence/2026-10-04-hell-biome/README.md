# Complete Hell biome overhaul — 2026-10-04

Completed all six accepted improvements for Hell maps 117–120, including the
generated caverns. This is presentation work; original layouts, fire-wall damage,
door/key rules, encounters, discoveries and saves are preserved.

| Recommendation | Implemented behavior |
| --- | --- |
| Charred formations | Three basalt/crag silhouettes, ruined pillars, charcoal ground with irregular stone fractures, and worn paving through the Gatekeeper hall. |
| Molten fissures/pockets | Eight cached frames of irregular flowing lava and cooling crust, with brief expanding bubbles. All molten pixels remain inside existing `0x24` fire-wall cells. |
| Firelight | A cached distance field spreads restrained orange light onto nearby floor; brightness fluctuates gently and rocks receive warm flicker. |
| Embers/ash/smoke | Sparse rising sparks, drifting ash and faint expanding smoke wisps. Visibility masking prevents any effect revealing unknown cells. |
| Landmarks | Recessed ruined arch at the northern exit, heavy iron doors, paired pillars, chain stakes, coal braziers and scorched paving. Gatekeeper drawing follows the arch so his face stays visible. |
| Varied fire | Small vent flames and larger clusters have different silhouettes, local phases and eight-Hz animation. Every original fire-wall cell retains an obvious hot hazard. |

`WorldHell.cs` follows the existing renderer-partial/cache pattern. Ground, lava
and light are compiled when the map instance or tiles change; animation uses the
existing scene clock. Particle effects are bounded drawing calculations, with
no gameplay RNG or particle simulation. Atmosphere draws below actors and is
omitted from area maps. The existing heat shimmer, sharp UI, scanlines and
miniature modes compose with the new world art. Pause freezes all effects.

## Visual evidence

- [Six-second animated preview](hell-biome-preview.mp4): actual frontend output,
  24 fps, Strong miniature with scanlines disabled for inspection.
- `map-117/118/119/120-before.png` and `-after.png`: all four Hell layouts.
- `gatekeeper-entrance.png`, `gatekeeper-waiting.png`, `hell-play.png` and
  `area-map.png`: entrance readability, live scenery and map presentation.
- `map-*-seed-*.png`: nine generated layouts across three map IDs and seeds
  1, 7 and 99, plus the authored Gatekeeper region.
- `native-hell-atlas.png`: compiled production sprites.
- Four sampled preview frames retain the lighting, flame, lava and air movement.

Scenes are isolated diagnostic fixtures, not an earned campaign continuation.
Before/after regions, generated layouts, the entrance and sampled animation
frames were visually inspected. Original and corrected arch placement were
compared to ensure the Gatekeeper's body and face remain visible.

## Verification

- Checked Release build: **zero warnings/errors**.
- **743 graphics checks** pass, including **58** new Hell checks: atlas integrity,
  ten layouts, visibility, fixed-clock repeatability, animated output, full-state
  isolation, eight-frame lava bounds, firelight, sparse atmosphere, non-Hell scope,
  opened doors, changed-tile cache clearing, visible Gatekeeper and independently
  phased flame silhouettes. Existing heat, pause and UI checks also pass.
- All 115 maps render. All **111 non-Hell raw map captures** are byte-identical.
  Across 215 comparable existing static captures, **10** Hell-related captures
  changed and **205** remain byte-identical. Animation frame sequences are
  excluded from this static comparison.
- All **26 gameplay core source files** and **four personal save/profile files**
  retain their pre-work hashes. Tests use isolated saves; save formats are unchanged.
- Simplicity review covered the complete production change and new checks.
  Existing map/visibility/clock/asset-loading/batching contracts are reused;
  there is one Hell renderer partial, three cached terrain textures and one
  sprite atlas. No new gameplay state, engine abstraction or particle framework.
  No actionable findings remain in the reviewed scope.

Current run: `build/graphics-checks/yipe-graphics-20261005-000339-c19d2cbd` (UTC).
Baseline: `build/graphics-checks/yipe-graphics-20261004-183922-957a5f75`.
Logs, source fingerprints and capture/save comparisons are retained here.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

## Asset provenance

The built-in imagegen tool generated the new transparent
[hell.png](../../Yipe.Desktop/Content/Art/Retro/World/hell.png) atlas. The complete
[production prompt and cell order](../../Yipe.Desktop/Content/Art/Retro/World/HELL_ART.md)
are retained beside it. Existing assets were not replaced. Palette compilation
and measured sprite foot anchors preserve the established native pixel scale.

All six recommendations are complete. Resume the remaining P5/P6 checklist in
`PORTING_PLAN.md`; platform/hardware verification remains its existing separate work.
