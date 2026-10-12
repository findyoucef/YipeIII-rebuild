# Fire Cave red heat correction — 2026-10-09 UTC

The user found the first remaster too cool and requested red lines between the
wall rocks and a red glow throughout the room. This revision addresses that
feedback across the same eleven Fire Cave rooms. Visual approval remains pending.

- Upper B6 entrance: [before](gameplay-72-before.png), [after](gameplay-72.png).
- Deep C6 chamber: [before](gameplay-113-before.png), [after](gameplay-113.png).
- Open upper C5 room: [before](gameplay-67-before.png), [after](gameplay-67.png).
- All eleven rooms have native, gameplay and isolated local-light captures.

The existing fine rock joints now use three ember-red/orange shades, varying in
world space and becoming brighter near magma. Front faces and the outside foot
edge stay dark. Rock and floor colours now support the red light instead of
reading as neutral grey.

A room-wide ambient pass warms existing world pixels and increases their red
brightness, with stronger bounce in the deep chambers. It reuses the existing
ambient/illumination blend states, adds no texture or blur, and runs before heat
and the interface. It belongs to the cave's base appearance even when local
pixel lighting is disabled. UI, other biomes, geometry, animation, audio and
gameplay are outside this correction.

Checked Release builds cleanly. The focused GPU run passes 207 checks, including
a new real-device check for black/transparent controls, unchanged alpha and
stronger deep-room red brightness. Existing door, fog, secret equivalence,
cache, creature, audio, heat and save/RNG checks remain in the focused run.
The [review record](review.json) contains the final full-suite result and
other-biome comparisons. [Scoped changes](changes.patch), [hashes](source-hashes.json)
and build/focused/full logs are retained beside the captures.

Tests use isolated saves. The running full adventure was not navigated,
reloaded or replaced for this revision. Personal file observations are recorded
in [personal-files.json](personal-files.json).
