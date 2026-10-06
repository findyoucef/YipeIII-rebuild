# P2 opening adventure complete — 2026-10-04

P2's creation → shops → caves → battles → rescues → King reward → save/reload
route is implemented and verified through the real MonoGame frontend. The
continuous P2–P4 goal continues with P3 and P4; this is not campaign completion.

- **500 core/input/storage checks pass**, including the independent C++ rules.
- **614 graphics checks pass** on the active macOS desktop. All twelve character
  choices, three princess variants, 115 map instances, 97 tile mappings, all
  creatures/objects, secret-terrain aliases and seven bridges (two town, five
  world) retain the common art contract. Native world reflections are clipped to
  water and drawn below decks and plants. Casino transactions run through overlay
  controls and match the independently verified core payout/RNG rules.
- The earned frontend route passes **all 42 complete native save/RNG checkpoints
  plus King return**, with 116 victories, 14 chests, two rescues, one stamp and
  its 500-gold reward. It renders 9,940 frames at the approved step timing.
- Production pause-menu Save at checkpoint 39 reopens in a separate graphics
  process, then earns the remaining battles and reward; another 135 frames.
- Native original WAV files load; a real sound instance enters Playing, a monster
  cue replaces/disposes it, and focus loss stops/releases it. Sound IDs/call order
  for the opening's attacks, spells, abilities, deaths, doors, restoration and
  King dialogue were checked against the C++ application. This tests device
  playback, not a subjective listening session.
- The retained cave, princess, King, sandstone entrance and casino captures were
  visually inspected. Approved town art, 16:9 overlays, scanlines, Strong miniature,
  250 ms movement and four-frame-per-second animation remain intact.

```sh
dotnet build monogame/Yipe.slnx -c Release -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
```

Inputs are controller-style frames; separate polling tests cover keyboard and
controller mappings. Physical-controller sessions and other operating systems
remain P5/P6 verification. Tests use isolated save/profile files. The only
opening fixture normalization is the explicitly chosen knight's cosmetic
portrait byte; every gameplay field and RNG compares exactly.

New quest artwork uses the built-in image tool. Assets and exact prompts are in
`Yipe.Desktop/Content/Art/Retro/World/README.md`. Full campaign/timed-route/late-game
restart verification remains open under P3/P4.
