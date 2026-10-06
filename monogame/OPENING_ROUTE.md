# Opening adventure reference route (P2.1)

The P2 target is the fresh Combat/Medium, seed-1 route in
[`tests/app_shortcut_tests.cpp`](../tests/app_shortcut_tests.cpp), from the
`walk_route`/`train_route` block near line 2130 through checkpoint 41 near line
2538, followed by Return to Surface and ordinary visits to the King. It begins
at Maynard `(5,5)`, level 1, zero gold/XP, rank-2 weapon/armor, starting food and
Return to Surface. No debug grant, map warp, loaded checkpoint or injected flags
may establish P2 acceptance.

| Stage | Ordinary actions and reference checkpoints |
| --- | --- |
| First supplies | Enter town cave `0x39`; chests/encounters reach `(2,8)` with 53 gold and `(11,1)` with 216. Return and buy weapon/armor 3, leaving 7 gold at town `(7,5)`. |
| North-field training | Explore `0x0b` through field/border loops, buy food, Sandals, Cheap Ring and Plain Necklace; weapon/armor 4 and Small Heal. Town `(5,4)` reaches level 5, 359 gold, equipment 4/4. |
| Cave supplies | Follow `0x39 -> 0x3f -> 0x45`, collect chests and use Other at the 180-gold coin pile. Reach `0x45 (11,10)`, level 5, 1,028 gold; return and buy weapon 7/armor 5, leaving 63. |
| Prepare rescue | Continue earned field battles; buy Gobbler Attack, its successor Insect Attack, armor 6 and food. Town `(5,4)`: level 7, 19 gold, equipment 7/6. |
| First southern entry | Follow `0x17 -> 0x18 -> 0x12 -> 0x3a`; fight Mouth Men and the first Stone Golem. Return to Surface and use Other on the flower passage, then return for a cheeseburger and fries, leaving 1 gold. The flower passage resets on map reload. |
| Rescue | Re-enter by the southern route, pass cleared guards, defeat paired/roaming golems and follow `0x3a -> 0x34 -> 0x33`. At `0x34 (8,8)`: level 8, 247 gold. Rescue slot 2 at `0x33 (4,3)` and choose Mystery: level 8, 349 gold, weapon 7/armor 6, 59/59 HP; one rescue and one mystery use, no Rainbow/Underworld key. |
| First stamp | Continue the retained route through `0x45 -> 0x4b -> 0x73 -> 0x6d`, buy Backstab, train in earned town-cave battles and buy weapon 10/armor 8. Defeat Wormoid at `0x6d (5,5)`: level 9, 1,378 gold, first stamp. Rescue slot 1 at `(8,8)` with Mystery: level 10, 1,738 gold, 5,008 XP, two rescues/mystery uses, no keys. |
| Return and report | Return to Maynard through normal movement/learned spells. The creation introduction changes no flags and consumes no RNG. Actual King visits first set quest flags 0 and 1; a subsequent visit with the first stamp sets flag 2 and grants 500 gold (message 733). Each visit draws a greeting. The fresh continuation casts Return to Surface, crosses the bridge via `3 66666666 9`, and enters the castle with `8` three times. Final town `(11,2)`: 2,238 gold and quest flags 0–2 set. |

The retained C++ test supplies real movement and dialogue/combat inputs, earned
purchases and explicit elapsed exploration ticks for regeneration. Its historical
71-encounter/seven-chest first-rescue and 116-victory/14-chest extended-route figures and expected checkpoints are **reference
expectations**, not fresh MonoGame verification. They have now been reproduced through the C# Adventure rules with full persistent-state/RNG equality, including fresh-process reopening before Wormoid. The real frontend now matches all 42 checkpoints and the King return, including a separate-process save/reload continuation; see [completion evidence](evidence/2026-10-04-p234-complete/README.md). Current approved walking animation
is 250 ms; logical exploration timer cadence and pause behavior must match C++.

Required systems: shops/selling/accessories, healing and food, sequential spell
learning, physical and spell combat, static/roaming encounters, loot and level-up,
map visibility and transitions, exploration regeneration, 14 one-time chests, Backstab training, Wormoid and the first stamp,
Other actions, princess choices, King dialogue and full-state restart. No late-game
key, climbing shoes or empowered Gatekeeper victory is required for this route.

P2.8 must save in an earned intermediate state, reopen in a separate process and
finish the rescue/return. Preserve command input and normalized state/RNG evidence;
presentation-only animation differences must not alter gameplay draws. A synthetic
controller replay can verify navigation, while physical-controller verification
must remain explicitly unclaimed unless it is actually performed.
