# Attack Weasel and Mystic Moose — 2026-10-07

Added 16 pixel-art poses each for the Attack Weasel (3) and Mystic Moose (4),
following the existing four-row idle/movement/action/defeat sheet contract.
The weasel has sniffing, tail flicks, a low run and a biting pounce. The moose
has slower breathing, deliberate hoof steps, an antler thrust and a heavier
settle. Its visual travel eases into and out of each step, still arriving within
250 ms. Dry-ground hoof plants get slightly stronger contact dust.

Both source sheets preserve the original character designs and compile through
the existing palette/foot-anchor pipeline. [Asset provenance and layout](../../Yipe.Desktop/Content/Art/Retro/World/MONSTER_ANIMATIONS.md)
retain their source IDs. Registered animation assets now drive authored-world
rendering and defeat timing, eliminating separate species lists at those sites.
The roster has 80 poses across five animated types; 39 types remain to expand.

Validation: checked local Release build with **zero warnings/errors**, **569 core
checks** and **1,591 graphics checks** pass, including 360 tour frames. Existing
pose, travel, real counterattack/defeat, palette/anchor and full game/battle/RNG
checks now cover all five species. Two additional checks verify the moose's
gentle start/settle, no overshoot and unchanged 250 ms endpoint.

The Hell scenery check previously used the original static weasel as its body
reference. It now uses the current animation pose and facing for animated
creatures, retaining the complete-silhouette comparison beside the low ruins.
Both placements pass; the scene's terrain and collision were unchanged.

Native sheets, exploration movement, real attack poses and early/late defeat
captures were visually inspected. The scoped simplicity review covered all code,
tests, assets and documentation changes with no remaining findings. No new
animation framework or persisted state was introduced; no core code changed.
The opening campaign replay was not repeated for this batch; the preceding
[first-pass verification](../2026-10-07-monster-animation/README.md) retains all
42 C++ checkpoints, King's reward and fresh-process continuation.

Tests used isolated saves/profiles. Personal save, map journal, profile and
preview hashes remained unchanged through verification; comparisons are retained
in [personal file checks](personal-file-checks.json). Only a local build was made;
no distributable was rebuilt. The batch is ready for user visual review.

Final graphics run:
`build/weasel-moose-verified/yipe-graphics-20261008-010656-578ea6ed`.

[Combined preview](weasel-moose.mp4), [weasel travel](monster-3-world.mp4),
[moose travel](monster-4-world.mp4), [weasel battle](monster-3-battle.mp4),
[moose battle](monster-4-battle.mp4), [weasel defeat](monster-3-defeat.mp4),
[moose defeat](monster-4-defeat.mp4), [weasel native sheet](monster-3-native-sheet.png),
[moose native sheet](monster-4-native-sheet.png), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
