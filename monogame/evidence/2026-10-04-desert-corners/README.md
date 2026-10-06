# Desert corner rendering fix — 2026-10-04

The renderer assigned the wrong orientation to three of four transition tiles,
leaving disconnected sand/grass triangles in authored desert patches. Original
bitmap `assets/embedded/bitmaps/138-1033.bmp` and `app/main.cpp::tile_graphic`
define `0x1b/0x1c` as the upper corners and `0x1d/0x1e` as the lower corners, with
sand facing their shared center. `WorldTerrain.DrawSandEdge` now follows that
arrangement and includes the full adjoining edges without the old two-pixel offset.

This is a small rendering correction: no new assets, terrain system, map edits,
collision changes or gameplay changes. Play and the area-map renderer share it.
The final simplicity review retained only the corrected orientation/coverage
expressions, an explanatory source comment, and removal of the now-unnecessary
zero-width guard. Existing graphics checks and before/after inspection were
sufficient; no new test infrastructure was added.

Validation:

- Checked Release build: zero warnings/errors (`build.log`).
- **648 graphics checks**, all **115 maps**, and 360 tour frames (`graphics.log`).
- Exactly **16** map captures changed: 1, 2, 3, 5, 7, 8, 9, 11, 12, 13, 14, 15,
  21, 30, 31, 36. These are exactly the maps containing the four corner types.
- **189** other captures are byte-identical, including village and map UI
  (`capture-comparison.json`).
- Visually inspected maps 1, 2, 5, 8, 9, 14, 21, 30 and 36, covering isolated
  patches, long boundaries, concave turns and map-edge continuations.
- Six retained before/after images show maps 2, 5 and 30.
- All pre-existing personal saves/profile files are unchanged
  (`user-save-preservation.json`). Comparing the previous feature's source
  fingerprints confirms only `WorldTerrain.cs` changed among C# source files.

Current run: `build/graphics-checks/yipe-graphics-20261004-173709-a98c14f1`.
Comparison: `build/graphics-checks/yipe-graphics-20261004-165409-103421f3`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Fix complete; continue remaining P5/P6 work from `monogame/PORTING_PLAN.md`.
