# Earned frontend opening and restart — 2026-10-04

The real MonoGame frontend completed character creation, the retained opening
route and the return to the King through movement and overlay controls. All
**42 C++ checkpoints plus the earned King return** match complete persistent
state and RNG. The route earns **116 victories, 14 chests, two rescues, the first
stamp and the 500-gold King reward**. The full run renders 9,939 frames.

At checkpoint 39 the runner saves using the production pause menu. After the
first process finishes, a new graphics process opens a copy through the normal
selected-save startup, verifies the reopened checkpoint and earns the remaining
battles, stamp and King reward. Its final pause-menu save also matches the live
session. Both processes use isolated files; personal saves are untouched.

```sh
dotnet build monogame/Yipe.slnx -c Release -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
```

An active graphical desktop is required. The option automatically starts the
second process. Retained logs are `opening.log` and `restart.log`; screenshots
show actual route states. The test translates old dialog choices to the new
menus without injecting gameplay state. It authors exploration ticks from the
reference wait commands, independently of renderer speed, while accepted steps
still animate for 250 ms. Keyboard/controller hardware polling is covered by
separate input checks; this run supplies controller-style overlay input frames.

The only fixture normalization is cosmetic: the retained C++ test's default
portrait field is zero; creation explicitly selects portrait one, the same
knight. The runner changes that single byte in the **expected copy**, checks
that no other payload byte changed, and recomputes its integrity hash. All
other fields, including gameplay RNG, compare exactly.

This establishes the opening, not full campaign completion. All-region artwork,
the other portrait choices, remaining interaction coverage and earned campaign
inputs/completion are still being implemented under the active P2–P4 goal.
