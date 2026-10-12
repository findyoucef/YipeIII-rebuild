# Day and night cycle implementation plan

Date: 2026-10-08. Status: implemented and verified. The checked local build, core/graphics suites,
earned opening and finale/restart all pass. The local Auto preview is open.
Revision: the user requested continuing time through pause and suspension.
Auto now follows wall-clock elapsed time independently of simulation pauses.

Add an automatic cycle with warm dusk, readable moonlit nights and soft dawn
colors. Keep the existing native pixel artwork, crisp outlines and clear paths.
The [presentation queue](PORTING_PLAN.md) originally called for selectable
sunset and moonlight palettes, followed by a cycle. This plan delivers both,
using the existing lighting, sunlight and weather systems.

## Recommended cycle and controls

One cycle takes **20 minutes of active exploration**. These timings are the implemented defaults and can be tuned after visual review.

| Phase | Duration | Appearance |
| --- | ---: | --- |
| Day | 10 minutes | The current approved daytime colors and sunlight |
| Dusk | 2 minutes | Amber highlights, muted violet shadows and windows becoming more prominent |
| Night | 6 minutes | Cool slate and blue moonlight, with warm windows and readable terrain and creatures |
| Dawn | 2 minutes | Cool night colors pass through pale peach into normal daylight |

The sequence repeats Day → Dusk → Night → Dawn → Day. A new adventure begins
halfway through Day, so the opening retains its familiar appearance and the
first dusk arrives after about five minutes of elapsed time, including pauses.

Add one **Time of day: Auto / Day / Dusk / Night** row to the existing scrolling
pause menu. Auto is enabled by default for new profiles and older profiles
without this setting. Day reproduces the existing daytime rendering; Dusk and
Night hold representative palettes. Dawn remains part of Auto and the test
gallery. Choosing a fixed mode suspends the automatic clock; returning to Auto
resumes its previous phase. Weather and Pixel lighting retain separate controls.

## Pixel art and readability

Use a small set of hand-tuned ambient colors drawn from the existing amber,
slate, blue and violet palette. Change their blend slowly over time at the native
320 × 180 world canvas. Keep texture sampling at PointClamp and preserve every
sprite's pixel clusters, silhouette, alpha and foot anchor.

Night should feel like moonlight rather than near-black scenery. Keep roads,
water edges, doors, bridges, enemies and the hero legible even with Pixel
lighting Off. Compare colors in the native captures before settling the darkest
ambient value. Temporal fades should not introduce spatial blur or smooth
painted shading into the artwork.

Make the existing gold window pixels and stepped local light more prominent
after dusk. Preserve their warm color rather than letting the ambient blue turn
them gray. Reuse the current window masks and visibility-aware light patches;
tune nighttime window contributions without washing out walls or nearby actors.
Existing cave torches and Hell flames keep their current appearance.

## Clock behavior and persistence

Advance a small presentation clock at the beginning of every update, before the
simulation's pause and focus gates in [TownGame.cs](Yipe.Desktop/TownGame.cs).
Keep a bounded phase within one cycle and derive the ambient tint, daylight and
night amounts from it. Use the elapsed difference between UTC timestamps so
the cycle catches up after suspension even if no update frames were delivered.
The cycle is independent of the machine's civil hour and gameplay randomness.
Clamp negative elapsed intervals to zero so a backward clock adjustment cannot
rewind the phase; re-anchor each update so a suspension gap is applied only once.

- Continue during pause, action panels, dialogue, combat, maps, loading, saving,
  the company ident, character creation and focus loss. On returning from app or
  machine suspension, catch up only the cosmetic cycle. Movement, encounters,
  puzzle timers and combat retain their existing simulation/pause behavior.
- Continue in caves and Hell, while applying the changing
  palette only to outdoor maps 1–40. This allows the player to emerge into a
  later phase. Crossing map boundaries must not reset the clock.
- Keep rain's `outdoorWeatherTime` independent. Turning rain or local lighting
  Off must not freeze or restart the day cycle.
- Preserve the selected mode when starting a new adventure, but reset its
  automatic phase to the opening daytime position.

Store the mode and last automatic phase in the existing desktop profile, which
already stores presentation preferences. Keep the `.y3m` adventure format and
map journal unchanged. Resume the last persisted cosmetic phase for the current
profile after restart; loading an adventure uses that profile's cosmetic clock.
Time while the process is closed is not counted; in-process suspension is.
This phase is a presentation preference, not an exact historical timestamp tied
to each adventure checkpoint.

Write the phase on a settings change, successful adventure save and normal
exit, using the profile's existing atomic write path. Avoid per-frame writes.
Validate mode values and finite phase bounds, and preserve the existing handling
of unreadable or newer profiles. A preference-write failure must not damage or
invalidate a successful adventure save.

## Lighting and weather integration

Apply ambient color to the world before presentation filters and overlays.
Keep local window and torch light visible above that ambient treatment. Review
the composition order in [TownGame.cs](Yipe.Desktop/TownGame.cs) so miniature,
vignette and heat continue to operate on the same world image.

Use one shared daylight amount for the following effects:

| Existing effect | Cycle behavior |
| --- | --- |
| Sun shafts | Fade through dusk, disappear at night and return through dawn; preserve the saved Off/Subtle/Strong preference |
| Sunlit hero rim and equipment glints | Follow the same daylight and cloud coverage as the visible beam |
| Dust illuminated by sun shafts | Fade with sunlight; clear stale beam highlights and glints at night |
| Cloud shadows | Retain daytime drift and feathered edges, with ground shade fading as daylight disappears |
| Rain and splashes | Keep their existing schedule and surface scope; tune nighttime contrast so drops stay visible without obscuring actors |
| Ordinary footfall dust and monster water contacts | Keep their existing behavior under the changing ambient light |
| Desert heat | Reduce the outdoor shimmer after sunset and restore it at dawn; preserve Hell's existing heat |

Changing phase must not rebuild cloud masks, reroll weather or replace monster
textures. Pass a continuous daylight multiplier into sunlight drawing and actor
sampling rather than switching the integer intensity preference abruptly.

Draw pause panels, dialogue, battle portraits, HP bars, damage text and pursuit
alerts at their existing readable brightness. Area and world maps retain their
normal colors for navigation. The underlying scenery's day/night phase continues
while those overlays are open; actor movement and scene-animation clocks remain
paused according to their existing rules.

The existing movement call supplies `DateTime.Now.Hour` to the King's greeting.
Preserve that core input and its RNG consumption in this presentation work;
the new cosmetic clock does not replace it. Preserve encounter behavior,
movement timing, quests, rewards, visibility and puzzle timers throughout.

## Implementation order

1. **Establish the static palettes.** Add a small `DayNightCycle.cs` phase
   evaluator and integrate its ambient tint with the native world rendering.
   Capture forced Day, Dusk, Night and Dawn in town, meadow, desert and water
   scenes. Settle readable night colors and warm windows before adding motion.
2. **Connect sunlight and local lights.** Coordinate beam strength, hero
   highlights, glints, illuminated dust and cloud shade with daylight. Tune
   window light and outdoor heat. Inspect caves and Hell for unintended changes.
3. **Advance the automatic clock.** Use elapsed UTC time before modal and focus
   gates, catch up suspension without simulating missed gameplay, and retain
   phase across map transitions. Verify transitions, wrap and clock rollback.
4. **Add controls and persistence.** Add the four-mode pause-menu row, profile
   fields, validation, save/exit persistence and a short Help explanation. Update
   menu-index-dependent input checks so Help, About, controls and Quit remain
   reachable with keyboard and controller.
5. **Verify the completed cycle.** Run the relevant core and graphics checks,
   capture a full accelerated test cycle, and observe ordinary-speed transitions
   in the local game. Run the earned opening and finale/restart routes after the
   shared rendering integration is stable. Record source hashes and evidence,
   then open the local build. Development uses local builds without repackaging.

## Verification and completion

Add focused clock/profile checks alongside the existing presentation tests and
a `DayNightChecks.cs` graphics fixture. Use accelerated time in the test runner
only; production retains the twenty-minute cycle.

Verify elapsed-time equivalence across update rates, all phase boundaries, wrap,
modal/focus progression with frozen gameplay, suspension catch-up, clock rollback,
fixed-mode suspension and map continuity. Test missing legacy
fields, invalid values, persisted phase round trips and save failures using
isolated profiles and saves.

Compare Day with the current renderer at the same scene time and preferences.
Capture native and presented images for all four phases, including a moving
hero, roaming monsters, doors, shorelines and a three-enemy battle. Verify that
night stays readable with local lighting disabled; warm windows remain distinct;
sunlight-linked effects vanish at night; cloud/rain controls remain independent;
and fog never reveals a hidden creature, light source or terrain tile.

Include miniature Off/Strong, vignette, scanlines, rain Off/Strong, and local
lighting On/Off in representative composition checks. Confirm caves and Hell
remain unchanged, maps and UI retain their colors, and rendering preserves the
complete gameplay state and RNG. Retain a full-cycle clip and phase comparison
gallery under `evidence/2026-10-08-day-night/` when implemented.

The current implementation baseline is a checked Release build with 569 core
and 2,162 graphics checks, plus earned opening and finale/restart verification.
Completion requires those existing checks and the new cycle checks to pass,
readable native captures across the outdoor biomes, preserved personal saves
and profiles during isolated testing, and a verified local launch.
