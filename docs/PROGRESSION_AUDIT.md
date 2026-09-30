# Fresh-game progression audit — 2026-09-22

**Current scope, 2026-09-30 (E084–E085):** all four new full fresh-game
fixtures complete on the E081 corrected combat build. Combat/Medium/1,
Quickness/Easy/2, Magic/Hard/3 and Random/Medium/4 earn every rescue, all six
stamps, movement prerequisites, STR 40+, empowered Gatekeeper victory and the
live town finale. Each author and separate fresh replay exits 0. All 108,879
state/RNG observations match exactly, together with command, full-trace and
final-save bytes. No observation checkpoint is loaded into these runs.
See [the corrected completion record](../evidence/corrected-completion/README.md)
and [current matrix](../evidence/verification-milestone/completion-matrix.json).

The E074–E080 fixtures below are preserved historical evidence for the E077
build; their earlier [matrix](../evidence/corrected-completion/historical-completion-matrix.json)
remains byte-identical. E082's eighteen earned native restart cases and five
continuations remain separate evidence. E085's clean Release suite passes 7/7
in 49.36 seconds; packaging is recorded in
[package.json](../evidence/corrected-completion/package.json).
Original GUI parity, balance and wider P8/P9, manual user play and manual
save/restart/import remain open. Native game/settings formats remain 20/1.

**Connected completion proven, 2026-09-29 (E074):** a fresh default
Combat/Medium, seed-1 production `run_game` earned all 20 rescues, all six stamps,
the Rainbow and Underworld keys, waterproof spray, climbing shoes, and strength
42. It defeated the empowered Gatekeeper, returned to town, displayed the finale
victory message, and ended alive at level 42 with Gatekeeper state 3. Resources
came from ordinary combat, loot, shop transactions, and regeneration. Eight
ordinary deaths and their Hell recovery are part of the connected run.

The input-only harness never loaded observation snapshots into the live game.
Its original 42 E066 checkpoints remain unchanged. The winning command stream
and complete observations are retained in `full-game/attempt-12`; earlier
attempts retain failed or interrupted prefixes. This is automated rebuild
progression evidence, not an original-runtime comparison or user play session.
The fresh replay, validation results, and package checks are recorded in
[`full-game/README.md`](../evidence/progression-audit/full-game/README.md).

**Verification update, 2026-09-29 (E075):** all seven Release tests now pass
after the sandbox was disabled, including native history write/read/clear. A
fresh default replay still matches all 15,367 state observations after the test
harness changes. `--full-game-variant TYPE DIFFICULTY SEED` starts through
production character creation with zero gold/XP and no progression flags.
Additional completion routes are being authored; retained Quickness/Easy seed-2
attempts are incomplete. The [original probe](../evidence/verification-milestone/original-probe/README.md)
matches 13,732 bounded rule cases, without establishing a visible original
playthrough or broader P8/P9 completion.

**Additional completion, 2026-09-30 (E076):** Quickness/Easy seed 2 earns all
twenty rescues, six stamps, movement prerequisites, STR 40, and the finale in
one connected production run. Seventeen ordinary deaths remain in its input
stream. A separate fresh replay exits 0 and matches all 15,996 state/RNG
observations, ending alive at level 52 with Gatekeeper state 3. The default
Combat/Medium/1 fixture still matches all 15,367 states after the test-only
physical combat policy. Magic/Hard/3 is being authored; Random/Medium/4 has
a level-46 earned-resource training checkpoint with an exact 34,420-state fresh
literal-input prefix replay; neither case has a completed fixture yet. See the [variant evidence](../evidence/verification-milestone/variants/README.md).
No production gameplay change or broader P8/P9 completion is claimed.

**Map repair and Magic completion, 2026-09-30 (E077–E078):** the earned
Magic/Hard seed-3 route exposed an omitted original room minimum-Y clamp and
stack overflow at generated map 120. Restoring the single clamp matches all
20,280 tile/RNG records across 130 original-machine-code grids, passes address
and undefined sanitizers, and preserves the default and Quickness fixtures
exactly. All seven Release tests pass in both existing and clean builds.
Magic/Hard now completes all prerequisites and the finale with thirteen ordinary
deaths; its independent fresh replay exits 0 and matches all 36,191 states.
Final level 55, STR 41 / DEX 42 / INT 50, Gatekeeper 3. Random/Medium remains
in progress; its thirteen-rescue/four-stamp checkpoint preserves all 35,526
states on the repaired build. Native game/settings versions remain 20/1.
See the [map evidence](../evidence/verification-milestone/generated-map-clamp/README.md)
and [Magic completion](../evidence/verification-milestone/variants/magic-hard-seed3/attempt-06/README.md).

**Additional-character matrix complete, 2026-09-30 (E079):** Random/Medium
seed 4 earns all twenty rescues, six stamps, prerequisites and the finale with
two ordinary deaths. An independent fresh replay on a separate clean Release
build exits 0 and matches every one of 36,072 states, the entire trace, command
bytes and final save bytes. Final level 54, STR 41 / DEX 40 / INT 50, Gatekeeper 3.
The four completed fixtures total 103,626 exact replay observations after E077.
No observation snapshot is loaded into a live run. Original GUI comparison,
manual persistence and broader P8/P9 remain open. See the
[Random record](../evidence/verification-milestone/variants/random-medium-seed4/attempt-02/README.md)
and [historical matrix](../evidence/corrected-completion/historical-completion-matrix.json).

**Corrected package, 2026-09-30 (E080):** the separate clean Release build
passes all seven tests in 44.80 seconds. Its development archive includes the
original room-clamp repair and current documentation. Independent extraction
confirms 137 assets, bundled SDL2/relative dependencies, ad-hoc signatures and
headless startup outside the checkout. The earlier E074/E075 archives remain
preserved. Native game/settings formats remain 20/1. The bounded verification
milestone is complete; original GUI, manual persistence and broader P8/P9 remain
open. See the [final package record](../evidence/verification-milestone/generated-map-clamp/package.json).

**Current status, 2026-09-23 (E062-E066):** all five baseline findings are addressed,
and one continuous fresh-game route rescues princess slots 2 and 1 and collects
Wormoid's stamp using earned resources. Both Mystery rewards leave the Rainbow
Key unearned.

The Underworld Key is awarded before quest advancement; all 145 chest slots are
extracted, rendered, and collectible, including waterproof spray; and cleared
Quizilog shops award tree-climbing shoes. Focused core/SDL cases cover earned
flags, both shop entrances, reward branches, chest rendering/removal, RNG order,
full inventory, generated maps, and opened-state persistence. E063 restores all
four miscellaneous catalogs and the price milestone. It also establishes a
second Rainbow Key acquisition path: Outback sells it at stage 2 and level 26
for `(difficulty + 6) * 500` gold. The original store exit advances a purchase
to stage 4, so ordinary visits restore its equipment row. These cases do not
prove a connected fresh-game route or normal combat balance on their own. E064 resolves
finding 4 through the explicit D8 stat/loot policy below. Save/restart/import
verification sessions and user play remain deferred.

**Priority update, 2026-09-24:** unfinished features and known differences from
the original now lead implementation. E065-E066 remain regression evidence;
further route automation is reserved for a concrete progression blocker or
final completion work. The route gaps below are not prerequisites to resuming
feature work. E067 restores mouse exploration and fixes scaled mouse input;
E068 implements the portrait statistics panel and its navigation, and E069
resolves single-key semantics, missing key mappings, and customization conflicts.

### Connected first rescue (E065)

E065 established an uninterrupted `run_game` in `tests/app_shortcut_tests.cpp`
from the default Combat/Medium character in town `(5,5)`, RNG seed 1, zero gold/XP,
rank-2 weapon/armor, and the ordinary starting food and Return to Surface spell.
It completes **71 encounters**, opens **seven chests**, buys equipment and
spells, and rescues **slot 2 on map `0x33` at `(4,3)`** without a death.
At this milestone the character is level 8 with 349 gold, weapon 7, armor 6,
and 59/59 HP.
Mystery grants full HP/SP restoration and increments its counter to 1; it does
not grant a Rainbow Key in this route.

| Stage | Earned progress |
| --- | --- |
| Town, `0x39`, north field `0x0b` | First chests and battles fund rank-3 equipment, food, and accessories; a chest's strength potion raises STR by one |
| North-field outings and town shops | Earn level 5, rank-4 equipment, and Small Heal; healing services spend actual gold |
| `0x39` -> `0x3f` -> `0x45` | Collect the remaining route chests and search the 180-gold coin pile; return to town and buy weapon 7/armor 5 |
| More north-field outings | Reach level 7; buy Gobbler Attack, its successor Insect Attack, armor 6, and food, leaving 19 gold |
| `0x17` -> `0x18` -> `0x12` -> `0x3a` | Defeat two Mouth Men and the first Stone Golem; use Return to Surface and the flower passage to return for a cheeseburger and fries, leaving 1 gold |
| Southern route -> `0x3a` -> `0x34` -> `0x33` | Revisit the cleared guards, defeat the paired golems and another roaming golem, then rescue the princess and receive Mystery healing |

The flower passage lasts only for the current map instance, so the return
expedition uses the southern route again. No terrain or production gameplay
changes were needed. The test supplies SDL directions, dialog selections,
combat inputs, and elapsed exploration time. A read-only player observation
selects healing/attack inputs; production code creates every encounter and
resolves every attack, purchase, reward, and RNG call. Stats, gold, inventory,
locations, quest flags, and encounter thresholds are never injected after
default construction. There are no casino winnings or suppressed encounters.

Twenty-four temporary native snapshots assert exact locations, levels, balances,
and equipment. Additional assertions cover starting defaults, the potion and
accessories, learned spells, cave coins, the exact seven chest flags, permanent
guard defeats, unchanged difficulty/encounter settings, and the rescue/reward.
Snapshots are read only for observation and never reloaded into play. All dialog
choices must be consumed; combat inputs and dialog searches are bounded.

Reproduce from the checkout root:

```sh
cmake --build build --target yipe_app_shortcut_tests --parallel 4
ctest --test-dir build -R '^yipe_app_shortcut_tests$' --output-on-failure
```

The E065 targeted SDL suite passed all 169 cases in about 20 seconds locally.
This is one seeded route with scripted time and input, not full progression, a balance
survey, an efficient leveling recommendation, or original-runtime parity. Its
174 bounded training outings and waits deliberately allow ordinary income and
SP regeneration. E066 continues the same route below. E074 subsequently proves
the remaining movement items, Outback Rainbow Key, and finale in this same
connected route; other classes/seeds and the alternative Mystery key path remain open.

### Connected second rescue and first stamp (E066)

The same `run_game` now continues from slot 2 through **slot 1 on `0x6d` at
`(8,8)`**. It defeats Wormoid and its two Giant Beetle companions, earns the
first stamp, and reaches level 10. The complete route totals **116 victories,
14 chests, two rescues, and zero deaths**. The second Mystery reward grants
**300 gold**, taking its counter to 2 without awarding a Rainbow Key.

| Stage | Earned progress |
| --- | --- |
| `0x33` chamber -> surface -> town | Collect the chamber chest; use two Intelligence Potions and one Dexterity Potion; return through the flower passage and buy a cheeseburger |
| `0x39` -> `0x3f` -> `0x45` -> `0x4b` | Collect both `0x45` chests, gain weapon rank 8, defeat guards, buy Backstab for 150 gold, and collect the `0x4b` chest |
| `0x73` -> `0x6d` | Collect all three `0x73` chests, gain armor rank 7, and reach `(3,10)` before returning to town |
| Twenty short town/`0x39` outings | Fight the respawning bat near `(10,2)`, rest, and cast Return to Surface; reach level 9 and 2,671 gold, then buy weapon 10, armor 8, and two cheeseburgers, leaving 721 gold |
| Cleared caves -> `0x6d` | Defeat Wormoid at `(5,5)` and record its permanent placement defeat, type-35 kill, and stamp; defeat another roaming group and rescue slot 1 |

The seven additional chests are `0x33 (4,1)`, `0x45 (5,5)` and `(5,7)`,
`0x4b (11,8)`, and `0x73 (11,10)`, `(11,4)`, and `(1,4)`. Final state is
level 10, **5,008 XP, 1,738 gold, 65/71 HP, 18/54 SP**, weapon 10/armor 8,
and STR/DEX/INT 19/15/12. Backstab remains learned, stamp count is 1, and
Gatekeeper state is 0. Neither key is present.

All 24 first-rescue checkpoints and their guarantees remain in place. E066
adds 18 checkpoints for **42 exact observations** of position, level, gold,
and equipment. Assertions also cover both rescue slots and reward messages,
the Mystery counter, potion growth, paid ability training, all 14 chest flags,
Wormoid's defeat/stamp transition, final resources, and unchanged starting rules
and survival across every checkpoint. Observations never feed back into play.
The combat input policy uses Backstab whenever it is learned and has at least
three AP; production combat still controls its cost and counterattack behavior.

The same reproduction command passes all **169 SDL cases in about 39 seconds**;
the existing route case is extended, so the case count is unchanged. Its CTest
timeout is now 90 seconds; the separate smoke timeout remains 30. No production
gameplay or save-format changes were needed. The extension adds 20 training
outings to E065's 174 and retains real regeneration and encounter rules.

Remaining connected coverage includes further Mystery rewards and traversal
with an earned Rainbow Key, Outback's level-26 purchase, other rescues/stamps
and movement items, and the finale. Extend this route when needed to investigate
a blocker or establish final completeness. This remains one seeded rebuild
route, not full progression, a balance survey, or original-runtime parity.

### Stat/loot policy and earned strength (E064)

New characters and legacy imports now use recovered mode 2: strength, dexterity,
and intelligence can grow to 50, and random weapon upgrades can reach rank 29.
This is a rebuild adaptation consistent with D5's all-supplied-content scope.
Creation and player status display the stat limit. Native saves retain their
stored mode: 0 caps stats at 30, 1 at 40, and 2 at 50; modes 0/1 retain random
weapon upgrades through rank 23. Existing higher stats/equipment are not reduced.
An older native character with mode 0 still cannot grow to strength 40.

Original `0x00445450` (levels) and `0x00445750` (items) apply these three stat
caps; `0x00445c20` applies the mode-dependent weapon ceiling. The selectors
`0x00412460` and `0x004123e0`, called by `0x0040d610`, set the mode from the
application's registration profile. The corrected `0x00443c90` serializer
omits player `+0x654`, so a `.yp3` cannot establish that original mode. Imports
deliberately use the rebuild default; native formats already contain the field.
No registration algorithm or original-binary change is part of this adaptation.

The linked-princess core case now grows a fresh Combat character from strength
16 to 40 by buying and consuming **24 potions**, using the real shop-entry,
stock, purchase, and item APIs. It supplies **30,000 gold**: twenty potions at
1,000 and four at 2,500 after the stat-total milestone. At earned strength 39,
the mechanism remains disabled; at 40, the unchanged map rule enables it.
The route then crosses `0x2d` to `0x2e`, opens the inner wall, passes the keyed
door, and records rescue slot 13. Funds, entry location, and the Rainbow Key
are supplied in this isolated case. E074 subsequently earns its prerequisites
and survives the connected route from town.

Focused cases cover potions and level growth below, at, and above every cap for
all three stats; weapon-loot boundaries for all modes; native mode retention;
legacy conversion; all four SDL creation choices; and the displayed saved limit.
The current diagnostic also reaches strength 50 and enables the gate through
funded purchases. This establishes the growth path, not ordinary-play income.

The findings below record the **E061 baseline before implementation**. That audit
changed no production gameplay or access policy and required no existing saves
or user play session. Its missing-writer findings and saved probe output are
historical evidence, not claims about the current checkout.

## Findings, in implementation priority order

### 1. Underworld Key is never awarded (P1)

`src/encounter.cpp`, `finish_encounter_victory`, checks `has_underworld_key`
before changing Gatekeeper state 1 to 2. No gameplay writer sets this flag.
Fresh players therefore cannot satisfy the finale prerequisite even after
winning the appropriate fight. Eight static Underworld Key doors also remain
locked. The existing `test_quest_progression_events` supplies the key before
testing the victory, hiding the missing acquisition.

Original `0x00405210` awards the key on a type-41 victory when document
`+0x0d2` (player `+0x082`) is zero. It selects resource 134, writes 1, and
skips the ordinary equipment-drop branch. The caller `0x00411180` then tests
the newly written key and changes state 1 to 2. This is an ordering dependency,
not a circular quest requirement. The preserved machine-code excerpt confirms
the award at `0x00405308`.

**Next change:** restore the first-victory key reward and its loot-branch
ordering, expose its message, and test both an early Hell victory and a victory
after the Earth Giant without preloading the key. A repeat victory must not
repeat the first-key reward.

### 2. Chest placements and rewards are absent, including shoe spray (P1)

The recovered map initializer `0x0041ec40` writes **145 chest placements on
75 map IDs** directly through its first argument. These are four `(x,y)`
slots at `CMap +0x310`; they are separate from the helper calls extracted by
`tools/extract_map_tables.py`. There are 143 placements on 74 static maps and
two on generated map `0x78`. The production `MapEvents`, renderer, movement
dispatch, and state model omit this chest collection.

Original movement dispatcher `0x0040e880` checks those four slots after
movement, removes the opened chest, and invokes `0x004450d0` for XP/gold and
the player-side opened flag at `+0x14e + map_id*4 + slot`. It calls
`0x00445c20` with chest-kind values 1–4 for equipment/item rewards. Critically,
**map `0x63`, slot 1, position `(9,1)` awards waterproof shoe spray** before
the random loot branch. The original write to player `+0x064` is confirmed at
`0x00445cc4`. In the rebuild this field is only initialized or loaded; there is
no gameplay award. Thirteen static stream cells consequently remain blocked
for fresh characters. Missing ordinary chests also remove a source of gold,
XP, equipment, and consumables.

**Next change:** extract the four chest slots, preserve their original indices,
and implement rendering, one-time collection, XP/gold, and reward selection.
Exercise the actual spray chest through movement without injecting the flag.
Full inventory, repeat visits, generated-map chests, and original RNG order
need focused cases. Durable chest-state design is part of implementation;
save/restart/import verification remains deferred.

### 3. Map `0x08` shop reward never grants tree-climbing shoes (P1)

Original movement actions 5 and 8 (weapon and miscellaneous shops) have an
extra branch on map `0x08`. `0x0041d660` checks whether any of five static
monster slots remain active. After the map's four authored groups are cleared,
visiting either shop grants document `+0x6c4` (player `+0x674`), the
tree-climbing-shoe flag, and shows dialog 421's reward variant before opening
the shop. An uncleared map shows its other dialog variant. No princess,
waterproof-spray, or pre-existing-shoe condition is part of this reward test.

The rebuild's action-5 branch opens the shop directly. Action 8 outside town
only displays a placeholder. Neither grants the shoes; victory alone is not
the original trigger. Eighteen static tree cells, including the secret-tree
reward route, require the missing flag.

**Next change:** restore the map-specific shop condition using the existing
active-placement state, display the recovered dialogue, and award shoes once.
Cover visiting before clearing, clearing all four groups, both shop entrances,
and revisiting. Merely injecting shoes into a movement test is insufficient.

### 4. The strength-40 route exceeds the fresh character's fixed cap (P1)

`Player::initialize` leaves `stat_cap_mode` at 0, giving a cap of 30. Both
ordinary level growth and strength potions respect `maximum_stat()`, and no
gameplay path changes the mode. `initialize_map_actions` requires strength 40
to expose the switch at `(11,9)` on `0x2d`, used by the tested route to rescue
slot 13 on `0x2e`. Funding repeated ordinary potion purchases still stops at
30. Enrage is combat-only and restores the base strength when combat ends;
it is not a normal exploration solution.

The original changes player `+0x654` through the registration/profile checks
called by `0x0040d610`, `0x00412460`, and `0x004123e0`. This connects the cap to
the already-open scope/access-policy question U013. It is not an undiscovered
strength-training quest. The strength-40 route test supplies the strength.

**Next change:** record a consistent stat-cap policy for D5's supported content,
then test growing to the required strength through the real progression APIs.
Keep the recovered map threshold unchanged. This audit does not reproduce or
alter registration checks and does not establish that every alternate route
to slot 13 is impossible.

### 5. Non-town miscellaneous shops remain placeholders (P2)

In `app/main.cpp`, movement action 8 only calls `show_freds_shop` for map
`0x11`; other locations display “This entrance leads to a miscellaneous shop.”
This contradicts any broad reading that all shops are usable. It also prevents
normal access to the original remote shop stock. It is separate from the
map-08 shoe award, which must precede the shop transaction.

**Next change:** recover and wire each map-specific miscellaneous stock/price
branch from `0x004402b0` and its callees. Audit essential consumable availability
and reachable purchases before attempting a combat-balanced full route.

## Dependencies that have an implementation path

The diagnostic defeats each **actual primary stamp-boss placement** through the
public combat APIs, with a deliberately strengthened player. The full groups
are constructed from the catalog; their HP is not edited. This verifies reward
transitions, not acquisition of the combat stats, boss reachability, or balance.

| Stamp | Map | Primary monster type | Result |
| --- | --- | --- | --- |
| 1 | `0x6d` | 35 | Stamp count 1 |
| 2 | `0x6e` | 36 | Stamp count 2 |
| 3 | `0x5f` | 37 | Stamp count 3 |
| 4 | `0x63` | 38 | Stamp count 4 |
| 5 | `0x5a` | 39 | Stamp count 5 |
| 6 | `0x61` | 40 | Stamp count 6; Gatekeeper state 1 |

Other placements of types 35–40 do not award a stamp unless the map environment
matches `type - 34`. The five-stamp stone-circle gate has an implemented writer
and consumer. It is not independently blocked by an absent stamp award.

The Rainbow Key has a real writer in `rescue_princess`: choose Mystery, whose
counter and level determine the override chance. A level-1, seed-1 diagnostic
starting with counter zero earns it on the third Mystery rescue (counter 4,
because a clue increments it again). This deliberately chooses rescue slots
directly; it does not prove that three corresponding princesses are reachable
in that order. Choosing non-Mystery rewards cannot produce the key, and rewards
are one-time. That restriction is supported by the original reward code and is
not automatically a rebuild defect. A reachable rescue order remains to prove.

The finale requires Gatekeeper state 2 and exactly 20 positive signed rescue
slots, followed by the town completion check. The King has a separate rescue
counter and rewards. Recovering every placement and testing this predicate with
injected state is not proof of a completable fresh game.

## Princess coverage and remaining route work

The inventory checks that the original placements cover slots 1–20 exactly once.
Only the explicitly described routes below have the earlier bounded route proof.

| Slot | Map | Position | Route evidence / follow-up |
| --- | --- | --- | --- |
| 1 | `0x6d` | `(8,8)` | E066: continues E065 with earned Backstab/equipment, Wormoid's stamp, and the second rescue; 116 victories, 14 chests, level 10, no deaths; Mystery grants 300 gold |
| 2 | `0x33` | `(4,3)` | E065: continuous default Combat/Medium seed-1 SDL route; earned resources, 71 victories, seven chests, no deaths, Mystery healing |
| 3 | `0x74` | `(1,1)` | Full prerequisite/encounter route open |
| 4 | `0x67` | `(11,3)` | Full prerequisite/encounter route open |
| 5 | `0x64` | `(1,10)` | Full prerequisite/encounter route open |
| 6 | `0x71` | `(11,1)` | Full prerequisite/encounter route open |
| 7 | `0x6a` | `(1,10)` | Full prerequisite/encounter route open |
| 8 | `0x41` | `(9,6)` | Full prerequisite/encounter route open |
| 9 | `0x58` | `(9,5)` | Full prerequisite/encounter route open |
| 10 | `0x63` | `(8,1)` | Same map as spray chest; full route open |
| 11 | `0x4a` | `(2,1)` | E059 platform route supplies Rainbow Key, suppresses roaming |
| 12 | `0x75` | `(9,1)` | Hell entry patch exists; full route open |
| 13 | `0x2e` | `(7,1)` | E064 extends E052 with potion-earned strength 40 and rescue; gold, starting location, and Rainbow Key supplied |
| 14 | `0x5a` | `(11,1)` | Full prerequisite/encounter route open |
| 15 | `0x31` | `(11,6)` | Full prerequisite/encounter route open |
| 16 | `0x56` | `(1,1)` | Full prerequisite/encounter route open |
| 17 | `0x61` | `(11,3)` | Earth Giant map; full route open |
| 18 | `0x5b` | `(1,10)` | Full prerequisite/encounter route open |
| 19 | `0x7f` | `(2,9)` | Map also contains type-41 placement; full route open |
| 20 | `0x85` | `(1,9)` | E059 timed-fire route; prerequisites/encounters still open |

The machine-readable inventory also lists every static keyed door, climbable
tree, and spray-dependent stream cell. A gate on a map is not necessarily
mandatory for its princess; tile presence alone is not a reachability proof.

## Evidence and reproduction

Files are under [`evidence/progression-audit`](../evidence/progression-audit):

- `catalog.py` and `catalog.json`: all 145 original chest placements, 20 princess
  records, and 49 conditional terrain cells. Generated-map chest terrain is
  null because its tile is determined at runtime.
- `probe.cpp`: current diagnostic for funded stat growth, all six stamp awards,
  unkeyed Gatekeeper victory, cleared-map-08 state, and fresh Mystery rewards.
- `probe-output.txt`: preserved E061 output, including the then-failing
  unkeyed Gatekeeper and strength-cap observations. E064 updates the probe's
  shop API and entry milestones, without replacing this historical output.
- `reward-disassembly.txt`: direct supplied-executable key/spray award branches.

From the checkout root:

```sh
python3 evidence/progression-audit/catalog.py \
  --analysis /Users/baouchiy/Downloads/YipeIII-analysis \
  > evidence/progression-audit/catalog.json
cmake --build build --parallel 4
c++ -std=c++20 -Wall -Wextra -Wpedantic -I include \
  evidence/progression-audit/probe.cpp build/libyipe_core.a \
  -o build/progression-audit
./build/progression-audit
```

The build and diagnostic ran successfully on the E061 working tree. A successful
diagnostic execution means observations were collected; it does **not** mean
the progression checks passed. The preserved `probe-output.txt` reports the
then-missing Underworld Key and disabled strength gate. Running the probe now
uses current code and should not overwrite that historical output. The probe
creates no save files.

E062 implements the Underworld Key award, chest acquisition, and the map-08 shoe
reward; E063 adds remote stock and Outback's alternative Rainbow Key source.
E064 resolves the stat-cap mismatch and demonstrates funded growth through the
strength-40 requirement. E065 proves the first connected rescue with earned
resources; E066 extends it to a second rescue and Wormoid's stamp. E074 now proves every rescue, stamp, movement prerequisite, and the finale in
one connected default-character run. The route uses Outback's Rainbow Key;
the alternative Mystery award retains focused coverage. P8/P9 remain open for
broader original-runtime, persistence, and platform verification.
