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

## Recover static map tables

```bash
./tools/extract_map_tables.py \
  --decompiled-dir /Users/baouchiy/Downloads/YipeIII-analysis/decompiled \
  --output assets/maps/original-map-tables.json \
  --cpp-output src/generated/original_maps.cpp
```

The command reads the map dispatcher and its static loader functions from the
Ghidra C export. It validates every 13×12 table before writing deterministic
JSON. Runtime-generated map IDs `0x76`–`0x78` are intentionally excluded.

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

## Run the original

See `ORIGINAL_RUNTIME.md` for the bottle, runtime hashes, launch command, and current limitations.
