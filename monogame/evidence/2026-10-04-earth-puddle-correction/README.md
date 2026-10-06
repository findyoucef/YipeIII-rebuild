# Smaller, irregular cave puddles — 2026-10-04

The user rejected the first puddles as too large, too perfectly shaped and too
blue for the cave. This correction applies to all fourteen puddles across upper
E3–E6 and deep E4–E6. Their centers and ordinary-floor walkability remain the same.

Footprints now combine a small uneven basin, a shallow extension and a dry-ground
notch, with local variation and mirrored shapes. Typical authored radii are
22×15 native pixels, previously about 38×24. A quiet five-color stone-brown
texture replaces the clean blue river pattern. Broad reflected patches, broken
edge glints, muddy banks and sparse slow highlights retain the pixel style.
Character reflections use a warm tint and still fade and clip at the shoreline.
The surrounding terrain's approved palette and contrast are unchanged.

[Walk-through video](puddle-walk.mp4) · [Reflection close-up](puddle-reflection.png)

| Region | Before | After |
| --- | --- | --- |
| Upper E3 | [Before](earth-57-before.png) | [After](earth-57-after.png) |
| Upper E4 | [Before](earth-63-before.png) | [After](earth-63-after.png) |
| Upper E5 | [Before](earth-69-before.png) | [After](earth-69-after.png) |
| Upper E6 | [Before](earth-75-before.png) | [After](earth-75-after.png) |
| Deep E4 | [Before](earth-103-before.png) | [After](earth-103-after.png) |
| Deep E5 | [Before](earth-109-before.png) | [After](earth-109-after.png) |
| Deep E6 | [Before](earth-115-before.png) | [After](earth-115-after.png) |

All seven native map views and the E3 entry/standing/exit gameplay frames were
visually reviewed. Gameplay captures use scanlines on and Strong miniature.
The isolated frontend movement fixture walks from upper E3 / 3,3 through 3,4
to dry ground at 3,6, retaining the approved 250 ms steps.

## Verification

Checked Release: **zero warnings/errors; 981 graphics integration checks pass**,
including all 115 maps, ten Hell layouts and 360 tour frames. Existing size and
texture assertions now check small footprints and discrete warm colors; the
previous exact-blue-river requirement intentionally no longer applies to caves.
Walkability, landmarks, reflection clipping/fading, animation, pause, visibility,
secret terrain, cache restoration and full gameplay-state/RNG checks pass.

Of 755 paired captures, 143 changed, all Earth-related. Only the seven Earth
full maps changed; the other 108 maps and all nine prior reflection captures
are byte-identical. See [capture comparison](capture-comparison.json).
All 26 core files, personal preview and profile remain identical to their
pre-work hashes. Adventure and journal changed while the live game was open;
implementation and tests did not overwrite or restore personal files. Tests
use isolated saves. See [preservation hashes](preserved-core-and-saves.json).

Baseline: `build/graphics-checks/yipe-graphics-20261005-051241-94a9ebef`.
Final run: `build/graphics-checks/yipe-graphics-20261005-052323-eef360a9`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

[Build log](build.log), [graphics log](graphics.log), [source hashes](source-inputs.json)
and [task diff](changes.patch) are retained here.

Two bounded Copilot Bridge reviews examined obsolete test assumptions and the
production diff. Drafts are retained as [test review](copilot-test-review.txt)
and [diff review](copilot-diff-review.txt). The primary agent independently checked
them, retained the behavioral coverage, replaced obsolete art assertions, and
verified the reported scan-bound issue algebraically before increasing horizontal
padding from two to four pixels. The primary agent performed the edits, checked
build, complete graphics run, image comparison and visual inspection.

Simplicity review covered all four changed files. Three production files modify
the existing mask, cached texture and reflection; no new state, textures, types,
assets or rendering passes were added. No remaining task-diff findings.

The prior game process closed during work. The verified build was reopened for
testing. This correction awaits user visual acceptance. Do not restore the
rejected large blue puddles. Other port work resumes at P5.6.
