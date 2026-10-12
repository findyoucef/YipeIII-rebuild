# Enemy information layout — 2026-10-07

The user found the original inspection screen crowded. It now uses a dedicated
card with one enemy portrait at exact 2x sprite scale and spaced HP, rank,
attack-rating and defense-rating rows. Hero resources and the other opponents
remain on the battle screens. A living-enemy position counter sits in the header.

Details switches to attack-spell resistance, defeat history and empowered traits;
Stats returns to the overview. Up/down selects Details/Stats or Back. Left/right
browses living enemies in either view without changing the attack target. Back
or Escape restores the original command menu. Inspection still consumes no turn,
resources or RNG state and remains open under Auto pacing.

The renderer uses live encounter values, including transformed rabbits. Partial
imported history remains labeled Recorded defeats. No gameplay or save-format
code changed. The two views reuse the existing overlay controls and independent
inspection cursor, with one local flag for the current view.

Checked local Release build: zero warnings/errors. All **569 core checks** and
**1,538 graphics checks** pass, including 360 tour frames. Inspection checks cover
focused rendering, live stat and detail regions, view switching, cursor/target
independence, both Back paths, living-enemy wrap/skip, complete game/battle/RNG
snapshot preservation, Auto isolation, transformed stats and native pixels.
The earned opening replay was not repeated for this presentation-only revision;
the preceding feature verification is retained in
[the original inspection evidence](../2026-10-07-enemy-info/README.md).

Stats and Details captures for normal, rabbit and empowered enemies were visually
inspected. Scoped simplicity review covered all production, tests and documentation
changes; no remaining findings. Existing unrelated working-tree changes were
preserved. Personal save, map journal, profile and preview hashes were unchanged
through verification. Tests used isolated files. Only a local build was made;
no distributable was repackaged.

Graphics run: `build/enemy-info-layout-graphics/yipe-graphics-20261007-234927-7437a263`.

[Stats](enemy-info.png), [Details](enemy-info-details.png),
[rabbit stats](enemy-info-rabbit.png), [rabbit details](enemy-info-rabbit-details.png),
[empowered stats](enemy-info-empowered.png), [empowered details](enemy-info-empowered-details.png),
[build log](build.log), [core log](core.log), [graphics log](graphics.log),
[personal file checks](personal-file-checks.json), [source hashes](source.sha256).
