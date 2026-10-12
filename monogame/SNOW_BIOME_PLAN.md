# Zephyr snow basin upgrade

Date: 2026-10-08. Scope: surface A3, map 13; Zephyr cave mouth at 5,4,
sign at 8,2, with the connected ridge continuing north into A2/map 7.
Location was resolved from the map catalog and the current full-region render.

Goal: implement all six approved upgrades as coherent native pixel art, retain
the existing level layout and gameplay, verify locally and open the full adventure.
Status: the user accepted the cold-white ground and textured cliff correction.
The follow-up ridge taper and continuous snowfall build with zero warnings/errors
and pass 39 focused GPU checks. Native and enlarged day renders were inspected.
The full adventure was opened; it remains running on the earlier build until
restarted. [Latest preview](evidence/2026-10-08-snow-transition/day.png) and
[verification](evidence/2026-10-08-snow-transition/review.json).

1. Compile connected cold-white snow coverage with small blue-gray drift clusters,
   rounded/irregular grass transitions and partly melted edge cells. The hidden
   gold tile uses exactly the normal snow treatment.
2. Cap connected cliff tops with snow while retaining the existing mountain
   contour and dark south faces. Coverage thins over 64 native pixels in small
   rock patches. Shared world coordinates and neighboring terrain preserve A2/A3 seams.
3. Use two snowy pine silhouettes with staggered trunk positions, varied sizes
   and mirrored saplings; every trunk stays in its authored blocked cell.
   Point sampling preserves crisp pixels. Keep rock/branch details restrained.
   [Revised tree layout](evidence/2026-10-08-snow-trees/day.png).
4. Give the Zephyr entrance a slate arch, dark open recess, snow crown/threshold
   and short icicles, matched to the same 48px prop scale and palette.
5. Keep gentle snow falling throughout the visible cold basin, including clear
   weather and Rain Off. Automatic storms add heavier flurries; warm areas retain rain.
   Low wind-blown powder stays on snow and below actors. Fog and UI remain clear.
6. At accepted planted walking contacts, add a short fading boot trail and tiny
   powder puffs clipped to actual snow. Cache three CC0 Kenney snow-crunch
   recordings through the existing quiet footstep bus and master controls.

The art sheet is generated with built-in imagegen, then compiled through the
existing `RetroArt` grid/palette importer. Snow geometry and effects are code
native; they do not introduce painted terrain textures or filtering. The new
sheet is `Yipe.Desktop/Content/Art/Retro/World/snow-props.png`.

Particles and tracks use the existing pausable scene clock. Auto day/night keeps
its approved wall-clock behavior. Tracks clear on actual map change, snap,
restore and load; idle, blocked or passive movement cannot add tracks. Bounds,
fog, secret aliases and same-map tile changes are part of verification.

Build/test through isolated local saves and profiles. Inspect native A3, A2,
Day/Night, clear/snowing, miniature and UI captures; check cache invalidation,
pixel palette/alpha, footprint lifecycle, weather masks and gameplay/RNG
snapshots. Run relevant core/full graphics checks and retain evidence under
`evidence/2026-10-08-snow-biome/`. Preserve personal files and any live session;
ordinary launch uses the existing full adventure, never the level-1 test scene.
Development does not repackage the app.
