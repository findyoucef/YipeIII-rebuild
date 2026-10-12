# Stone Golem walking revision — 2026-10-07

The first golem movement poses barely changed the legs, so travel read as a
standing creature sliding. The revised four poses show a lifted stone foot and
dark sole above the ground, a planted supporting leg and a clear change of
leading foot. Each accepted 250 ms tile move plays one swing/plant pair; the next
move uses the opposite leg. The plant holds for 120 ms after arrival before
returning to idle. Eased travel, shadows, contact dust and the pursuit cue remain
in place.

The built-in imagegen tool produced a separate transparent 2 × 2
[walking source](../../Yipe.Desktop/Content/Art/Retro/World/monster-golem-walk.png).
Its four poses replace only the movement row during the existing native asset
compilation. The first candidate raised the same foot in both swing poses; a
targeted bottom-row correction produced the final source. [Exact prompts and
source IDs](generation-prompts.txt) and [source hash](source.sha256) are retained.
All twelve idle, attack/hurt and defeat poses are pixel-identical to the preceding
batch; [native pose comparison](pose-preservation.json).

Validation: checked local Release build, **zero warnings/errors**, **569 core
checks** and **1,626 graphics checks** pass, including 360 tour frames. Five new
checks exercise actual rendered swing/plant pairs on successive and diagonal
steps, unchanged-tile synchronization, independent golems, actor replacement
and full session/RNG preservation during continuous strides and turning.
Existing checks continue to cover all 112 poses, native palette/ground anchors,
mirrored facing, 250 ms arrival, pause/modal/map behavior and real combat/defeat.
Native art and the forward/turn/back walking capture were visually inspected.

The scoped simplicity review covered all new code, tests, asset integration and
documentation against `build/golem-walk-before/`, with no remaining findings.
The only new visual state is one alternating-foot boolean on each existing
monster motion object. No core code, saved state, step duration or encounter
timing changed. The preceding [earned opening replay](../2026-10-07-bird-golem-alert/opening.log)
remains the gameplay reference and was not repeated for this pose revision.

Tests use isolated saves/profiles. Personal save, map journal, preview and profile
hashes remained unchanged through verification; [file checks](personal-file-checks.json).
The project was built locally; no distributable was repackaged. The revised walk
awaits user visual review.

The updated local game was opened successfully in normal mode after verification;
no earlier game instance was running. [Launch record](launch.json).

Graphics run:
`build/golem-walk-verified/yipe-graphics-20261008-020158-84dab6bf`.

[Four-second continuous walking preview](golem-walk.mp4), [native sheet](monster-8-native-sheet.png),
[first foot lifted](golem-stride-007.png), [first foot planted](golem-stride-009.png),
[opposite foot lifted](golem-stride-013.png), [opposite foot planted](golem-stride-015.png),
[build log](build.log), [core log](core.log), [graphics log](graphics.log).
