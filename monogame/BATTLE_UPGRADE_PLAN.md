# Battle upgrade implementation plan

Date: 2026-10-07. Implemented and verified. The user approved upgrades 1–5 and selected
**victory and rewards presentation** as upgrade 6. The first combat feedback
pass is accepted. Cloud refinement remains deferred.

## Scope and constraints

Preserve the original combat formulas, turn order, costs, rewards, targeting
semantics, random stream and adventure-save format. Use the existing pixel art,
bitmap font, audio recordings and 320 × 180 canvas. Development builds and runs
locally; do not rebuild a distributable. Preserve unrelated edits and personal
saves. Do not terminate an existing game with possible unsaved progress.

The core already supplies per-enemy counterattack damage and per-target spell
damage. Presentation will capture actor/resource values before the chosen action,
resolve that action once, then animate the recorded result. It must never rerun
combat to produce an animation, sample gameplay randomness for particles, or use
health differences as a substitute for actual damage (healing, shuffle and rabbit
replacement make that incorrect).

## 1. Animated attacks and individual counterattacks

- Capture the original selected target and visible actors before resolution.
- Use a brief anticipation/lunge, a pixel slash, impact flash/sparks and recoil.
- Follow with one counterattack at a time in the core's slot order. Each enemy's
  motion, incoming damage number, hero HP change and sound share the same timing.
- Show zero-damage attacks without inventing damage; use a neutral outcome label
  where zero may represent armor, invisibility or a miss.
- Preserve abilities that do not cause counterattacks. Successful escape must
  not create an enemy attack. Retain a defeated/replaced actor long enough to
  animate its transition, without making it a selectable living target.
- Pause/focus loss freezes the presentation. Continue can dismiss it immediately;
  no later sound or particle may leak into the next overlay.

Acceptance: rendered frames demonstrate outgoing and sequential incoming actions;
normal attacks, abilities, items, spells and fleeing exactly match independent
core state and RNG results.

## 2. Persistent battle HUD and animated health bars

- Keep hero HP, SP and AP visible in battle commands, spell/item choices and results.
- Add compact enemy HP bars with exact numeric values. Animate display values from
  their captured state to the actual result; a trailing segment marks recent loss.
- Show healing and resource use at the associated action, including HP restoration
  before a counterattack and level-up changes after victory.
- Keep text and controls clear at native resolution, preserving point sampling.
- Give combat its own compact layout within the existing overlay, with paged
  result prose and scrollable command lists; retain the ordinary overlay layout.

Acceptance: captures include low HP, healing with retaliation, resource spending,
three enemies, long values and final victory/defeat; animation settles exactly on
the core's actual values with no gameplay mutation.

## 3. Direct targeting and area previews

- Left/right changes the selected living enemy on the main battle menu and spell
  power selection; up/down continues to select commands and powers.
- Retain Change target for discoverability and existing controls/replays.
- Draw clear pixel brackets and the selected target's name. Skip dead/empty slots,
  wrap consistently, and preserve the current command when changing targets.
- Highlight all affected living enemies for high-power damage spells. Healing
  previews the hero; shuffle previews its affected group; rabbit replacement
  must not imply that the player can choose its internally determined victim.
- Display targeting guidance and costs before confirmation. Casting, canceling and
  targeting must never accidentally spend resources or advance a turn.

Acceptance: keyboard/controller InputFrame checks cover wrap/skip, target retention,
single/area/self previews, cancellation, and precise gameplay/RNG isolation.

## 4. Distinct spell and ability effects

- Build small deterministic native-pixel motifs from existing drawing primitives.
- Healing: rising green crosses; full/super healing varies strength and AP cue.
- Gobbler Attack: closing bite marks; Insect Attack: moving tiny swarm;
  Brain Crush: contracting violet marks; Health Suction: particles traveling back
  to the hero plus actual healing; HP Shuffle: cycling links; Rabbit Replacement:
  a small transformation puff and the actual replacement sprite.
- Backstab: sharper slash; Frighten: outward pulses and departure of its actual
  victim; Invisibility: a brief translucent silhouette; Enrage: a warm rising aura.
- Time existing spell/ability sounds to the visual event. Keep effects inside the
  actor stage and out of the HUD, descriptions and command text.

Acceptance: every combat spell and ability has a retained representative capture;
no false damage from healing/shuffle/transformation, no changed random consumption,
and no effects after switching overlays.

## 5. Optional automatic return to commands

- Add a separately saved **Battle pacing: Manual / Auto** preference. Manual is
  the default for both new and existing profiles.
- Auto returns from ordinary, single-page, ongoing results only after all action
  feedback has completed and the result has had time to be read.
- Victory, defeat, escape completion, failures and multi-page notices retain
  confirmation. Continue always allows an immediate manual advance.
- On automatic return, consume no input against the newly created command menu
  that frame; held/repeated confirmation must not launch an unintended attack.
- Pause, focus loss, storage and other overlays suspend automatic progression.

Acceptance: saved/default/legacy preferences pass; Manual stays put; Auto returns
once and spends no turn/RNG/resources; pause and all terminal results remain safe.

## 6. Victory and rewards presentation

- Finish the final impact with a short pixel dissolve of defeated enemies.
- Display clearly separated gold and XP gains, keeping exact original rewards,
  equipment upgrades, keys and other existing reward text available.
- Add a restrained level-up celebration when the real result reports a level-up,
  with the actual level and HP/SP gains. Do not grant or calculate rewards again.
- Keep rewards awaiting confirmation under both pacing settings. No new asset
  pipeline, gameplay rewards or progression rules are needed.

Acceptance: victories with/without level-up, multi-target kills and rewards beyond
one page remain readable and acknowledgeable; continuation grants nothing twice.

## Implementation order and verification

1. [x] Read the combat/action/audio/input boundaries; capture the working-tree and
   personal-file baseline; clarify the sixth feature; create this plan and goal.
2. [x] Add a small combat-only presentation record, captured from existing state
   and actual results. Replace the old aggregate hero-damage animation.
3. [x] Implement the actor timeline, HUD and combat layout together, then targeting
   and spell/ability motifs. Keep the generic overlay behavior intact.
4. [x] Add saved pacing and victory/level-up presentation; update menu/help text.
5. [x] Extend existing combat/profile integration checks at real UI boundaries.
   Verify core parity, timing, cancellation, targeting, persistence and pixels.
6. [x] Build checked Release locally, run the core and graphics suites, inspect
   screenshots/animations and replay the earned opening with fresh-process reload.
7. [x] Review the complete scoped diff with the simplify skill; retain logs,
   captures, source hashes and personal-file comparison; update the porting plan.
8. [x] Open the new local build if no existing game would be disrupted; report the
   outcome and verification. Mark the goal complete only after required work passes.

No additional combat mechanics or balance changes are included. Visual acceptance
of this expanded presentation will remain with the user after implementation.

## Completed verification

Checked local Release: zero warnings/errors; 569 core and 1,523 graphics checks
pass. The earned opening and its fresh-process continuation match all 42 reference
checkpoints plus the King's reward, including 116 victories. The earlier running
game closed before the updated local build was opened. No package was rebuilt.
The scoped simplify review found no remaining issues.

[Captures, animation, logs and source hashes](evidence/2026-10-07-battle-upgrades/README.md).
