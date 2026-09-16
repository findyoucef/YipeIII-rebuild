# Yipe! III reconstruction

This repository is a clean-room, behavior-oriented reconstruction of Yipe! III 1.1.6.0 from the supplied 2003 Windows executable. The original binary is an input and reference; generated decompiler output is evidence, not compilable source.

The current build contains the first platform-neutral gameplay modules: player defaults, health changes, consumable item effects, level progression, the original Visual C++ random-number generator, the external item-name parser, map grids and event records, movement, the secret-tree puzzle, equipment and service transactions, the three casino games, all twelve learned spells, and combat for groups of up to three opponents, including Backstab, Frighten, Invisibility, and Enrage. Generated catalogs preserve all 186 recovered monster placements, all 41 monster definitions and damage ranges, and all 42 weapon damage ranges. Its SDL2 application renders the recovered startup menu, all 112 static maps and three runtime-generated maps, weapon, armor, healing, miscellaneous, magic, training, and casino interactions, and the first Giant Beetle encounter from original bitmap resources. The remaining game systems are tracked in the [rebuild plan](../YipeIII-analysis/REBUILD_PLAN.md).

## Build and test

Requirements: CMake 3.20 or newer, a C++20 compiler, and SDL2.

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
./build/yipe3
```

The `yipe3` executable renders the 288×397 startup view at 2× scale. Click New Game, Load Game, or Quit, or use N, L, Q, and Escape. New Game prompts for Warrior, Rogue, or Wizard and enters map `0x11` at `(5,5)`. Move with the arrow keys or WASD, inspect player state with I, cast an exploration spell with C, and save with F5. Shop, service, training, and casino entrances invoke their recovered transactions. Surface edges and recovered depth entrances load their corresponding static maps; restoration, locked-door, surface-return, and fire-wall tiles dispatch their original effects. Cross the north edge and reach `(2,8)` to fight the Giant Beetle with Attack, Use Item, Cast Spell, and Flee actions. Q or Escape offers Save and Quit.

Load Game resumes the native `save.y3r` stored in SDL's per-user application-data directory. Version 6 saves preserve progression, equipment, learned spells, ability and special-movement state, changed map tiles, world position, the defeated encounter, and random-generator state; the loader still accepts game versions 2–5 and the earlier player-only version 1 format. The core also provides a strict, import-only legacy `.yp3` reader recovered from the original's static archive path; real-file compatibility remains unverified until an original save sample is available. SDL2 remains absent from the core target so gameplay tests stay independent of rendering.

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
