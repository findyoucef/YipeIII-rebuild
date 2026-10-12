# Outdoor sun shafts — 2026-10-07

Added warm diagonal sunlight to surface maps, with a separate saved
**Off / Subtle / Strong** pause-menu setting. Subtle is the default for new and
older profiles. Unequal cloud openings form slowly drifting bands with stepped
native-pixel shading; the field remains anchored to the world during camera
movement. It uses the existing scene clock, so pause, overlays and focus loss
freeze it with the scenery. Caves, Hell, maps, UI and studio branding are unaffected.

The cached mask uses one draw over the world before miniature/heat presentation.
Destination-color blending preserves dark outlines and black undiscovered pixels.
No gameplay, map templates, RNG, adventure-save format or existing user files
were changed. Cloud shadows remain queued. The implementation and selected
captures were reviewed; user visual acceptance is still pending.

The checked local Release build passed with zero warnings/errors:
**560 gameplay/input checks**, including the unchanged C++ rule comparisons, and
**1,278 graphics checks**. New coverage verifies strength persistence and legacy
defaults, invalid preference bounds, surface/cave/Hell scope, frozen/advancing
animation, camera anchoring and texture wrapping, bounded warm brightness,
opaque alpha, exact black/native pixels, combined effects and unchanged
gameplay snapshots/RNG and UI. All check saves and profiles are isolated.

Commands, from the repository root:

```sh
dotnet build monogame/Yipe.slnx -c Release -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/monogame-sun-shafts-graphics
dotnet run --project monogame/Yipe.Desktop -c Release --no-build
```

Full graphics run:
`build/monogame-sun-shafts-graphics/yipe-graphics-20261007-200520-214a598f`.
No distributable was rebuilt; local builds are the development workflow.
Preview.6 remains the previous package and does not include this effect.

Retained evidence: [Off](map-017-off.png), [Subtle](map-017-subtle.png),
[Strong](map-017-strong.png), [combined town effects](map-017-combined.png),
[combined desert effects](map-001-combined.png), [menu](menu.png),
[12-second animation](animation.mp4), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
The animation assembles 72 production-rendered frames at six frames per second;
it is a visual sample, not a performance measurement.
