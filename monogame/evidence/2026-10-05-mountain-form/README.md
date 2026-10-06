# Mountain form and height — 2026-10-05

Historical revision: the later [cave-stone correction](../2026-10-05-cave-stone/README.md)
has fresh GPU evidence and resolves the graphics-context blocker described below.
This packet's CPU harness imports retained native art from before that correction;
it does not run the current `RetroArt.Load` processing.

Primary reference: **surface F3 / 2,6**, map 18. The coordinate is grass immediately
west of the diagonal ridge. The prior treatment left a staircase of small rounded
blocks filled with dense cobblestone texture. This revision uses larger connected
corner cuts, broad irregular facets and 12–16 native-pixel front faces. The original
rock texture supplies quieter, larger surface clusters. Existing slate colors,
binary transparency, cave artwork and ground-contact details remain.

[F3 comparison](f3-before-preview.png): previous GPU capture on the left; current
CPU terrain preview on the right. Further comparisons are in `before/` and
`preview/`: desert A1/F1, Maynard E3, F3, C4, D4, E4 and F4.

## Verification and limits

- Checked Release solution build: zero warnings/errors (`build.log`).
- 530 core/input/storage checks pass (`core.log`).
- 249 direct terrain checks pass (`cpu.log`): all 36 surface maps retain native
  palette/binary alpha, clear non-mountain cells, solid blocked centers, unchanged
  maps and complete save/RNG snapshots, and deterministic caches. All 40,960
  matching region-overlap pixels agree, including world wrap. Checks also cover
  secret equivalence, mutable terrain, meadow/desert contact, paved approaches,
  broad F3 contours, lighter tops/darker fronts, and connected foothill centers.
- F3's contour moves across at least 24 native pixels over the sampled tile row,
  with at least 12 distinct edge positions. Front/top fixture luminance differs
  by at least 18/255. These checks reject a return to tiny rounded square steps
  and flat terrain shading; visual acceptance still belongs to the user.
- Main terrain remains within mountain cells. Desert foothills reuse the same
  already-walkable footprint and sample neighboring cliff colors. Town and world
  still use the same `MountainTerrain` class; ground/actor ordering, visibility
  checks and the area-map fog cover are unchanged.
- The current sandbox cannot create MonoGame's OpenGL context. The graphics
  integration runner failed during startup with `NoSuitableGraphicsDeviceException`
  (`graphics-unavailable.log`). **No fresh full GPU suite or gameplay screenshot is
  claimed.** The previous 1,194 graphics checks are a historical baseline.
  The same startup failure persisted across three consecutive goal turns; the
  last retry at 2026-10-05 21:45 UTC is retained in `graphics-recheck.log`.
  The goal is blocked on this remaining verification, with implementation and
  independent checks complete.

The preview harness compiles the actual current `MountainTerrain.cs`, with
in-memory replacements only for the texture upload/read API. It imports the
retained native terrain and actual desert pixels, then composites the output
with prior captured foregrounds. Cave sprites are re-overlaid unchanged; desert
foothill cutouts use the sand texture. These previews validate terrain shape,
palette and composition, but do not replace checking GPU blending, post-effects
or the live camera. Raw terrain outputs are saved under `build/mountain-form-headless`.

## Reproduce

From the repository root, with .NET 10 and FFmpeg installed:

```sh
bash monogame/evidence/2026-10-05-mountain-form/run-cpu.sh
```

The harness sources and original import fixtures are retained here. Its project
links current production code rather than a rewritten terrain algorithm. The
previous terrain implementation is included solely to isolate foreground pixels
in the captured reference images.

On a graphical desktop that allows MonoGame to create a context, finish the GPU
verification with:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Inspect map 18 and the retained village/desert/narrow-passage references at native
size and in gameplay. The extended graphics checks also retain an F3 area-map
capture. No personal save or live game was navigated, reloaded or overwritten.
