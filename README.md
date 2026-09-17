# Yipe! III reconstruction

This repository is a clean-room, behavior-oriented reconstruction of Yipe! III 1.1.6.0 from the supplied 2003 Windows executable. The original binary is an input and reference; generated decompiler output is evidence, not compilable source.

The current build contains the first platform-neutral gameplay modules: player defaults, health changes, consumable item effects, level progression, the original Visual C++ random-number generator, the external item-name parser, map grids, fog-of-war visibility, and event records, movement, the secret-tree puzzle, princess rescues, the King's quest dialog and rewards, equipment transactions and combat bonuses, service transactions, the three casino games, all twelve learned spells, and combat for groups of up to three opponents, including Backstab, Frighten, Invisibility, and Enrage. Generated catalogs preserve all 20 princess placements, all 22 map-authored pursuers, all 186 recovered monster placements, all 41 monster definitions and damage ranges, all 42 weapon damage ranges, and the original sound-effect and monster-sound aliases. Its SDL2 application renders the recovered startup menu, all 112 static maps and three runtime-generated maps, princesses and their four reward choices, weapon, armor, healing, miscellaneous, magic, training, casino, and King interactions, and every recovered monster placement from original bitmap resources. Repeatable placements return after a map reload, high-rank monsters occupy and render across their original 2×2 footprint, and transient roaming monsters use the recovered encounter threshold, environment tables, spawn constraints, and pursuit movement. Map-authored pursuers remain within their recovered local bounds and enter ordinary combat. It also plays the supplied WAV assets through SDL audio and persists the recovered sound and keyboard settings. The remaining game systems are tracked in the [rebuild plan](../YipeIII-analysis/REBUILD_PLAN.md).

## Build and test

Requirements: CMake 3.20 or newer, a C++20 compiler, and SDL2.

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
./build/yipe3
```

The `yipe3` executable renders the 288×397 startup view at 2× scale. Click New Game, Load Game, or Quit, or use N, L, S for Settings, Q, and Escape. New Game prompts for Warrior, Rogue, or Wizard and enters map `0x11` at `(5,5)`. The recovered movement defaults use the arrow keys for cardinal movement and numeric-keypad 1/3/7/9 for diagonals; WASD remains an adapted convenience alias. Use I for inventory, C to cast an exploration spell, Ctrl+T for player status, F5 to save, and F10 for settings. Shop, service, training, and casino entrances invoke their recovered transactions. Surface edges and recovered depth entrances load their corresponding static maps; restoration, locked-door, surface-return, and fire-wall tiles dispatch their original effects. Enter any undefeated monster tile to fight its recovered one-to-three-opponent group. The combat view accepts the five configured action keys directly, retains mouse buttons for every action, and cycles targets with the arrow keys or Tab. Q or Escape offers Save and Quit outside combat.

Load Game resumes the native `save.y3r` stored in SDL's per-user application-data directory. Version 10 saves preserve progression, equipment, learned spells, quest and princess-reward state, ability and special-movement state, changed map tiles, world position and surface-return anchor, permanent monster defeats, and random-generator state; repeatable encounters remain cleared only until the map is reloaded. The loader still accepts game versions 2–9 and the earlier player-only version 1 format. Sound and the 13 recovered key bindings are stored separately in checksummed `settings.y3r`. Invalid settings are reported and reset with sound off. The core also provides strict, import-only readers for legacy `.yp3` saves and original `Yipe3.dat` auxiliary data; real `.yp3` compatibility remains unverified until an original save sample is available. SDL2 remains absent from the core target so gameplay, settings, and resource-mapping tests stay independent of rendering and playback.

The original's asynchronous one-sound-at-a-time behavior is adapted to SDL's queued audio API. Embedded effects are converted to the active device format, and missing per-monster WAV files use the original generic death sound. Exact effect timing and modifier-key binding behavior remain open comparison work.

## Evidence and status

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
