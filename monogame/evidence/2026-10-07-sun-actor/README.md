# Sunlit character and footstep dust — 2026-10-07

Walking into an outdoor sun shaft now adds a warm highlight along the upper and
left-facing sprite edges. Its stepped brightness follows the existing light mask
and fades within about 0.2 seconds after leaving. Existing adjacent silver-colored
sprite pixels can catch a one- or two-pixel glint for 0.18 seconds, with a
three-second cooldown while moving. The highlight follows the current animation
frame for the original hero and all 12 alternate appearances, preserves the
silhouette and dark outlines, and draws with the hero before foreground scenery.

Dry grass, roads and sand release two native-pixel dust motes per footfall only
inside sunlight. Motes stay at the footfall in world space, drift upward a few
pixels and fade over 0.65 seconds, with a 12-mote cap. Water, bridge decks, fire,
platforms and shaded ground emit none. Scene changes and teleports clear the
transients. Both features follow the saved sun-shaft strength, freeze on the scene
clock and disappear when Sun shafts is Off. Area-map rendering suppresses them.

The local checked Release build passes with zero warnings/errors, **560 core/input
checks** and **1,313 graphics checks**. Thirty-five new graphics checks cover
the sampling, appearances/poses, silhouette/outline preservation, glint size and
cooldown, fade, surface and lighting restrictions, mote lifecycle, teleport/Off
reset, cave/Hell exclusion, production pause, area-map exclusion and complete
gameplay/RNG isolation. All diagnostic sessions, saves and profiles are isolated.
No gameplay, collision, map-template or adventure-save code changed.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/monogame-sun-hero-graphics
dotnet run --project monogame/Yipe.Desktop -c Release --no-build
```

Full graphics run:
`build/monogame-sun-hero-graphics/yipe-graphics-20261007-201618-84bddea3`.
No distributable was rebuilt. The previous Preview.6 package has neither the sun
shafts nor these additions; use the local build for review.

Retained evidence: [walking frame](walking-003.png), [standing frame](walking-012.png),
[four-second walking sample](walking.mp4), [equipment glint](hero-glint.png),
[warm rim](hero-rim.png), [dust birth](dust-birth.png), [fading dust](dust-fading.png),
[original hero](appearance-00.png), [alternate 4](appearance-04.png),
[alternate 9](appearance-09.png), [build log](build.log), [core log](core.log),
[graphics log](graphics.log), [source hashes](source.sha256).

The walking sample uses Strong sun shafts and disables miniature/scanlines for
inspection; it assembles 96 production-rendered frames at 24 fps and is not a
performance measurement. Captures have been visually inspected; user visual
acceptance remains pending.
