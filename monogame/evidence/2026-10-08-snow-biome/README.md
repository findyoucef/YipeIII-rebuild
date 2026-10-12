# Zephyr snow basin

> **Visual review update:** The user accepted the cold-white correction. The latest follow-up adds gradual ridge snow and continuous snowfall: [current day view](../2026-10-08-snow-transition/day.png), [clear-weather snowfall](../2026-10-08-snow-transition/clear-weather.png), [39 focused GPU checks](../2026-10-08-snow-transition/checks.log), [review](../2026-10-08-snow-transition/review.json). The older captures and verification below document the initial version.

All six requested upgrades are implemented for **surface A3/map13**, with the
Zephyr entrance at **5,4**, sign at **8,2**, and the connected ridge north into A2.
The current map render and authored events established the location before work.

[Native snow/walking animation](snow-walking.gif), [clear basin](a3-clear.png),
[snowfall](a3-snowing.png), [day](day.png), [night](night.png),
[combined night presentation](night-combined.png), [before](a3-before.png).

| Upgrade | Delivered behavior |
| --- | --- |
| Snow ground | Connected cream coverage, sparse wind-shaped drift lines, irregular grass transitions and partly melted edges |
| Cliffs | Frost/snow on connected tops, retaining the existing contour and dark slate faces |
| Vegetation/details | Two snow-laden pine variants with tiny crown motion and anchored roots; sparse frosted rocks/fallen branch |
| Entrance | Dark slate arch, snowy crown/threshold and short icicles at the authored Zephyr mouth |
| Weather | Sparse drifting flakes replace rain inside the visible cold mask; wind powder stays on snow below actors |
| Walking | Brief fading boot impressions and lifted powder at accepted plants, plus three cached CC0 Kenney snow crunches |

Particles/contacts use the pausable scene clock; Auto daylight keeps its approved
wall-time progression. The existing Rain intensity setting controls snowfall.
Sound/Volume and important-effect priority apply to snow footsteps. Tracks clear
on map changes, snaps, restore and load; blocked/idle/passive movement adds none.

[Recorded snow-footstep preview at default gain](snow-footsteps.wav),
[sound sources/CC0 credits](../../Yipe.Desktop/Content/Audio/Footsteps/README.md).
The new sprite source was generated with **built-in imagegen**:
[project asset](../../Yipe.Desktop/Content/Art/Retro/World/snow-props.png),
[exact prompt](art-generation-prompt.txt), [generation/import record](art-generation.json),
[native 96×96 compiled atlas](snow-props-native.png). Runtime import uses the
existing 48px grid, shared palette, binary alpha and PointClamp rendering.

Verification:

- Checked local Release: zero warnings/errors. [Build log](build.log).
- **613 core checks** pass, including eighteen credited recorded footsteps.
  [Core log](core-final.log).
- **2,278 full graphics checks** pass, including all 41 monsters, combat,
  daylight and the snow fixture. [Graphics log](graphics.log).
- Focused native GPU checks cover aliases, melting edges, matching ridge
  continuity, weather/impact masks, fog, planted contacts, fading/cleanup,
  same-map invalidation and same-session gameplay/RNG snapshots.
  The final reviewed fixture passes **38 focused checks**. [Focused log](focused.log).
- Core source remains byte-identical to the task baseline. Terrain, routes,
  encounters, rewards, secrets and save format are unchanged.

The west A2/A3 overlap already has an authored skirt-versus-sand difference at
map7 `4,11` and map13 `4,1`. Continuity checks compare the matching solid ridge
spine rather than requiring unlike authored boundaries to match.

Testing uses isolated saves/profiles. [Personal file audit](personal-files.json)
shows all adventure and map files unchanged. The active profile's day/night phase
was updated by the previous running game at 18:57:11 UTC and was retained as the
latest user state. No personal file was rolled back. Before ordinary launch, the
full adventure and its latest map/profile sidecars are protected in another backup.
[Launch record](launch.json), [protected files](launch-backup.json).

Antigravity supplied a bounded draft test-risk checklist; primary source review
and executable checks establish the results. Its headless candidate-edit step
was permission-denied, so the primary agent wrote/reviewed the fixture without
changing permissions. [Delegation record](delegation.json).

The scoped diff was reviewed for simplicity: one snow presentation module reuses
the existing renderer, mountain geometry, weather controls, planted-step boundary
and audio loader. No clock framework, persistent footprint format or gameplay
logic was added. Development builds locally without repackaging.
