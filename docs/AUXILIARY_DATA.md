# Auxiliary data and high scores

## Original `Yipe3.dat` format

`Yipe3.dat` is a headerless MFC `CArchive` payload for the original
`CUserSettings` object. It is separate from player `.yp3` saves. The mapping is
based on serializer `0x00414030`, default initialization at `0x00414340` and
`0x004143c0`, and the captured 447-byte file in
`evidence/original-runtime/Yipe3-2026-09-15.dat`.

All integers are little-endian. Each of the first 45 records contains an MFC
`CString` followed by a signed 32-bit value. Short ANSI strings begin with one
length byte. Longer strings use `ff` plus a 16-bit length; `ffff` then selects a
32-bit length. The original helper also recognizes `ff feff` as a Unicode marker.

| File order | In-memory offset | Type | Meaning |
| --- | ---: | --- | --- |
| 0 | `+0x004` and `+0x0b8` | 45 × (`CString`, `i32`) | High score names and values, interleaved on disk |
| 1 | `+0x174`…`+0x182` | 8 × `u16` | Northwest, north, northeast, west, east, southwest, south, and southeast virtual-key codes |
| 2 | `+0x190` | `i32` | Registration-related value; exact name pending |
| 3 | `+0x170` | `i32` | Registration-related value; exact name pending |
| 4 | `+0x184`, `+0x18c`, `+0x186`, `+0x188`, `+0x18a` | 5 × `u16` | Run, attack, cast, use-item, and special virtual-key codes in serializer order |
| 5 | `+0x16c` | `i32` | Registration-related value; exact name pending |

The first eight defaults are Windows virtual-key values, not character codes:
Numpad 7, Up, Numpad 9, Left, Right, Numpad 1, Down, and Numpad 3. The five
combat defaults in memory order are Run `R`, Cast `C`, Use Item `I`, Special
`S`, and Attack `A`. The captured file contains those defaults, names `Yip`
and `Youcef` in the middle 15-entry score group, and values 1302, 0, and 4529
in the three registration-related fields.

`load_original_auxiliary_data` implements a bounded reader for this exact field
order. It rejects truncation, trailing bytes, and the Unicode string variant,
which has not been observed in a real Yipe! III file.

The rebuild does not overwrite `Yipe3.dat`. `settings_from_original` imports
its 13 bindings into the portable `GameSettings` action order: eight movement
directions followed by Attack, Run, Cast, Use Item, and Special. Native
`settings.y3r` stores those bindings and the sound toggle with a versioned
header and checksum; high scores and the three unresolved registration fields
remain outside that native settings file. The original sound toggle is an
application-profile `Y3Settings/Sounds` value rather than part of `Yipe3.dat`.

## Recovered high-score rules

The update routine `0x00414540`, caller `0x0041a5c0`, and dialog resource 160
define three 15-entry groups: Easy at index 0, Medium at 15, and Hard at 30.
The update routine narrows difficulty to a signed 16-bit value; 2 selects Medium,
3 selects Hard, and every other value selects Easy. Initialization at
`0x00414340` sets every name to `----` and every value to zero.

| Relative indices | Category | Submitted value |
| --- | --- | --- |
| 0–2 | Most Kills | Sum of all 50 defeat counters (`0x00446c30`, false argument) |
| 3–5 | Most Princesses | Count of positive signed 16-bit values across all 30 rescue slots (`0x00446c10`), not `princesses_found` |
| 6–8 | Most Cash at One Time | Current gold, not cumulative earnings |
| 9–11 | Highest Level | Current level |
| 12–14 | First 3 Players to Complete Game | Finisher names in arrival order; numeric values are left unchanged |

Ranked categories compare names case-sensitively. A returning name keeps its
highest value; an equal or higher submission moves ahead of preceding ties.
A new name qualifies at or above the third-place value, shifts ahead of ties,
and evicts the old third place. Thus zero qualifies against the empty rows.
If an imported category already contains duplicate names, the last matching
row is updated, preserving the original behavior rather than normalizing it.
Finishers append to the first `----` slot, never duplicate an existing name,
and never evict an earlier finisher once all three slots are full.

The rebuild uses `quest_progress_total` for the 50-counter total, including
historical aggregate progress retained by older native saves with incomplete
per-type history. The Kills display's 41-type total remains distinct.
Princess totals use `Player::rescued_princess_count`, shared with Special
statistics and the finale's exactly-20-rescues condition. No saved counter is
rewritten when reading these totals.

The SDL application submits the player when opening high scores during play,
immediately after returning from a victorious combat that increased the level
(`0x00411180`), and on the finale (`0x0040e330`). Resuming a completed game also
submits it, so a completion from before this feature can be recorded. Startup H
only displays the table, initially on Medium; in-game views start on the
player's difficulty. Easy/Medium/Hard buttons switch between all five categories.
Ctrl+H and the player-status button are adapted access paths, not new key bindings.

## Native high-score persistence

`highscores.y3r` is separate from player `save.y3r` (now version 16) and
`settings.y3r`. The core can save the legacy reader's `high_scores` array
directly; the application does not automatically import or overwrite an
original `Yipe3.dat`. Original `.yp3` files also remain import-only.

All fields are little-endian:

| Offset | Type | Meaning |
| --- | --- | --- |
| 0 | 4 bytes | Magic `Y3HS` |
| 4 | `u16` | Format version 1 |
| 6 | `u32` | Payload byte count |
| 10 | `u32` | CRC32 of the payload, using the native game-save algorithm |
| 14 | 45 records | Each record is `u16` name byte count, name bytes, and `i32` value |

Names are length-delimited bytes, limited to 65,535 bytes. All 45 entries,
including the otherwise unused finisher values, survive round trips. Unsupported
magic/version, invalid lengths/checksums, truncated records, and trailing data
are rejected. A missing file starts with the original defaults; a damaged file
raises an error and is not reset or overwritten. Writes use the existing native
temporary-file-and-replacement path. The in-memory table is published only
after the write succeeds, and unchanged submissions do not write.

**Accepted persistence adaptation:** the original update return flag is set
only for a new ranked insertion. Its caller immediately saves auxiliary data
only when that flag is set; same-name improvements and completion-only changes
can remain memory-only until another save. Native high scores persist every
actual table change. Ranking and completion order are preserved, but this save
timing is deliberately different. Exact original dialog timing remains unverified.
