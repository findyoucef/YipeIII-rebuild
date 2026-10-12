# Monster animation and movement — 2026-10-07

Implemented the first animation pass for the rabbit (1), Giant Beetle (2), and
Rabid Bat (5), with 16 new pixel-art poses each: idle, movement, wind-up/strike/
recovery/hurt, and four defeat stages. The original character identities and
foot anchors are preserved. Native compilation uses the existing palette and
binary alpha, with one shared scale correction per sheet. Source sheets remain
in the project; their [provenance and layout](../../Yipe.Desktop/Content/Art/Retro/World/MONSTER_ANIMATIONS.md)
are documented.

All roaming/pursuing actors now move smoothly over 250 ms. The three animated
species have facing, creature-specific idle cycles, rabbit hops, beetle scuttling,
bat flapping/hovering, ground shadows and small dry-ground landing effects.
Spawns, replacements, removals, map changes and relocations reset correctly.
Pause, maps and modal exploration panels freeze travel. Pending encounters wait
for the visible approach to finish. Battle poses follow the existing impact,
sound and HP timeline; inspection stays stationary and completed results settle.

Checked local Release build: **zero warnings/errors**, **569 core checks** and
**1,575 graphics checks** pass. Thirty-seven new graphics checks cover all poses,
palette/anchors, cycle variety, mirrored facing, fallback art, smooth travel,
independent actors, diagonal movement, actor/map/relocation boundaries, pause and
modal behavior, battle approach timing, combat-menu idles, actual counterattacks,
actual killing blows and full game/battle/RNG preservation. The suite renders
360 tour frames in addition to the creature captures.

The earned opening replay matches all **42 C++ checkpoints plus the King's
reward**, with **116 victories, 14 chests and two rescues**. It renders 9,956
frames, followed by 136 in a fresh-process save/reload continuation. The initial
replay revealed that its scripted inputs only waited for player motion and
overlays. It now also waits for monster travel and pending interactions, matching
the new visual timing. Expected fixtures, encounter rules, combat calculations,
rewards, RNG and saved formats were not changed.

Native sheets, exploration samples, battle idles and early/late defeat poses were
visually inspected. The scoped simplicity review covered all production, test,
asset and documentation changes; no remaining findings. Runtime changes reuse
the existing command/overlay flow, with two small visual actor states and one
animation sheet per selected species. No core source code changed.

Tests used isolated saves/profiles and no task command wrote personal game files.
The personal save and map journal changed while the previously launched game
was open; profile and preview hashes were unchanged. Actual before/after values
are retained in [personal file checks](personal-file-checks.json). Development
used a local build only. No distributable was rebuilt. The first pass is ready
for user visual review before extending animation art to the other 41 types.

Final graphics run:
`build/monster-animation-accepted-checks/yipe-graphics-20261008-004431-f2425ed8`.

[Combined animation preview](monster-animations.mp4),
[rabbit travel](monster-1-world.mp4), [beetle travel](monster-2-world.mp4),
[bat travel](monster-5-world.mp4), [rabbit battle](monster-1-battle.mp4),
[beetle battle](monster-2-battle.mp4), [bat battle](monster-5-battle.mp4),
[rabbit defeat](monster-1-defeat.mp4), [beetle defeat](monster-2-defeat.mp4),
[bat defeat](monster-5-defeat.mp4), [battle idles](battle-idles.png),
[rabbit sheet](monster-1-native-sheet.png), [beetle sheet](monster-2-native-sheet.png),
[bat sheet](monster-5-native-sheet.png), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [opening/restart log](opening.log),
[source hashes](source.sha256).
