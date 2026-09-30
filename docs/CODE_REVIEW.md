# macOS game-code review — 2026-09-30

Reviewed the current game sources and macOS adapters, concentrating on native
save/import framing and version migration, save/open/quit control flow, input
dispatch, timer pauses, map events, and combat/shop/quest changes. This is a code
review with automated checks, not an original-game playtesting result.

## Resolved findings

**Save-write failures discarded the active session.** An I/O exception from
`save_current_game` previously unwound `run_game`. The application either exited
or returned to startup, losing unsaved progress. Save and Save As now display
Save Game Failed and retain the live game. Save and Leave and window close stay
in the game when saving fails. A save destination is adopted only after its
file has been written; failed writes are not added to recent files.

Regression tests drive the production event loop through failed F5, Ctrl+S,
Save As, Quit/New/Open with Save and Leave, window close, and a first save. Each
case produces a real temporary-file write failure, continues movement, and
successfully saves to a different destination. Tests compare the original file
byte for byte and check player state, RNG, recent files, and paused exploration
time. Direct missing-directory checks also verify that failed Save As and first
save do not adopt the unwritten destination.

**High-score write failures interrupted gameplay.** Score submission from
statistics, Ctrl+H, level-up, completion, and completed-game resume now shares
an error handler that displays High Scores Save Failed and continues play.
The existing persistence helper commits the in-memory table only after the
file write succeeds. An event-loop regression verifies unchanged in-memory
and persisted scores, continued movement and saving, paused exploration time,
and a successful later score submission.

## Validation

A separate clean arm64/macOS 26.0 Release build completed without compiler
warnings. All seven CTest checks passed in 45.98 seconds, including 665 application
cases and the write-failure regressions above. The extracted CPack
archive passed verification of 137 assets, bundled SDL2 and relative library
paths, ad-hoc signatures, and headless startup outside the source directory.
The GitHub workflow passes `actionlint` and runs the same build, test, package,
and verification steps.

Clang's static analyzer completed without diagnostics for all 18 distinct
non-generated production translation units, including the two macOS adapters.
The changed application source was analyzed again after the repairs with no
diagnostics.
The findings came from control-flow review and focused I/O diagnostics; a clean
analyzer result does not cover application exception recovery.

The [first hosted CI attempt](https://github.com/findyoucef/YipeIII-rebuild/actions/runs/36786718551)
was rejected before any checkout or build step: GitHub reported, "The job was
not started because an Actions budget is preventing further use." Hosted CI
therefore remains unverified until the account's Actions budget allows a run.
The local build/test/package results above are independent of this restriction.

Large progression traces and original-UI captures remain local. The four small
original save fixtures required by the automated import tests are included in
the repository. Original GUI parity and interactive distribution remain outside
this review.
