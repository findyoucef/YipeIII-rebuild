# Redder Hell palette — 2026-10-04

The user found Hell insufficiently red. Updated the existing color treatment in
`WorldHell.cs`: deep ember-red floor, warm red basalt and soot, brighter red-orange
fissures, and a stronger warm firelight falloff. The flagstones use lighter warm
ash tones to retain clear separation from the ground. Shapes, collision, sprite
scale, animation timing, fog and the recent door-crown fix are unchanged.

This changes color constants and light strength inside the existing terrain
cache. No new assets, fields, helpers, shaders or postprocessing passes. The
complete task diff was reviewed for simplicity.

## Visual comparisons

- Path: [before](path-before.png) / [after](path-after.png).
- Entrance: [before](entrance-before.png) / [after](entrance-after.png).
- Generated Hell: [before](generated-walls-before.png) / [after](generated-walls-after.png).
- Raw captures of all four Hell maps are retained alongside these comparisons.

The path and entrance show scanlines on and Strong miniature. Generated Hell
uses Strong miniature with diagnostic scanlines off. Visually reviewed the
warmer terrain, path contrast, entrance and generated walls.

Checked Release build: zero warnings/errors. **752 graphics checks pass**,
including ten Hell layouts, fog/door rendering, lava containment, animation,
heat composition, gameplay and RNG preservation. Only the four Hell map captures
changed. All 111 other maps and five reflection captures are byte-identical.
All 26 gameplay core files and four personal save/profile files retained their
pre-work hashes. Tests use isolated saves.

Passing run: `build/graphics-checks/yipe-graphics-20261005-020909-4f076a84`.
Baseline: `build/graphics-checks/yipe-graphics-20261005-013907-f6a1aaad`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The personal game was closed during this work; the updated build was reopened
after verification. User acceptance of the red palette is pending. Resume other
port work from `PORTING_PLAN.md`.
