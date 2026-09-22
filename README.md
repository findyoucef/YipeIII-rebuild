# Yipe! III reconstruction

This repository is a clean-room, behavior-oriented reconstruction of Yipe! III 1.1.6.0 from the supplied 2003 Windows executable. The original binary is an input and reference; generated decompiler output is evidence, not compilable source.

The current build contains the first platform-neutral gameplay modules: player defaults, health changes, consumable item effects, level progression, the original Visual C++ random-number generator, the external item-name parser, map grids, fog-of-war visibility, and event records, movement, the secret-tree puzzle, princess rescues, the King's quest dialog and rewards, equipment transactions and combat bonuses, service transactions, the three casino games, all twelve learned spells, and combat for groups of up to three opponents, including Backstab, Frighten, Invisibility, and Enrage. Generated catalogs preserve all 20 princess placements, all 22 map-authored pursuers, all 186 recovered monster placements, all 41 monster definitions and damage ranges, all 42 weapon damage ranges, and the original sound-effect and monster-sound aliases. Its SDL2 application renders the recovered startup menu, all 112 static maps and three runtime-generated maps, all 20 placed location messages, princesses and their four reward choices, weapon, armor, healing, miscellaneous, magic, training, casino, and King interactions, and every recovered monster placement from original bitmap resources. Repeatable placements return after a map reload, high-rank monsters occupy and render across their original 2×2 footprint, and transient roaming monsters use the recovered encounter threshold, environment tables, spawn constraints, and pursuit movement. Map-authored pursuers remain within their recovered local bounds and enter ordinary combat. Combat defeat now sends the player to Yipe Hell, where the Gatekeeper accepts his difficulty-based fee or a fight before reopening the route to town. The application also plays the supplied WAV assets through SDL audio and persists the recovered sound and keyboard settings. The remaining game systems are tracked in the [rebuild plan](../YipeIII-analysis/REBUILD_PLAN.md).

## Build and test

Requirements: CMake 3.20 or newer, a C++20 compiler, and SDL2. The macOS
file chooser also requires Apple's Objective-C++ toolchain and AppKit.

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
./build/yipe3
```

The `yipe3` executable renders the 288×397 startup view at 2× scale. Click New Game, Load Game, or Quit, or use N, L, S for Settings, H for High Scores, Q, and Escape. New Game offers Combat, Quickness, Magic, and Random attributes; Random can be rerolled. Enter a name, choose Easy/Medium/Hard, and click one of eight portraits (S toggles four Silly Mode variants). The new character enters map `0x11` at `(5,5)`. The recovered movement defaults use the arrow keys for cardinal movement and numeric-keypad 1/3/7/9 for diagonals; WASD remains an adapted convenience alias. Use I for inventory, C to cast an exploration spell, Ctrl+Shift+O for Other, Ctrl+N for a new game, Ctrl+O to choose a saved game, Ctrl+T for player status, Ctrl+H for high scores, F5 or Ctrl+S to save, Ctrl+Shift+S for Save As, and F10 for settings. New/Open during play ask whether to save current progress before leaving; a new game prompts for a .y3r location on its first save. Shop, service, training, and casino entrances invoke their recovered transactions. Surface edges and recovered depth entrances load their corresponding static maps; restoration, locked-door, surface-return, location-message, and fire-wall tiles dispatch their original effects. Enter any undefeated monster tile to fight its recovered one-to-three-opponent group. The combat view accepts the five configured action keys directly, retains mouse buttons for every action, and cycles targets with the arrow keys or Tab. Defeat relocates the player to map `0x75` at one HP; paying or defeating the Gatekeeper opens the exit on the next attempt. Q or Escape offers Save and Quit outside combat.

Other inspects the current tile, drinks from water holes, collects hidden bridge/cave/snow treasure, opens the flower-triggered mountain passage, and uses the secret Hell tunnel or five-stamp stone circle. Secret Hell entry leaves HP and the death count unchanged. The mountain passage resets when the map reloads; treasure and snow-search progress remain saved.

All eleven map-specific movement hints now follow their recovered conditions. Map entry enables the strength-40 wall mechanism, opens Hell's east boundary after the global Gatekeeper quest advances, and clears an occupied `0x25` wall tile without changing its neighbors. These entry patches are recomputed on map load.

Ctrl+T opens player status; its Kills button shows five pages of monster defeat counts and the death count. Names remain hidden until a positive kill count, as in the original dialog. Ordinary kills are counted even when you later flee; Earth Giant and Gatekeeper count only after their entire group is defeated. These counts also advance the King's equipment hint.

The player-status Special button shows collected stamps, rescued princesses, both keys, the four special items, and the current learned ability. Missing keys or special items display None. The rescue total counts positive signed values in all 30 rescue slots, as do high scores and the finale; it is distinct from the King's separate princess quest counter. This read-only panel uses existing save state and does not submit high scores or change progression.

The Equipment button shows the weapon, body armor, helmet, gloves, ring, boots,
and amulet with each item's armor class (AC) and weapon class (WC). Player status
shows the combined combat AC/WC, including special-tier bonuses. Both shops and
Equipment use the supplied `assets/Yipe3Items.txt` beside the executable, with
the recovered embedded names as fallback if the file is missing or invalid.
Accessory names come from the original resource strings. The panel is read-only;
native saves now use version 16. Original-runtime and visible-dialog comparison
remain pending.

F1 opens Help at startup or during exploration. It includes current controls,
the configured keys, original attribution, and all 28 pages of the original
2003 manual. The manual retains historical text and interface descriptions;
the opening Help page describes this rebuild's controls. Character creation's
Help button opens the original New Character Creation page. The manual has
Previous, Next, Start, and End controls; Enter or Escape closes it. Ctrl+E opens
Equipment directly, Ctrl+I opens Items, Ctrl+C opens Spells, and Ctrl+S saves.
These fixed shortcuts take priority over exploration movement bindings.

High Scores is available from startup H, exploration Ctrl+H, or the player-status High Scores button. Easy, Medium, and Hard each have three records for kills, princesses, cash held at one time, level, and the first three finishers. Opening it during play submits the current player; startup only displays existing records. Scores also submit immediately after a combat level-up, at the finale, and when resuming a completed game. A separate checksummed `highscores.y3r` in the same per-user directory preserves every table change, including same-name improvements and completion-only updates. A damaged score file is reported rather than silently reset. See [high-score rules and persistence](docs/AUXILIARY_DATA.md).

Load Game opens a macOS file chooser for native `.y3r` saves and import-only original `.yp3` files. Imported games require a separate native `.y3r` save path; the original file is never overwritten. Version 16 saves preserve character specialty and the selected portrait alongside progression, equipment, learned spells, quest and princess-reward state, ability and special-movement state, changed map tiles, world position and surface-return anchor, permanent monster defeats, per-monster encounter history and defeat counts, death count, Hell entry, Gatekeeper recovery state, Other-action treasure/water/snow state, the Wormoid-defeat hint flag, and random-generator state; repeatable encounters remain cleared only until the map is reloaded. The loader still accepts game versions 2–15 and the earlier player-only version 1 format. Version 15 and earlier native saves infer specialty from the former class field. Older native saves retain their aggregate progress and any Wormoid flag but lack per-type kill counts: the Kills view labels those counts as starting at the statistics upgrade. Versions before 14 may show the giant warning again. Sound and the 13 recovered key bindings are stored separately in checksummed `settings.y3r`. Invalid settings are reported and reset with sound off. The core provides strict, import-only readers for legacy `.yp3` saves and original `Yipe3.dat` auxiliary data, and the macOS Open dialog exposes `.yp3` import; real `.yp3` compatibility remains unverified until an original save sample is available. SDL2 remains absent from the core target so gameplay, settings, and resource-mapping tests stay independent of rendering and playback.

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
