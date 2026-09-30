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

## Keyboard editor and precedence (E069)

Dialog 161 contains thirteen ordinary `EDIT` controls, not a Windows hot-key
control. `0x00409fa0` intercepts `WM_KEYDOWN`, obtains a label with
`GetKeyNameTextA`, and stores the low 16 bits of `wParam` directly. There is no
modifier packing. `0x00409c60` copies the settings into the dialog and
`0x00409e10` copies them back; neither detects duplicate bindings. The earlier
claim that modifier combinations were an unfinished original feature was wrong.

`0x0040a9b0` filters individual virtual keys. It excludes Tab, Escape, Enter,
Backspace, Shift, Control, several lock/system keys, and F1-F12, while admitting
the OEM punctuation ranges `0xba..0xc0` and `0xdb..0xde`. The rebuild now maps
those eleven punctuation keys and the six keypad operators `0x6a..0x6f` to named,
persisted bindings. Its previously supported function/navigation keys remain a
D2 extension; Escape still cancels capture. Capture ignores standalone modifier
and lock events, reports unsupported chords/keys, and waits for a valid choice.
Gameplay still matches a single key rather than giving Shift a separate binding;
Control shortcuts take precedence in exploration. Alt/Command/AltGr-modified
events are ignored rather than leaking into ordinary gameplay.

Movement `0x0040bbb0` tests North, Northeast, Northwest, South, Southwest,
Southeast, West, East in that order. Each branch accepts its configured key and
fixed keypad direction; cardinal branches also accept the arrow key. Thus an
earlier configured binding can preempt a later fixed direction. The rebuild
restores these branches and retains WASD only as the documented fallback.

Combat `0x004060f0` tests Run/Escape, Attack/Enter, Cast, Use Item, then Special,
before left/right targeting. The rebuild now uses this action order, including
Run before the Enter fallback if an existing/explicitly accepted native binding
uses Enter. Arrow/Tab target cycling remains a D2 adaptation and can be replaced
by a configured combat action; the mouse Next Target button remains available.

Settings and Current Keys show actual conflicts, including exploration's
Q/Escape/F1/F5/F10 commands, overlapping movement/item/cast bindings, combat
duplicates, and overridden fallback keys. Editing defaults to canceling a
conflicting change; explicit acceptance preserves it. This warning UI is an
adaptation rather than an invented original duplicate-key prohibition. Native
settings version 1 still stores exactly thirteen `u16` codes and the sound flag;
no game-save or original-file format changes were made. Static disassembly is
retained in `evidence/corrected-analysis/key-input.asm`; visible original input,
focus, keyboard-layout, and timing comparisons remain open.

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

## High Score Code (E070)

Command 32785 (`0x8011`), mapped at executable file offset `0x90838`, invokes
`0x0041c630` and dialog 458. The dialog displays the code and the historical
destination `YipeScores@aol.com` (initializer `0x00408a90`). The rebuild offers
High Score Code inside High Scores during play, including the statistics path.
Copy Code copies only the generated string to the local clipboard. No email is
sent, and availability of the original service is unknown. Startup has no code
button because there is no active player. Browsing a different difficulty's
score table does not change the player's code.

Direct disassembly recovers formatting arguments and the gold transformation
omitted by the decompiler. In order, level, current gold, and the wrapping signed
32-bit sum of all 50 defeat counters use divisors 15, 450, and 60. A positive
input encodes `value - 1`; a nonpositive input encodes zero. Each component is
`quotient + "L" + remainder + "W"`, except gold uses `1000 - remainder`.
Append signed-short Gatekeeper state times three, `W`, the sum of those three
quotients, `W`, and signed-short difficulty. Append one more `W` only when raw
player `+0x6a4` (document `+0x6f4`) equals 5. For example, level 31, gold 902,
122 defeats, Gatekeeper 3, difficulty 3, and a zero suffix field produce
`2L0W2L999W2L1W9W6W3`. The fourth, random-number branch is unreachable because
the loop runs exactly three times. Generating or copying a code consumes no RNG
and changes no player state.

The suffix field is extended entry 35 in the original archive, initialized to
zero by `0x00443ad0`. Its gameplay meaning and any writer beyond initialization
and loading remain unresolved; it is not the registration-mode field. It stays
offset-named as `Player::value_06a4`, imports without normalization, and persists
as a raw `u16` in native version 20. Versions 1-19 default it to zero because
they did not retain it; a previous conversion cannot recover a discarded value.

Unlike ranked tables' compatibility aggregate, codes always sum the recorded
50 counters, as `0x00446c30(false)` does. Native saves with incomplete per-type
history show an explicit warning that earlier defeats are excluded. The code
does not pretend those missing counters were recovered or assign the aggregate
to a fabricated monster type. Original-runtime code/dialog comparison remains
open. See `evidence/corrected-analysis/high-score-code.asm` and U026.

## Native high-score persistence

`highscores.y3r` is separate from player `save.y3r` (now version 20) and
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
