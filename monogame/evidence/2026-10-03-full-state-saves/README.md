# Full-state save verification

P1.5 is complete on 2026-10-03, America/Denver. The new version 1 `.y3m` format
persists the entire character, world position, gameplay RNG, remembered map tiles
and permanent defeated-placement IDs. The pause menu now offers Save game and
Load game. Existing town-position and legacy game files remain untouched.

| Check | Result | Evidence |
| --- | --- | --- |
| Locked restore | All four projects passed | [Restore](restore.log) |
| Checked Release rebuild | Zero warnings/errors | [Build](build-checked.log) |
| Core/input/save checks | 367 passed, including 141 new full-save checks | [Checks](core-checks.log) |
| Separate-process restart | Writer and reader children passed full-state and subsequent item/RNG comparisons | [Checks](core-checks.log) |
| Version 1 fixture | 854 bytes; envelope length and payload SHA-256 also checked independently with Python | [Fixture metadata](save-format-fixture.json), [fixture](../../Yipe.Tests/Fixtures/full-save-v1.y3m) |
| Real graphics | 161 checks, 360 tour frames, recording off | [Graphics](graphics-checks.log) |
| Existing user files | Save-directory filenames and all previous file fingerprints unchanged | [Isolation](save-isolation.log) |

The new tests cover every one of Player's 65 scalar properties and seven arrays,
complete world coordinates, alternate maps, permanent edits/defeats, Unicode,
raw signed metadata, wrapped counters and 1,024 subsequent RNG outputs/states.
They also verify independent loaded state, snapshot capture before async I/O,
entry/water/relocation behavior and D7 platform support. Every truncated prefix
fails; additional cases cover wrong versions/formats, excess data, invalid UTF-8,
flags, coordinates, counts, duplicate entries, unavailable maps and corruption.
Checksummed malformed inputs exercise structural validation independently of SHA.

Actual storage checks include write-permission failure, failure during replacement,
preservation of the previous successful file, cleanup and eight concurrent writers
using unique temporary files. The Unix permission-specific pair is skipped on
Windows. Separate child processes write and reopen a populated fixture, then use
an inventory item and compare the complete ensuing state/RNG. All test files are
isolated; no user-path save was created or overwritten.

Eight additional graphics checks confirm full character/progression/RNG loading,
new session/map/renderer installation and old texture disposal. Corrupt, missing,
unsupported-area/position and failed saves preserve the current session and its
animation or previous save file as appropriate. The earlier movement, effects,
controller simulation and town-boundary checks still pass. The fresh
[Save game menu](save-game-menu.png) was visually inspected; a
[default town capture](default-town.png) is retained. Artwork, rendering/effects,
input and walking speed were not changed.

The preceding 624 town, 8,192 RNG, 11,322 character, 311,728 map/world and 21,511
session C++ reference records still pass their digest checks. This work does not
add a C++ save importer or claim cross-format/full-campaign persistence parity.
The native compatibility fixture was generated from the version 1 writer and
its expected state is specified separately in the test; it fixes the new layout
for future compatibility checks.

The [format contract](../../SAVE_FORMAT.md) describes widths/order, validation,
transient-state exclusions and atomic replacement. Reproduction instructions are
in [GameSaveChecks](../../Yipe.Tests/README.md#full-state-save-checks).
[Source fingerprints](source-sha256.json) cover 63 inputs; the
[change manifest](changed-inputs.json) compares against P1.4. Null previous hashes
mean newly included inputs, which can include pre-existing C++ reference files.
No C++ gameplay source changed. [Toolchain and working tree](toolchain.txt).

The final simplicity review covered the explicit codec, session placement state,
desktop snapshot/replacement, frontend handoff and fixtures. No generic persistence
framework or duplicate Player model was introduced. No implementation changes were
needed after the final checks. The graphics run preceded the final three core-only
malformed-map-edit assertions; its production and graphics-runner sources match
the final fingerprints.

Generated-map reopening/RNG, complete C++ persistence comparisons, legacy imports,
physical controllers, other desktop platforms and console execution remain their
planned tasks. Abrupt power-loss durability is not established by these tests.
Display settings and transient map/animation state are deliberately separate from
the reference's persistent game state. The current frontend remains the Maynard
preview; it cannot display wider-world saves yet.

Next is P1.6: explicit preview migration and C++/original import policy. Resume from
[PORTING_PLAN.md](../../PORTING_PLAN.md). No jobs were left running and no commits
were made. HEAD remains `349fffe71e052dd4e011f1fe09c0e3080f5bb723`; the MonoGame tree
is still untracked, so that commit does not contain this implementation.
