# Monster animation and movement

Objective: complete idle, movement, attack, hurt and defeat animation for every
monster in the original roster, with readable creature-specific locomotion.
Preserve encounter rules, combat calculations, RNG sequence and saved data.
Development uses local builds and runs; packaging is separate.

## Roster and current coverage

The authoritative [monster definitions](../assets/game-data/original-monster-definitions.json)
contain **41 distinct monsters**, IDs 1–41. At goal start, seven had complete animation sheets:
Small Bunny (1), Giant Beetle (2), Attack Weasel (3), Mystic Moose (4), Rabid Bat
(5), Ancient Seagull (6) and Stone Golem (8). The goal covers the other **34 monsters**. The King,
Princess and Skill Teacher are three NPC atlas entries and are outside this plan.
Empowered Gatekeeper and transformed-rabbit encounters are states of existing
types, rather than additional roster entries.

Each monster receives a minimum of 16 poses: four idle, four movement,
wind-up/strike/recovery/hurt, and four defeat poses. Completing the remaining
roster adds **544 base poses**, bringing coverage from 112 to **656 base poses**.
The golem's four-pose walking replacement replaces its movement row and does not
increase that count. Extra movement or boss-state poses can be added when a
specific reviewed sequence needs them; 16 is the baseline, not a limit on quality.

The revised golem walk was accepted in the user's latest feedback. Existing bird
and pursuit work remain available in the local build. Before this rollout, the accepted local build passed 569 core and 1,626 graphics
checks. Current complete-roster verification is recorded below.

## Remaining-roster rollout

All thirteen implementation batches are verified: **41 / 41 animated**, with
**656 base poses**. The full checked local Release passes **569 core and 2,162
graphics checks**, with zero build warnings/errors. The earned opening,
separate-process reload, empowered Gatekeeper, final rescues and Maynard ending
all pass with the final assets. The completed local build is open and the goal is complete; no package was rebuilt. [Live rollout progress](evidence/2026-10-07-monster-roster/progress.json)
records each completed batch and its checks.

The completed rollout inspected each batch before expanding its movement
treatment to the next group. The order puts
ordinary movement families before the bosses that depend on them. Counts include
the seven already animated monsters.

| Batch | Scope | Monster IDs and names | New base poses | Animated total afterward |
| --- | --- | --- | ---: | ---: |
| 1 | Early grounded creatures | 7 Mouth Man; 9 Hairy One-Eye | 32 | 9 / 41 |
| 2 | Stone winged creatures | 10 Baby Gargoyle; 11 Adult Gargoyle | 32 | 11 / 41 |
| 3 | Humanoid magic and fire | 12 Evil Wizard; 16 Burning Skeleton; 20 Fire Lord | 48 | 14 / 41 |
| 4 | Small fire and winged creatures | 13 Fire Bug; 14 Flame Flier; 18 Little Daemon | 48 | 17 / 41 |
| 5 | Horse, fur and bird | 15 Cloaked Stallion; 19 Snow Monster; 24 Squawker | 48 | 20 / 41 |
| 6 | Rooted and squat bodies | 17 Venus Man Trap; 23 Tongue Toad; 31 Lump | 48 | 23 / 41 |
| 7 | Wind and hovering spirits | 21 Evil Air Wave; 22 Air Thing; 26 Wisp | 48 | 26 / 41 |
| 8 | Water creatures | 25 Water Ghost; 28 Electric Eel; 29 Merman | 48 | 29 / 41 |
| 9 | Crab and worm | 27 Ice Cave Crab; 32 Earl Earthworm | 32 | 31 / 41 |
| 10 | Late humanoids | 30 Hand Head; 33 Muscles; 34 Axe Man | 48 | 34 / 41 |
| 11 | First regional bosses | 35 Wormoid; 36 Stone King; 37 Flame Beast | 48 | 37 / 41 |
| 12 | Remaining regional bosses | 38 Air Dragon; 39 Soaked Spike; 40 Earth Giant | 48 | 40 / 41 |
| 13 | Final guardian | 41 Gatekeeper, including empowered presentation | 16 | 41 / 41 |

For movement families already established in the game, generate the full sheet
and inspect its compiled movement row first. Revise that row before accepting
the idle/combat/defeat integration. New or difficult body plans may start with a
separate movement strip. This preserves the movement-first review while reducing
generation calls for familiar gaits. Every rendered asset remains native pixel
art in the existing palette; source artwork is compiled with binary alpha and
rendered with point sampling.

## Treatment for every remaining monster

These are presentation designs, not new combat abilities. Attacks illustrate
the existing resolved counterattack and its impact moment. Creature body plans
come from the current three monster atlases, which were inspected for this plan.

| Batch / ID | Creature | Idle and movement | Attack, hurt and defeat |
| --- | --- | --- | --- |
| 1 / 7 | Mouth Man | Jaw breathing/blink; short alternating-foot waddle with a side-to-side weight shift | Jaw draws back, snaps and recovers; flinch closes the mouth; knees buckle into a squat slump |
| 1 / 9 | Hairy One-Eye | Eye blink/fur shift; heavy alternating steps with clear foot lift and plant | Fist wind-up and forward punch; eye squint/recoil; knee drop and fur-covered slump |
| 2 / 10 | Baby Gargoyle | Wing twitch and curious head turn; quick wing-assisted hops with a small shadow beneath | Short claw lunge; wing tuck on hurt; fold wings and settle into a small stone crouch |
| 2 / 11 | Adult Gargoyle | Slow wing flex and breathing; heavier hop/wingbeat with a deliberate landing | Broad claw sweep and braced recovery; recoil; wings sag as the body crumples into stone |
| 3 / 12 | Evil Wizard | Robe breathing, staff/hand movement; short robed strides with visible hem and foot changes | Staff/hand wind-up and magical thrust using the existing hit; recoil; staff lowers and robes fold into a heap |
| 3 / 16 | Burning Skeleton | Restrained flame cycling and rib/head movement; alternating skeletal steps with distinct knees and feet | Arm/claw strike; bones recoil; jointed collapse with flames diminishing |
| 3 / 20 | Fire Lord | Small ember/flame changes and upright breathing; deliberate armored footfalls | Palm/arm wind-up and fiery sweep; braced recoil; kneel and slump as embers settle |
| 4 / 13 | Fire Bug | Antenna/mandible movement and shell highlights; alternating six-leg scuttle based on the beetle treatment | Mandible bite/lunge; shell dips on hurt; legs fold beneath the shell and ember highlights dim |
| 4 / 14 | Flame Flier | Compact wingbeat and restrained flame changes; flight with a detached ground shadow | Wing/beak dive and recovery; flutter on hurt; wings fold and the flame body settles |
| 4 / 18 | Little Daemon | Tail/wing twitch; quick claw-foot steps and brief wing-assisted hops | Claw swipe with counterbalancing tail; flinch; folded wings and a crouching collapse |
| 5 / 15 | Cloaked Stallion | Breathing, ear flick and cloak settling; readable four-leg stepping cycle with cloak lag | Rear and foreleg strike; recoil; forelegs bend and the cloak settles over the lowered body |
| 5 / 19 | Snow Monster | Breathing/blink and fur movement; broad heavy foot lifts and plants | Arm swing; compressed fur recoil; knees buckle into a shaggy heap |
| 5 / 24 | Squawker | Crest twitch, blink and small beak movement; alternating bird steps and short hops | Beak draws back and snaps; crest recoil; crouch and fold into a resting bird pose |
| 6 / 17 | Venus Man Trap | Leaf sway and jaw breathing; visible root lifts and root-to-root scooting when the game accepts travel | Jaw lunge/snap; leaves recoil; stem bows, mouth closes and leaves droop |
| 6 / 23 | Tongue Toad | Throat breathing and blink; crouch, leap and planted landing | Tongue extends and retracts within the cell; body flinch; legs tuck and body settles low |
| 6 / 31 | Lump | Subtle squash/breathing; visible small-foot shuffle and body weight transfer | Compress then bump forward; squash on hurt; flatten into a resting lump |
| 7 / 21 | Evil Air Wave | Changing wind curls around a stable face; smooth hovering swirl with a faint detached shadow | Spiral coils then thrusts; disturbed curls on hurt; unwind and shrink into a final curl |
| 7 / 22 | Air Thing | Curling arm/body movement; hovering drift without a planted-foot gait | Arms draw back and sweep; recoil ripples; curls fold inward and settle |
| 7 / 26 | Wisp | Small flame-tail and core changes; restrained bobbing with a faint ground shadow | Core gathers and pulses forward at impact; tail recoils; flame tail contracts into a small resting ember |
| 8 / 25 | Water Ghost | Flowing arms and watery tail; gliding body undulation with contact treatment matching the actual terrain | Arm/wave thrust; body ripple on hurt; watery silhouette sinks and contracts |
| 8 / 28 | Electric Eel | Small coil and electrical pixel changes; S-shaped body undulation, with terrain-appropriate contact | Coil tightens then lashes forward with a brief electrical accent; recoil; uncoil and settle |
| 8 / 29 | Merman | Breathing, fin motion and trident shift; tail-driven travel, not a human walking gait | Trident draws back and thrusts; fin/body recoil; trident lowers and tail curls into a resting pose |
| 9 / 27 | Ice Cave Crab | Claw/eye twitch; alternating sideways crab legs, facing retained toward travel/action | Claw draws back and snaps; shell recoil; legs fold inward under the shell |
| 9 / 32 | Earl Earthworm | Head sway and body breathing; compression/extension across visible segments | Head rears and lunges; segment recoil; body coils down into a low heap |
| 10 / 30 | Hand Head | Fingers flex and central eye blinks; alternating hunched humanoid steps | Palm draws back and slaps; fingers curl on hurt; legs fold and palm droops over the body |
| 10 / 33 | Muscles | Breathing and small arm flex; heavy, leg-led stride with counter-swinging fists | Fist winds up and punches; braced recoil; knee drop and shoulder slump |
| 10 / 34 | Axe Man | Breathing and small grip adjustment; planted boot steps with axe weight lag | Axe raises, swings and recovers; guarded flinch; kneel and lower the axe within the sprite cell |
| 11 / 35 | Wormoid | Head/maw breathing and coiled segment motion; large compression/extension cycle | Maw coils back and lunges; segment recoil; upright body sinks into the coil |
| 11 / 36 | Stone King | Crown/head shift and heavy breathing; the revised golem's alternating foot-plant treatment with its own silhouette | Heavy stone fist wind-up/strike; braced recoil; crown tilts as the body collapses into stone |
| 11 / 37 | Flame Beast | Slow breathing and restrained ember-crack changes; broad heavy footfalls | Shoulder/arm wind-up and slam; recoil; kneeling collapse with embers settling |
| 12 / 38 | Air Dragon | Wing, head and curled-tail movement; deliberate wing-assisted travel with a readable ground shadow | Wing/neck wind-up and claw/neck lunge; wings recoil; fold wings and curl down into a resting body |
| 12 / 39 | Soaked Spike | Breathing, fin/spike/body movement; weighted footfalls or water contact matching the existing placement/terrain | Braced body/claw lunge; recoil ripples; body folds down while all coral-like spikes stay inside the cell |
| 12 / 40 | Earth Giant | Slow breathing and moss/head shift; large alternating strides and strong planted weight | Arm raises and delivers a broad strike; heavy recoil; knees buckle and the body settles into a rock mound |
| 13 / 41 | Gatekeeper | Armored breathing, key/hand movement and a braced stance; heavy armored steps wherever accepted movement exists | Key/fist wind-up and broad strike; guarded recoil; armor folds into a grounded collapse. Empowered presentation adds stronger stance/ember accents to the same resolved timeline |

## Implementation rules and dependencies

1. **Define the movement before generating a full sheet.** Review a native-size
   movement strip for each creature. Grounded walkers must visibly lift one foot
   while another supports the body, transfer their weight and plant the next
   foot. Use the golem's successful swing/plant alternation for suitable bipeds.
   Quadrupeds, many-legged creatures, worms, roots, tails and fliers get distinct
   cycles matching their anatomy. A changing torso on sliding feet fails review.
2. **Keep the current art contract.** Use the existing atlas identity as the
   reference, one body scale, shared palette, binary alpha, native 48-pixel cells
   and original ground anchors. Keep every wing, tongue, axe, staff and spike
   inside the cell. Preserve separate source art and exact generation/edit
   prompts. Use a dedicated replacement strip for focused revisions when it
   preserves already accepted poses, as the golem revision demonstrated.
3. **Extend existing rendering at the point it is needed.** Reuse
   `MonsterMotion`, `WorldMonsterAnimation`, existing actor identity/reset logic,
   the alternating-step flag, battle clocks and action snapshots. Add only the
   particular pose timing, flight offset or terrain contact that a batch needs.
   Keep ordinary accepted travel at 250 ms. Authored stationary placements
   animate idle/action poses in place; they do not acquire movement through this
   presentation work. Bosses that occupy 2 × 2 tiles keep that footprint and
   their original anchors when rendered at the existing larger scale.
4. **Use actual terrain and existing movement permissions.** Types 25–29 can
   travel on water as well as their ordinary allowed ground tiles. Choose wet
   contact or dry contact from the actual tile. Do not expand water access for
   other creatures or add earth dust beneath hovering bodies. Animated roots
   illustrate an accepted move rather than immobilizing the Venus Man Trap.
   Include land-to-water and water-to-land movement previews, checking mid-step
   contact, shadows and fog at the boundary rather than only each terrain in
   isolation.
5. **Integrate the existing battle result.** Idle, attack, recovery, hurt and
   defeat poses follow the existing resolved turn and sound/HP timeline. Staff,
   flame, electrical and tongue accents are visual treatments of that result.
   Extend the current action snapshot with a needed variant flag only when the
   empowered Gatekeeper presentation requires it. Preserve independent enemy
   slots, direct targeting, inspection layout, replacement transformations,
   defeat accounting, Auto pacing and rewards.
6. **Handle the special guardian rendering path.** Gatekeeper has an explicit
   waiting-guard draw beside the underworld exit, separate from regular monster
   placement rendering. Animate that path too while preserving the unobscured
   face and key, recessed arch, fog mask and quest-state visibility. Test ordinary
   and empowered encounters and the final rescue/ending handoff.
   In empowered encounters, verify all difficulty-dependent companion groups:
   Flame Beast (37) plus Air Dragon (38), Soaked Spike (39) or Earth Giant (40).
   Inspect all three battle slots together during strikes and defeat, keeping
   silhouettes, HP labels and targeting cues clear. The 2 × 2 world footprint
   does not enlarge the existing battle portrait bounds.

## Per-batch workflow and completion criteria

1. Inspect the existing identity and a real placement/encounter. Define each
   creature's movement sequence, attack silhouette and final resting pose.
2. Generate and compile the movement preview first. Inspect it at actual world
   size in a continuous walk with a turn, then at battle size. Resolve missing
   foot lift/plant, clipped anatomy, body-scale drift or unreadable motion before
   expanding to all sixteen poses.
3. Finish the idle/action/defeat rows and connect them through the existing asset
   registration and presentation. Keep timing for each creature purposeful:
   faster small creatures, slower heavy creatures, restrained background idles.
4. Run the checked local Release build and existing core/graphics checks once the
   batch is integrated. After the full baseline checks, ordinary art batches use
   the focused monster/combat graphics mode; the final roster runs the complete
   graphics suite and earned routes. Expand the existing roster checks for new sheets; add a
   focused regression only for a new behavior or boundary. Verify all poses are
   nonempty native art, correctly anchored and contained, with distinct cycles.
   The current static-art fallback check uses Mouth Man (7); move that fixture to
   an unanimated type or NPC when batch 1 adds Mouth Man, and retain NPC fallback
   coverage after all monsters are animated.
5. Verify forward, mirrored and diagonal travel; independent actors; pauses and
   modal panels; fog/offscreen clipping; actor/map reset; pursuit cues;
   approach-to-battle timing; real counterattack/hurt/defeat; three-enemy battles;
   and unchanged full gameplay, battle state and RNG. Include water contact,
   hovering shadow, long appendage, large footprint or variant checks as relevant
   to that batch. Keep inspection stationary and readable.
6. Retain the native sheets, continuous movement/turn clip, battle/defeat clip,
   build/check logs and source provenance. Update this plan and `PORTING_PLAN.md`,
   then open the local build for visual review. Build locally during iteration;
   packaging remains a separate milestone.

A batch is complete when its art and behavior pass those checks and its movement
reads correctly in the captured game view. Incorporate user visual feedback
throughout the rollout. If a batch exposes a shared rendering or
timing issue, fix that boundary and rerun its relevant checks before continuing.
Review requests are concrete clips and runnable builds, rather than proposed art
that has not been integrated.

Antigravity can help with bounded creature briefs, pose prompt drafts, focused
test ideas and small candidate patches within a batch. The primary agent reviews
each material result against the actual assets/code, integrates accepted changes
and owns visual inspection and validation. Work is delegated when it reduces
effort, with the batch and writable paths specified rather than a broad request
to redo the roster.

The user also requested Antigravity for animation artwork. The installed CLI
exposes an `image-generator` specialist through `invoke_subagent`; its main Flash
session does not expose a direct `generate_image` tool. A Gemini 3.8 Flash Low
test produced a Mouth Man art candidate in 54.4 seconds. Its JPEG output had a
painted checkerboard, ground marks and the wrong grid, so it remains a preview.
Use this route for bounded sprite-art candidates when useful, with primary
checks for exact layout, real alpha, palette, anchors and actual gait before
integration. [Capability test and source prompt](evidence/2026-10-07-antigravity-art/README.md).

## Final roster verification

- Confirm IDs 1–41 each have a complete registered animation sheet, with no
  duplicate IDs, omissions or monster types falling back to a static world or
  battle portrait. NPCs may retain their separate existing treatment.
- Inspect a contact sheet and world/battle animation gallery for all 41 at native
  size. Check silhouette, body scale, outlines, foot/contact anchors, tempo and
  effect density across the roster. Exercise multi-enemy battles and all six
  regional bosses with their actual companions.
- Run the full core/graphics suites and the earned opening route. Run the earned
  late route through empowered Gatekeeper, final rescues, save/restart and ending.
  Repeat these earlier within the rollout when a shared timing/rendering change
  affects their boundary; asset-only iterations use their batch checks.
- Confirm startup/loading and steady play remain comparable to the existing
  baseline. Texture creation stays at content load; drawing creates no new
  textures per frame. Keep optional presentation settings and menu/map behavior
  consistent. Record personal save/profile preservation through isolated checks.

Completion target: **41 / 41 animated monsters**, all ordinary and boss paths
covered, current gameplay/save behavior preserved, and a local build ready for
the later distributable milestone. All thirteen batches and final native art cleanup are verified; retained evidence
and current coverage are recorded in the rollout above.

Plan audit: the live asset registry, canonical monster definitions, batch table
and individual creature treatments were compared. All 34 remaining IDs occur
exactly once in both tables; names, 13-batch totals, 544 new base poses and 656
final base poses agree. A focused Antigravity review led to the explicit wet/dry
boundary and empowered-companion checks above. Its palette-allocation suggestion
was excluded: `RetroArt` already uses one fixed palette with nearest-color
compilation, so it has no per-creature slots to exhaust. The primary review
confirmed terrain permissions, companion composition and fixed battle bounds
directly in the current code.

## First-pass scope and acceptance (completed)

1. Animate accepted roaming and pursuing steps over the same 250 ms used for the
   player. Track the two live actors separately, maintain facing, reset on actor
   replacement and map changes, and snap relocations. Pause and modal panels freeze
   travel. A pending encounter opens after the visual approach finishes.
2. Supply 16 real pixel-art poses per selected creature: four idle, four movement,
   wind-up/strike/recovery/hurt, and four defeat poses. Preserve each identity and
   compile to the existing palette and native 48-pixel cells, with a shared body
   scale and the original ground anchor. Keep source artwork in the project.
3. Use creature-specific timing and movement: occasional rabbit ear twitches and
   blinks, a hopping step, beetle antenna/leg cycles and a scuttle, bat flapping
   and hovering. Keep ground shadows under hops and flight. Add brief restrained
   dry-ground landing dust to rabbit and beetle travel.
4. Connect idle poses to battle commands and synchronize attack, hurt and defeat
   poses with the existing impact/sound/HP timeline. Keep readable stats, target
   cues, and reward presentation. Completed action presentations settle cleanly.
5. Verify complete sheets, anchoring, facing, independent travel, pause/modal
   behavior, map/spawn/removal boundaries, approach-to-battle timing, native pixels
   and gameplay/RNG preservation. Run the existing core/graphics suites and earned
   opening replay. Inspect native art, exploration motion and battle captures;
   open the local game for review.

## First-pass implementation (completed)

- `MonsterMotion` owns only visual travel for an existing actor. `Adventure` still
  owns destinations, movement choices, encounters and combat.
- `WorldMonsterAnimation` loads and normalizes the three sheets once, selects
  poses, and draws movement, shadows and contact effects. Other monsters use
  their original artwork with the same smooth travel.
- The existing command synchronization starts the visual steps. Exploration
  updates advance them; pending interactions wait for arrival.
- Battle rendering selects poses from the existing resolved-action snapshot.
  Combat menu idles use the overlay clock; inspection remains stationary.

## First-pass verification

- Implementation complete. All 48 poses, native palette/anchors, distinct cycles
  and mirrored facing were verified. Exploration travel and real combat/defeat
  captures were visually inspected.
- Checked local Release build: zero warnings/errors; 569 core and 1,575 graphics
  checks pass. The opening replay matches all 42 C++ checkpoints plus the King
  reward, 116 victories, 14 chests and two rescues, with a fresh-process save/load
  continuation. The replay now waits for all visible actor travel before sending
  its next scripted input; the expected gameplay fixtures are unchanged.
- [Evidence and animation preview](evidence/2026-10-07-monster-animation/README.md)
  retain the logs, native sheets and movement/combat/defeat clips. A local build
  is ready for review; no package was rebuilt.

## Second batch: Attack Weasel and Mystic Moose

The user authorized expanding the style to the weasel (3) and moose (4). Both now
have 16 poses following the same sheet contract. The weasel has faster idle
timing, a low running stride and a biting pounce. The moose has slow breathing,
weighted travel that settles at its hoof plant, an antler thrust and slightly
stronger ground contact dust. All accepted steps still finish within 250 ms.

Validation reuses the existing pose/movement/combat checks for all five animated
types, adding coverage for the moose's gentle start/settle and unchanged step
duration. Asset registration also drives authored-world rendering and defeat
timing, avoiding separate lists of animated species.

The second batch passes the checked local Release build with zero warnings/errors,
569 core checks and 1,591 graphics checks. Native sheets, world motion and real
combat/defeat captures were visually inspected. [Evidence and preview](evidence/2026-10-07-weasel-moose/README.md).

After the second batch, the roster had 80 poses across five monsters. The other
36 monsters and three NPC atlas entries retained their original artwork.

## Third batch: Ancient Seagull, Stone Golem and pursuit alert

The user authorized bird (6), golem (8) and the pursuit cue. Each creature has
16 authored poses in the existing four-row sheet layout. The bird turns its
head, stretches its wings, makes short wing-assisted hops and pecks. The golem
shifts slowly, takes eased heavy steps, braces for a punch and collapses into
a stone heap. Its dry-ground landing produces brief gray contact dust, including
underground. Travel still arrives within 250 ms.

The alert is an outlined amber `!` on the first accepted visible step that reduces
the creature's distance to the hero. It follows the animated position, rises
slightly and fades within 0.7 seconds. An actor-local latch suppresses repeats
for the rest of that visible chase. Spawns, movement away, hidden terrain and
offscreen approaches stay quiet. Replacement, map changes, relocations and loss
of visibility clear the cue. Pause, exploration panels and maps freeze its clock;
the glyph is hidden during those views. The existing encounter rules and battle
handoff are unchanged. No awareness state is added to gameplay or saved data.

The cue draws on the sharp presentation layer above heat, lighting and miniature
effects, before menus. Individual symbol pixels are clipped to discovered
terrain. Ground anchors and original sprite heights position the cue consistently
for animated creatures and the remaining original-art roster.

Validation expands the existing art/travel/combat/defeat checks to all seven
animated species, adds weighted golem travel coverage and exercises cue timing,
reset, visibility, fog, offscreen, pause/modal/map and state/RNG boundaries.
Native sheets, world motion, real battle/defeat and pursuit captures were inspected.
The roster now has 112 poses across seven monsters; 34 monsters and the three
NPC atlas entries retain their original artwork.

The third batch passes the checked local Release build with zero warnings/errors,
569 core checks and 1,621 graphics checks. The earned opening replay again matches
all 42 C++ checkpoints plus the King reward, 116 victories, 14 chests and two
rescues, including its fresh-process save/load continuation. Expected gameplay
fixtures remain unchanged. [Evidence and preview](evidence/2026-10-07-bird-golem-alert/README.md).

## Golem walking revision

The user found that the golem did not read as walking. Its four movement poses
now show a visibly raised stone foot and dark sole, a planted support leg and
clear alternating leg silhouettes. A separate 2 × 2 walking sheet replaces
only the original movement row; the other twelve compiled poses were verified
pixel-identical to the preceding batch.

Each accepted 250 ms move now plays one swing/plant pair. The leading foot
alternates between moves, including diagonals and turns, and the plant holds
for 120 ms after arrival before returning to idle. The existing eased travel,
ground shadow, dust, pause/modal clocks and encounter handoff remain intact.
One visual boolean on each existing motion object tracks the leading foot and
resets on actor replacement or resynchronization.

The checked local Release build has zero warnings/errors. All 569 core and
1,626 graphics checks pass. Five additional checks exercise the actual rendered
swing/plant pairs, unchanged-tile synchronization, independent actors, replacement
resets and full session/RNG preservation during continuous strides and turning.
The native sheet and a four-second walking capture were visually inspected.
The preceding earned opening replay remains the gameplay reference; it was not
repeated for this pose/timing revision. [Evidence and walking preview](evidence/2026-10-07-golem-walk/README.md).

## Separate NPC pass — 2026-10-11

The King, Skill Teacher and all three Princess appearances now have a separate
completed [NPC animation pass](NPC_ANIMATION_PLAN.md), adding 60 native poses.
The 41-monster roster and its 656 poses are unchanged.
