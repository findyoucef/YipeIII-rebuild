# Timed routes and item feedback — 2026-10-04

The checked Release build passes without warnings. **502 core/input/storage checks**
and **627 real-device graphics checks** pass. These extend the retained P2 evidence.

The platform route enters map 74 from map 68, boards normally, saves and reopens
through production pause-menu storage, reaches princess 11 behind the Rainbow Key
door, then boards again and returns safely. The fire-wall route reaches princess
20 on map 133 and returns during a fresh opening without damage. Both execute
normal game updates at the approved 250 ms walking pace. Overlays pause timers.
The three retained captures were visually inspected.

These are isolated diagnostic scenarios with stated prerequisites (Rainbow Key,
100 HP and suppressed roaming encounters); they do not establish earned acquisition.
The continuing campaign supplies that separate proof. Save/reopen here occurs in
the same process; independent D7 core restart cases and the separate-process earned
opening are retained elsewhere.

Inventory results now show the original numeric amount. A consumed surface potion
plays the original return cue after acknowledging its item notice. Two additional
graphics checks exercise that relocation and real sound-instance replacement.
Name entry reserves Space and movement letters for typing; Enter still confirms.

Reproduce from the repository root:

```sh
dotnet build monogame/Yipe.slnx -c Release -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

A real graphical desktop/audio device is used. Synthetic controller frames do not
claim physical-controller validation. Personal saves are not used.

The statistics audit restores all 41 monster rows, reveals names only after a
positive defeat count, and labels incomplete imported history. Totals, carried
capacity and current ability are shown. Classic/Silly names can now be toggled
during play without changing portrait, ranks or RNG. Five additional graphics
checks pass; the retained history overlay was visually inspected.
