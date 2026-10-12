# Combat feedback — 2026-10-07

Implemented the user's next approved visual feature. The hero and enemies remain
visible while combat results appear. Actual outgoing damage drives a brief sprite
flash, four small sparks and rising gold numbers; incoming damage responds 240 ms
later in coral. Area spells show individual target damage. A killing blow stays
attached to the struck slot after automatic target selection, and defeated sprites
dim after the impact. Zero damage produces no false hit or floating zero.

Results keep paged text and Continue available throughout. The separate saved
Screen shake toggle defaults Off, including existing profiles without the field.
On adds at most one native pixel per axis for 160 ms per impact. Effects pause
with combat and expire within 1.14 seconds. The implementation reuses the current
overlay, sprites and actual combat results, with no new texture, particle manager,
gameplay random draw, combat rule or adventure-save field.

Validation: checked local Release build with **zero warnings/errors**, **569 core
checks** and **1,447 graphics integration checks**. The new coverage compares real
UI actions against independent core results for attacks, area and single-target
spells, healing/shuffle/transformation, Backstab/Frighten, item counterattacks,
successful/failed escape, victory and defeat. It checks exact full gameplay and
RNG state, native pixels, fixed result text, pause/expiry, clearing between
overlays, bounded shake and independently saved On/Off preferences. The full suite
also renders 360 tour frames.

The earned opening replay also passes: all 42 C++ checkpoints plus the King's
reward, 116 victories, 14 chests and two rescues across 9,940 rendered frames.
Its fresh-process save/reload continuation passes with 135 further frames and
restores 14 discovered regions and 17 travel links. [Opening log](opening.log).

Final graphics run:
`build/combat-feedback-graphics/yipe-graphics-20261007-230232-2815e224`.
Enemy/hero hit, area-spell and victory captures were inspected for readable
numbers and result text. The scoped simplicity review covered all combat changes,
the preference/menu additions and their tests; no further findings. Visual
acceptance remains with the user. Cloud refinement remains deferred.

Development used a local build only; no package was rebuilt. Tests use isolated
saves and profiles. Personal save, map journal, preview and profile hashes were
unchanged from their pre-task values. [Hash comparison](personal-file-checks.json).

[Animation](combat-feedback.mp4), [choices](choices.png),
[enemy hit](enemy-hit.png), [hero hit](hero-hit.png), [settled](settled.png),
[area spell](area-spell.png), [victory](victory.png), [defeat](defeat.png),
[Screen shake menu](screen-shake-menu.png), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
