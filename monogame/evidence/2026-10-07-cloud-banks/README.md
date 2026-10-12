# Cloud shape, shade and motion revision — 2026-10-07

The user rejected the preceding crisp-cloud pass on all three axes: rounded
shapes, dark/flat shade, and movement/sun-shaft behavior. The selected direction
remains crisp pixel clouds; this revision addresses those issues together.

- Replaced the authored circle clusters with deterministic, tiled cloud banks
  combining broad forms, medium lobes and smaller breaks. The field uses the
  same hashing/interpolation pattern as the existing terrain code and never
  consumes gameplay RNG. It is generated once, not regenerated each frame.
- Reduced maximum shade from 10%/20% to 7%/14% for Subtle/Strong. Cloud interiors
  have gentle density variation; boundaries retain a narrow pixel fringe.
- Clouds travel diagonally at 3.2 and 0.7 native pixels/second, independently of
  the shafts' two-pixel horizontal drift. Linear sampling blends cloud-mask
  opacity during fractional travel; terrain is still rendered and enlarged
  with sharp native pixels.
- Prepare the sun mask on a native-sized render target before scene rendering.
  The moving cloud mask attenuates it, allowing clouds to cross and dim shafts
  instead of sliding together as a fixed pattern. Hero highlights, glints and
  illuminated dust use matching bilinear cloud coverage on the CPU.

Saved Off/Subtle/Strong preferences and defaults, outdoor scope, pause/focus
freezing, camera anchoring, black outlines, and unaffected UI/maps remain.
Clouds Off uses the original sunlight rendering path; shafts Off still permits
cloud shadows. No gameplay, maps, personal saves or profile files were changed.

Verification: checked local Release build, zero warnings/errors; **562
core/input checks** and **1,354 graphics integration checks** passed. Updated
cloud checks cover lighter bounded shade, density variation, independent shaft
attenuation, matching rendered/actor light at several clocks/cameras, and an
additional smooth fractional-motion check. Existing scope, alpha, pixel-scale,
pause, UI/map, save/RNG and other effects checks pass.

An earlier full run ended with exit 143 after passing the cloud and shaft
checks; the retained final run below completed successfully. The earlier
shape-only experiment is superseded by this combined revision.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/monogame-cloud-motion-graphics
```

Full final graphics run:
`build/monogame-cloud-motion-graphics/yipe-graphics-20261007-215208-d5c202f9`.
No distributable was rebuilt.

Retained captures: [Off](map-017-off.png), [Subtle](map-017-subtle.png),
[Strong](map-017-strong.png), [clouds only](map-017-clouds-only.png),
[combined town](map-017-combined.png), [combined desert](map-001-combined.png),
and [eight-second motion preview at 24 fps](animation.mp4).
The preview assembles 192 frames from the production renderer; it is not a
performance measurement. Screenshots and animation frames were inspected.

[Build log](build.log), [core/input log](core.log), [graphics log](graphics.log),
[source hashes](source.sha256).

Simplicity review: production changes are confined to the existing SunShafts
class and its preparation call in TownGame. A reused render target and blend
state handle independently moving cloud occlusion using the same preparation
pattern as PixelLighting. The noise and cloud-sampling helpers directly serve
shape generation and actor/render consistency. No shader build pipeline,
weather framework, new dependency, or per-frame CPU texture generation was added.
The revised visuals remain subject to user review.
