# Enemy information — 2026-10-07

Added **Enemy info** to the battle commands while retaining Flee in the initial
six visible rows and keeping Change target in the scrollable list. The panel
retains the battle HUD, HP labels and target brackets. It shows the inspected
enemy's actual attack/defense ratings, rank, attack-spell resistance, defeat
count and empowered status when active. Ratings are explicitly distinguished
from damage per hit. Incomplete imported histories use “Recorded defeats.”

Left/right browses living enemies, wrapping and skipping defeated/empty slots.
An overlay-local inspection cursor leaves the actual battle target unchanged.
Back or Escape/controller Back restores the same parent menu and command.
Inspection uses no turn or resources, changes no gameplay/RNG state, and remains
open under Auto pacing. A transformed rabbit uses its live altered stats rather
than the previous creature's or catalog defaults. No new combat knowledge table,
save field, unlock mechanic or speculative elemental weakness was added.

Validation: checked local Release build with **zero warnings/errors**, **569 core
checks** and **1,534 graphics checks** pass. Eleven new inspection checks cover
live stats and partial history, separate inspection/attack cursors, living-target
wrap/skip, unchanged full game/battle/RNG snapshots, Auto isolation, both Back
paths, transformed stats, empowered traits, text fit and exact native pixels.
The full graphics suite also renders 360 tour frames.

The earned opening replay and fresh-process continuation also pass: all 42 C++
checkpoints plus the King's reward, 116 victories, 14 chests and two rescues.
The replay renders 9,940 frames, followed by 135 after restarting and reloading
the isolated save. [Opening and restart log](opening.log).

Final graphics run:
`build/enemy-info-graphics/yipe-graphics-20261007-234019-7eca7f35`.
The normal, rabbit and empowered captures plus the revised command menu were
visually inspected. The scoped simplify review covered all production, help,
test and documentation changes; no further findings. The feature reuses the
existing overlay and target-navigation flow with one independent cursor.

Tests use isolated saves and profiles. No task command wrote personal game files.
The personal save and map journal changed while the previously launched game
was open; profile and preview hashes remained unchanged. Actual comparisons are
retained in [personal-file-checks.json](personal-file-checks.json). Development
used a local build only; no distributable was rebuilt.

[Enemy info](enemy-info.png), [transformed rabbit](enemy-info-rabbit.png),
[empowered enemy](enemy-info-empowered.png), [command menu](choices.png),
[build log](build.log), [core log](core.log), [graphics log](graphics.log),
[source hashes](source.sha256).
