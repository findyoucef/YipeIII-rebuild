# Day and night cycle

The automatic twenty-minute cycle is enabled by default for new and legacy
profiles. Active exploration advances through ten minutes of day, two of dusk,
six of night and two of dawn. Menus, combat, maps, storage and focus loss freeze
the clock. The Auto/Day/Dusk/Night setting and suspended phase persist separately
from gameplay saves. A new adventure starts at midday.

Night uses a cool ambient multiplier at native resolution and stronger warm
window lighting. Source artwork and silhouettes remain unchanged. Sun shafts,
hero highlights, glints, illuminated dust, cloud shade and outdoor heat follow
daylight. Rain keeps its existing independent schedule and pale pixel streaks.
Caves and Hell retain their lighting while the outside clock continues during
underground exploration. UI and navigation maps keep their usual colors.

## Phase previews

| Scene | Day | Dusk | Night | Dawn |
| --- | --- | --- | --- | --- |
| Town | [Day](map-17-day-lit.png) | [Dusk](map-17-dusk.png) | [Night](map-17-night.png) | [Dawn](map-17-dawn.png) |
| Surface 1 | [Day](map-1-day-lit.png) | [Dusk](map-1-dusk.png) | [Night](map-1-night.png) | [Dawn](map-1-dawn.png) |
| Surface 8 | [Day](map-8-day-lit.png) | [Dusk](map-8-dusk.png) | [Night](map-8-night.png) | [Dawn](map-8-dawn.png) |
| Surface 33 | [Day](map-33-day-lit.png) | [Dusk](map-33-dusk.png) | [Night](map-33-night.png) | [Dawn](map-33-dawn.png) |

[Full cycle](full-cycle.mp4) accelerates twenty minutes into ten seconds.
The 320 × 180 previews sample the actual displayed frame, including existing heat
and miniature composition. The time-of-day pass itself preserves native pixels.
[Night with local lighting Off](map-17-night-unlit.png), [three-enemy battle](battle-night.png),
[pause setting](menu-night.png), and [rain with presentation effects](night-combined.png)
cover readability and composition.

## Verification

The checked local Release build has zero warnings/errors. **582 core checks**
and **2,204 graphics checks** pass. Clock tests cover update-rate equivalence,
phase boundaries, wrap and fixed palettes; profile tests cover default Auto,
legacy files, round trips and invalid values. Graphics checks cover freezes,
map continuity, independent rain timing, underground preservation, bright UI,
night brightness without local light, and undiscovered light sources.

The earned opening matches all 42 C++ checkpoints plus King return and a separate
process reload. The earned late route defeats the empowered Gatekeeper, saves, restarts in a
separate process, completes the final rescue and displays the live Maynard ending. Tests use isolated
saves and profiles. Core source hashes remain identical to the pre-change baseline;
the existing real-hour King greeting and gameplay random stream are preserved.

- [Build](build.log), [core](core.log), [graphics](graphics.log),
  [earned opening](opening.log), [earned finale](late.log).
- [Source review](source-review.json), [personal file audit](personal-files.json).
- [Implementation plan](../../DAY_NIGHT_PLAN.md).

Development builds and runs locally; no distributable was repackaged.

## Live local preview

The local town preview runs Auto from the beginning of dusk using an isolated
imported town save. Its clock advanced from 600 to 705.65 seconds during ordinary
play, and the real pause-menu modes persisted that phase when returning to Auto.
[Early dusk](live-dusk-start.png), [later dusk](live-dusk-later.png), and
[launch record](launch.json) retain the native window and profile verification.
The separate window containing a copy of the personal Hell save was preserved;
Hell correctly keeps its original lighting. [Personal files](personal-files.json)
remain unchanged. The preview starts near dusk for inspection; new adventures
start at the implemented midpoint of day.
