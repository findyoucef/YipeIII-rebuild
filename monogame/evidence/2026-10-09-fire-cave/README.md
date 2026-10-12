# Fire Cave volcano remaster — 2026-10-09 UTC

Implemented the approved six-part plan across upper caves 67/71/72/73 and deep
caves 95/100/101/107/111/112/113. The entrance connects to surface B6/map32.
Implementation and verification are complete; user visual/audio approval is pending.

## Review

- Entrance: [before, native](map-72-before.png), [after, native](map-72.png),
  [default gameplay](gameplay-72.png), [isolated light](light-72.png).
- Deep rescue room: [before, native](map-113-before.png), [after, native](map-113.png),
  [default gameplay](gameplay-113.png), [six-second gameplay clip](deep-room.mp4).
- Unstable floor: [native map73](map-73.png). Long dark cracks retain the original
  gameplay warning; thin ember seams elsewhere are sealed ground decoration.
- All eleven rooms have native, light-mask and gameplay captures in this folder.
  Before captures use the prior full-suite scene; hero positions differ from the
  new focused scenes. These are isolated render fixtures, not an earned traversal.

## Treatment

Connected charcoal basalt uses small fractured facets, narrow joints, chipped
contours and shaded front faces. Quiet ash/cooled-flow floors keep routes and
actors readable. Authored molten openings stay inside existing blocked rock;
eight native frames animate bright flow and dark cooling crust. Bubbles, sulfur
flecks and cooled shards supply material detail.

Amber light reuses the approved contour-mask path. Pixel-sized heat refraction
keeps the character and interface sharp; Fire Cave bypasses miniature blur.
Embers, intermittent smoke and ash use presentation time and deterministic seeds.
Deep chambers increase heat, particles and ambience. Existing monsters receive
warm world tint/contact shadows, retaining all poses, scales and battle artwork.

The 58-second stereo ambience mixes Blender Foundation's CC-BY-3.0
[Lava loop](https://opengameart.org/content/lava-loop) and PagDev's CC0
[Fireplace Sound loop](https://opengameart.org/content/fireplace-sound-loop).
Filtering, pitch adjustment, mixing and a two-second wrap crossfade are recorded
in the [source manifest](../../Yipe.Desktop/Content/Audio/Ambience/fire-cave-sources.json).
[Attribution](../../Yipe.Desktop/Content/Audio/Ambience/FIRE-CAVE-CREDITS.txt) ships
with the content. This is a sample-based volcanic sound design, not a claim that
the sources were recorded inside a volcano. Existing stone/grit footstep samples
follow the ground. Audio uses one ambient voice, master controls, fade-in,
action-sound priority and pause/focus/menu lifecycle.

## Verification

- Checked Release build: zero warnings/errors.
- 613 core checks; 206 focused and 2,614 full GPU checks passed.
- Full suite covers all 41 monsters and 360 tour frames.
- Magma containment, doors, fog, secret aliases, terrain/light invalidation,
  crisp heat, fixed-clock animation, cave day/night isolation, footsteps,
  creature/battle isolation, audio lifecycle and full save/RNG preservation.
- Only the 11 intended map captures changed. The remaining 104 maps, all 22
  other-biome light references and all 73 breeding-cave captures are identical
  to the prior approved baseline. [Comparison](capture-comparison.json).
- Original map data and core source have no diff. Local desktop binary matches
  the binary used by the graphics runner. No repackaging.

The original Fire Cave neighbors have different wall layouts in their overlapping
border bands. Checks compare matching walkable interiors using world coordinates;
they do not claim those pre-existing layouts have identical full borders.

Tests use isolated saves under their artifact folders. The personal adventure's
save/journal/profile changed after the initial hash snapshot while this task was
underway; their modification times precede the final full run. We left those
changes intact and did not restore or replace them. They remained stable between
the later observation and completion. [Hash observations](personal-files.json).

Build/core/focused/full logs, [review record](review.json), [source hashes](source-hashes.json)
and the scoped [integration diff](integration.patch) are retained here. Full
intermediate frames remain under the local artifact directories in the review record.
