# NPC animation assets

Five transparent source sheets add **60 poses** for the King, Skill Teacher and
three existing Princess rescue appearances. They were created with the built-in
imagegen tool using `monsters-03.png` and `quests.png` as identity references.
The existing atlases are retained.

| Installed asset | Identity |
| --- | --- |
| [npc-king-animation.png](npc-king-animation.png) | Crown, white beard, blue tunic, red cape |
| [npc-teacher-animation.png](npc-teacher-animation.png) | Olive hood, white beard, planted wooden staff |
| [npc-princess-pink-animation.png](npc-princess-pink-animation.png) | Blonde hair, pink gown |
| [npc-princess-purple-animation.png](npc-princess-purple-animation.png) | Auburn hair, purple gown |
| [npc-princess-blue-animation.png](npc-princess-blue-animation.png) | Blonde hair, blue gown |

Every sheet has four columns and three rows: idle/breath/blink/return,
conversation anticipation/gesture/hold/return, celebration anticipation/gesture/
hold/return. Idle holds are longer than blinks. Nearby actors occasionally greet;
conversation gestures are restrained. Reward reactions play once, then idle.
The Teacher keeps his staff grounded; NPCs do not wander or affect collision.

`WorldNpc.cs` uses `RetroArt.Load` to compile native 48 × 48 cells with the shared
palette and binary alpha. One size correction per sheet matches the existing
identity. Each frame's feet/skirt hem centers the body, so an extended hand does
not slide it sideways. The feet use a common baseline. World rendering adds a
small stepped shadow, restrained cave tint and the existing scene lighting.
Dialogue portraits use the same native art, with a separate header to preserve
full-width text and choices. Portrait time advances independently from the paused
world; pause and focus loss freeze the portrait too.

The King's first source had boots crossing a row boundary. A built-in imagegen
edit added clear gutters before the corrected sheet was installed. Native
compiled sheets and actual game captures were inspected after import.

After world review, the King uses a 36-pixel destination (75% scale) with point
sampling, eight pixels farther forward at the castle doorstep. A smaller, darker
contact shadow anchors his boots. His dialogue portraits retain their original
size. [Placement correction video](../../../../../evidence/2026-10-11-king-grounding/king-grounding.mp4).

[Exact generation prompts and source paths](../../../../../evidence/2026-10-11-npc-animation/generation.json),
[King correction prompt](../../../../../evidence/2026-10-11-npc-animation/king-grid-correction.json),
[review gallery/video](../../../../../evidence/2026-10-11-npc-animation/review.html),
and [plan](../../../../../NPC_ANIMATION_PLAN.md).
