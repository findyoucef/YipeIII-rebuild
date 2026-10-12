# Complete monster roster animation

All **41 monsters** now have idle, movement, wind-up/strike/recovery/hurt and
four-stage defeat poses: **656 base poses**, including **544 newly added poses**.
Source references and the original three atlases remain in the project.
All thirteen batches pass a checked local Release build with zero warnings/errors
and 569 core checks. The full suite passes **2,162 graphics checks**; the earned opening replay matches
all 42 C++ checkpoints plus the King return and fresh-process continuation.
The earned late route defeats the empowered Gatekeeper, saves, restarts in a
separate process and completes the final rescue and live Maynard ending.

Every compiled cell is 48 × 48 with binary alpha, the shared fixed palette and
point sampling. Source sheets were inspected and corrected for repeated foot
leads, mistaken creature identity, detached marks, shadows and cell overlap.
The nine earlier accepted sheets remain byte-identical after the toe-anchor repair.

## Preview and evidence

The final contact sheets compare equal 48-pixel source cells; authored regional
bosses retain their existing doubled world scale. Each batch retains its native
sheets, continuous stride/turn, actual battle and defeat clips. Water batches also
include shoreline-crossing clips and native ripple masks. The final batch adds
waiting-guard visibility checks, ordinary/empowered parties and all three
empowered companion groups, including real slot-by-slot killing blows.

- [Idle roster](roster-idle.png), [travel poses](roster-travel.png),
  [strike poses](roster-strike.png), [defeat poses](roster-defeat.png).
- [Latest native sheets](final/native/) and [all 41 stride/battle/defeat clips](final/clips/).
- [Current ordinary/empowered boss parties](final/parties/) and [waiting-guard previews](batch-13/).
- Final [build](final/build.log), [core](final/core.log), [graphics](final/graphics.log),
  [earned opening](final/opening.log) and [earned finale](final/late.log) logs.
- [Exact source/generation metadata](assets/), [rollout progress](progress.json).

## Batch checks

The first two batches run the full graphics suite. Later art iterations use the
focused monster/combat mode; every registered creature is still checked.
Batches 3–4 share a verified run after the source cleanup and foot-anchor fix.
The first final full run captures all 41 creatures. After six source sheets were
corrected for stray pixels, a second full suite rechecks all 41 and recaptures those
six plus all boss parties. Its new detached-mark checks pass for every new sheet.
The retained batch artifacts show their original iteration; final galleries and
clips show the accepted cleanup. The full suite also rechecks world effects,
controls, maps and storage. Content startup plus five frames takes 2.73 seconds.

| Batch | New IDs | Graphics checks | Evidence |
| --- | --- | ---: | --- |
| 1 | 7, 9 | 1658 | [Log](batch-01/graphics.log) |
| 2 | 10, 11 | 1676 | [Log](batch-02/graphics.log) |
| 3 | 12, 16, 20 | 358 | [Log](batch-03/graphics.log) |
| 4 | 13, 14, 18 | 358 | [Log](batch-04/graphics.log) |
| 5 | 15, 19, 24 | 405 | [Log](batch-05/graphics.log) |
| 6 | 17, 23, 31 | 438 | [Log](batch-06/graphics.log) |
| 7 | 21, 22, 26 | 472 | [Log](batch-07/graphics.log) |
| 8 | 25, 28, 29 | 512 | [Log](batch-08/graphics.log) |
| 9 | 27, 32 | 534 | [Log](batch-09/graphics.log) |
| 10 | 30, 33, 34 | 575 | [Log](batch-10/graphics.log) |
| 11 | 35, 36, 37 | 620 | [Log](batch-11/graphics.log) |
| 12 | 38, 39, 40 | 671 | [Log](batch-12/graphics.log) |
| 13 | 41 | 743 | [Log](batch-13/graphics.log) |

## Scope and review

Presentation reuses the existing two actor trackers and combat timeline. It
preserves accepted 250 ms travel, logical destinations, AI/terrain permissions,
combat calculations, RNG and save format. The empowered flag is copied into the
display-only resolved actor snapshot; it creates no new saved state. Texture
creation and palette compilation happen at content load, with no per-frame
texture creation. Source/pose placement and native graphics checks were reviewed
by the primary agent. Antigravity supplied an earlier rendering draft; the later
water-test draft timed out and supplied no usable result. Final artwork used the
built-in image generator after the Antigravity image experiment failed alpha/grid
requirements. No distributable was repackaged during this work. The completed local build is
open using a separate copy of the current preview save; the existing game process
was preserved. [Launch verification](final/launch.json).

An earlier core run encountered the existing concurrent-import test failure once.
The subsequent core runs pass all 569 checks. No storage/core code was changed
for the monster rollout; the tests and launch use isolated saves.

[Personal-file audit](personal-files.json): three files differ from the earlier
pre-goal hashes; their modification times precede the first graphics run. The
verification checkpoint records the current files without restoring or writing
them. All graphics runs select an isolated save and its per-save profile.
