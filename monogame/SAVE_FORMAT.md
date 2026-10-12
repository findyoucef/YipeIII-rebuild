# MonoGame full-state saves, version 1

**Pause > Adventure > Save game / Load game** uses the current adventure's path.
Each new adventure receives a unique `Saved Adventures/<slot>/game.y3m` beneath
`<LocalApplicationData>/YipeIII-MonoGame`; the slot is written on manual save.
**Title > Load game** lists those slots and existing root `.y3m` files, including
the older `game.y3m`. Explicitly loaded preview files use an isolated library
beside the selected file. New adventure creation never reuses a loaded save's
destination. This format belongs to the
C# port. It is distinct from `town-preview.json`, C++ `.y3r`, and original `.yp3`
files. Explicit import creates a new `.y3m` without modifying the source; see the
[import policy](#explicit-import-policy) below.
The frontend restores all 112 static maps and three generated caves. Map
discoveries use a separate optional companion described below; the version 1
gameplay bytes are unchanged.

The authoritative codec is [GameSaveCodec](Yipe.Core/GameSaveCodec.cs).
Serialization is explicit, with no runtime reflection or dependency on a graphics
device. Future schema changes require a new version and an explicit reader policy.
Do not silently reorder fields or regenerate the version 1 compatibility fixture
to accommodate an incompatible implementation.

## Envelope

All numbers are little-endian. There is no padding. Maximum file size is 4 MiB.

| Offset | Length | Meaning |
| ---: | ---: | --- |
| 0 | 8 | ASCII `YIPE3MG` followed by a zero byte |
| 8 | 2 | Unsigned version, currently 1 |
| 10 | 4 | Signed payload byte length, exactly file length minus 46 |
| 14 | 32 | SHA-256 of the payload |
| 46 | Remaining bytes | State payload |

The checksum detects corruption; it is not authentication. Unknown versions,
wrong magic, truncation, excess data, invalid lengths and hash mismatches fail
before a session is constructed.

## Payload

Fields are stored in this order:

1. The complete Player: **65 scalar properties and seven fixed arrays**, in the
   explicit order of `WritePlayer` / `ReadPlayer` in the version 1 codec. This
   matches the Player declaration order when version 1 was introduced. `Name`
   begins with a two-byte unsigned UTF-8 byte length, then exactly that many
   strictly valid UTF-8 bytes (at most 65,535). Integers retain their declared
   widths and signedness; `SpecialAbility` is one byte, `HellGatekeeperState` a
   signed two-byte integer. Booleans are exactly one byte, 0 or 1. Derived values
   such as `MaximumStat` are recomputed, not stored.
2. World location: X, Y, RegionX, RegionY, MapLayer as signed four-byte integers;
   AlternateMapSet as a boolean; MapId as a signed four-byte integer.
3. The full four-byte unsigned RNG state.
4. A signed four-byte map-change count, then each change as MapId, X and Y
   (signed four-byte integers), followed by Tile (unsigned two-byte integer).
5. A signed four-byte defeated-placement count, then unsigned two-byte placement
   IDs. IDs are zero-based, 0–185, distinct from Player's per-monster counters.

The fixed arrays, embedded among Player's scalar fields, are:

| Array | Elements | Element encoding |
| --- | ---: | --- |
| Inventory | 30 | Unsigned 16-bit; includes carried slots and original metadata |
| InventoryValues | 30 | Unsigned 16-bit raw storage, including signed rescue values |
| QuestFlags | 30 | Boolean |
| OpenedChests | 128 | Byte, restricted to four chest bits (0–15) |
| Spells | 15 | Unsigned 16-bit |
| EncounteredMonsterTypes | 42 | Boolean |
| MonsterDefeats | 50 | Signed 32-bit, including original wrapped counts |

Their lengths are part of version 1 and are not written as separate counts.
Gold, experience, equipment, special ability/enrage state, quest and shop progress,
surface returns, timers stored on Player, death state and all other Player fields
are included. Raw metadata and signed counters are preserved without clamping.

## Validation and restoration

Both encode and decode validate progression enums, chest bits, shop stage,
relocation values and destinations, name encoding/length, positive Intelligence,
Level 1–100, Difficulty 1–3, positive maximum HP and nonnegative maximum SP/AP.
Current HP/SP/AP and historical counters are not silently normalized.

World coordinates must fit the grid and original region/layer ranges, match the
computed map ID, and agree with Player's stored map/set. Maps and persistent edits
must exist in the static catalog or be generated maps 118–120. Generated-map
save positions must be interior cells. Non-sentinel surface returns must have
consistent surface coordinates. A queued Surface relocation requires an actual
return destination; 0 and the used-potion sentinel 1 are not valid queued targets.

Tile-change counts are bounded by (static catalog size + 3) × 156; duplicate map/cell keys,
unavailable map IDs and off-grid coordinates are rejected. Defeated placements
must be unique and in 0–185. These rules also validate the state produced by an
import; files outside this supported state domain are rejected rather than repaired.

Decode constructs a separate `GameSession` only after validation. The session's
opening sequence first loads the saved map, then applies any pending relocation
(and loads its destination). Entry actions run, followed by
persistent tile changes and, when there was no queued relocation, D7 platform support. This preserves the
reference's reload behavior: for example, HealingWaterAvailable is stored but
rearmed by map entry. Opened-chest bits filter the newly created map. Unrecorded
tile edits, visibility, moving-platform phase, temporary defeats, active monsters,
animation, camera and buffered input are not saved. Map discoveries and display
preferences have separate persistence; neither changes runtime cave visibility.

## Map discovery companion

`GameSaveStore` writes optional presentation-only notes to the same basename with
`.map.json`, for example `game.y3m` and `game.map.json`. JSON version 1 stores the
SHA-256 of the **complete saved `.y3m` bytes**, discovered region IDs/environments,
156 column-major seen flags/terrain snapshots/landmark markers per region, and
unique undirected travel links. It does not load unvisited maps or draw RNG.
The reader bounds files to 512 KiB, validates the version, hash, region IDs,
array lengths and links, and clears any contents attached to unseen cells.

Both files use the existing temporary-file/flush/atomic-replacement procedure.
The adventure is replaced first, then its map notes; the pair is **not** a single
filesystem transaction. An interrupted pair, corrupt notes or a hash mismatch
cannot block a valid game load: notes are ignored and the pause menu reports
that previous notes are unavailable. Failure writing notes similarly reports
that the adventure saved but its map notes did not. Missing notes are a normal
older-save case with no warning. Loading or starting a new adventure replaces
the in-memory discovery journal, avoiding history from a different playthrough.

Copy both files to retain exploration history. A `.y3m` alone remains portable
and fully playable, beginning discovery from its loaded location. Static map
notes accumulate revealed cells across visits. Generated maps 118–120 clear
their remembered layout on entry/reload, because the core generates a new room;
this adds no random draws beyond the existing reload contract below. Combat
and live cave visibility continue to use the original transient visibility rules.

## Generated-map restart contract

Maps 118–120 use the original room/corridor generator and column-major smoothing,
including inclusive RNG draws for zero-width entry rooms and the recovered lower
room-boundary clamp. Static templates remain available through `CreateMap`;
`LoadMap` chooses a static copy or fresh generation with the session RNG.

Only the current RNG state is saved, not a generated layout or its initial seed.
Reopening generates a **new** layout from that saved state and advances the RNG.
Persistent tile edits and opened-chest filtering then apply to the new instance.
Even a queued relocation first generates the saved cave before loading its
relocation destination, matching the C++ application. Import conversion itself
never generates a map; the first load advances RNG exactly once.

## Desktop replacement and frontend handoff

[GameSaveStore](Yipe.Desktop/GameSaveStore.cs) captures validated bytes synchronously
before asynchronous I/O, so animation or later gameplay cannot alter a write in
progress. It writes a uniquely named temporary file in the destination directory,
flushes that file to disk, closes it, and replaces the destination with a rename.
Concurrent writes use separate temporary files; the last successful replacement
wins. Cleanup runs on success and failure without hiding the original error.
The adapter requires a `.y3m` destination.

Loads bound file size before allocating. The frontend keeps the existing session
until loading, preview compatibility checks and creation of its replacement
renderer succeed. Successful load clears the old tween/buffer and disposes the
old renderer. Invalid, missing, corrupt or unsupported saves leave the live game
and renderer unchanged. Saving during a step captures the accepted logical tile.
Saves are manual. An explicit `--load-save path.y3m` opens the selected file at
startup and uses it for subsequent Save/Load commands. Without that option,
startup creates the usual preview session. Legacy files are never auto-migrated.

This was verified on macOS, including actual permission and replacement failures.
Other-platform behavior and abrupt power-loss durability are not established by
these tests; the containing directory is not explicitly synced by this adapter.

## Compatibility fixture and verification

[full-save-v1.y3m](Yipe.Tests/Fixtures/full-save-v1.y3m) is a small, checked-in native
version 1 fixture with Unicode, signed metadata/counters, all arrays, persistent
tiles, defeated placements and a maximum unsigned RNG seed. Its expected state
is defined by `GameSaveChecks.Fixture`. It is a new-format compatibility fixture,
not a C++ save or evidence of legacy compatibility.

[GameSaveChecks](Yipe.Tests/GameSaveChecks.cs) verifies every property independently
of the codec's field list, snapshot ownership, corruption and structural errors,
write failures, concurrent writes, entry behavior and separate-process restart.
See [test instructions](Yipe.Tests/README.md#full-state-save-checks) and
[P1.5 evidence](evidence/2026-10-03-full-state-saves/README.md).

## Explicit import policy

[SaveImporter](Yipe.Core/SaveImporter.cs) converts bounded input into validated
version 1 bytes without constructing a session. The desktop adapter reads the
source and atomically creates a **new** destination; it refuses existing files,
including during competing imports, and cleans its private temporary files.
No import replaces a live session. Load the resulting file explicitly afterward.

| Source | Supported policy |
| --- | --- |
| Preview `.json` | Exactly version 1, MapId 17 and a valid X/Y; no missing, duplicate or extra fields. Maximum 4 KiB. Creates the default Warrior, seed 1, at that position. Preview files never contained earned character progress. |
| C++ `.y3r` | `Y3RS`, version **20**, exact length and valid CRC-32. Imports all persistent state represented by the C++ reader. Names must be valid UTF-8. Versions 1–19 must first be opened and re-saved by the supplied C++ rebuild; newer versions are rejected pending a reviewed reader. |
| Original `.yp3` | Recovered ANSI CArchive CString framing (8/16/32-bit lengths), followed by exactly 5,064 bytes. Imports the same mapped fields as `src/legacy_save.cpp`, with matching signed values and wrapping defeat totals. Interprets name bytes as Windows-1252; this is the chosen encoding policy, not detection of the original machine's code page. Unicode CString and unknown layouts are rejected. |

`.yp3` has no saved RNG stream or native map-change/placement lists. Seed 1 and
other absent fields use the C++ defaults (including D8 stat-cap mode 2, specialty
Combat, Silly Mode off). Unknown archive fields remain only in the preserved
source; conversion does not claim a lossless round trip back to `.yp3`. The
original validation formula must match within its documented tolerance of one;
unlike the C++ reader's returned warning flag, a mismatch here rejects conversion.
Names must also fit the `.y3m` UTF-8 length limit after conversion.

All formats must pass the full-state statistics, location, enum and collection
validation described above. Inconsistent locations, unavailable maps, duplicate
tile edits/placement IDs and malformed counts are rejected without guessing at
repairs, even if a more permissive C++ reader can parse them. Preserve such a
source for diagnosis. Extensions and file signatures are not interchangeable;
loading raw legacy bytes with **Load game** never performs migration implicitly.

From the repository root (choose a destination that does not already exist):

```sh
dotnet run --project monogame/Yipe.Desktop -c Release -- --import-save "/path/to/old.yp3" "/path/to/imported.y3m"
dotnet run --project monogame/Yipe.Desktop -c Release -- --load-save "/path/to/imported.y3m"
```

The import command needs no graphics window and reports format-specific limits.
The same command accepts preview `.json` and C++ `.y3r`. The current town frontend
reports saves outside Maynard without installing their session; conversion and
core verification support the wider world now, while its presentation remains P2/P3.
There is no claim that unported combat/quests can yet be played from an imported save.

[PersistenceChecks](Yipe.Tests/PersistenceChecks.cs) and its independent
[C++ oracle](Yipe.Tests/persistence_reference.cpp) verify full imported state,
map generation, pending relocation, repeated save/reopen and item/RNG continuation.
See [P1 completion evidence](evidence/2026-10-04-p1-complete/README.md).
