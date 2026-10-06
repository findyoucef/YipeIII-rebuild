# P1 completion: imports, generated caves and persistence parity

Completed 2026-10-04 on Apple Silicon macOS. This closes P1.6/P1.7 and the P3.2
core-generation dependency. P1 is complete; the exact next task is P2.1 in the
[port plan](../../PORTING_PLAN.md). No commit was made; preserve the working tree.

## Delivered behavior

- Explicit preview JSON, C++ version 20 `.y3r` and recovered `.yp3` conversion
  into a new `.y3m`. Sources and existing destinations are preserved. No personal
  save was converted. The [import policy](../../SAVE_FORMAT.md#explicit-import-policy)
  specifies older-version upgrade, name encoding, validation and absent-field limits.
- `--import-save source destination.y3m` works without graphics;
  `--load-save path.y3m` opens a selected save through the existing safe desktop
  load path. The preview continues to report destinations outside Maynard.
- Original generated caves 118–120, with entry validation, exact RNG order,
  chest identities and column-major smoothing. Reopening regenerates from the
  saved RNG before queued relocation; conversion itself consumes no RNG.
- Durable independent C++ fixtures and separate-process conversion/reopen tests.
  The version 1 byte layout and original compatibility fixture remain unchanged.

## Verification

| Evidence | Result |
| --- | --- |
| [restore.log](restore.log) | Locked restore of all four projects |
| [build-checked.log](build-checked.log) | Release rebuild with overflow checking; zero warnings/errors |
| [core-checks.log](core-checks.log) | 478 checks; all previous rule digests remain passing |
| [reference.json](reference.json) | 2,640 generated cases; 24 C++ save fixtures with 192 complete snapshots; oracle/fixture fingerprints |
| [graphics-checks.log](graphics-checks.log) | 164 checks, 360 tour frames, no recording |
| [save-isolation.log](save-isolation.log) | All pre-existing personal save-directory files unchanged |
| [toolchain.txt](toolchain.txt) | Runtime, compiler, host and UTC date |
| [documentation-checks.log](documentation-checks.log) | Local links, checklist IDs, fixture integrity and reviewed scope |
| [source-sha256.json](source-sha256.json) | Final relevant source, fixture, reference-input and documentation fingerprints |
| [changed-inputs.json](changed-inputs.json) | Files added/changed during this goal, relative to the local pre-goal snapshot |

The core runner retains 624 town, 8,192 RNG, 11,322 character, 311,728 map/world
and 21,511 session reference records. New generated comparisons include eight
seeds × three maps × all 110 interior cells, including seed 2566201570. The C++
oracle compiled with warnings as errors and UBSan, without diagnostics.

The 20 native and four synthetic original fixtures cover all Player fields/arrays,
location, RNG, persistent tiles and placements. Each expected file records four
reopen/healing-item cycles. Separate writer and reader processes convert all 24
files, then compare 96 reload/item/save cycles against those results. Tests also
reject all truncated prefixes, invalid versions/framing/flags/counts/checksums,
cross-format loads and extended preview JSON. They verify concurrent create-only
imports, preserved sources/destinations, and CLI success/refused/malformed exits.

Graphics checks retain the previous presentation and full-save cases, adding
explicit imported-file startup, generated-candidate RNG isolation, and rejection
of raw preview data without resetting earned state. The
[menu capture](save-game-menu.png) was visually inspected; the
[town capture](default-town.png) retains the approved rendering. Artwork, effects,
input and walking timing were not changed.

## Reproduction and limits

Run the locked restore, checked rebuild and core/graphics commands documented in
[the test README](../../Yipe.Tests/README.md#p1-import-generated-maps-and-restart-comparisons)
and [graphics README](../../Yipe.GraphicsChecks/README.md). The former includes the
exact standalone C++ oracle build/run command. Reproduced fixture files must match
[Fixtures/Persistence](../../Yipe.Tests/Fixtures/Persistence); the larger generated
trace is reproducible under `build/monogame-reference/persistence/` and its digest
is retained here. Normal C# checks use the checked-in fixtures and expected digest.

The graphics run was `build/graphics-checks/yipe-graphics-20261004-060950-ddcfa60b`.
It preceded the final core rebuild; subsequent code changes only completed CLI
error handling and its tests. Graphics/renderer implementation was unchanged.
No test or game processes remain running.

This verifies translated P1 rules and persistence, not a completed campaign.
Full arrival quest/combat/chest execution, visibility/timers and wider-world
presentation remain P2/P3. Original `.yp3` unknown fields are retained only in its
source, absent RNG uses seed 1, and Windows-1252 is an explicit name-decoding policy.
Physical controllers, Windows/Linux and official console execution remain untested.
Historical full C++ replay artifacts and C# CI remain the existing P0 tasks.

The changed production paths were reviewed against the pre-goal snapshot for
correctness and simplicity: explicit format readers share existing validation and
encoding, with no parallel save-state model or speculative framework. No remaining
material findings were identified after fixing clean CLI error reporting.
