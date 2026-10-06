# Walkable Earth Cave puddles — 2026-10-04

The user approved the corrected Earth Cave contrast and retro palette, then
requested river-style puddles that reflect the character while walking through
them. Each of the seven Earth regions now has two irregular shallow pools.
They reuse the existing river texture, shoreline, animated ripples and clipped
character reflection. Reflection strength follows the animated boots and fades
at the shoreline. The underlying tiles remain ordinary walkable floor.

The approved cave terrain, movement speed, encounters, secrets and save format
are unchanged. Puddles avoid chests, stairs and rescue positions and require no
water equipment. Existing decorative wall drips and room identities remain.

## Preview and locations

[Walk-through video](puddle-walk.mp4) · [Character reflection](puddle-reflection.png)

The video uses the real frontend renderer with scanlines on and Strong miniature,
and an isolated movement fixture at the approved 250 ms walking pace. It walks
south from upper E3 / 3,3 through the puddle at 3,4 to dry ground at 3,6. All seven
native map views and entering, standing, leaving and dry gameplay frames were
visually inspected. Fullscreen before/after views are retained below.

| Region | Map ID | Puddle centers (local tiles) | Before | After |
| --- | --- | --- | --- | --- |
| Upper E3 | 57 | 3,4 and 8,8 | [Before](earth-57-before.png) | [After](earth-57-after.png) |
| Upper E4 | 63 | 5,5 and 9,3 | [Before](earth-63-before.png) | [After](earth-63-after.png) |
| Upper E5 | 69 | 8,6 and 4,2 | [Before](earth-69-before.png) | [After](earth-69-after.png) |
| Upper E6 | 75 | 7,6 and 8,2 | [Before](earth-75-before.png) | [After](earth-75-after.png) |
| Deep E4 | 103 | 6,5 and 8,8 | [Before](earth-103-before.png) | [After](earth-103-after.png) |
| Deep E5 | 109 | 7,5 and 7,1 | [Before](earth-109-before.png) | [After](earth-109-after.png) |
| Deep E6 | 115 | 4,8 and 8,2 | [Before](earth-115-before.png) | [After](earth-115-after.png) |

## Verification

Checked Release build: **zero warnings/errors**. **981 graphics integration
checks pass**, covering all 115 maps, ten Hell layouts and 360 tour frames.
The 84 added checks cover walkability without equipment, exact river texture
reuse away from shorelines, ripple/reflection timing, visibility and water
clipping, smooth entry, dry-ground exit, and terrain cache invalidation.
Existing palette, contrast, secret, pause and complete-state/RNG checks pass.

The first test run caught a test fixture error: a cross-shaped interior sample
included pixels within the diagonal shoreline band. The corrected sample checks
the full surrounding square. No production change was needed for that failure.

Of 684 paired PNGs, 72 Earth-related captures changed. Only the seven Earth full
maps changed; the other 108 full maps and all nine existing reflection captures
are byte-identical. Seventy-one new captures comprise seven puddle views and
64 walk-through frames. See [capture comparison](capture-comparison.json).

All 26 core source files and the personal preview retain their pre-work hashes.
The user's live game ran during implementation; adventure, journal and profile
hashes changed during that time. Those personal files were neither restored nor
overwritten by the implementation or tests. Tests use isolated saves. See
[preservation hashes](preserved-core-and-saves.json).

Baseline: `build/graphics-checks/yipe-graphics-20261005-044628-1a27d151`.
Final run: `build/graphics-checks/yipe-graphics-20261005-051241-94a9ebef`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

[Build log](build.log), [graphics log](graphics.log), [source hashes](source-inputs.json)
and [task diff](changes.patch) are retained here.

Simplicity review covered all six changed production/check files: four production
files extend the existing water mask, draw path and reflection. One localized
placement method supplies the authored pools; there are no new textures, state
fields, types or save data. No remaining findings in the task diff.

The prior game process closed during verification. The verified build was
reopened for testing. Puddle visual acceptance is pending; the earlier contrast
correction is accepted. Other port work resumes at P5.6.
