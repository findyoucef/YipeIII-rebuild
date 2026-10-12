# Yipe cave techniques and evidence

All code and evidence paths below are relative to the YipeIII-rebuild root.
Read only the sections relevant to the requested change, and verify current
source before reusing a detail that may have evolved.

## Approved reference

The user approved the final local cave build with “this looks great!” after the
contour-lighting correction. The final reference is:

- `monogame/evidence/2026-10-09-breeding-light-contours/gameplay-59.png`
- `monogame/evidence/2026-10-09-breeding-light-contours/gameplay-59-before.png`
- `monogame/evidence/2026-10-09-breeding-light-contours/map-59.png`
- `monogame/evidence/2026-10-09-breeding-light-contours/gameplay-59-unlit.png`
- `monogame/evidence/2026-10-09-breeding-light-contours/light-59.png`
- `monogame/evidence/2026-10-09-breeding-light-contours/review.json`

Equivalent captures exist for the other five rooms. The reference cave comprises
59/60/65/66 (upper A4/B4/A5/B5) and 105/106 (deep A5/B5). Other environment-7 maps
are outside that treatment. These IDs describe the example, not future scope.

The earlier `breeding-grounds` and `breeding-walls` evidence includes rejected
art. `breeding-wall-detail` records the move to smaller stone facets;
`breeding-cohesion` records floor and creature integration. Prefer the final
contour captures when assessing the complete appearance.

### Accepted Fire Cave floor

The user accepted the cooled-lava floor with “awesome” and later specifically
asked how it had been fixed so well. Use these captures for the floor treatment:

- `monogame/evidence/2026-10-09-fire-floor/gameplay-67-before.png`
- `monogame/evidence/2026-10-09-fire-floor/gameplay-67.png`
- `monogame/evidence/2026-10-09-fire-floor/gameplay-72.png`
- `monogame/evidence/2026-10-09-fire-floor/gameplay-113.png`
- `monogame/evidence/2026-10-09-fire-floor/review.json`

The evidence JSON predates that acceptance and still says review was pending.
`monogame/PORTING_PLAN.md` records the subsequent approval. The approved red wall
joints and room glow predated the floor revision and were preserved; the floor
improvement came from material structure and pixel detail, not new lighting.

## Walls and continuity

Start with `monogame/Yipe.Desktop/WorldBreeding.cs`, especially `PrepareBreeding`,
`StoneAt`, `BreedingStone` and `BreedingWall`. `WorldHell.cs` supplies the finer
stone-detail reference; `WorldEarth.cs` supplies existing deterministic noise
helpers. Reuse appropriate code locally before inventing a general biome system.

The current map is 13×12 cells at 32 native pixels per cell: 416×384 pixels.
Gameplay renders through a 320×180 camera canvas with presentation effects.
“Low resolution” in this iteration meant oversized facets, not too few display
pixels: the rejected broad slabs were roughly 34×24; the preferred stone seed
spacing is 13×11 native pixels, matching Hell's detail scale.

The successful construction:

- Build a connected stone occupancy mask from terrain and neighboring rooms.
  Breeding walls normalize ordinary `0x23` and secret `0x44` as identical rock.
- Cut exposed corners and vary the inset in short pixel steps. The reference
  uses an 8-pixel corner cut and a 1–5-pixel irregular inset. Keep the silhouette
  angular without decorating the entire perimeter with a bright rail.
- Use jittered nearest/second-nearest stone seeds for small facets. Their distance
  difference yields narrow joints; the upper part of a facet gets a short
  highlight. The reference uses about 1.1 pixels for a joint and a highlight
  interval below 2 pixels, with darker 11–15-pixel front faces.
- Anchor terrain detail in world coordinates. Adjacent regions overlap two tiles,
  so native origins advance by 11×32 horizontally and 10×32 vertically. A seed
  based only on local coordinates or a different map ID will produce seams.
  Sample neighbors where an edge calculation crosses the current region.
- Cache the texture and silhouette together; rebuild after map/tile changes.
  Keep secret aliases visually identical after rebuilding.

These values are reference measurements. Different geology may call for other
facets or colours while retaining the same pixel density and readable depth.

## Floor and creatures

`WorldBreeding.cs` combines low-contrast broad noise, a small ordered dither and
sparse grey gravel. The accepted floor ramp is `(32,35,43)`, `(45,47,54)`,
`(52,54,60)`, `(67,70,77)`. This muted mineral dirt harmonized with the slate;
the earlier rusty-brown floor did not. Use a different coherent ramp for ice,
volcanic rock or another requested material.

Ground-contact darkness measures distance to actual stone pixels, including
carved corners. The reference searches a 5×5 neighborhood: squared distance
≤2 uses the darkest soil shade, and ≤8 uses the next shade. Measuring distance
from square wall cells instead produced broad dark rectangular boxes.

Props are authored by room role: communal nests, feeding debris, holding-cell
bedding, brood chambers and larger-monster dens. Clip decoration to suitable
terrain and keep it away from important events and routes. Share floor shades
with prop shadows and dust; do not scatter the breeding cave's nests into every
new cave by default.

In `WorldMonsterAnimation.cs`, the accepted world treatment multiplies sprites
by `(210,222,236)` and existing contact-shadow opacity by 1.8 in breeding rooms.
The existing shadow footprint, pose, hover height and sprite scale remain intact.
Ground dust also shares the soil palette. Battle portraits and appearance on
leaving the cave remain unchanged. These are restrained integration controls,
not a reason to regenerate approved monster artwork or darken enemies until
identification becomes difficult.

## Material-specific floors

Start with a material and formation process, then choose marks that express it.
“More detail” alone is insufficient direction. For the Fire Cave, broad smooth
patches gave way to flat fractured lava crust, folded flow ridges and ash/grit.
Reuse that relationship between shape and material when choosing another cave's
floor; copying the volcanic pattern with a new colour is not enough.

Work at three scales:

- **Large forms:** define slabs, compacted patches, erosion or sediment deposits.
  Keep them broad and flat enough to read as ground beside the wall's smaller,
  raised facets. Vary their boundaries without exposing the square tile grid.
- **Surface structure:** add selective shallow seams, folds, scratches or water
  tracks that explain how the material formed. Use restrained dark/light pixel
  pairs for shallow relief; avoid making every floor edge look like a ledge.
- **Fine detail:** scatter sparse grit, ash, chips or frost in the same colour
  ramp. Leave quiet areas around routes and actors instead of filling every
  pixel with high-contrast noise.

Choose a low-contrast floor ramp related to the walls and the room's illumination.
Keep bright accents sparse and motivated by the material. Ground-contact darkness
must follow the actual wall silhouette, including carved corners. Keep existing
hazard markings visually distinct and draw them above ordinary decorative marks;
cosmetic cracks must not obscure a real unstable-floor warning.

These are adaptation ideas, not previously approved presets:

| Cave material | Broad structure | Restrained surface detail | Accents and integration |
| --- | --- | --- | --- |
| Volcanic basalt | Irregular flat cooling plates | Folded flow ridges, ash and grit | Sparse sealed ember veins; darker floor beneath hot walls |
| Earth or mineral dirt | Compacted patches and exposed stone | Small pebbles, shallow wear and dust | Wall-related mineral colours; debris clustered at rock feet |
| Limestone | Worn shelves or eroded patches | Fine fissures and water-deposit streaks | Chalky chips and darker damp recesses; avoid bright white coverage |
| Ice | Broad frozen sheets and packed snow patches | Sparse hairline fractures, frost and scuffs | Short glints near plausible light; distinguish ice from rock edges |
| Wet or flooded rock | Eroded rock and sediment bands | Silt, water tracks and selective puddles | Dark damp patches and localized reflections; preserve readable paths |

For the implemented volcanic example, inspect the non-stone branch of
`PrepareFire` in `monogame/Yipe.Desktop/WorldFire.cs`. Jittered seeds spaced about
24×18 native pixels form flat plates; the nearest/second-nearest distance gap
defines thin seams. Selected plates receive shallow folded lines. Ordered dither
breaks up broad colour patches, individual pixels supply grit, and a sparse mask
adds ember seams. These dimensions suit this floor, not every material.

The floor is generated in code using the existing palette, world-coordinate
seeds and terrain cache. Reuse those paths when suitable: stable coordinates
keep details fixed during movement and continuous across overlapping rooms.
Do not increase render resolution or add blur to compensate for poorly chosen
feature sizes. Keep the user's approved wall and lighting treatment intact when
the requested correction concerns only the floor.

Review before/after at native resolution and actual gameplay scale, including an
open area, a narrow wall junction and a room with hazard markings. Judge whether
the ground reads as its intended material, sits below the walls, and supports
creature visibility. For a floor-only edit, compare unchanged wall pixels and
light masks where possible; this helped isolate the successful Fire Cave change.

## Contour lighting

See `monogame/Yipe.Desktop/PixelLighting.cs`: `Build`, `BuildCaveMask`,
`ReachesPixel`, `Prepare` and `Apply`; and the draw ordering in `TownGame.cs`.
The contour implementation supports breeding rooms and Fire Cave, selecting
`BreedingStone` or `FireStone` with the matching light sources. Adapting it to
another cave requires that cave's actual silhouette and light sources;
neither existing mask is a generic mask for all rock environments.

Separate three things when diagnosing a square-looking effect:

1. Baked floor/contact shading, visible even with local lighting disabled.
2. Radial light intensity and its wall/door occlusion, visible in the light mask.
3. Fog, camera scaling and presentation effects over the final world.

The successful light path uses circular stepped masks (128-pixel mask size;
reference radii 36/56/44 for windows/torches/fire or fungi). It traces visibility
at native-pixel resolution through the same stone silhouette used for terrain.
A wall-mounted source may leave its supporting rock; after entering air, the
ray lights a shallow next face and stops rather than passing through that wall.
Closed doors remain blockers. Include a separated rock corner in a fixture:
connected source rock and a distinct occluder are different cases.

A small cross filter weights the current visibility sample by two and its four
neighbors by one, divided by six. This feathers occlusion by one native pixel
without blurring the artwork. Cave lamps keep circular reach above the source;
the old horizontal torch cutoff made a conspicuous straight edge.

Bake masks only when terrain changes. Existing cell patches still gate both
source and destination on discovery each frame and apply time-based flicker.
Max blending caps overlapping light; the application pass brightens existing
colours while preserving black outlines and alpha. Preserve this behavior when
adapting the effect. Fog's undiscovered-cell boundary is intentional, not a wall
contour to soften away.

`TownGame.Draw` prepares lights before drawing the world. Ensure the requested
map's silhouette is prepared before the light bake, rather than relying on a
cached silhouette from the previous room. Dispose replaced GPU masks.

## Atmosphere

`DrawBreedingAtmosphere` uses an 8 Hz presentation clock for sparse drips, tiny
elliptical ripples, spores near fungi and occasional grit. Each drawn pixel is
clipped to discovered terrain, and source props are checked against current
terrain so removed walls do not leave floating effects. Pause freezes this
scene animation; it is separate from the game's continuously advancing world
clock. Do not use atmosphere to hide unfinished walls or floors.

For optional sound, inspect `GameAudio.cs` and
`monogame/Yipe.Desktop/Content/Audio/Ambience/sources.json`. The example uses a
credited CC0 wind/drip loop, quiet exploration fade, one voice, pause/menu/map/
focus gates, volume/mute support and release on biome exit. Keep the licensing
record with any newly chosen sound; do not assume this exact ambience fits every
cave. Match footsteps to the actual material using existing recorded assets.

## Verification

Use `monogame/Yipe.GraphicsChecks/BreedingGroundChecks.cs` as an example of
isolated captures and regression boundaries, not a test that covers all caves.
`PixelLightingChecks.cs` exercises shared lighting, doors, fog, blend limits and
UI isolation. Consult `monogame/Yipe.GraphicsChecks/README.md` for current flags.

From the repository root, a checked local build is:

```sh
dotnet build monogame/Yipe.GraphicsChecks/Yipe.GraphicsChecks.csproj -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:CheckForOverflowUnderflow=true
```

For changes to the breeding reference itself:

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --breeding-only --output build/cave-review
```

For changes affecting shared rendering or other caves, use the relevant focused
checks and the full graphics run as appropriate:

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --output build/cave-review-full --capture-monsters 1
```

The monster flag limits exported animation clips; it still checks all registered
monsters. These runs need a graphical desktop. Run only one graphics checker at
a time and do not rebuild its binaries mid-run. No repackaging is needed.

Choose checks that protect the changed behavior:

- Native palette/opacity and wall/floor readability; matching overlap continuity.
- Changed rock/doors invalidate caches; restoring them restores the picture.
- Secret aliases remain identical, hidden sources cast no visible light, and
  effects never reveal unexplored cells.
- A ray grazes a cut corner but stops at the neighboring solid face; an opaque
  door blocks the floor behind it; a reopened path restores the light mask.
- Creature silhouettes, hover/scale, battle art and biome-exit restoration.
- Frozen presentation time repeats exactly; rendering preserves complete
  session/save/RNG snapshots. Audio gates matter only when audio changed.

Inspect before/after at native and normal gameplay scale, including lighting
on/off and an isolated mask when diagnosing light. Compare unaffected map and
lighting captures against a known prior build. Match frozen scene state so
animation differences do not masquerade as regressions. The final breeding
reference passed 131 focused and 2,414 full checks; these are historical counts,
not fixed acceptance thresholds for future work.
