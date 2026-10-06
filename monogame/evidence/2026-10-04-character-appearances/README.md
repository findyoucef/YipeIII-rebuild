# Character appearances — 2026-10-04

All twelve original appearance slots are selectable through controller-friendly
creation overlays: eight Classic and four Silly Mode choices. Selection preserves
attributes and seed; inventory slot 28 retains the original portrait number.
The approved knight keeps its existing animation. Other appearances have four
facings and paired walk poses at the same 250 ms step / four-frame cadence,
including motion trails and town bridge reflections.

**595 graphics checks pass**, including all 115 map instances, all twelve
creation choices and 112 new atlas palette, pose, anchor and animation checks.
Both picker layouts and the sorceress in the approved town were visually
inspected at 1280×720. New assets were generated/edited with the built-in image
tool; exact prompts and source images are recorded in the World art README.

The earned frontend opening was then repeated: all 42 C++ persistent-state/RNG
checkpoints plus King return match; 116 victories, 14 chests, two rescues and
9,940 rendered frames. Its production pause-menu save also reopens in a fresh
process and completes the remaining route (135 additional rendered frames).
Only the expected fixture's zero/default portrait metadata is normalized to the
explicitly selected knight; gameplay bytes remain exact.

```sh
dotnet build monogame/Yipe.slnx -c Release -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
```

These checks use a real graphics device and synthetic controller-style inputs,
not a physical-controller session. Full campaign proof remains in progress.
