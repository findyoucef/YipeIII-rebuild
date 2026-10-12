# Moving cloud shadows — 2026-10-07

Implemented the requested goal: broad, faint cloud shade outdoors, coordinated
with the existing sun shafts and independently adjustable through a saved
**Off / Subtle / Strong** pause-menu setting. Subtle defaults for both new and
older profiles. Missing preferences do not cause a profile rewrite; invalid
strength values preserve the original file through the existing error path.

Overlapping cloud lobes form irregular patches with four native-pixel shade
steps. Subtle darkens by up to 10%; Strong by up to 20%. The cached cloud mask
shares the shafts' world coordinates and two-pixel-per-second drift, and leaves
every lit shaft pixel unshaded, including the illuminated dust/glint openings.
Clouds work independently with sun shafts Off. The shade is drawn before local
lighting, miniature/heat presentation and UI; it affects outdoor scenery and
actors while preserving the existing lighting passes. Scene-clock pause/focus
behavior freezes both masks together. Caves, Hell, maps and UI are unaffected.

The local checked Release build passes with zero warnings/errors, **562
gameplay/input checks** and **1,350 graphics checks**. Thirty-seven new graphics
checks cover biome scope, full state/RNG preservation, combined effects, bounded
stepped shade, opaque alpha and black pixels, exact Off bypass, frozen/drifting
animation, camera anchoring, texture wrapping, clear sun openings at multiple
times/positions, independent settings and profile reopen, production pause, pixel
scaling and unaffected pause/action/map UI. Profile checks also cover old defaults
and both invalid strength bounds. Existing sunlight, hero-glint/dust and other
graphics checks pass. Diagnostic saves and profiles are isolated.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/monogame-cloud-shadows-graphics
dotnet run --project monogame/Yipe.Desktop -c Release --no-build
```

Full graphics run:
`build/monogame-cloud-shadows-graphics/yipe-graphics-20261007-211011-d64a32c4`.
No distributable was rebuilt. Preview.6 remains the previous package and does not
include the sunlight/cloud effects; use the local build for review.

Retained evidence: [Off](map-017-off.png), [Subtle](map-017-subtle.png),
[Strong](map-017-strong.png), [combined town effects](map-017-combined.png),
[combined desert effects](map-001-combined.png), [menu](menu.png),
[16-second animation](animation.mp4), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
The animation assembles 64 production-rendered frames at four frames per second;
it is a visual sample, not a performance measurement. Captures were visually
inspected. Implementation and verification are complete; user visual acceptance
remains separate.
