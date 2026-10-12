# Monster animation assets — 2026-10-08

Forty-one transparent source sheets provide **656 base poses** across monster IDs
1–41. They extend the identities in the three original `monsters-01/02/03.png` atlases.
The golem also uses a separate four-pose walking replacement.
The original monster atlases are preserved. Sources were generated with the
built-in imagegen tool using that atlas as the identity/style reference.

| File | Monster type | Generated source |
| --- | --- | --- |
| monster-rabbit-animation.png | 1, Small Bunny | exec-80b25280-9fc2-43ac-aec3-d65a07f85b71.png |
| monster-beetle-animation.png | 2, Giant Beetle | exec-32ce73b1-9f2f-4f37-8d73-352783b5e89d.png |
| monster-weasel-animation.png | 3, Attack Weasel | exec-d77c8012-1271-465d-b01e-779be0c7aa57.png |
| monster-moose-animation.png | 4, Mystic Moose | exec-2ba84f14-0b15-4f78-a950-ed3f2ab8fa7c.png |
| monster-bat-animation.png | 5, Rabid Bat | exec-14f5d3dc-4217-4861-a0a7-5f78b39e2946.png |
| monster-bird-animation.png | 6, Ancient Seagull | exec-1794eca1-278b-4d44-aed0-5ffe0d1a4d94.png |
| monster-golem-animation.png | 8, Stone Golem | exec-83cf3d3a-e55a-4eee-bef9-878eb3f62758.png |
| monster-golem-walk.png | 8, Stone Golem walking replacement | exec-9d1a9a39-9a74-4c91-95c6-47ad5adfdc9b.png |

Each 16-pose sheet has four columns and four rows, compiled to native 48-pixel cells:

| Row | Left to right |
| --- | --- |
| 1 | Four idle poses: rabbit ear twitch/blink, beetle antenna movement, weasel sniff/tail flick/blink, moose breathing/ear flick, bat wing cycle, bird head turn/wing stretch, golem slow head/shoulder shift |
| 2 | Four movement poses: rabbit crouch/hop/extension/landing, beetle alternating legs, weasel low running strides, moose deliberate hoof steps, bat wingbeats, bird short hops/wingbeats, golem heavy stone steps |
| 3 | Wind-up, strike, recovery, hurt |
| 4 | Four stages of defeat, ending in a resting pose |

The generation spec required the same character, facing right, camera, body
proportions and pixel scale in every cell; transparent padding; full complete
figures; restrained pixel clusters and dark outlines matching the reference;
no text, grid lines, scenery, shadows, particles or additional creatures.

`RetroArt.Load` compiles the source to the shared palette and binary alpha.
`WorldMonsterAnimation.LoadMonsterAnimations` applies one common size correction
per sheet against the original idle height, then translates every pose to the
original monster foot anchor. It preserves pose changes and checks for empty or
overflowing cells. The whole process runs at load time. Rendering uses native
pixels with point sampling; left-facing poses are mirrored. Ground shadows,
flight/hop offsets, dry landing dust and water contact are drawn separately.
Newer sheets fit every appendage within a clear native border. Anchoring uses
the actual opaque pixels after resampling, preventing a missed one-pixel toe
from moving the foot baseline. Accepted earlier sheets remain unchanged.

The second batch adds the Attack Weasel and Mystic Moose. The weasel's action row
uses a crouch, biting pounce, recovery and flinch; the moose lowers its head,
thrusts with its antlers, recovers and recoils. Moose travel eases into and out of
its stride within the same 250 ms duration. Its dry-ground hoof plant has slightly
stronger contact dust. Both preserve the original designs and pose-grid contract.

The third batch adds the Ancient Seagull and Stone Golem. The bird opens its
wings during short hops and winds up for a peck. The golem braces, punches,
recovers and recoils, then collapses into a stone heap on defeat. Its slow idle,
eased travel and brief gray dry-ground contact dust give it weight without
changing the 250 ms step duration. Both use the same native asset pipeline.

The golem's original walking row was too similar to its standing poses. The
replacement `monster-golem-walk.png` is a 2 × 2 sheet, in row-major order:
near-foot swing, near-foot plant, far-foot swing, far-foot plant. Each swing
shows a raised stone foot and dark sole above the ground, with the other leg
supporting the body. The compiler copies just these four poses into the original
sheet's movement row before the shared size/ground-anchor pass. The other twelve
poses remain unchanged. Each accepted step alternates the leading foot and plays
its swing/plant pair; the plant holds for 120 ms after arrival before idle returns.
The first candidate raised the same foot in both swing poses and was corrected
before use. [Exact built-in imagegen prompts](../../../../../evidence/2026-10-07-golem-walk/generation-prompts.txt)
and [walk verification](../../../../../evidence/2026-10-07-golem-walk/README.md).

[Implementation plan](../../../../../MONSTER_ANIMATION_PLAN.md) and
[verification evidence](../../../../../evidence/2026-10-07-monster-animation/README.md).
[Weasel/moose evidence](../../../../../evidence/2026-10-07-weasel-moose/README.md).
[Bird/golem and pursuit evidence](../../../../../evidence/2026-10-07-bird-golem-alert/README.md).

## Completed remaining roster

The following 34 sheets complete the roster. Source identity references, exact
generation/edit prompts, chosen originals and SHA-256 fingerprints are retained
in the linked metadata. All final game textures are native pixel art: 48-pixel
cells, the existing fixed palette, binary alpha and point sampling. NPC entries
42–44 retain their separate original artwork. Final cleanup for IDs 24, 26, 34,
36, 38 and 40 uses compiled native references to remove row-boundary export
marks; the graphics suite now checks for distant detached pixels in every new sheet.

| Creature | Source asset | Prompt/source metadata |
| --- | --- | --- |
| 7 Mouth Man | [monster-mouth-man-animation.png](monster-mouth-man-animation.png) | [monster-07.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-07.json) |
| 9 Hairy One-Eye | [monster-one-eye-animation.png](monster-one-eye-animation.png) | [monster-09.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-09.json) |
| 10 Baby Gargoyle | [monster-baby-gargoyle-animation.png](monster-baby-gargoyle-animation.png) | [monster-10.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-10.json) |
| 11 Adult Gargoyle | [monster-adult-gargoyle-animation.png](monster-adult-gargoyle-animation.png) | [monster-11.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-11.json) |
| 12 Evil Wizard | [monster-wizard-animation.png](monster-wizard-animation.png) | [monster-12.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-12.json) |
| 13 Fire Bug | [monster-fire-bug-animation.png](monster-fire-bug-animation.png) | [monster-13.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-13.json) |
| 14 Flame Flier | [monster-flame-flier-animation.png](monster-flame-flier-animation.png) | [monster-14.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-14.json) |
| 15 Cloaked Stallion | [monster-stallion-animation.png](monster-stallion-animation.png) | [monster-15.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-15.json) |
| 16 Burning Skeleton | [monster-skeleton-animation.png](monster-skeleton-animation.png) | [monster-16.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-16.json) |
| 17 Venus Man Trap | [monster-man-trap-animation.png](monster-man-trap-animation.png) | [monster-17.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-17.json) |
| 18 Little Daemon | [monster-daemon-animation.png](monster-daemon-animation.png) | [monster-18.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-18.json) |
| 19 Snow Monster | [monster-snow-monster-animation.png](monster-snow-monster-animation.png) | [monster-19.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-19.json) |
| 20 Fire Lord | [monster-fire-lord-animation.png](monster-fire-lord-animation.png) | [monster-20.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-20.json) |
| 21 Evil Air Wave | [monster-air-wave-animation.png](monster-air-wave-animation.png) | [monster-21.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-21.json) |
| 22 Air Thing | [monster-air-thing-animation.png](monster-air-thing-animation.png) | [monster-22.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-22.json) |
| 23 Tongue Toad | [monster-toad-animation.png](monster-toad-animation.png) | [monster-23.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-23.json) |
| 24 Squawker | [monster-squawker-animation.png](monster-squawker-animation.png) | [monster-24.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-24.json) |
| 25 Water Ghost | [monster-water-ghost-animation.png](monster-water-ghost-animation.png) | [monster-25.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-25.json) |
| 26 Wisp | [monster-wisp-animation.png](monster-wisp-animation.png) | [monster-26.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-26.json) |
| 27 Ice Cave Crab | [monster-crab-animation.png](monster-crab-animation.png) | [monster-27.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-27.json) |
| 28 Electric Eel | [monster-eel-animation.png](monster-eel-animation.png) | [monster-28.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-28.json) |
| 29 Merman | [monster-merman-animation.png](monster-merman-animation.png) | [monster-29.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-29.json) |
| 30 Hand Head | [monster-hand-head-animation.png](monster-hand-head-animation.png) | [monster-30.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-30.json) |
| 31 Lump | [monster-lump-animation.png](monster-lump-animation.png) | [monster-31.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-31.json) |
| 32 Earl Earthworm | [monster-earthworm-animation.png](monster-earthworm-animation.png) | [monster-32.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-32.json) |
| 33 Muscles | [monster-muscles-animation.png](monster-muscles-animation.png) | [monster-33.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-33.json) |
| 34 Axe Man | [monster-axe-man-animation.png](monster-axe-man-animation.png) | [monster-34.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-34.json) |
| 35 Wormoid | [monster-wormoid-animation.png](monster-wormoid-animation.png) | [monster-35.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-35.json) |
| 36 Stone King | [monster-stone-king-animation.png](monster-stone-king-animation.png) | [monster-36.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-36.json) |
| 37 Flame Beast | [monster-flame-beast-animation.png](monster-flame-beast-animation.png) | [monster-37.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-37.json) |
| 38 Air Dragon | [monster-air-dragon-animation.png](monster-air-dragon-animation.png) | [monster-38.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-38.json) |
| 39 Soaked Spike | [monster-soaked-spike-animation.png](monster-soaked-spike-animation.png) | [monster-39.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-39.json) |
| 40 Earth Giant | [monster-earth-giant-animation.png](monster-earth-giant-animation.png) | [monster-40.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-40.json) |
| 41 Gatekeeper | [monster-gatekeeper-animation.png](monster-gatekeeper-animation.png) | [monster-41.json](../../../../../evidence/2026-10-07-monster-roster/assets/monster-41.json) |

Humanoid foot leads alternate per accepted tile move; horses use opposing
diagonal hoof pairs. Crab legs, worm segments, roots and tails have their own
cycles. Heavy motion eases without changing the 250 ms arrival. Hops lift above
a fixed shadow; hovering creatures have a faint shadow and never emit dust or
contact. Eel, merman, crab and Soaked Spike ripples clip to the actual water mask
at the interpolated feet; dry contact returns after landing on land.

The waiting Gatekeeper uses the idle cycle after doorway scenery, with its
existing quest/fog conditions. Empowered combat favors the wide braced pose
and uses a cached palette variant for sharper key/ember accents. The variant
follows the resolved actor snapshot through attack, hurt and defeat; it adds
no combat rules or saved fields. Enemy inspection uses a stationary first pose.

[Complete roster evidence and previews](../../../../../evidence/2026-10-07-monster-roster/README.md).
