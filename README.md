# Yipe! III reconstruction

This repository is a clean-room, behavior-oriented reconstruction of Yipe! III 1.1.6.0 from the supplied 2003 Windows executable. The original binary is an input and reference; generated decompiler output is evidence, not compilable source.

The current build contains the first platform-neutral gameplay module: player defaults, health changes, consumable item effects, the original Visual C++ random-number generator, and the external item-name parser. Its SDL2 application renders the recovered startup menu from the original bitmap resources. The remaining game systems are tracked in the [rebuild plan](../YipeIII-analysis/REBUILD_PLAN.md).

## Build and test

Requirements: CMake 3.20 or newer, a C++20 compiler, and SDL2.

```bash
cmake -S . -B build -DCMAKE_BUILD_TYPE=Debug
cmake --build build --parallel
ctest --test-dir build --output-on-failure
./build/yipe3
```

The `yipe3` executable renders the 288×397 startup view at 2× scale. Click the recovered New Game, Load Game, or Quit rows, or use N, L, Q, and Escape. New and Load currently report the selected transition to the caller while their screens are reconstructed. SDL2 remains absent from the core target so gameplay tests stay independent of rendering.

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

The preserved Ghidra project and export indexes are under `evidence/analysis-snapshot`. The original executable is deliberately not duplicated here; its expected hash and all supplied-file hashes are in the input manifest.

Prepared assets under `assets/embedded` preserve all extracted PE resources. Bitmap conversion only adds the file header required by ordinary BMP loaders; palettes and pixel bytes are unchanged.
