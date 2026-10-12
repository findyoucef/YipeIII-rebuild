# NPC animation and personality

Goal: bring the King, Princess and Skill Teacher to life with native pixel-art
poses, restrained idle motion and distinct interaction gestures. Preserve all
three Princess variants and the original dialogue, choices, quest rewards,
training costs, world rules, RNG and save data. Use local builds only.

The existing remaster checkpoint was committed as `0af5031` and pushed to
`origin/main` before this work began.

## Implementation and review

- [x] Capture the existing NPC world and interaction presentation with isolated
  adventures; fingerprint personal saves and original gameplay files.
- [x] Create and inspect consistent pose sheets: dignified King nods and open-hand
  greetings, the Teacher's staff-supported idle and instructional gesture, and
  Princess breathing/blinking, greeting and grateful celebration. Retain the
  blonde/pink, auburn/purple and blonde/blue rescue appearances.
- [x] Compile through the existing fixed palette and binary-alpha native sprite
  pipeline. Keep stable feet, crisp pixels, grounded shadows and environmental
  lighting. Do not add NPC wandering or change event locations.
- [x] Animate appropriate world actors and add readable dialogue portraits using
  the same poses. Play teaching and rescue/reward reactions at the existing event
  boundaries. Animation must not delay choices or advance gameplay during dialogue.
- [x] Verify dialogue pagination, selection/back behavior, rewards/training,
  disappearance after rescue, fog, pause, deterministic rendering and save/RNG
  preservation. Run local Release and relevant core/frontend graphics checks.
- [x] Inspect before/after screenshots and animation video at gameplay scale;
  retain review media and provenance under `evidence/2026-10-11-npc-animation`.

Completion requires implementation, passing verification and reviewable captures.
User aesthetic approval is a separate review after delivery.

## Completion — 2026-10-11

Implemented all five appearance sheets (60 poses), world NPCs and portrait
reactions. Local Release passes 632 core/input checks, 183 focused and
4,036 full graphics checks, including 360 tour frames. All 33 personal files
and 31 core/map files remain byte-identical.

[Review gallery and 55-second video](evidence/2026-10-11-npc-animation/review.html)
and [verification](evidence/2026-10-11-npc-animation/review.json).
User aesthetic approval remains pending. This work follows the pushed checkpoint
and remains local for review; no package was rebuilt.
