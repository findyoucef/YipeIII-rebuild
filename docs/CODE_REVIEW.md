# macOS game-code review — 2026-09-30

Reviewed the current game sources and macOS adapters, concentrating on native
save/import framing and version migration, save/open/quit control flow, input
dispatch, timer pauses, map events, and combat/shop/quest changes. This is a code
review with automated checks, not an original-game playtesting result.

## Finding

**[P1] Save-write failures can discard the active session —
`app/main.cpp:3576`.** `save_current_game` calls `save_game_atomic` without
catching I/O failures. F5/Ctrl+S, Save As, window close, and Save and Leave in
the Quit/New flows likewise lack a local catch. If the chosen directory is
removed or the temporary file cannot be written, the exception unwinds
`run_game`. A newly created game's outer handler exits with code 1; a loaded
game's handler reports Load Game Failed and returns to startup, losing the
unsaved in-memory session.

A temporary diagnostic compiled against the existing application test harness
called Save, Save As, and Save and Leave with a missing parent directory. All
three let `could not open temporary save file` escape. No save was created,
and no existing player file was modified. The code's outer exception handlers
establish the session-loss consequence; no desktop session was used for this
diagnostic.

The localized repair is to catch save errors at the shared save boundary, show
Save Game Failed, and return false so callers retain the active game and its
previous destination. Add event-loop coverage for failed Save/Save As/quit/New
and a successful retry. This review does not modify game behavior.

## Validation

A separate clean arm64/macOS 26.0 Release build completed without compiler
warnings. All seven CTest checks passed in 51.47 seconds. The extracted CPack
archive passed verification of 137 assets, bundled SDL2 and relative library
paths, ad-hoc signatures, and headless startup outside the source directory.
The GitHub workflow passes `actionlint` and runs the same build, test, package,
and verification steps.

Clang's static analyzer completed without diagnostics for all 18 distinct
non-generated production translation units, including the two macOS adapters.
The save-error finding comes from control-flow review and the focused diagnostic;
a clean analyzer result does not cover application exception recovery.

Large progression traces and original-UI captures remain local. The four small
original save fixtures required by the automated import tests are included in
the repository. Original GUI parity and interactive distribution remain outside
this review.
