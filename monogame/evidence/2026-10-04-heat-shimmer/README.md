# Desert and Hell heat shimmer — 2026-10-04

Implemented the requested impression of hot air with smooth, upward-drifting
refraction of scenery. Desert strength follows the existing continuous sand
footprint, fading into mixed terrain and leaving water calm. All four Hell maps
(117–120, including generated rooms) use a stronger effect. Other biomes bypass
the pass. The camera and gameplay positions are unchanged.

`HeatEffect` follows the existing `MiniatureEffect` GPU-rendering approach:
point enlargement to presentation resolution, then a small BasicEffect texture
mesh with two slowly moving waves. Maximum horizontal offsets are 0.75 native
pixels in desert and 1.15 in Hell. Continuous scene time avoids stepped motion;
smooth falloffs leave a calm ellipse around the hero and prevent border smearing.
The UI is composited afterward, keeping menus, text and area/world maps sharp.
Pause, menus and loss of focus freeze the shared scene clock. The effect works
with miniature Off, Subtle and Strong; no external shader compiler or new art
asset is required. GPU buffers are reused and disposed with the game.

## Visual evidence

- [Desert animation](desert-heat-preview.mp4)
- [Hell animation](hell-heat-preview.mp4)

Each preview contains six seconds at 24 fps from the real frontend Draw path,
with Strong miniature and scanlines disabled to make the shimmer easy to inspect.
The generated fixture positions are diagnostic, not earned campaign progress.
Sampled frames and captures of all four Hell maps, mixed grass/desert, miniature
Off and the area map are retained here. Frames were visually inspected alongside
the temporal smoothness and pixel-preservation checks below.

## Verification

- Checked Release build: zero warnings/errors.
- **685 graphics checks** pass, including **28** new heat checks covering biome
  selection, all four Hell regions, fixed-clock repeatability, temporal change,
  smooth motion, complete viewport coverage, exact hero/nearby-scene preservation,
  pause freezing, untouched menu text, mixed-region grass preservation and full
  gameplay snapshot/RNG isolation.
- All 115 raw map renders remain byte-identical. Of 205 existing comparable
  static captures, only `yipe-generated-map.png` changes, as expected for its
  newly applied Hell heat. The other **204** remain byte-identical.
- All **26 gameplay core source files** and **four personal save/profile files**
  retain their before-work hashes. Test saves are isolated. No save format change.
- Final simplicity review covers the new effect and its two integration points:
  sand-field/biome sampling and world presentation before UI. Existing data and
  clock are reused, with no added gameplay state, particle system or generic
  post-processing framework. No actionable issues remain in that scope.

Current run: `build/graphics-checks/yipe-graphics-20261004-183922-957a5f75`.
Baseline: `build/graphics-checks/yipe-graphics-20261004-181347-429970ce`.
Logs, comparison lists and source/preservation fingerprints are retained here.
Earlier incomplete check runs exposed fixture issues (world coordinates and
menu-selection easing), corrected in the checks before this passing run.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Feature complete. Continue the P5/P6 checklist in `PORTING_PLAN.md`; physical
controller/display and platform performance verification remain separate.
