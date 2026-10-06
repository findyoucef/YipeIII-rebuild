# Area and world map — 2026-10-04

Completed the accepted map recommendation as a presentation-only desktop feature.
M / View opens a full-screen area map first; Actions also contains Map.
Enter/South switches to the visited-world overview; Left/Right changes discovered
layers; M, Escape, View or East closes. Start opens pause over the map.

The area view fits all 13 × 12 tiles, uses existing art, marks the player's
position/facing and visible services, entrances, stairs and bridges. The world
view arranges surface, upper/deep caves and alternate regions by original grid
coordinates, drawing known adjacent/wrapped crossings and listing current-region
travel links (including other layers). Unvisited areas and unseen cells remain
dark, and hidden passages/trees receive no special markers. The area view always
shows the current region; the world grid is an overview, not a region browser.

Map viewing freezes movement, encounters, hazard timers and scene animation.
It clears buffered movement and resumes without replaying paused time. Active
interactions and text entry retain control. Map artwork/UI stays sharp under
Strong miniature, while the approved scanlines remain in effect.

Discoveries use a matching `.map.json` companion bound to the SHA-256 of the
complete saved `.y3m` bytes. Each file is replaced atomically, adventure first;
the pair is not a single transaction. Missing notes are a normal older-save case.
Damaged or mismatched notes are ignored with a message, without blocking a valid
adventure. Keep both files together when copying a save. Static discoveries
accumulate; regenerated caves clear the previous shape. The gameplay save format
and core visibility/RNG behavior remain unchanged.

## Verification

- Checked Release build: zero warnings/errors (`build.log`).
- **529** core/input/storage checks (`core.log`), including 27 new journal/input
  cases. Covers secret markers, visible-only discovery, all 115 coordinates,
  generated reset, unchanged gameplay bytes/RNG, corruption/stale hashes and
  failures writing either file. All files used isolated temporary directories.
- **648** graphics checks and 360 tour frames (`graphics.log`), including 15 new
  map checks. Covers pause/mid-step behavior, navigation, world layers, generated
  rendering, sharp UI, fog pixels, production save/load and menu/text handoff.
  Existing appearance checks also verify that new adventures reset map history.
- Earned opening: all **42** original C++ checkpoints plus King return still
  match. Real combat refuses map interruption. Separate-process continuation
  restores **14 earned regions and 17 travel links**, preserves discoveries,
  finishes the route and saves again (`opening.log`, `opening-resume.log`).
  The runner's pre-existing selected-portrait normalization remains unchanged.
- `earned-restart/` retains the earned save, companion and input cursor used by
  that fresh-process check. Diagnostic map screenshots use constructed visit
  history to display multiple layers; they are not proof of earned progression.
- Eight map captures retained and inspected at 1280 × 720, including controller
  hints, cave fog, generated terrain, and alternate-world placement.
- All **26** gameplay-core source files retain their campaign-completion hashes
  (`unchanged-core.json`). All **three** pre-existing personal save/profile files
  retain their pre-task hashes (`user-save-preservation.json`).
- **63 of 77** comparable prior graphics captures are byte-identical, including
  village, world and terrain diagnostics (`prior-render-comparison.json`). The
  fourteen portrait/creation captures now occur after the extra map walking
  checks at a different ambient scene time; art assets were not changed.

Main graphics run: `build/graphics-checks/yipe-graphics-20261004-165409-103421f3`.
Opening run: `build/graphics-checks/yipe-graphics-20261004-165012-00036070`, with
child `yipe-graphics-20261004-165059-d568a417`. These build paths are disposable;
the selected evidence and restart pair are retained here.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
```

Graphics checks require an active desktop and exclusive window focus. Controller
mapping is exercised with synthetic gamepad states and the controller UI is
rendered; physical-controller and other-OS verification remain P5/P6 work.

## Review and resume

The implementation uses the existing input, action menu, save store, renderer,
visibility boundary and partial TownGame structure. `MapJournal` contains only
presentation data that must survive visits/saves. `TownMapOverlay` contains the
map controls and rendering; the extracted scenery method is shared by play and
map views. No separate simulation, unvisited map generation, or new gameplay
abstraction was introduced. The pre-handoff simplicity review found no remaining
actionable issues in this feature's production diff.

Feature complete. The updated app was launched for user testing. Continue the
remaining P5/P6 tasks in `monogame/PORTING_PLAN.md`; do not redo P1–P4 or assume
console readiness. No commit was requested. `source-inputs.json` fingerprints
the feature's source and documentation in this untracked MonoGame working tree.
