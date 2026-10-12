# Six battle upgrades — 2026-10-07

The user accepted the first combat feedback pass, requested a detailed plan and
implementation goal, and selected victory/rewards presentation as the sixth
upgrade. [Detailed plan](../../BATTLE_UPGRADE_PLAN.md).

1. The hero lunges and strikes; each surviving enemy responds in the core's slot
   order. Recoil, flashes, sparks, individual damage numbers and the original
   sounds follow those impacts. Abilities retain their original absence of
   counterattacks. Zero damage receives a neutral label.
2. Hero HP/SP/AP and enemy bars remain visible through commands, spell/item menus,
   failures and results. Display values animate from snapshots with a trailing
   damage segment. Healing and resource potions appear before retaliation;
   level-up gains appear with the reward celebration.
3. Left/right targets living enemies without changing the chosen command/power.
   Pixel brackets preview single, area, self and the actual rabbit-replacement
   target. The existing Change target command remains available.
4. Distinct pixel motifs cover all combat spells and abilities: crosses, bite
   marks, insects, contracting magic, health suction, shuffle links, transformation
   puffs, Backstab slashes, Frighten pulses, invisibility and an Enrage aura.
5. A separate saved Battle pacing preference defaults Manual. Auto returns from
   ordinary single-page ongoing results after their animation and reading time.
   It consumes no input against the newly opened command menu. Terminal outcomes,
   failures and multi-page results retain confirmation. Pause/focus loss freezes
   the presentation through the existing game update boundary.
6. Final impacts add breakup particles and a fade; victory displays gold and XP,
   with a celebration for real level-ups. Existing upgrades, keys and level gains
   stay in the paged text. Dismissing results never grants rewards again.

The implementation captures the original actors/resources, resolves the existing
core action once, and animates recorded outcomes. It adds no gameplay formula,
RNG draw, new combat mechanic or adventure-save field. All visual work uses
existing sprites, sounds, font and native-pixel drawing primitives. The scoped
simplicity review removed per-frame timeline LINQ iteration and found no further
issues in the presentation, UI/input/preferences integration or regression tests.

## Verification

Checked local Release build: **zero warnings/errors**. **569 core checks** and
**1,523 graphics integration checks** pass. The graphics suite also renders 360
tour frames. The combat checks compare production UI actions with independent
core results and full gameplay/RNG snapshots, including every combat spell,
all four abilities, target changes, item/resource potions, escape, defeat,
victory, level-up, automatic progression and manual/automatic persistence.
Sound-instance checks verify that the outgoing impact and enemy response start
at their separate times. Frame comparisons verify native pixels, stable result
text, expiry and no presentation leaking into later overlays.

The earned opening replay passes all 42 C++ checkpoints plus the King's reward:
116 victories, 14 chests, two rescues and 9,940 rendered frames. Its fresh-process
continuation also passes, restoring 14 discovered regions and 17 travel links,
then rendering 135 further frames through the saved battles and return.
[Opening and restart log](opening.log).

Final graphics run:
`build/battle-upgrade-graphics/yipe-graphics-20261007-232517-24fa6e9e`.
Inspected the command HUD, area-target preview, sequential counterattack,
Insect Attack, Health Suction, Rabbit Replacement and level-up captures.
Visual acceptance of the expanded presentation remains with the user.

Tests used isolated saves/profiles. No task command wrote personal game files.
The personal save and map journal changed while the previously launched game
was open; the preview and profile hashes remained unchanged. Their actual
before/after values are retained in [the comparison](personal-file-checks.json).
No package was rebuilt. Cloud refinement remains deferred.

[Battle animation](battle-turn.mp4), [commands](choices.png),
[area preview](area-target-preview.png), [spell HUD](spell-list-hud.png),
[inventory HUD](inventory-hud.png), [first counter](counterattack-1.png),
[second counter](counterattack-2.png), [third counter](counterattack-3.png),
[Insect Attack](spell-3-High.png), [Health Suction](spell-7-High.png),
[Rabbit Replacement](spell-11-Low.png), [level-up](level-up.png),
[pacing menu](battle-pacing-menu.png), [build log](build.log),
[core log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
