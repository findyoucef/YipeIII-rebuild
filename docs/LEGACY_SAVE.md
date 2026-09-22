# Legacy `.yp3` save analysis

This is the static field map for the player serializer at `0x00443c90`, called through the document serializer at `0x0044f760`. The import implementation follows the original executable's archive path exactly, but compatibility is not yet demonstrated against a real `.yp3` sample.

## Archive framing and string encoding

The open-document path at `0x00471d80` constructs a load-mode `CArchive`
directly over the selected file and invokes the document serialization slot.
`Yipe_SerializeDocument` immediately delegates to the embedded player. No
class name, schema number, magic value, or other document prefix is written by
this path: the file begins with the player's CString.

The ANSI CString writer at `0x00472233` uses the standard variable-length MFC
form recovered directly from its branches:

- lengths below `0xff` use one byte;
- longer lengths begin with `ff` and a 16-bit length;
- a 16-bit value of `ffff` is followed by a 32-bit length;
- a 16-bit value of `fffe` denotes a Unicode string and is followed by another
  encoded character count.

This executable writes ANSI strings. The import reader retains their bytes in
`std::string` and rejects the Unicode archive variant explicitly. After the
name bytes, the 1.1.6.0 player payload has exactly 5,064 bytes. That fixed size
lets the reader reject a framed object, an older/newer field layout, truncation,
and trailing data rather than silently shifting the field map.

## Serialization order

All offsets below are in-memory offsets in the original player object. They are not file offsets.

1. CString at `+0x004`.
2. Thirteen 32-bit integers at `+0x008`, `+0x00c`, `+0x010`, `+0x014`, `+0x018`, `+0x024`, `+0x028`, `+0x01c`, `+0x020`, `+0x02c`, `+0x05c`, `+0x030`, and `+0x034`.
3. A 32-bit validation value produced by `0x00445020`.
4. Five 16-bit values at `+0x038` through `+0x040`.
5. The 32-bit death count at `+0x658`.
6. A 16-bit value at `+0x07e`, a 32-bit value at `+0x070`, 16-bit values at `+0x07c` and `+0x65c`, then 32-bit values at `+0x074` and `+0x078`.
7. Seven booleans serialized as 32-bit integers from `+0x06c`, `+0x060`, `+0x061`, `+0x062`, `+0x063`, `+0x064`, and `+0x64c`.
8. 512 pairs of booleans serialized as 32-bit integers from arrays at `+0x152` and `+0x352`.
9. Fifteen 16-bit values at `+0x10c`.
10. Ten pairs: a boolean-as-32-bit at `+0x148` and a 16-bit value at `+0x080`.
11. Thirty triples: a 16-bit item ID at `+0x094`, a 16-bit value at `+0x0d0`, and a boolean-as-32-bit at `+0x12a`.
12. Fifty triples: a 16-bit value at `+0x65e`, a 32-bit value at `+0x584`, and a boolean-as-32-bit at `+0x552`.
13. Five 16-bit values at `+0x042`, `+0x044`, `+0x046`, `+0x048`, and `+0x050`.
14. A 32-bit value from `+0x058`, stored after subtracting 203, followed by a 32-bit value at `+0x054`.
15. The `+0x065` healing-related boolean, mirrored through saved field `+0x69c`, is appended as a 32-bit integer by the write branch. The original read branch derives `+0x065` from the already loaded `+0x69c` array entry and does not consume the appended copy.

The load branch reverses the `-203` transform. It calls `0x00445060` to compare the stored validation value with the recomputed value, accepts a difference from -1 through +1, and stores the result at `+0x64e`.

## Validation helper

`0x00445020` and `0x00445060` use this integer expression:

```text
value_at_0x58 / 2
  + (maximum_health - current_health) * 2
  + u16_at_0x3e
  + u16_at_0x3c
  + u16_at_0x38
  + i32_at_0x34
  + i32_at_0x30
```

The write branch stores zero instead when the prior validation flag is false. This is a consistency check, not evidence of a cryptographic checksum.

## Import mapping

`load_legacy_save` exposes a `LegacySave` containing both a rebuild `GameState`
and every serialized field not yet understood. The known mapping includes the
player name, primary stats, HP/SP/AP, level, weapon and armor ranks, five minor
equipment ranks, XP, transformed gold, world coordinates, computed map ID,
difficulty, level-up stat bias, learned special ability, restoration count, tree/key movement state, inventory,
and the recovered extended item-status fields. The King-dialog trace also maps
`+0x05c` to its quest score/value, `+0x063` to the Lucky Rabbit Foot, the 30
`+0x0d0` values and `+0x12a` flags to quest hints and one-time conversations,
the sum of the 50 `+0x584` values to the early equipment hint, and extended
entries 2, 8, 9, and 18 to Gatekeeper, stamp, princess, and Mystery-reward
state. Player offset `+0x658` stores the death count, extended entry 14 records
entry through the secret Yipe Hell passage, and extended entry 19 stores the
local Hell Gatekeeper state. The Other action maps `+0x060` and `+0x061` to bridge
treasure and healing-water availability, extended entry 27 (`+0x694`) to collected
cave coins, and entry 30 (`+0x69a`) to the snow-search stage. `+0x06c` remains in
the offset-based import object: it is a transient redraw request, not a saved
mountain unlock. Combat setup marks `+0x552[monster_type]` for each
opponent, so entries 0–41 are imported as per-monster encounter history;
entries 42–49 remain available in the offset-based import object. Defeat counters
at `+0x584` instead use `monster_type - 1`: index 34 supplies the zero/nonzero
Wormoid-defeat condition used by map `0x6d`'s giant warning. All 50 counters,
including the nine reserved entries, are imported into `Player::monster_defeats`
with complete-history status. The aggregate retains the original 32-bit sum,
and `has_defeated_wormoid` retains the hint condition. Other unresolved arrays
likewise remain there so future reconstruction does not
require reparsing the file.

The chosen character portrait at `+0x0cc` is item-ID array entry 28 and is
imported through `Player::inventory[28]`. Extended entry 15 at `+0x67c` is the
level-up stat bias (`Player::combat_style`); it is separate from the selected
Combat/Quickness/Magic/Random specialty at `+0x6cc`, which controls spell costs
and some combat behavior. The 1.1.6.0 serializer does not write `+0x6cc`, so
an original `.yp3` file cannot establish the selected specialty. The importer
retains the default Combat specialty as an explicit inference. Native saves
from rebuild version 16 onward store specialty separately; versions 2–15 infer
it from the old rebuild class value.

The original validation expression is evaluated and reported through
`validation_passed`. A mismatch is non-fatal, matching the original loader,
which stores the result as player state rather than rejecting the document.
The importer never writes `.yp3` files.

## Special statistics fields

Dialog 395, opened by the Stats dialog's Special button at `0x0044c3e0`,
displays the following existing player fields. Its initialization at
`0x0044d2d0` and control bindings at `0x0044d1f0` establish the mapping:

| Player offset | Display | Rebuild state |
| --- | --- | --- |
| `+0x66e` | Rare Stamps Collected, formatted as a signed 16-bit integer | `stamps_found` |
| `+0x0d0[0..29]` | Princesses Rescued, count of positive signed 16-bit slots | `rescued_princess_count()` over `inventory_values` |
| `+0x080`, `+0x082` | Rainbow Key, Underworld Key | `has_rainbow_key`, `has_underworld_key` |
| `+0x065` | Ring of Bonus Healing | `has_bonus_healing_ring` |
| `+0x674` | Tree Climbing Tools | `has_tree_climbing_shoes` |
| `+0x064` | Waterproof Shoe Spray | `has_waterproof_shoe_spray` |
| `+0x063` | King's Lucky Rabbit Foot | `has_lucky_rabbit_foot` |
| `+0x074` | Current Ability: None, Backstab, Frighten, Invisibility, or Enrage | `special_ability` |

Resource strings 133-134, 171-174, and 451-454 supply the names. The key and
special-item lists retain this order, hide unowned items, and display None
when empty. SDL adapts the two-column dialog to a read-only message box reached
through Ctrl+T, Special; closing it returns to player status. It does not
change quest counters, write saves, or submit high scores. Native save version
15 already stores all the required state, so this panel adds no save fields.

Legacy field `+0x074` now maps supported ability IDs 0-4 into
`Player::special_ability`, while its offset-based copy remains available as
`unknown_0074`. Unknown IDs are rejected explicitly rather than silently
losing the learned ability or narrowing an invalid value to a different ID.
This maps the learned ability only; the neighboring raw `+0x078` field and
active-effect restoration remain outside this panel checkpoint.

The panel uses the rebuild's existing normalized possession booleans. The
original displays keys and tree tools only for a raw short equal to 1, while
its other three item flags use a nonzero byte. Tree tools already preserve
the equality rule on legacy import; the existing key importer normalizes any
nonzero value to true. Exact display parity for noncanonical legacy key values
is therefore not claimed, and this checkpoint does not change key import or
movement access behavior. The native loader rejects unknown ability IDs;
the display also reports an invalid ability explicitly rather than inventing
a name. Original-runtime presentation comparisons remain pending.

## Rebuild-native saves

The native rebuild format is independent of MFC and legacy `.yp3` files. Its
14-byte header contains magic `Y3RS`, a little-endian 16-bit version, a
little-endian 32-bit payload size, and a CRC-32 of the payload. Version 1 stores
the initially reconstructed player fields. Version 2 appends experience, gold,
equipment, combat settings, world coordinates, the first encounter's completion
state, and the Visual C++ random-generator state. Version 3 appends ability
points, the learned special ability, its active duration, and the three Enrage
stat backups. Version 4 appends the limited-restoration counter and the four
movement flags for waterproof shoe spray, tree-climbing shoes, and both keys. The
loader gives version-1 files the starting world
state, gives version-2 files a full starting AP pool, and defaults fields added
by newer formats. Version 5 appends changed map IDs, coordinates, and tile values
so opened doors and linked mechanisms survive map changes and restart. Version 6
appends the 15 learned-spell values recovered from legacy offset `+0x10c`.
Version 7 appends the recovered King quest state and Lucky Rabbit Foot. Version 8
appends the princess Mystery-reward counter recovered from offset `+0x682`.
Version 9 appends the stable indices of every permanently defeated monster
placement. Version 10 appends the signed region coordinates for the entrance
saved by the original Return-to-Surface state. Version 11 appends encounter
history for monster type IDs 0–41. Version 12 appends the death count, local
Hell Gatekeeper state, and secret-passage entry flag. Version 13 appends three
one-byte flags for bridge treasure, healing water, and collected cave coins,
followed by the 16-bit snow-search stage. Earlier versions default both
availability flags to true and the coin/snow progress to zero. Version 14 appends
one byte for `has_defeated_wormoid`. Versions 1–13 default it to false: their
aggregate quest total and encounter flags cannot reconstruct the missing per-type
defeat history, so the giant warning may appear again. Version 15 appends the
50 signed 32-bit defeat counters and a one-byte history-completeness flag.
New games and legacy imports have complete history. Native versions 1–14
start with zero per-type counts and incomplete history: neither encounter flags
nor defeated placements establish how many kills occurred. The existing quest
aggregate and version-14 Wormoid predicate are retained, not assigned to invented
per-type counts. Subsequent kills update both the per-type counters and the
aggregate; the incompleteness flag survives resaving and is shown in the Kills
view. The counters, aggregate, and hint predicate are maintained through
`Player::record_monster_defeat`, without changing old payload offsets.
When importing version 9,
defeats now known to be reload-respawning migrate into the transient current-map
state instead of becoming permanent. The loader accepts all fifteen versions,
derives missing return coordinates from the current region for older files,
and migrates the first-encounter bit in versions 2–8 to the Giant Beetle's
placement index 5. Writes use a temporary file and atomic replacement so a
failed write does not destroy the previous save.

## Verification status

- Static evidence fixes the headerless framing, CString forms, field order,
  widths, gold transform, and validation expression.
- Byte-level synthetic tests exercise every field family, both supported ANSI
  length forms practical for player names, validation, unknown-field retention,
  truncation, and the unsupported Unicode marker.
- No original `.yp3` exists in the preserved runtime evidence or analysis
  workspace. Capture paired R08 saves and load them in the rebuild before
  claiming original-save compatibility.
