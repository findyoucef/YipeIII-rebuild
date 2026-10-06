# Mountain style correction — 2026-10-05

The user rejected the prior mountain form treatment because its pixel art did
not fit the surrounding world. At surface F3 / 2,6 (map 18), broad flat gray
facets and enlarged texture fragments contrasted with the cave entrance's fine
rock clusters. Retaining the palette had not retained the visual style.

`MountainTerrain.cs` now samples the original rock atlas at native scale. The
procedural facets, large flat shading regions and facet seed helper are removed.
Broader connected contours remain, with textured lower faces shaded in the
existing palette. Meadow/sand contacts, secret equivalence, cave sprites,
collision, saves and shared town/world/map rendering remain unchanged.

[F3 comparison](f3-comparison.png): rejected treatment left, corrected treatment
right, both enlarged with nearest-neighbor sampling. These are **CPU terrain
composites with previously captured foregrounds, not fresh gameplay screenshots**.
The corrected previews in `preview/` cover eight surface regions; F3, Maynard
(map 17) and desert F1 (map 6) were visually reviewed. User acceptance is pending.

Verification of this revision:

- Checked Release solution build: zero warnings/errors (`mountain-style-build.log`).
- 530 core/input/storage checks pass (`mountain-style-core.log`).
- 249 existing CPU terrain checks pass (`mountain-style-cpu.log`), including all
  36 surface regions, all 40,960 matching overlap pixels, native palette/binary
  alpha, secret equivalence, terrain mutation, save/RNG isolation, contours and
  top/front contrast. No checks or tolerances were weakened for this correction.
- The full graphics runner still fails before rendering with
  `NoSuitableGraphicsDeviceException` (`mountain-style-graphics.log`, 22:28 UTC).
  Live camera, effects and GPU integration verification remain outstanding.

The existing reproducible harness compiles current production terrain code:

```sh
bash monogame/evidence/2026-10-05-mountain-form/run-cpu.sh
```

It writes current previews under `build/mountain-form-headless`. Harness sources,
input art and original foreground captures remain in the earlier evidence packet.
The earlier packet's source hashes and previews describe that older revision;
`source.sha256` here records this correction.

On a desktop session capable of creating MonoGame's OpenGL context, run:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Then inspect F3 / 2,6, the village cave and desert mountain contacts in gameplay.
The user's live game and personal saves were not navigated or restarted.
