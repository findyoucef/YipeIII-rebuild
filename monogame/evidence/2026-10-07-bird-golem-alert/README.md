# Ancient Seagull, Stone Golem and pursuit alert — 2026-10-07

Added 16 authored pixel-art poses each for the Ancient Seagull (6) and Stone
Golem (8), covering idle, travel, wind-up/strike/recovery/hurt and defeat. The
bird makes short wing-assisted hops and pecks. The golem has a slow idle, eased
heavy steps, a punch and a collapse into a stone heap. Brief gray dust marks
its dry-ground contact, including underground. Accepted steps still arrive
within 250 ms. Both sheets preserve the original identities and use the existing
native palette and foot-anchor compilation pipeline.

An outlined amber `!` marks a roaming or pursuing creature's first accepted
visible step toward the hero. It follows the creature, rises slightly and fades
within 0.7 seconds, without repeating throughout that visible chase. Spawns,
retreats, unexplored terrain and offscreen approaches stay quiet. Replacement,
map changes, relocation and loss of visibility clear the cue. Pause, exploration
panels and maps freeze its clock and hide it. Symbol pixels are clipped to
discovered terrain; its sharp presentation layer keeps it readable above heat,
lighting and miniature effects. Encounter rules and the existing approach-to-
battle handoff are unchanged.

Validation: checked local Release build with **zero warnings/errors**, **569 core
checks** and **1,621 graphics checks** pass, including 360 tour frames. The existing
pose, palette, anchor, world travel, real combat/defeat and full session/battle/RNG
checks now cover all seven animated species (112 poses). Two additional checks
verify weighted golem travel and its unchanged endpoint. Fourteen cue checks
cover triggering, repeat suppression, timing, visibility, fog, offscreen
rearming, pause/modal/map freezing, actor/map resets and state isolation.

The earned opening replay matches all **42 C++ checkpoints plus the King reward**,
with 116 victories, 14 chests and two rescues across 9,956 rendered frames. Its
fresh-process save/load continuation also matches, restores 14 regions and 17
travel links, and renders 136 continuation frames. Expected gameplay fixtures
are unchanged. [Opening log](opening.log).

The initial run caught a whole-desert native-pixel assertion affected by the
existing heat refraction. The native-scale fixture now uses a cave without
heat, and the glyph moved above world effects to preserve its sharpness.
The desert pursuit capture verifies the final readable presentation. No core
code or saved schema changed; cue timing and the repeat latch are visual state
on the existing two monster-motion objects.

Native sheets, exploration hops/steps, real battle strikes and defeat poses,
and the pursuit capture were visually inspected. The complete scoped simplicity
review covered code, tests, assets and docs against the pre-task workspace:
no remaining findings, no new animation framework, and no production refactor
solely for testing. All unrelated working-tree changes were preserved.

Tests use isolated saves/profiles. Personal save, map journal, profile and preview
hashes remained unchanged through verification; [file comparisons](personal-file-checks.json)
are retained. Only the local project was built; no distributable was repackaged.
This batch is ready for user visual review.

The updated local executable was launched successfully. The earlier game was
still running, so the new instance uses a separate copy of the saved adventure,
map journal and profile under `build/bird-golem-alert-preview/`. The earlier
instance and its unsaved progress were preserved. [Launch record](launch.json).

Final graphics run:
`build/bird-golem-alert-verified/yipe-graphics-20261008-014507-48b12091`.

[Combined 18.2-second preview](bird-golem-pursuit.mp4), [bird travel](monster-6-world.mp4),
[golem travel](monster-8-world.mp4), [bird battle](monster-6-battle.mp4),
[golem battle](monster-8-battle.mp4), [bird defeat](monster-6-defeat.mp4),
[golem defeat](monster-8-defeat.mp4), [pursuit](pursuit.mp4),
[bird native sheet](monster-6-native-sheet.png), [golem native sheet](monster-8-native-sheet.png),
[sharp desert alert](pursuit-016.png), [build log](build.log), [core log](core.log),
[graphics log](graphics.log), [asset provenance](../../Yipe.Desktop/Content/Art/Retro/World/MONSTER_ANIMATIONS.md),
[source hashes](source.sha256).
