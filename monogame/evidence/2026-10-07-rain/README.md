# Rain and surface splashes — 2026-10-07

The user asked to move on from cloud shadows while remaining unhappy with their
appearance. Cloud refinement is deferred, not visually accepted. This implements
the next queued effect: optional rain and surface splashes.

Rain has its own saved Off / Subtle / Strong control, defaulting to Off for new
and existing profiles. Once enabled it rains outdoors continuously until disabled.
No automatic weather schedule or rain audio is included. Automatic showers of
30–60 seconds roughly every 5–10 minutes of outdoor play were suggested in the
conversation but have not been requested or implemented.

The effect uses short stepped pixel streaks, tiny ground splashes and expanding
water rings clipped to existing shoreline masks. Bridge cells receive no water
rings. Impacts render beneath characters/buildings; falling drops render over
the scene before miniature, heat and interface composition. It is world-anchored,
freezes with scene time and consumes no gameplay randomness. Rain is excluded
from caves, Hell and map views. Strong doubles the drop count.

The implementation is one stateless effect with two draw passes and a shared
deterministic drop sampler. The scoped simplicity review found no further issues;
there is no particle lifecycle, cache, weather scheduler or new gameplay state.
Renderer/profile/menu call sites and existing reflection-based graphics checks
were updated for the new setting.

Validation: checked local Release build, zero warnings/errors; **564 core/input
checks and 1,385 graphics integration checks pass**, including 30 dedicated rain
checks plus its default-setting check. The graphics suite also rendered 360 tour
frames. All four existing personal save/journal/profile files retained their
pre-check hashes. No package was rebuilt.

Final graphics run:
`build/rain-graphics/yipe-graphics-20261007-221812-08b1bfdb`.
It retains 96 rain animation frames at 24 fps. Static Strong, combined-effect and
menu captures were inspected for pixel clarity, scene readability and menu layout.
The visual result remains ready for user review.

[Off](map-017-off.png), [Subtle](map-017-subtle.png),
[Strong](map-017-strong.png), [combined effects](map-017-combined.png),
[desert](map-001-strong.png), [menu](menu.png), [animation](rain.mp4),
[build log](build.log), [core/input log](core.log), [graphics log](graphics.log),
[source hashes](source.sha256).
