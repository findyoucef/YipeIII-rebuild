# Hell pixel-art shading — 2026-10-04

The user approved the corrected cave puddles and asked to restore more of Hell's
retro appearance while preserving its overall look. This revision covers Hell
maps 117–120: A4–D4 on the alternate/Hell layer, not surface D4.

The existing terrain compiler now resolves its ground and lava colors to fourteen
deliberate ember-red, charcoal and ash shades. Small floor marks form pixel
clusters; rock facets, molten seams and flagstones have distinct color steps.
Firelight uses low-opacity bands and an eight-Hz flicker clock. Smoke uses small
clusters and stepped opacity. Native rock detail, rounded connected walls, the
flat irregular path, floor fissures, low ruins and complete door crowns remain.
The approved heat shimmer, scanlines and miniature effect are unchanged.

## Visual review

- Path: [before](hell-path-defaults-before.png) / [after](hell-path-defaults-after.png).
- Entrance: [before](hell-a4-defaults-before.png) / [after](hell-a4-defaults-after.png).
- Monsters and low ruins: [before](hell-a4-6-7-defaults-before.png) / [after](hell-a4-6-7-defaults-after.png).
- East door: [before](hell-east-door-defaults-before.png) / [after](hell-east-door-defaults-after.png).
- Generated region: [before](map-118-before.png) / [after](map-118-after.png).
- [Animated preview](hell-motion.mp4), with Strong miniature and diagnostic scanlines off.

All four regions have new `hell-ID-defaults.png` (scanlines on, Strong miniature)
and `hell-ID-crisp.png` (scanlines/miniature off) captures. Heat remains enabled.
Gameplay review covered the entrance, path and all three generated regions; native
maps and the crisp view were also inspected. The path retains clear contrast
against the red floor. All fixtures are isolated from the user's live game.

## Verification

Checked Release: **zero warnings/errors; 981 graphics integration checks pass**
across all 115 maps and ten Hell layouts, with 360 tour frames. Existing checks
cover fog, door crowns, monster/ruin overlap, molten containment, changed terrain,
animation, pause, heat composition and complete gameplay-state/RNG preservation.
Eight extra captures record all four regions with default effects and crisp art.

Of 755 paired PNGs, 174 changed, all depicting Hell (including the two generically
named generated-map captures, which use map 118). Only the four Hell full maps
changed; the other 111 maps, all Earth/puddle captures and all nine existing
reflection captures are byte-identical. See [comparison](capture-comparison.json).
All 26 core files and personal preview/profile hashes remain identical. Adventure
and journal hashes changed while the live game was open. Implementation and tests
did not overwrite or restore personal saves; tests use isolated storage. See
[preservation hashes](preserved-core-and-saves.json).

Baseline: `build/graphics-checks/yipe-graphics-20261005-052323-eef360a9`.
Final run: `build/graphics-checks/yipe-graphics-20261005-053357-bfbfa487`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

[Build log](build.log), [graphics log](graphics.log), [source hashes](source-inputs.json)
and [task diff](changes.patch) are retained here.

Copilot Bridge supplied bounded [terrain advice](copilot-review.txt) and a
[candidate diff review](copilot-diff-review.txt). The primary agent checked the
source, performed all edits, and independently built, tested and visually reviewed
the result. The final version retains native rock sampling: enlarging rock pixels
in the reviewed candidate obscured the original forms at gameplay scale.

Simplicity review covered both changed files. One production file extends the
existing cached painting and effects; its local palette and nearest-shade function
serve both ground and lava. No new fields, textures, assets, settings or rendering
passes were added. The check file adds comparison captures only. No remaining
task-diff findings.

The live game remains open on the prior build; it was not closed or navigated.
Save and restart to load this update. Hell visual acceptance is pending; the
smaller brown cave puddles are now accepted. Other port work resumes at P5.6.
