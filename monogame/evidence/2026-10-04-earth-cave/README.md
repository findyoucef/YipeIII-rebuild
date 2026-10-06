# Earth Cave visual goal — complete, 2026-10-04

The user subsequently rejected this version's muted contrast and smooth shading.
The [contrast and retro correction](../2026-10-04-earth-contrast/README.md) supersedes
its palette and terrain shading while retaining the six features below.

Implemented all six approved changes across **upper E3–E6** (maps 57, 63, 69,
75) and **deep E4–E6** (maps 103, 109, 115). Locations were resolved from the
world-map mapping, environment IDs and actual region renders before editing.

- [x] Continuous natural walls: connected sediment formations, rounded exposed
  corners, worn edges, fissures and shaded front faces replace repeated rock tiles.
- [x] Broad floor variation: compacted clay, dusty patches, sparse gravel and damp
  soil vary across the region rather than repeating at tile boundaries.
- [x] Roots and embedded details: tapering roots, wall fossils, mineral seams,
  low stones and small fungi remain below actors and within the terrain artwork.
- [x] Distinct rooms: E3 has long entrance roots; upper E4 emphasizes sediment;
  upper E5 has ammonite fossils; upper E6 has buried masonry. Deep E4 has branching
  mineral seams, deep E5 has damp green soil and drip pools, and deep E6 combines
  buried masonry and a fossil beside the existing return-route layout.
- [x] Restrained depth lighting: wall-foot shadows, front-face shading, warm
  entrance light, cooler deep-cave tones and small local highlights retain readable
  floors. There is no whole-screen darkening layer.
- [x] Subtle animation: intermittent falling dust, drips and expanding ripples,
  plus slowly breathing fungal light. All effects use scene time, freeze on pause
  and respect exploration visibility.

## Visual comparisons

These gameplay captures use scanlines on and Strong miniature. Corresponding
`map-ID-before.png` / `map-ID-after.png` files show the full native-pixel region.

| Region | Feature | Before | After |
| --- | --- | --- | --- |
| Upper E3 / 57 | Entrance roots | [Before](earth-57-before.png) | [After](earth-57-after.png) |
| Upper E4 / 63 | Sediment chamber | [Before](earth-63-before.png) | [After](earth-63-after.png) |
| Upper E5 / 69 | Fossil walls | [Before](earth-69-before.png) | [After](earth-69-after.png) |
| Upper E6 / 75 | Buried masonry | [Before](earth-75-before.png) | [After](earth-75-after.png) |
| Deep E4 / 103 | Mineral seams | [Before](earth-103-before.png) | [After](earth-103-after.png) |
| Deep E5 / 109 | Damp grotto | [Before](earth-109-before.png) | [After](earth-109-after.png) |
| Deep E6 / 115 | Old stonework | [Before](earth-115-before.png) | [After](earth-115-after.png) |

Also retained: [five-second native animation capture](damp-cave-animation.mp4),
[miniature off](earth-109-crisp.png) and [paused view](earth-paused.png).
Full regions and default-effects gameplay views were visually inspected.

## Verification

Checked Release build: **zero warnings/errors; 883 graphics checks pass**,
covering all 115 maps, ten Hell layouts and 360 tour frames. The 74 additional
checks cover Earth terrain opacity/cache stability, nonrepeating floor patches,
animation timing and sparsity, clipping to known tiles, secret-wall/floor
indistinguishability including their ambience, tile-change invalidation,
gameplay/RNG preservation, biome isolation, seven frontend captures and pause.

Only the seven Earth Cave full-map captures changed. The other 108 maps and all
five reflection captures remain byte-identical. Of 628 paired screenshots, 16
changed: seven maps, seven new baseline Earth gameplay views and two existing
Earth frontend captures. Logs and comparison hashes are retained here.

All 26 gameplay core files retain their pre-work hashes. The implementation does
not change routes, collision, encounters, secrets, save formats or gameplay RNG.
Tests use isolated saves. The user's live game was left open throughout; its
adventure, journal and profile files changed during that interval, so they are
**not** claimed byte-identical. Before/after hashes are recorded without restoring
or overwriting those files. The preview save remained identical.

One 416×384 terrain texture is cached per currently prepared Earth region and
invalidated on map/tile changes. On this Mac, final-run preparation measured
10.9–12.5 ms per region; 100 unchanged cache checks took at most 0.1 ms at the
log's precision. These are desktop measurements, not a console performance claim.

Baseline: `build/graphics-checks/yipe-graphics-20261005-042922-1b2ac02f`.
Final run: `build/graphics-checks/yipe-graphics-20261005-043518-0594a6df`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Simplicity review covered `WorldEarth.cs`, the small integration changes in
`WorldRenderer`/`WorldTerrain`, and the graphics checks. The implementation uses
the established biome partial-class/cache pattern, one static terrain texture
and two small effect-anchor lists. No new asset pipeline, gameplay abstractions,
rendering passes or per-frame terrain uploads. No remaining findings.

No presentation work remains unfinished. The existing live MonoGame process
still uses the earlier build; save and restart it to see this version. User visual
acceptance is pending. Other port work resumes at P5.6 in `PORTING_PLAN.md`.
