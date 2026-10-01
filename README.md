# Yipe! III reconstruction

This repository is a clean-room, behavior-oriented reconstruction of Yipe! III 1.1.6.0 from the supplied 2003 Windows executable. The original binary is an input and reference; generated decompiler output is evidence, not compilable source.

The current build contains the first platform-neutral gameplay modules: player defaults, health changes, consumable item effects, level progression, the original Visual C++ random-number generator, the external item-name parser, map grids, fog-of-war visibility, and event records, movement, the secret-tree puzzle, princess rescues, the King's quest dialog and rewards, equipment transactions and combat bonuses, service transactions, the three casino games, all twelve learned spells, and combat for groups of up to three opponents, including Backstab, Frighten, Invisibility, and Enrage. Generated catalogs preserve all 20 princess placements, all 22 map-authored pursuers, all 186 recovered monster placements, all 41 monster definitions and damage ranges, all 42 weapon damage ranges, and the original sound-effect and monster-sound aliases. Its SDL2 application renders the recovered startup menu, all 112 static maps and three runtime-generated maps, all 20 placed location messages, princesses and their four reward choices, weapon, armor, healing, miscellaneous, magic, training, casino, and King interactions, and every recovered monster placement from original bitmap resources. Repeatable placements return after a map reload, high-rank monsters occupy and render across their original 2×2 footprint, and transient roaming monsters use the recovered encounter threshold, environment tables, spawn constraints, and pursuit movement. Map-authored pursuers remain within their recovered local bounds and enter ordinary combat. Combat defeat now sends the player to Yipe Hell, where the Gatekeeper accepts his difficulty-based fee or a fight before reopening the route to town. The application also plays the supplied WAV assets through SDL audio and persists the recovered sound and keyboard settings. The remaining game systems are tracked in the [feature inventory](docs/FEATURE_COVERAGE.md).

The E075–E080 verification milestone passed all seven Release tests, including
native recent-file write/read/clear after sandbox removal. A bounded probe
executes original machine code and matches 13,732 rule outputs; see its
[record and limits](evidence/verification-milestone/original-probe/README.md).
The default completion fixture remains unchanged. Quickness/Easy seed 2 also
completes, with all 15,996 observations matching an independent fresh replay;
see the [variant record](evidence/verification-milestone/variants/README.md).
Magic/Hard seed 3 also completes with all 36,191 states matching a fresh replay.
That route exposed a missing original generated-room clamp; the one-line repair
matches all 20,280 tile/RNG records from original machine code across 130 grids.
Random/Medium seed 4 now completes on a separate clean build with all 36,072
states exact. The four-fixture matrix totals 103,626 matching observations.
The corrected clean-build development package passes independent extraction,
137-asset, bundled-SDL, signature and empty-directory startup checks (E080).
These four completion fixtures describe the E077 build and remain preserved.

The bounded parity work (E081–E082) corrects three original combat rules:
the level-28–37 defense roll, the level-2–3 low-HP reduction, and zero-damage
misses after failed fleeing. All seven Release tests pass. Eighteen late-game
saves reopen in separate processes with every serialized field preserved except
the original generated-map RNG advancement; five restarted continuations pass
through bosses, a regenerated Hell corridor and the finale. New bounded
comparisons match 379,629 records, with composed/UI-dependent limits explained
in the [persistence and parity record](evidence/persistence-parity/README.md).
The old literal completion streams diverge under these corrected random rules
and remain preserved as historical evidence. Visible original comparisons,
manual persistence and broader P8/P9 remain open.
The separate E083 clean Release package passes all seven tests in 46.16 seconds
and independent extraction, 137-asset, bundled-SDL, signature and empty-directory
startup checks. Its identity and limits are recorded in
[package.json](evidence/persistence-parity/package.json).

E084 replaces the current completion baselines with four new earned fresh-game
routes on the corrected combat build. Combat/Medium/1, Quickness/Easy/2,
Magic/Hard/3 and Random/Medium/4 all earn twenty rescues, six stamps, movement
prerequisites, STR 40+, empowered Gatekeeper victory and the live town finale.
Separate fresh replays exit 0 and match 20,507, 19,025, 34,763 and 34,584 states
respectively: 108,879 total, with identical full traces, commands and final saves.
No observation checkpoint is loaded into play and no character state is injected.
See [the corrected completion record](evidence/corrected-completion/README.md).
E085 builds the final documentation into a separate clean Release development
archive; all seven tests pass in 49.36 seconds. Its independent package checks
and identity are recorded in [the current package record](evidence/corrected-completion/package.json).

## macOS CI

The [macOS workflow](.github/workflows/macos.yml) builds Release on GitHub's
Apple Silicon `macos-26` runner with pinned SDL2 Classic 2.32.10. It runs all
seven CTest checks, creates the application archive, and verifies extracted
assets, bundled SDL2, signatures, and startup outside the checkout. Successful
runs upload `YipeIII-macOS-arm64`;
test results are retained even when a later step fails. Pushes to `main`, pull
requests, and manual dispatch run the same checks with read-only permissions.

The current target is arm64/macOS 26.0. See the [code review](docs/CODE_REVIEW.md)
for the resolved save and high-score error handling bugs and their regression
coverage. The large local progression traces,
runtime captures, and analysis outputs referenced by historical evidence records
are retained locally and are not required to build or run this CI workflow.

## Build and test

Requirements: CMake 3.20 or newer, a C++20 compiler, and SDL2. The macOS
file chooser and printing also require Apple's Objective-C++ toolchain and AppKit.

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
open "build/Yipe III.app"
```

On non-macOS builds the executable remains `./build/yipe3`. macOS builds create
`Yipe III.app`; the executable inside is `Contents/MacOS/Yipe III`.

To make an installable macOS archive, run:

```bash
cpack --config build/CPackConfig.cmake -G TGZ
python3 tools/verify_mac_package.py build/packages/YipeIII-rebuild-0.1.0-Darwin.tar.gz
```

The archive contains `Yipe III.app`, this README, and the documentation. Extract
it and open the app, or move it to Applications. Packaging embeds SDL2 and its
license, includes the runtime assets under `Contents/Resources/assets`, fixes
library paths, and applies an ad-hoc signature. Homebrew is needed to build with
the recorded dependencies, but the packaged app does not load Homebrew libraries.
The app directly in the build directory still uses the development SDL2 library;
`cmake --install build --prefix "$PWD/build/install"` also creates a bundled copy.

The verifier requires Python 3.9+ and Apple's command-line tools. It extracts the
archive to a temporary directory, checks dependencies and signatures, and runs
headless smoke with a loader trace from an empty working directory. This remains
a development release: interactive use on other machines, Developer ID signing,
and notarization have not been verified. One fresh default Combat/Medium seed-1
automated run completes all 20 rescues, six stamps, earned prerequisites, and the
finale; see the [progression audit](docs/PROGRESSION_AUDIT.md). The package uses
the build machine's selected architecture and macOS deployment target. The
current recorded package is arm64 with a macOS 26.0 minimum; older macOS
versions and Intel Macs have not been tested.

The `yipe3` executable renders the 288×397 startup view at 2× scale. Click New Game, Load Game, or Quit, or use N, L, S for Settings, H for High Scores, Q, and Escape. New Game offers Combat, Quickness, Magic, and Random attributes; Random can be rerolled. Enter a name, choose Easy/Medium/Hard, and click one of eight portraits (S toggles four Silly Mode variants). You can read the King's original introduction before entering map `0x11` at `(5,5)`. The recovered movement defaults use the arrow keys for cardinal movement and numeric-keypad 1/3/7/9 for diagonals; WASD remains an adapted convenience alias. Use I for inventory, C to cast an exploration spell, Ctrl+Shift+O for Other, Ctrl+N for a new game, Ctrl+O to choose a saved game, Ctrl+T for player status, Ctrl+H for high scores, F5 or Ctrl+S to save, Ctrl+Shift+S for Save As, and F10 for settings. New/Open during play ask whether to save current progress before leaving; a new game prompts for a .y3r location on its first save. Shop, service, training, and casino entrances invoke their recovered transactions. Surface edges and recovered depth entrances load their corresponding static maps; restoration, locked-door, surface-return, location-message, and fire-wall tiles dispatch their original effects. Enter any undefeated monster tile to fight its recovered one-to-three-opponent group. The combat view accepts the five configured action keys directly, retains mouse buttons for every action, and cycles targets with the arrow keys or Tab. Defeat relocates the player to map `0x75` at one HP; paying or defeating the Gatekeeper opens the exit on the next attempt. Q or Escape offers Save and Quit outside combat.

The status bar is visible by default below the game view. It shows the current
view and Caps Lock, Num Lock, and Scroll Lock indicators. **Ctrl+B** at startup
or while exploring, or **F10 → Toggle Status Bar**, shows/hides it for this
application session. It leaves all map tiles accessible and is excluded from
printing. **F6 or Shift+F6** switches between the map and Player Status; the
statistics view pauses exploration as before.

Name and secret-code entry support cursor movement with Left/Right/Home/End and
selection with Shift. Ctrl or Command + A/X/C/V/Z selects all, cuts, copies,
pastes, or undoes the last edit. Undo again restores that edit. **F2 or
right-click** opens the same Edit commands. The window title shows `|` for the
cursor and brackets around selected text. Entry accepts one line up to 128 UTF-8
bytes; oversized or multiline paste reports an error and leaves the selection
intact. Navigation/deletion operates on Unicode code points. The original
Ctrl+Insert, Shift+Insert, Shift+Delete, and Alt+Backspace aliases also work.
These commands edit only text: they cannot undo or change gameplay. Ctrl+C
continues to cast spells while exploring.

You can also explore with the mouse: left-click any map tile to attempt one step
in its direction, including diagonals. Each release moves at most one tile;
clicking your character or outside the map does nothing. Mouse movement uses
the same terrain, encounters, shops, and map transitions as keyboard movement.
Startup choices, portraits, and combat buttons also follow the displayed
positions when the window is scaled or letterboxed (E067).

F10 edits single-key bindings, including punctuation and keypad operators.
The original editor stored key codes, not modifier combinations; the rebuild
now explains this and retries rather than silently storing only part of a chord.
Settings shows conflicts with other actions, fixed shortcuts, and target cycling.
A conflicting change defaults to Cancel Change; Use Key Anyway retains it with
the displayed precedence. Existing settings are not rewritten to remove conflicts.
Arrow keys and keypad 1-4/6-9 remain movement inputs after rebinding, with the
original interleaving of fixed and configured directions. WASD stays a fallback.
Alt/Command-modified keys do not trigger gameplay, and holding an item/cast key
does not repeatedly open its dialog. Native settings remain version 1 (E069).

Exploration now advances while idle: recovered timers move platforms, cycle fire walls and other hazards, restore SP/AP, and expire repellent. The platform on map `0x4a` and fire openings on `0x85` provide tested princess routes; see [routes and fidelity limits](docs/MAPS.md#timed-princess-routes-e059). Dialogs pause exploration. Saving aboard a platform restores support under the rider on reopen; timed terrain resets without permanent map edits.

All 145 original treasure chests now render and award one-time gold, XP, and
equipment or consumables. The chest at `(9,1)` on map `0x63` grants waterproof
shoe spray. Clearing Quizilog's four monster groups and visiting either its
weapon or miscellaneous shop grants tree-climbing shoes. The first type-41
Gatekeeper victory awards the Underworld Key before advancing the quest.
These acquisition paths are implemented (E062). All four miscellaneous shops
now sell their original stock, including remote equipment, stat potions,
repellent, and SP healing. The Outback Shop also sells the Rainbow Key after
the price-increase milestone and level 26 (E063). E074 proves these acquisitions in a connected fresh-game completion; see the
[progression audit](docs/PROGRESSION_AUDIT.md).

New characters can raise strength, dexterity, and intelligence to 50, making the
strength-40 passage attainable through ordinary stat growth. Random weapon
upgrades can reach rank 29. This is the explicit supplied-content policy in
[D8](docs/DECISIONS.md), not a reproduction of the original registration system.
Native saves keep their stored 30/40/50 limits; legacy `.yp3` imports use 50
because they contain no profile mode. Creation and player status show the limit.
E064 covers the strength passage with funded potion purchases. E074 earns its
money and STR 42 in the connected completion run, then rescues that princess.

Other inspects the current tile, drinks from water holes, collects hidden bridge/cave/snow treasure, opens the flower-triggered mountain passage, and uses the secret Hell tunnel or five-stamp stone circle. Secret Hell entry leaves HP and the death count unchanged. The mountain passage resets when the map reloads; treasure and snow-search progress remain saved.

All eleven map-specific movement hints now follow their recovered conditions. Map entry enables the strength-40 wall mechanism, opens Hell's east boundary after the global Gatekeeper quest advances, and clears an occupied `0x25` wall tile without changing its neighbors. These entry patches are recomputed on map load.

Ctrl+R rereads the sign at the player's current tile. Ctrl+T opens an in-window
statistics panel with the selected character portrait, level, XP, gold, HP/SP/AP,
attributes, stat limit, combined AC/WC, and carried-item count. Click its buttons,
select with Tab/Shift+Tab or arrows and press Enter, or use B/K/H/S/E/D for Back,
Kills, High Scores, Special, Equipment, and Details. Escape returns to play.
The window title and native Details page retain the complete character name;
long names are abbreviated on the bitmap panel, and names outside its font are
explicitly directed to Details. Exploration remains paused while viewing stats.

The Kills button shows five pages of monster defeat counts and the death count. Names remain hidden until a positive kill count, as in the original dialog. Ordinary kills are counted even when you later flee; Earth Giant and Gatekeeper count only after their entire group is defeated. These counts also advance the King's equipment hint.

The player-status Special button shows collected stamps, rescued princesses, both keys, the four special items, and the current learned ability. Missing keys or special items display None. The rescue total counts positive signed values in all 30 rescue slots, as do high scores and the finale; it is distinct from the King's separate princess quest counter. This read-only panel uses existing save state and does not submit high scores or change progression.

The Equipment button shows the weapon, body armor, helmet, gloves, ring, boots,
and amulet with each item's armor class (AC) and weapon class (WC). Player status
shows the combined combat AC/WC, including special-tier bonuses. Both shops and
Equipment use the supplied `assets/Yipe3Items.txt` in the runtime resources, with
the recovered embedded names as fallback if the file is missing or invalid.
F10 during play offers Toggle Silly/Normal Mode: Silly Mode uses the original
38 weapon and 30 armor names in both shops and Equipment. Creation's S toggle
also selects this mode and limits portrait selection to its four variants.
Changing mode during play keeps the selected portrait and equipment ratings.
Accessory names come from the original resource strings. The panel is read-only;
native saves now use version 20, retaining opened-chest, miscellaneous-shop,
and High Score Code suffix state while accepting versions 1-19. Original-runtime and visible-dialog comparison
remain pending.

Silly Mode is saved with each character, separately from sound/key preferences.
This persistence is an explicit adaptation: the supplied original serializer
omits the mode flag. Earlier native saves and imported `.yp3` files default to
Normal Mode without changing their portrait. The supplied original sound loader
ignores its mode argument; the rebuild does not introduce alternate mode sounds.

On macOS, **R at startup** or **Ctrl+L** opens Recent Games. Previous/Next browse
full paths; Open follows the same validation, import, and save-before-leaving
flow as the file chooser. Successful saves and accepted opens enter macOS's
persistent recent-document list. Its length follows the system preference.
Cancel and failed opens/writes do not add entries. Clear List forgets the list
without deleting saves. A missing or invalid recent file reports an error and
leaves the current game active. Imported `.yp3` entries still require a separate
native destination. Other platforms do not yet retain recent files.

F1 → **Profile / Registration** explains the rebuild's lack of an original
serial number and its existing stat/loot policy. During play it also shows the
current character's stored limits. Original Registration opens the three
preserved 2003 manual pages; their contacts, prices, and services are historical.
The panel does not change character limits or accept registration codes.

F1 opens Help at startup or during exploration. It includes current controls,
the configured keys, original attribution, and all 28 pages of the original
2003 manual. The manual retains historical text and interface descriptions;
the opening Help page describes this rebuild's controls. Character creation's
Help button opens the original New Character Creation page. The manual has
Previous, Next, Start, and End controls; Enter or Escape closes it. Ctrl+E opens
Equipment directly, Ctrl+I opens Items, Ctrl+C opens Spells, and Ctrl+S saves.
These fixed shortcuts take priority over exploration movement bindings.

High Scores is available from startup H, exploration Ctrl+H, or the player-status High Scores button. Easy, Medium, and Hard each have three records for kills, princesses, cash held at one time, level, and the first three finishers. Opening it during play submits the current player; startup only displays existing records. Scores also submit immediately after a combat level-up, at the finale, and when resuming a completed game. A separate checksummed `highscores.y3r` in the same per-user directory preserves every table change, including same-name improvements and completion-only updates. A damaged score file is reported rather than silently reset. See [high-score rules and persistence](docs/AUXILIARY_DATA.md).

During play, High Scores also offers **High Score Code**, with the original
encoding and a **Copy Code** button. The code uses the current player's level,
gold, all 50 recorded defeat counters, Gatekeeper state, difficulty, and original
suffix field. Older saves with incomplete defeat history are labeled because
their earlier defeats are excluded. Copying is local; the dialog identifies the
historical email destination without sending anything or promising service
availability. Native version 20 retains the suffix field on subsequent saves;
versions 1-19 default that previously unretained field to zero.

On macOS, **Ctrl+P** opens **Print / Preview** for the current map. The native
print panel previews the page and offers printer selection and Save as PDF.
**Ctrl+Shift+P** opens Page Setup, including at startup, for paper, orientation,
and scale. Accepted print preferences carry into subsequent print operations
within the application session; a previous PDF destination is never reused
automatically. Cancel returns to the game without changing progress.

Printing captures the logical 416-by-384 game view, including its current fog
and sprites, without window borders or letterboxing. The map fits one page and
is centered within the selected margins and printer's printable area.
Exploration stays paused while the panels are open. macOS combines Print and
Print Preview in one native panel rather than reproducing the MFC preview
toolbar. Non-macOS printing currently reports that it is unsupported.

Load Game opens a macOS file chooser for native `.y3r` saves and import-only original `.yp3` files. Imported games require a separate native `.y3r` save path; the original file is never overwritten. Version 17 saves preserve character specialty, Silly Mode, and the selected portrait alongside progression, equipment, learned spells, quest and princess-reward state, ability and special-movement state, changed map tiles, world position and surface-return anchor, permanent monster defeats, per-monster encounter history and defeat counts, death count, Hell entry, Gatekeeper recovery state, Other-action treasure/water/snow state, the Wormoid-defeat hint flag, and random-generator state; repeatable encounters remain cleared only until the map is reloaded. The loader still accepts game versions 2–16 and the earlier player-only version 1 format. Version 15 and earlier native saves infer specialty from the former class field. Older native saves retain their aggregate progress and any Wormoid flag but lack per-type kill counts: the Kills view labels those counts as starting at the statistics upgrade. Versions before 14 may show the giant warning again. Sound and the 13 recovered key bindings are stored separately in checksummed `settings.y3r`. Invalid settings are reported and reset with sound off. The core provides strict, import-only readers for legacy `.yp3` saves and original `Yipe3.dat` auxiliary data, and the macOS Open dialog exposes `.yp3` import; four original-produced early-game `.yp3` files now pass import and native save/reload (see [R08 evidence](evidence/original-runtime/r08/README.md)). Late-game state, active effects, and other original versions remain unverified. SDL2 remains absent from the core target so gameplay, settings, and resource-mapping tests stay independent of rendering and playback.

The original's asynchronous one-sound-at-a-time behavior is adapted to SDL's queued audio API. Embedded effects are converted to the active device format, and missing per-monster WAV files use the original generic death sound. Exact effect timing and modifier-key binding behavior remain open comparison work.

## Evidence and status

- [Feature and menu coverage](docs/FEATURE_COVERAGE.md)
- [Decisions](docs/DECISIONS.md)
- [Evidence ledger](docs/EVIDENCE.md)
- [Open questions and differences](docs/UNKNOWNS.md)
- [Input manifest](docs/INPUT_MANIFEST.tsv)
- [Tools and reproducible commands](docs/TOOLS.md)
- [Original-runtime observations](docs/REFERENCE_CASES.md)
- [Original-runtime setup](docs/ORIGINAL_RUNTIME.md)
- [Legacy save analysis](docs/LEGACY_SAVE.md)
- [Original auxiliary-data format](docs/AUXILIARY_DATA.md)
- [Map recovery and generated catalog](docs/MAPS.md)

The preserved Ghidra project and export indexes are under `evidence/analysis-snapshot`. The original executable is deliberately not duplicated here; its expected hash and all supplied-file hashes are in the input manifest.

Prepared assets under `assets/embedded` preserve all extracted PE resources. Bitmap conversion only adds the file header required by ordinary BMP loaders; palettes and pixel bytes are unchanged.
