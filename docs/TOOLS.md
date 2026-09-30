# Tools and reproducible commands

Recorded on 2026-09-15 on arm64 macOS 26.6.2.

| Tool | Version | Purpose |
| --- | --- | --- |
| Ghidra | 12.1.3 | Static analysis and decompilation |
| OpenJDK | 21.0.12.1 | Ghidra runtime |
| Python | 3.14.6 | PE resource extraction script |
| Apple Clang | 21.0.0 | C++20 build |
| CMake | 4.4.3 | Configure/build/test generation |
| Git | 2.54.0 | Source control |
| SDL2 | 2.32.10 | Selected application UI, input, rendering, and audio layer |
| Whisky | 3.7.0 build 55 | Run the original 32-bit Windows executable on Apple Silicon |
| Whisky Wine | `wine-11.0` from library archive `v3.1.1` | Original-runtime compatibility layer |

## Verify supplied inputs

```bash
./tools/check_inputs.sh '/Users/baouchiy/Downloads/Yipe! III'
```

## Build and test

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
```

`cpack --config build/CPackConfig.cmake -G TGZ` writes an installable archive to
`build/packages/`. On macOS it contains `Yipe III.app`, documentation, and the
package verifier. CMake's BundleUtilities copies non-system dependencies into
the application and rewrites their load paths; the install script then signs
nested libraries and the bundle ad hoc and verifies the result. The build-tree
app still uses development dependencies; the installed/packaged app includes SDL2.

Run `python3 tools/verify_mac_package.py build/packages/YipeIII-rebuild-0.1.0-Darwin.tar.gz`
to extract the archive outside the checkout, reject external dependencies and
symlinks, verify signatures and required content, and run headless smoke from an
empty working directory. The loader trace must show the bundled SDL2 and no
Homebrew libraries. Python 3.9+ and Apple's command-line tools are required for
this optional verifier. Interactive launch on other machines,
and Developer ID signing/notarization remain open.

## Prepare embedded resources

```bash
python3 tools/prepare_embedded_assets.py \
  /Users/baouchiy/Downloads/YipeIII-analysis/resources
```

This verifies every extracted payload against `resources/index.json`, preserves all 301 raw resources, adds BMP file headers to the 128 DIB resources, copies the 39 embedded WAV files, and writes a resource manifest.

Decode the Windows interface templates:

```bash
python3 tools/catalog_windows_ui.py assets/embedded/raw
```

The resulting `assets/embedded/ui-catalog.json` records all dialog/control geometry, labels, IDs, styles, the application menu tree, and accelerator bindings.

CMake copies all prepared embedded WAV files and the 20 supplied external
monster WAV files, plus `Yipe3Items.txt`, into the runtime asset tree. The
application smoke test uses SDL's dummy video and audio drivers, decodes every
dispatched embedded effect, checks all packaged equipment names, and verifies
that every packaged monster WAV is present and decodable.

The shortcut regression target includes the application translation unit to test
its existing event loop without changing production visibility. It substitutes
native dialog calls, the preference directory, and a controlled clock, runs SDL with dummy
drivers, and writes saves under a fresh temporary directory. Its original 115 cases
cover Control/Shift sides, lock keys, repeats, Save As cancellation, and actual
save destinations/content. The suite also covers F10 mode
changes and save/reload, both shops and Equipment, global-preference separation,
all 12 creation portraits, mode transitions, invalid selection, and cancellation.
Timer cases cover idle updates, both princess routes through movement/rescue/return, saved riders, closing fire damage, and modal pauses. A real R08 archive passes Open/native Save without changing the original. Core persistence cases cover version 20, older-version defaults, and invalid or missing mode/chest/shop/suffix bytes. Chest and Quizilog cases exercise spray acquisition, sprite removal, both shoe-reward entrances after earned victories, and one-time state. Miscellaneous-shop cases enter all four real entrances, buy with exact gold, exercise Outback's key offer/exit/re-entry, and use the purchased key on a real door. High Score Code cases cover active-player access, copy/cancel/failure/retry, recorded-counter warnings, and unchanged player/RNG state under a delayed modal. These verify routing and content, not native panel rendering.

Mouse cases attach the actual window ID to events, exercising SDL's logical
input scaling at three window sizes including letterboxing. They cover eight
movement directions, ignored clicks, startup/portrait/combat selection, and
complete mouse/keyboard result equivalence for terrain and arrival actions.
Statistics cases compare rendered pixels for all 12 portraits and invalid-ID
fallbacks at those same three sizes, retain every numeric field and full names,
exercise every button and keyboard navigation, and cover redraw/quit events.
Ctrl+T cases preserve exploration state through a simulated long modal visit;
High Scores still submits and persists the current player only when selected.
Keyboard cases cover all 13 edited/saved bindings, seventeen added key mappings,
conflict acceptance/cancel, unsupported chords and retry, close/quit propagation,
fixed keys after rebinding, actual exploration/combat dispatch, and modifier/repeat
handling. Core cases cover every pair of duplicate movement/combat actions and
unchanged native settings persistence. For keyboard changes also build/run
`yipe_core_tests`; it is the existing core runner without per-case selectors.
E065-E066's continuous two-rescue route remains regression coverage. For the
input changes alone, build `yipe3` and `yipe_app_shortcut_tests`, then run
`ctest --test-dir build -R '^yipe_app_(smoke|shortcut_tests)$' --output-on-failure`.

## Recover the original manual

Recover the 28 original Help and Info pages, preserving each title and body:

```bash
python3 tools/extract_help.py \
  --strings /Users/baouchiy/Downloads/YipeIII-analysis/resources/strings.jsonl \
  --output app/generated/original_help.hpp
```

The generated header is checked in; ordinary builds do not need the analysis
directory. SDL smoke checks every formatted page for word preservation and
line length, page bounds, and startup F1/repeat handling.

## Recover static map tables

```bash
./tools/extract_map_tables.py \
  --decompiled-dir /Users/baouchiy/Downloads/YipeIII-analysis/decompiled \
  --output assets/maps/original-map-tables.json \
  --cpp-output src/generated/original_maps.cpp
```

The command reads the map dispatcher, static loader functions, and compact event
records from the Ghidra C export. It validates every 13×12 table and all event
helper counts before writing deterministic JSON and C++. Runtime-generated map
IDs `0x76`–`0x78` are intentionally excluded.

## Recover monster catalogs

```bash
./tools/extract_monster_placements.py \
  --source /Users/baouchiy/Downloads/YipeIII-analysis/decompiled/functions/0041ec40.c \
  --output assets/maps/original-monster-placements.json \
  --cpp-output src/generated/original_monsters.cpp

./tools/extract_monster_definitions.py \
  --stats-source /Users/baouchiy/Downloads/YipeIII-analysis/decompiled/functions/00405640.c \
  --damage-source /Users/baouchiy/Downloads/YipeIII-analysis/decompiled/functions/004042e0.c \
  --strings /Users/baouchiy/Downloads/YipeIII-analysis/resources/strings.jsonl \
  --output assets/game-data/original-monster-definitions.json \
  --cpp-output src/generated/original_monster_definitions.cpp
```

The placement extractor validates all 186 records and assigns stable catalog
indices before generating the runtime catalog. The definition extractor joins
the 41 setup-stat records, damage
ranges, resource names, and bitmap IDs, and fails when any required field is
missing.

## Recover weapon damage ranges

```bash
python3 tools/extract_weapon_damage.py \
  --executable '/Users/baouchiy/Downloads/Yipe! III/Yipe!III.exe' \
  --objdump /opt/homebrew/opt/llvm/bin/llvm-objdump \
  --output assets/game-data/original-weapon-damage.json \
  --cpp-output src/generated/original_weapon_damage.cpp
```

The extractor statically disassembles `0x00417ab0`, follows the register values
written into the function's local table, and requires a valid minimum/maximum
pair for every weapon rank from 1 through 42.

## Re-export the preserved Ghidra project

Close the project in the Ghidra GUI first. The canonical generated output remains in `/Users/baouchiy/Downloads/YipeIII-analysis`; the copy under `evidence/analysis-snapshot` is a point-in-time snapshot.

```bash
env JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home \
  /opt/homebrew/opt/ghidra/libexec/support/analyzeHeadless \
  /Users/baouchiy/Downloads/YipeIII-analysis/ghidra YipeIII \
  -process YipeIII.exe -noanalysis \
  -scriptPath /Users/baouchiy/Downloads/YipeIII-analysis/scripts \
  -postScript ExportYipe.java \
  /Users/baouchiy/Downloads/YipeIII-analysis/decompiled
```

## Apply the checked Ghidra types and signatures

```bash
env JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home \
  /opt/homebrew/opt/ghidra/libexec/support/analyzeHeadless \
  /Users/baouchiy/Downloads/YipeIII-analysis/ghidra YipeIII \
  -process YipeIII.exe -noanalysis \
  -scriptPath /Users/baouchiy/Downloads/YipeIII-analysis/scripts \
  -postScript ApplyYipeTypes.java
```

Ghidra saves a processed project automatically. `analyzeHeadless` 12.1.3 has no `-save` option.

## Recover mouse movement

The view's message-map entry at file offset `0x8f7f8` maps `WM_LBUTTONUP`
(`0x202`) to `0x00410f50`. The callback was absent from the earlier C export;
direct disassembly establishes tile-based direction selection and its calls
to the same movement and action routines used by keyboard input (E067).

```bash
od -Ax -tx4 -j $((0x8f7f8)) -N 24 '../Yipe!III.exe'
/opt/homebrew/opt/llvm/bin/llvm-objdump -d \
  --start-address=0x410f50 --stop-address=0x411081 '../Yipe!III.exe' \
  > evidence/corrected-analysis/mouse-input.asm
```

## Recover keyboard input

Dialog 161 and `0x00409fa0` establish raw single-key capture, not a hot-key control.
The filter and movement/combat branch order are retained as direct executable
evidence (E069). Reproduce `evidence/corrected-analysis/key-input.asm` with:

```bash
{
  /opt/homebrew/opt/llvm/bin/llvm-objdump -d --start-address=0x4060f0 --stop-address=0x406420 '../Yipe!III.exe'
  /opt/homebrew/opt/llvm/bin/llvm-objdump -d --start-address=0x409fa0 --stop-address=0x40a4c0 '../Yipe!III.exe'
  /opt/homebrew/opt/llvm/bin/llvm-objdump -d --start-address=0x40a9b0 --stop-address=0x40aa4a '../Yipe!III.exe'
  /opt/homebrew/opt/llvm/bin/llvm-objdump -d --start-address=0x40bbb0 --stop-address=0x40bdd5 '../Yipe!III.exe'
} > evidence/corrected-analysis/key-input.asm
```

## Recover High Score Code

The encoding, including formatting arguments missing from Ghidra's output,
is retained in E070's direct disassembly. Reproduce it with:

```bash
/opt/homebrew/opt/llvm/bin/llvm-objdump -d --start-address=0x41c630 --stop-address=0x41caac '../Yipe!III.exe' > evidence/corrected-analysis/high-score-code.asm
```

The message-map record is at file offset `0x90838`; dialog 458 and initializer
`0x00408a90` identify the code and historical email address. The original view
vtable at file offset `0x8f844` also establishes the original printing path:
slots `+0xf4`/`+0xfc` point to preparation `0x471056` (returns 1) and printing
`0x470df7`, which calls virtual `+0xf0`, the game draw handler `0x40b880`.
The print/preview command handlers are `0x484797`/`0x483082`. E071 implements
their native macOS equivalents; framework origin does not exclude commands
from the rebuild scope.

## Printing and preview (E071)

The original Print, Preview, and Setup message-map records are at executable
file offsets `0x925c8`, `0x925f8`, and `0x922c0`, respectively. Print/Preview use
the view callback described above. `0x40b880` draws the 13-by-12 map through
`0x40bdf0` and its visible chests, monsters, special sprites, and player through
`0x40e2d0`. The rebuild reuses `render_game` on a logical-size SDL render target,
then passes an owned RGBA snapshot to AppKit. It never reads the already-swapped
window backbuffer or prints letterboxing.

The macOS `yipe_printing_tests` CTest target exercises the real print operation
with PDF saving and no printer job. It writes four fixtures under
`build/print-output/`: Letter portrait, Letter landscape, small custom paper,
and 50 percent scale. CoreGraphics rasterization checks all four image regions,
orientation, aspect ratio, fit, and centering within the intersection of margins
and the printer's imageable area. Color classification allows ColorSync's
channel conversion. All four PDFs also render with Poppler. SDL input cases
compare the captured pixels with the game frame across six maps and three
window sizes, and compare full saved state before/after delayed print panels.

An additional desktop-only invocation opens and automatically cancels both
native panels, checking that cancellation preserves print preferences:

```bash
build/yipe_printing_tests "$PWD/build/print-output" --cancel-panels
```

It runs the cancellation timer in AppKit's modal run-loop mode, requires an
active macOS desktop session, and submits no physical print job. The ordinary
CTest target does not open these panels. Physical printer behavior and an
original-runtime comparison remain open; the native preview and centered
single-page layout are recorded adaptations, not exact MFC presentation claims.

## Run the original

See `ORIGINAL_RUNTIME.md` for the bottle, runtime hashes, launch command, and current limitations.

## Recover omitted timer callbacks

```bash
env JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home \
  /opt/homebrew/opt/ghidra/libexec/support/analyzeHeadless \
  /Users/baouchiy/Downloads/YipeIII-analysis/ghidra YipeIII \
  -process YipeIII.exe -noanalysis \
  -scriptPath "$PWD/evidence/scripts" -postScript RecoverMapTimer.java \
  "$PWD/evidence/corrected-analysis/map-timer"
```

The corrected exports preserve the callback, platform mover, and hazard
mutator. Direct disassembly and the message-map offset are recorded beside
them. No terrain or executable patch is involved.

The prior Win32 capture helper can be built with MinGW:

```bash
mkdir -p build/reference-runtime
x86_64-w64-mingw32-gcc -Wall -Wextra -O2 evidence/scripts/original_ui.c \
  -o build/reference-runtime/original_ui.exe -lgdi32 -luser32
```

Run it under the original's Wine prefix with `list` to enumerate the game's
controls; handles change between runs. `click HWND`, `key HWND VK`,
`command HWND ID`, and `text HWND TEXT` send ordinary window messages.
`capture` uses PrintWindow, which returned black pixels on this host and
is not accepted as visual evidence. The R08 directory preserves the actual
archives and control dumps, their hashes, the capture sequence, and limits.


## Recent-document integration checks (E072)

```sh
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel 4
ctest --test-dir build --output-on-failure
ctest --test-dir build -R file_history --output-on-failure
```

The complete 2026-09-25 run passed seven targets in 43.35 seconds, including
585 SDL cases. The three native history fixture phases launch independent
processes with bundle ID `org.yipe3rebuild.tests.filehistory`, which the test
asserts before touching history. They do not use the game's history. The write
phase adds two fixture files (including spaces and Unicode), re-adds a normalized
path after a distinct recent-use timestamp, and checks order/deduplication.
Read checks persistence in the next process; Clear removes the test history and
fixture files. CTest fixture dependencies also run cleanup after a failure.
AppKit can normalize Unicode path spelling; assertions compare file identity.
The test bundle is not installed or included by CPack.

One earlier SDL run printed all 565 then-current assertions passed but exceeded
the process timeout during exit; the final rebuilt full suite exited normally.
No timeout increase was made. This is recorded separately from gameplay results.


## Edit and status-bar checks (E073)

The normal build/CTest commands now exercise 653 SDL cases. The full seven-target
run passed in 45.37 seconds. The SDL suite writes
`build/interface-output/status-bar.bmp`, a 416×404 capture with all 384 map rows
and a separate 20-pixel strip. This image was visually inspected. Printing is
compared byte-for-byte with the hidden-bar map and must restore the previous
logical dimensions. Existing hidden-bar pixel checks remain, with separate
visible-bar input/render cases and the continuous E066 route run with the bar.

Text input events and native clipboard failures are scripted through the existing
SDL test boundary. An injected wait-failure test initially spun because its stub
returned SDL_SetError's `-1`, while SDL_WaitEvent reports failure as `0`. Sampling
identified the stub loop; the corrected stub returns zero. Final suites exit
normally without a timeout increase. This was a test-harness error.

Original command recovery is recorded in
`evidence/corrected-analysis/interface-commands.asm` and `menu-commands.txt`.
The absence of an aligned direct Edit-command entry is recorded as such, rather
than inventing a gameplay undo operation from generic resource entries.

## Full-game completion driver (E074)

`env SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy
./build/yipe_app_shortcut_tests --full-game` runs the existing fresh-game opening
and then reads commands from stdin in the same `run_game`. Its printed temporary
directory contains `commands.txt`, `trace.txt`, and observation snapshots.

Commands are `walk DIGITS` (numeric-keypad directions), `wait TICKS`, `spell INDEX`
(low power), `combat INDEX low/high` (attack-spell preference), `heal INDEX low/high`,
`target STEPS`, `opening INDEX low/high|off`, `flee on/off`, `mana on/off`,
`type TEXT` (the next blocking text picker), `key i`, `key c`, `key other`,
`snapshot`, and `quit`. Native dialogs print available button IDs and
accept `choice ID`. Combat uses earned Backstab, healing spells/food, and the
selected attack spell through ordinary SDL input. Commands and dialog searches
are bounded; EOF and invalid commands fail explicitly. `quit` succeeds only
after the saved observation shows all 20 rescues, six stamps, and Gatekeeper
state 3, the unchanged default class/difficulty, earned movement items and
strength requirement, a living player in town, and the live finale message.
The unchanged opening checks must also pass.

In opt-in full-game mode, the SDL rasterization/presentation calls are omitted
during the progression run to avoid repainting millions of hidden rest frames.
Game code, timer callbacks, input, and native observation saves still run.
A rendered Release run and this Debug run matched all 2,114 observations
through level 19, including RNG and every logged player field; the comparison
is retained in `evidence/progression-audit/full-game/rendering-comparison.json`.
The ordinary 653-case SDL suite still uses real rendering and passed afterward.
This acceleration supplies state/progression evidence, not a visual playthrough.

The winning fixture is `evidence/progression-audit/full-game/attempt-12/commands.txt`.
Replay it from fresh construction with:

```sh
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy \
  ./build-release/yipe_app_shortcut_tests --full-game \
  < evidence/progression-audit/full-game/attempt-12/commands.txt
```

Earlier `attempt-*` logs include failed and interrupted sessions. The final
replay comparison and limitations are documented in `full-game/README.md`.
`evidence/progression-audit/navigate.cpp` proposes static-terrain routes from an
observation snapshot. It does not simulate moving terrain, encounters, or Other
actions, and its proposed directions require verification in the actual game.

## Variant completion and original-rule probe (E075)

`--full-game-variant TYPE DIFFICULTY SEED` uses the same input-only continuation
after normal production character creation. TYPE is 1 Combat, 2 Quickness,
3 Magic, or 4 Random; DIFFICULTY is 1 Easy, 2 Medium, or 3 Hard; SEED is a uint32.
It starts at level 1 with zero gold/XP and no earned progression flags. The same
completion requirements apply, with the selected specialty/difficulty preserved.
Observation files are never loaded into the running game. An early quit/EOF
fails; retained authoring prefixes are not passing completion fixtures.

The [original-rule probe record](../evidence/verification-milestone/original-probe/README.md)
contains compiler/runner commands, case counts, hashes and limitations for
`evidence/scripts/runtime_compare.cpp`. It executes original machine code in
isolated objects and compares outputs against public rebuild APIs. It does not
drive the original GUI or satisfy every reference case.

E076's [Quickness/Easy seed-2 fixture](../evidence/verification-milestone/variants/README.md)
replays literal inputs without the Python authoring helper and matches all
15,996 states from fresh creation. `combat off` selects physical attacks while
retaining learned healing; it also preserves mana potions. A later
`combat INDEX low/high` restores spell attacks. This affects only the input bot.
`flee above HP` reads the actual initialized enemies and repeatedly selects the
ordinary flee action when any enemy exceeds that HP. `flee on/off` clears this
optional limit. Encounters, failed escapes, counterattacks and resource costs
still execute normally. The inactive policy preserves both completed fixtures:
the default replay matches 15,367 states and Quickness/Easy/2 matches 15,996;
the full Release suite passes all seven tests in 44.77 seconds. See
`variants/*-flee-policy-comparison.json` and `variants/ctest-flee-policy.log`.

`evidence/progression-audit/navigate.cpp` reads an observation save and proposes
static terrain moves. Compile it against `build-release/libyipe_core.a` with
the C++20 compiler and `-I include`. Its optional sixth argument is the observed
RNG state immediately before entering the current generated Hell map at (1,1).
For example, the E076 map-118 instance uses pre-entry RNG 102089327 and prints
post-generation RNG 2727344946, matching the live entry observation. Generated
corridors are not stored in native saves, so the planner reconstructs that one
instance with the production generator. It neither changes the live RNG nor
loads a snapshot into the run. It does not simulate future generated instances,
encounters, Other actions, or moving/timed terrain. Every proposal must still
be checked against the actual production input result.

E077 adds `--generated-maps` to both original and rebuild probe modes. It checks
130 complete grids (seeds 0..63 and the earned-route crash seed 2566201570,
entries (1,1) and (11,10)). All 20,280 tile/RNG records match; sanitizer and
regression details are in [the clamp evidence](../evidence/verification-milestone/generated-map-clamp/README.md).
E078 retains [Magic/Hard seed-3 commands](../evidence/verification-milestone/variants/magic-hard-seed3/attempt-06/commands.txt)
for `--full-game-variant 3 3 3`: an independent fresh replay exits 0 and matches
all 36,191 states.

E079 retains [Random/Medium seed-4 commands](../evidence/verification-milestone/variants/random-medium-seed4/attempt-02/commands.txt)
for `--full-game-variant 4 2 4`. The independent separate clean-build replay
exits 0 and matches all 36,072 states, command bytes, complete trace and final
save bytes. [The historical matrix](../evidence/corrected-completion/historical-completion-matrix.json)
links all four completed fixtures and their 103,626 exact observations.

The corrected E080 archive is built with
`cmake --build build-generated-map-clamp-release --target package` and checked
with `python3 tools/verify_mac_package.py` against its TGZ path. The
[package record](../evidence/verification-milestone/generated-map-clamp/package.json)
retains the final checksum and local verification limits; earlier E074/E075
archives remain preserved.

E081/E082 adds `--counterattacks`, `--flee` and `--late-rules` to the same
offline original/native probe. [The record](../evidence/persistence-parity/README.md)
distinguishes directly executed routines from composed failed-flee/purchase
arithmetic and excludes UI-dependent reward/quest finalization.

The SDL harness adds explicit partial/checkpoint modes `--checkpoint` and
`--checkpoint-variant TYPE DIFFICULTY SEED`. They require a production save and
explicit quit and clearly state that completion was not asserted. Normal
`--full-game*` modes still require the live finale. `YIPE_CHECKPOINT_COMMANDS`
is a whitespace-separated list of input-command ordinals whose completed saves
are copied to `checkpoint-N.y3r` within the isolated test directory. No copied
observation is reloaded into the fresh creator run.

`--resume-game SAVE.y3r` starts a separate process and uses production native
Open on an actual saved file. It requires save/quit but does not assert full
completion. Input policy commands may be restored after Open; they do not alter
character fields. E082 exercises eighteen restart files and five ordinary
continuations. `compare_saves.cpp` normalizes RNG only in offline observations
to compare every serialized field. Those normalized files never enter play.
The four E077 completion fixtures remain preserved. E084 supplies new literal
baselines after the three E081 random combat corrections.

E083 uses `cmake --build build-persistence-parity-release --target package` and
the same independent `verify_mac_package.py` against its TGZ. The separate clean
Release suite passes all seven tests in 46.16 seconds. [The package record](../evidence/persistence-parity/package.json)
includes the final archive hash, prior-archive preservation and scope limits.

E084 records four corrected-build production-created full-game routes and
independent fresh-process replays: 108,879 exact states in total, with identical
commands, full traces and final saves. The [completion record](../evidence/corrected-completion/README.md)
contains the four `replay_fixture.py` invocations and preservation checks. That
helper starts a new native process and supplies only retained literal inputs;
it never uses native Open or a checkpoint. It refuses existing replay directories.
The author helper's boat waits and earned-resource loops expand to ordinary
native commands; route planning reads observations without loading them into play.

E085 uses the separate clean `build-corrected-completion-release` build. The
seven-test Release suite passes in 49.36 seconds. Build the archive with
`cmake --build build-corrected-completion-release --target package` and verify
its TGZ with `python3 tools/verify_mac_package.py`. The
[current package record](../evidence/corrected-completion/package.json) retains
the archive identity, source-document checks and four unchanged older archives.
